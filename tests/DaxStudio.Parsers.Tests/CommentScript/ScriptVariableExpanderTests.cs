using DaxStudio.Parsers.CommentScript;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace DaxStudio.Parsers.Tests.CommentScript
{
    [TestClass]
    public class ScriptVariableExpanderTests
    {
        private static ScriptVariableExpander FixedClock(DateTime local, DateTime utc)
            => new ScriptVariableExpander(() => local, () => utc);

        [TestMethod]
        public void Expand_SimpleVariable()
        {
            var e = new ScriptVariableExpander();
            e.SetVariable("Dir", "C:\\Reports");
            Assert.AreEqual("C:\\Reports\\out.csv", e.Expand("$(Dir)\\out.csv"));
        }

        [TestMethod]
        public void Expand_ConcatenatesMultipleVariables()
        {
            var e = new ScriptVariableExpander();
            e.SetVariable("Dir", "C:\\out");
            e.SetVariable("Name", "sales");
            e.SetVariable("Env", "prod");
            Assert.AreEqual("C:\\out\\sales-prod.csv", e.Expand("$(Dir)\\$(Name)-$(Env).csv"));
        }

        [TestMethod]
        public void Expand_IsCaseInsensitive()
        {
            var e = new ScriptVariableExpander();
            e.SetVariable("OutDir", "X");
            Assert.AreEqual("X", e.Expand("$(outdir)"));
        }

        [TestMethod]
        public void Expand_NestedVariableReference()
        {
            var e = new ScriptVariableExpander();
            e.SetVariable("Root", "C:\\ci");
            e.SetVariable("Out", "$(Root)\\out");
            Assert.AreEqual("C:\\ci\\out\\f.txt", e.Expand("$(Out)\\f.txt"));
        }

        [TestMethod]
        public void Expand_NowAndUtcNowUseInjectedClock()
        {
            var local = new DateTime(2026, 7, 21, 13, 5, 9);
            var utc = new DateTime(2026, 7, 21, 3, 5, 9);
            var e = FixedClock(local, utc);
            Assert.AreEqual("2026-07-21", e.Expand("$(now:yyyy-MM-dd)"));
            Assert.AreEqual("03-05-09", e.Expand("$(utcnow:HH-mm-ss)"));
        }

        [TestMethod]
        public void SetVariable_CapturesBuiltInEagerly_StaysStableAsClockAdvances()
        {
            var now = new DateTime(2026, 7, 21, 8, 0, 0);
            var e = new ScriptVariableExpander(() => now, () => now);
            e.SetVariable("Stamp", "$(now:HHmmss)");

            // Advance the clock after the SET was captured.
            now = new DateTime(2026, 7, 21, 9, 30, 0);

            // The captured variable is frozen at its SET-time value...
            Assert.AreEqual("080000", e.Expand("$(Stamp)"));
            Assert.AreEqual("080000", e.Expand("$(Stamp)"));
            // ...but a fresh direct reference reflects the advanced clock.
            Assert.AreEqual("093000", e.Expand("$(now:HHmmss)"));
        }

        [TestMethod]
        public void SetVariable_BakesDateIntoPath()
        {
            var day = new DateTime(2026, 7, 21);
            var e = new ScriptVariableExpander(() => day, () => day);
            e.SetVariable("OutDir", "C:\\Report\\$(now:yyyy-MM-dd)");
            Assert.AreEqual("C:\\Report\\2026-07-21\\sales.csv", e.Expand("$(OutDir)\\sales.csv"));
        }

        [TestMethod]
        public void Expand_EnvironmentVariable()
        {
            var name = "DAXSTUDIO_TEST_VAR_" + Guid.NewGuid().ToString("N");
            Environment.SetEnvironmentVariable(name, "envval");
            try
            {
                var e = new ScriptVariableExpander();
                Assert.AreEqual("envval", e.Expand($"$(env:{name})"));
            }
            finally
            {
                Environment.SetEnvironmentVariable(name, null);
            }
        }

        [TestMethod]
        public void Expand_UndefinedVariable_Throws()
        {
            var e = new ScriptVariableExpander();
            Assert.Throws<CommentScriptCommandException>(() => e.Expand("$(missing)"));
        }

        [TestMethod]
        public void Expand_UnknownNamespace_Throws()
        {
            var e = new ScriptVariableExpander();
            Assert.Throws<CommentScriptCommandException>(() => e.Expand("$(foo:bar)"));
        }

        [TestMethod]
        public void Expand_UndefinedEnvVariable_Throws()
        {
            var e = new ScriptVariableExpander();
            Assert.Throws<CommentScriptCommandException>(
                () => e.Expand("$(env:DAXSTUDIO_DEFINITELY_NOT_SET_" + Guid.NewGuid().ToString("N") + ")"));
        }

        [TestMethod]
        public void Expand_BadDateFormat_Throws()
        {
            var e = new ScriptVariableExpander();
            // A single-'%' custom format specifier without a following char is invalid.
            Assert.Throws<CommentScriptCommandException>(() => e.Expand("$(now:%)"));
        }

        [TestMethod]
        public void Expand_EscapedDollarParen_YieldsLiteral()
        {
            var e = new ScriptVariableExpander();
            e.SetVariable("Var", "X");
            Assert.AreEqual("$(Var) = X", e.Expand("$$(Var) = $(Var)"));
        }

        [TestMethod]
        public void SetVariable_ForwardReference_Throws()
        {
            // Because SET expands eagerly, a variable can only reference names defined above it. A
            // reference to a not-yet-defined variable fails immediately (which also makes storing a
            // reference cycle impossible).
            var e = new ScriptVariableExpander();
            Assert.Throws<CommentScriptCommandException>(() => e.SetVariable("Out", "$(Root)\\out"));
        }

        [TestMethod]
        public void SetVariable_SelfReference_Throws()
        {
            // A SET value is expanded at the point it executes, so it can never reference the variable
            // it is defining - not even when that name was defined earlier in the script.
            var e = new ScriptVariableExpander();
            Assert.Throws<CommentScriptCommandException>(() => e.SetVariable("myFile", "$(myFile).csv"));

            e.SetVariable("Out", "C:\\a");
            Assert.Throws<CommentScriptCommandException>(() => e.SetVariable("Out", "$(Out)\\sub"));
            Assert.Throws<CommentScriptCommandException>(() => e.SetVariable("Out", "$(OUT)\\sub"));

            // the previous value is left untouched by the rejected SET
            Assert.AreEqual("C:\\a", e.Expand("$(Out)"));
        }

        [TestMethod]
        public void SetVariable_EscapedSelfReference_IsAllowed()
        {
            // "$$(" is the escape for a literal "$(" so it is not a reference and must not be rejected as
            // a self-reference. (Re-expanding the stored literal is a separate, pre-existing behaviour.)
            var e = new ScriptVariableExpander();
            e.SetVariable("myFile", "$$(myFile)");
            Assert.IsTrue(e.ContainsVariable("myFile"));
        }

        [TestMethod]
        public void SetVariable_BuiltInWithMatchingName_IsAllowed()
        {
            // A built-in namespace reference is never a self-reference, even from a variable of that name.
            var e = new ScriptVariableExpander(() => new DateTime(2024, 1, 15), () => new DateTime(2024, 1, 15));
            e.SetVariable("now", "$(now:yyyy-MM-dd)");
            Assert.AreEqual("2024-01-15", e.Expand("$(now)"));
        }

        [TestMethod]
        public void Expand_NoReferences_ReturnsInputUnchanged()
        {
            var e = new ScriptVariableExpander();
            Assert.AreEqual("C:\\plain\\path.csv", e.Expand("C:\\plain\\path.csv"));
            Assert.AreEqual("a $ b", e.Expand("a $ b"));
        }

        [TestMethod]
        public void Reset_ClearsVariables()
        {
            var e = new ScriptVariableExpander();
            e.SetVariable("X", "1");
            Assert.IsTrue(e.ContainsVariable("X"));
            e.Reset();
            Assert.IsFalse(e.ContainsVariable("X"));
        }

        [TestMethod]
        public void ExpandBatches_ExpandsTargetsInOrderAcrossBatches()
        {
            var day = new DateTime(2026, 7, 21);

            var batch1 = new ScriptBatch();
            batch1.Commands.Add(new VariableCommand("Dir", "C:\\ci"));
            batch1.Commands.Add(new VariableCommand("Stamp", "$(now:yyyyMMdd)"));
            batch1.Commands.Add(new ExportCommand(ExportTarget.Metrics, "$(Dir)\\m-$(Stamp).vpax"));

            // A later batch (after a "--> GO") still sees variables set earlier.
            var batch2 = new ScriptBatch();
            var assert = new AssertTableCommand(AssertTableMode.Ordered) { FilePath = "$(Dir)\\baselines\\p.csv" };
            batch2.Commands.Add(assert);

            var use = new UseCommand("$(Dir)");
            batch2.Commands.Add(use);

            ScriptVariableExpander.ExpandBatches(new[] { batch1, batch2 }, () => day, () => day);

            var metrics = (ExportCommand)batch1.Commands[2];
            Assert.AreEqual("C:\\ci\\m-20260721.vpax", metrics.FileName);
            Assert.AreEqual("C:\\ci\\baselines\\p.csv", assert.FilePath);
            Assert.AreEqual("C:\\ci", use.DatabaseName);
        }

        [TestMethod]
        public void ExpandBatches_UndefinedVariable_Throws()
        {
            var batch = new ScriptBatch();
            batch.Commands.Add(new AssertTableCommand(AssertTableMode.Ordered) { FilePath = "$(missing)\\p.csv" });
            Assert.Throws<CommentScriptCommandException>(
                () => ScriptVariableExpander.ExpandBatches(new[] { batch }));
        }
    }
}
