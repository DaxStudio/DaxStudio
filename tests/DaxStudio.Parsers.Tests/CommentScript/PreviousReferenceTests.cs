using DaxStudio.Parsers.CommentScript;
using DaxStudio.Parsers.PreProcessor;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace DaxStudio.Parsers.Tests.CommentScript
{
    /// <summary>
    /// Tests for the <c>PREVIOUS</c> assert operand, which is sugar for "the previous batch that runs
    /// a query" and is resolved after parsing by <c>PreviousReferenceResolver</c>.
    /// </summary>
    [TestClass]
    public class PreviousReferenceTests
    {
        private static PreProcessResult Parse(string input) => AntlrPreProcessor.Parse(input);

        private static BaselineReference BaselineOf(ScriptBatch batch)
            => batch.Commands.OfType<AssertCommand>().FirstOrDefault()?.Baseline;

        private static string[] BaselineNames(ScriptBatch batch)
            => batch.Commands.OfType<BaselineCommand>().Select(b => b.Name).ToArray();

        [TestMethod]
        public void ResolvesToThePrecedingQueryBatch()
        {
            var result = Parse("EVALUATE { 1 }\n" +
                               "--> GO\n" +
                               "--> ASSERT DURATION <= PREVIOUS\n" +
                               "EVALUATE { 2 }\n");

            Assert.IsEmpty(result.CommandErrors);

            // The target batch gained a synthesised baseline capture...
            CollectionAssert.AreEqual(new[] { "(previous:0)" }, BaselineNames(result.Batches[0]));

            // ...and the reference now points at it, while still reading as PREVIOUS.
            var reference = BaselineOf(result.Batches[1]);
            Assert.AreEqual("(previous:0)", reference.Name);
            Assert.IsTrue(reference.IsPrevious);
            Assert.IsFalse(reference.IsUnresolvedPrevious);
            Assert.AreEqual("PREVIOUS", reference.ToString());
        }

        [TestMethod]
        public void SkipsALeadingCommentOnlyBatch()
        {
            // "--> CONNECT" / "--> USE" batches carry no query, so PREVIOUS must look past them.
            var result = Parse("--> CONNECT SERVER localhost\\tab19\n" +
                               "--> USE \"Adventure Works\"\n" +
                               "--> GO\n" +
                               "EVALUATE { 1 }\n" +
                               "--> GO\n" +
                               "--> ASSERT DURATION <= PREVIOUS\n" +
                               "EVALUATE { 2 }\n");

            Assert.IsEmpty(result.CommandErrors);
            Assert.AreEqual("(previous:1)", BaselineOf(result.Batches[2]).Name);
            Assert.IsEmpty(BaselineNames(result.Batches[0]));
            CollectionAssert.AreEqual(new[] { "(previous:1)" }, BaselineNames(result.Batches[1]));
        }

        [TestMethod]
        public void SkipsAnInterposedCommentOnlyBatch()
        {
            // A "--> SHOW METRICS" batch between the two queries must not become the baseline.
            var result = Parse("EVALUATE { 1 }\n" +
                               "--> GO\n" +
                               "--> SHOW METRICS\n" +
                               "--> GO\n" +
                               "--> ASSERT DURATION <= PREVIOUS\n" +
                               "EVALUATE { 2 }\n");

            Assert.IsEmpty(result.CommandErrors);
            Assert.AreEqual("(previous:0)", BaselineOf(result.Batches[2]).Name);
            Assert.IsEmpty(BaselineNames(result.Batches[1]));
        }

        [TestMethod]
        public void ChainsAcrossThreeBatches()
        {
            var result = Parse("EVALUATE { 1 }\n" +
                               "--> GO\n" +
                               "--> ASSERT DURATION <= PREVIOUS\n" +
                               "EVALUATE { 2 }\n" +
                               "--> GO\n" +
                               "--> ASSERT DURATION <= PREVIOUS * 0.9\n" +
                               "EVALUATE { 3 }\n");

            Assert.IsEmpty(result.CommandErrors);
            Assert.AreEqual("(previous:0)", BaselineOf(result.Batches[1]).Name);
            Assert.AreEqual("(previous:1)", BaselineOf(result.Batches[2]).Name);

            // The middle batch is BOTH a capture (for batch 2) and an asserter (against batch 0).
            CollectionAssert.AreEqual(new[] { "(previous:1)" }, BaselineNames(result.Batches[1]));
            Assert.HasCount(1, result.Batches[1].Commands.OfType<AssertCommand>().ToList());
        }

        [TestMethod]
        public void TwoReferencesInOneBatchSynthesiseOnlyOneCapture()
        {
            var result = Parse("EVALUATE { 1 }\n" +
                               "--> GO\n" +
                               "--> ASSERT TABLE PREVIOUS\n" +
                               "--> ASSERT DURATION <= PREVIOUS\n" +
                               "--> ASSERT ROWCOUNT = PREVIOUS\n" +
                               "EVALUATE { 2 }\n");

            Assert.IsEmpty(result.CommandErrors);
            CollectionAssert.AreEqual(new[] { "(previous:0)" }, BaselineNames(result.Batches[0]));
        }

        [TestMethod]
        public void WorksOnAllThreeAssertionKinds()
        {
            var result = Parse("EVALUATE { 1 }\n" +
                               "--> GO\n" +
                               "--> ASSERT TABLE UNORDERED PREVIOUS\n" +
                               "--> ASSERT ROWCOUNT = PREVIOUS\n" +
                               "--> ASSERT SE_QUERIES <= PREVIOUS\n" +
                               "EVALUATE { 2 }\n");

            Assert.IsEmpty(result.CommandErrors);
            var batch = result.Batches[1];

            var table = batch.Commands.OfType<AssertTableCommand>().Single();
            Assert.AreEqual(AssertTableMode.Unordered, table.Mode);
            Assert.AreEqual("(previous:0)", table.Baseline.Name);
            Assert.IsTrue(table.HasTableDefinition);

            Assert.AreEqual("(previous:0)", batch.Commands.OfType<AssertRowcountCommand>().Single().Baseline.Name);
            Assert.AreEqual("(previous:0)", batch.Commands.OfType<AssertCommand>().Single().Baseline.Name);
        }

        [TestMethod]
        public void CarriesTheFactor()
        {
            var result = Parse("EVALUATE { 1 }\n" +
                               "--> GO\n" +
                               "--> ASSERT DURATION <= PREVIOUS * 0.9\n" +
                               "EVALUATE { 2 }\n");

            var reference = BaselineOf(result.Batches[1]);
            Assert.AreEqual(0.9, reference.Factor);
            Assert.AreEqual("PREVIOUS * 0.9", reference.ToString());
        }

        [TestMethod]
        public void NoEarlierQueryBatchIsACommandError()
        {
            var result = Parse("--> ASSERT DURATION <= PREVIOUS\nEVALUATE { 1 }\n");

            Assert.IsNotEmpty(result.CommandErrors);
            StringAssert.Contains(result.CommandErrors[0].Msg, "PREVIOUS");
        }

        [TestMethod]
        public void OnlyCommentBatchesBeforeItIsACommandError()
        {
            var result = Parse("--> CONNECT SERVER localhost\\tab19\n" +
                               "--> GO\n" +
                               "--> ASSERT DURATION <= PREVIOUS\n" +
                               "EVALUATE { 1 }\n");

            Assert.IsNotEmpty(result.CommandErrors);
        }

        [TestMethod]
        public void MixesWithAnExplicitNamedBaseline()
        {
            var result = Parse("--> BASELINE \"original\"\n" +
                               "EVALUATE { 1 }\n" +
                               "--> GO\n" +
                               "--> ASSERT DURATION   <= PREVIOUS\n" +
                               "--> ASSERT SE_QUERIES <= BASELINE \"original\"\n" +
                               "EVALUATE { 2 }\n");

            Assert.IsEmpty(result.CommandErrors);

            // Batch 0 carries both the user's named capture and the synthesised PREVIOUS capture.
            CollectionAssert.AreEquivalent(new[] { "original", "(previous:0)" }, BaselineNames(result.Batches[0]));

            var asserts = result.Batches[1].Commands.OfType<AssertCommand>().ToList();
            Assert.AreEqual("(previous:0)", asserts[0].Baseline.Name);
            Assert.IsTrue(asserts[0].Baseline.IsPrevious);
            Assert.AreEqual("original", asserts[1].Baseline.Name);
            Assert.IsFalse(asserts[1].Baseline.IsPrevious);
        }

        [TestMethod]
        public void PreviousKeywordDoesNotBreakUnquotedUseValues()
        {
            var result = Parse("--> USE PREVIOUS\nEVALUATE { 1 }\n");

            Assert.IsEmpty(result.CommandErrors);
            Assert.AreEqual("PREVIOUS", result.Batches[0].Commands.OfType<UseCommand>().Single().DatabaseName);
        }

        [TestMethod]
        public void SynthesisedCaptureDoesNotLeakIntoTheQueryText()
        {
            var result = Parse("EVALUATE { 1 }\n" +
                               "--> GO\n" +
                               "--> ASSERT DURATION <= PREVIOUS\n" +
                               "EVALUATE { 2 }\n");

            // The resolver appends a command object after the walk; it must not alter the executable DAX.
            StringAssert.Contains(result.Batches[0].QueryText, "EVALUATE { 1 }");
            Assert.DoesNotContain("BASELINE", result.Batches[0].QueryText);
            Assert.DoesNotContain("previous", result.Batches[0].QueryText);
        }

        [TestMethod]
        public void AssertingBatchWithNoQueryIsACommandError()
        {
            // "Compare THIS query to the previous one" needs a query in this batch. Without one the
            // batch never executes and the assertion would be evaluated against the baseline batch's
            // own metrics - a trivially passing false green.
            var result = Parse("EVALUATE { 1 }\n" +
                               "--> GO\n" +
                               "--> ASSERT DURATION <= PREVIOUS\n");

            Assert.IsNotEmpty(result.CommandErrors);
            StringAssert.Contains(result.CommandErrors[0].Msg, "no query");
        }

        [TestMethod]
        public void SkipsAShowDependenciesBatchWhoseQueryIsNeverExecuted()
        {
            // SHOW DEPENDENCIES / SHOW DIAGRAM consume the batch's DAX as their analysis target and do
            // NOT execute it, so such a batch produces no results or timings to be a baseline for.
            foreach (var show in new[] { "DEPENDENCIES", "DIAGRAM" })
            {
                var result = Parse("EVALUATE { 1 }\n" +
                                   "--> GO\n" +
                                   $"--> SHOW {show}\n" +
                                   "EVALUATE { 2 }\n" +
                                   "--> GO\n" +
                                   "--> ASSERT DURATION <= PREVIOUS\n" +
                                   "EVALUATE { 3 }\n");

                Assert.IsEmpty(result.CommandErrors, show);
                Assert.AreEqual("(previous:0)", BaselineOf(result.Batches[2]).Name, show);
                Assert.IsEmpty(BaselineNames(result.Batches[1]), show);
            }
        }

        [TestMethod]
        public void AShowDependenciesBatchCannotUsePreviousItself()
        {
            // Its own query is never run, so there is nothing for the assertion to be about.
            var result = Parse("EVALUATE { 1 }\n" +
                               "--> GO\n" +
                               "--> SHOW DEPENDENCIES\n" +
                               "--> ASSERT DURATION <= PREVIOUS\n" +
                               "EVALUATE { 2 }\n");

            Assert.IsNotEmpty(result.CommandErrors);
        }

        [TestMethod]
        public void ShowMetricsBatchStillRunsItsQueryAndCanBeTheTarget()
        {
            // Every SHOW variant other than DEPENDENCIES / DIAGRAM leaves the batch query to run, so
            // such a batch IS a valid PREVIOUS target.
            var result = Parse("--> SHOW METRICS\n" +
                               "EVALUATE { 1 }\n" +
                               "--> GO\n" +
                               "--> ASSERT DURATION <= PREVIOUS\n" +
                               "EVALUATE { 2 }\n");

            Assert.IsEmpty(result.CommandErrors);
            Assert.AreEqual("(previous:0)", BaselineOf(result.Batches[1]).Name);
        }

        [TestMethod]
        public void ReservedBaselineNamesAreRejected()
        {
            // The baseline store is keyed by name and is last-write-wins, so a user name colliding with
            // a generated one would silently compare against the wrong batch.
            foreach (var name in new[] { "(previous:0)", "(previous)", "(default)" })
            {
                var result = Parse($"--> BASELINE \"{name}\"\nEVALUATE {{ 1 }}\n");
                Assert.IsNotEmpty(result.CommandErrors, name);
                StringAssert.Contains(result.CommandErrors[0].Msg, "reserved", name);
            }
        }

        [TestMethod]
        public void OrdinaryBaselineNamesWithPunctuationAreStillAllowed()
        {
            var result = Parse("--> BASELINE \"v1 (original)\"\nEVALUATE { 1 }\n");

            Assert.IsEmpty(result.CommandErrors);
            CollectionAssert.AreEqual(new[] { "v1 (original)" }, BaselineNames(result.Batches[0]));
        }

        [TestMethod]
        public void ABlankQuotedNameIsTheUnnamedBaseline()
        {
            // BaselineCommand and BaselineStore both normalise blank to the unnamed baseline, so the
            // parser must too - otherwise the duplicate guard compares "" against "(default)", lets both
            // through, and the two captures silently collide on the same store key.
            var result = Parse("--> BASELINE \"\"\nEVALUATE { 1 }\n");

            Assert.IsEmpty(result.CommandErrors);
            var cmd = result.Batches[0].Commands.OfType<BaselineCommand>().Single();
            Assert.IsTrue(cmd.IsDefault);
            Assert.IsFalse(cmd.IsSynthesised);
        }

        [TestMethod]
        public void ABlankQuotedNameTripsTheDuplicateBaselineGuard()
        {
            var result = Parse("--> BASELINE\n" +
                               "EVALUATE { 1 }\n" +
                               "--> GO\n" +
                               "--> BASELINE \"\"\n" +
                               "EVALUATE { 2 }\n");

            Assert.IsNotEmpty(result.CommandErrors);
            StringAssert.Contains(result.CommandErrors[0].Msg, "already defined");
        }

        [TestMethod]
        public void RunsItsQueryIsTheSharedRuleForWhichBatchesExecute()
        {
            // ScriptBatch.RunsItsQuery / ConsumesQueryAsAnalysisTarget are the single definition used by
            // BOTH the batch-execution loop and the PREVIOUS resolver, so they cannot drift apart.
            var result = Parse("--> SHOW DEPENDENCIES\n" +
                               "EVALUATE { 1 }\n" +
                               "--> GO\n" +
                               "--> SHOW METRICS\n" +
                               "EVALUATE { 2 }\n" +
                               "--> GO\n" +
                               "--> CONNECT SERVER localhost\\tab19\n");

            Assert.IsTrue(result.Batches[0].ConsumesQueryAsAnalysisTarget);
            Assert.IsFalse(result.Batches[0].RunsItsQuery, "SHOW DEPENDENCIES consumes the query");

            Assert.IsFalse(result.Batches[1].ConsumesQueryAsAnalysisTarget);
            Assert.IsTrue(result.Batches[1].RunsItsQuery, "SHOW METRICS leaves the query to run");

            Assert.IsFalse(result.Batches[2].RunsItsQuery, "comment-only batch has no query");
        }

        [TestMethod]
        public void SynthesisedCapturesAreFlagged()
        {
            var result = Parse("EVALUATE { 1 }\n" +
                               "--> GO\n" +
                               "--> ASSERT DURATION <= PREVIOUS\n" +
                               "EVALUATE { 2 }\n");

            var synthesised = result.Batches[0].Commands.OfType<BaselineCommand>().Single();
            Assert.IsTrue(synthesised.IsSynthesised);
        }

        [TestMethod]
        public void UserAuthoredCapturesAreNotFlaggedAsSynthesised()
        {
            var named = Parse("--> BASELINE \"v1\"\nEVALUATE { 1 }\n");
            Assert.IsFalse(named.Batches[0].Commands.OfType<BaselineCommand>().Single().IsSynthesised);

            var unnamed = Parse("--> BASELINE\nEVALUATE { 1 }\n");
            Assert.IsFalse(unnamed.Batches[0].Commands.OfType<BaselineCommand>().Single().IsSynthesised);
        }

        [TestMethod]
        public void IsDetectedByTheDscmdUnsupportedBaselineGuard()
        {
            // dscmd cannot evaluate baseline assertions yet, so FileCommand warns when a script uses
            // them. A PREVIOUS reference must trip the same detection - otherwise it would silently
            // report a confusing error instead of the explicit "not supported" warning. This mirrors
            // the predicate in DaxStudio.CommandLine\Commands\FileCommand.cs.
            var result = Parse("EVALUATE { 1 }\n" +
                               "--> GO\n" +
                               "--> ASSERT DURATION <= PREVIOUS\n" +
                               "EVALUATE { 2 }\n");

            var usesBaselines = result.Batches.SelectMany(b => b.Commands).Any(c =>
                c is BaselineCommand
                || (c as AssertCommand)?.Baseline != null
                || (c as AssertRowcountCommand)?.Baseline != null
                || (c as AssertTableCommand)?.Baseline != null);

            Assert.IsTrue(usesBaselines);
        }
    }
}
