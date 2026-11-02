using System;
using System.Collections.Generic;
using System.Linq;
using DaxStudio.Parsers.CommentScript;
using DaxStudio.Parsers.Dax;

namespace DaxStudio.Parsers.PreProcessor
{
    /// <summary>
    /// Resolves the <c>PREVIOUS</c> operand of an <c>--&gt; ASSERT</c> command
    /// (<c>--&gt; ASSERT DURATION &lt;= PREVIOUS</c>) into an ordinary baseline reference, by
    /// synthesising a <see cref="BaselineCommand"/> on the batch it refers to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>PREVIOUS</c> means <b>the previous batch that actually runs a query</b>, so comment-only
    /// batches (a leading <c>--&gt; CONNECT</c>, an interposed <c>--&gt; SHOW METRICS</c>) are skipped
    /// over and it always reads as "the query before this one".
    /// </para>
    /// <para>
    /// This runs as a post-parse pass rather than inside <c>PreProcessorListener</c> because
    /// <see cref="ScriptBatch.QueryText"/> is not assigned until <b>after</b> the parse-tree walk
    /// completes. Resolving here lets the check use exactly the same "batch has a non-blank QueryText"
    /// test that the batch-execution loop uses to decide which batches run, so the parser and the
    /// runtime can never disagree about which batch <c>PREVIOUS</c> refers to.
    /// </para>
    /// <para>
    /// Once resolved, a <c>PREVIOUS</c> reference is indistinguishable from a named
    /// <c>--&gt; BASELINE</c> to everything downstream (trace auto-start, the per-batch capture hooks,
    /// the baseline store and the assertion engine), so no other component needs to know it exists.
    /// </para>
    /// </remarks>
    internal static class PreviousReferenceResolver
    {
        /// <summary>
        /// Resolves every unresolved <c>PREVIOUS</c> reference in <paramref name="batches"/>, adding a
        /// command error for any that has no earlier batch with a query to compare against.
        /// </summary>
        public static void Resolve(IList<ScriptBatch> batches, ICollection<Error> commandErrors)
        {
            if (batches == null || batches.Count == 0) return;

            for (int batchIndex = 0; batchIndex < batches.Count; batchIndex++)
            {
                var batch = batches[batchIndex];
                if (batch?.Commands == null) continue;

                foreach (var reference in UnresolvedReferences(batch))
                {
                    // "Compare THIS query to the previous one" is meaningless without a query in this
                    // batch. Left unchecked the batch would never execute, its assertions would fall to
                    // the end-of-run pass, and they would be evaluated against the baseline batch's own
                    // metrics - trivially passing. A false PASS is the worst outcome for a test feature,
                    // so this is a hard error.
                    //
                    // This guard is also load-bearing for INDEX correctness: because a resolved PREVIOUS
                    // implies both this batch and the target batch run their queries, the executable-batch
                    // list always holds more than one entry, so ResultsTargetGrid.GetExecutableBatches
                    // never takes its collapse-to-a-single-(0, wholeQuery)-entry path. That keeps the
                    // batch indexes the resolver produced aligned with the ones the runtime uses.
                    if (!RunsItsQuery(batch))
                    {
                        commandErrors?.Add(new Error
                        {
                            Msg = "'PREVIOUS' compares this batch's query against the previous one, but this batch has no query to run. Add a query to this batch, or remove the assertion.",
                            Line = LineOf(batch, reference),
                            Column = 0,
                        });
                        continue;
                    }

                    var targetIndex = FindPreviousQueryBatch(batches, batchIndex);
                    if (targetIndex < 0)
                    {
                        commandErrors?.Add(new Error
                        {
                            Msg = "'PREVIOUS' has no earlier batch that runs a query to compare against. Add a query in an earlier batch (separated by '--> GO'), or capture a named baseline with '--> BASELINE \"name\"'.",
                            Line = LineOf(batch, reference),
                            Column = 0,
                        });
                        continue;
                    }

                    var name = BuildName(targetIndex);
                    EnsureBaselineCaptured(batches[targetIndex], name);

                    // Keep IsPrevious set so the assertion still displays as "PREVIOUS" rather than
                    // this internal name.
                    reference.Name = name;
                }
            }
        }

        /// <summary>
        /// The generated baseline name for the batch a <c>PREVIOUS</c> resolves to. Names in this shape
        /// are rejected by the parser when written by a user (see
        /// <see cref="BaselineReference.IsReservedName"/>), so a synthesised name can never collide with
        /// a user-authored one.
        /// </summary>
        private static string BuildName(int targetBatchIndex) => $"(previous:{targetBatchIndex})";

        private static IEnumerable<BaselineReference> UnresolvedReferences(ScriptBatch batch)
        {
            foreach (var cmd in batch.Commands)
            {
                var reference = BaselineOf(cmd);
                if (reference != null && reference.IsUnresolvedPrevious) yield return reference;
            }
        }

        private static BaselineReference BaselineOf(ScriptCommand cmd)
        {
            switch (cmd)
            {
                case AssertCommand a: return a.Baseline;
                case AssertRowcountCommand r: return r.Baseline;
                case AssertTableCommand t: return t.Baseline;
                default: return null;
            }
        }

        private static int LineOf(ScriptBatch batch, BaselineReference reference)
        {
            foreach (var cmd in batch.Commands)
            {
                if (ReferenceEquals(BaselineOf(cmd), reference)) return cmd.Line;
            }
            return 0;
        }

        /// <summary>
        /// Walks backwards for the closest earlier batch that actually runs its query. Returns -1 when
        /// there is none.
        /// </summary>
        private static int FindPreviousQueryBatch(IList<ScriptBatch> batches, int fromIndex)
        {
            for (int i = fromIndex - 1; i >= 0; i--)
            {
                if (RunsItsQuery(batches[i])) return i;
            }
            return -1;
        }

        /// <summary>
        /// True when a batch sends its DAX to the server. Delegates to
        /// <see cref="ScriptBatch.RunsItsQuery"/>, which is the single definition shared with the
        /// batch-execution loop so the parser and the runtime cannot disagree about which batches run.
        /// </summary>
        private static bool RunsItsQuery(ScriptBatch batch) => batch?.RunsItsQuery ?? false;

        /// <summary>
        /// Adds the synthesised <see cref="BaselineCommand"/> to the target batch unless it already has
        /// one under that name. Several references can resolve to the same batch (e.g. an
        /// <c>ASSERT TABLE PREVIOUS</c> and an <c>ASSERT DURATION &lt;= PREVIOUS</c> in the same batch,
        /// or two later batches both pointing back at it), and the batch must only be captured once.
        /// </summary>
        /// <remarks>
        /// Appending after the parse-tree walk is safe: a batch's <c>Output</c> buffer is only used for
        /// diagnostics, and its executable <see cref="ScriptBatch.QueryText"/> is assigned separately
        /// from the raw script text, so adding a command object here cannot change the DAX that runs.
        /// </remarks>
        private static void EnsureBaselineCaptured(ScriptBatch target, string name)
        {
            if (target.Commands.OfType<BaselineCommand>()
                .Any(b => string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            target.Commands.Add(new BaselineCommand(name));
        }
    }
}
