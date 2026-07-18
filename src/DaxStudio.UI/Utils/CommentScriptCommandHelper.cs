using System;
using System.Collections.Generic;
using System.Linq;
using ADOTabular;
using DaxStudio.Parsers.CommentScript;
using DaxStudio.UI.ViewModels;

namespace DaxStudio.UI.Utils
{
    /// <summary>
    /// Pure helper logic used when executing Comment Script <c>--&gt; USE</c> and
    /// <c>--&gt; TRACE</c> commands. Kept free of any UI / <c>DocumentViewModel</c> state so the
    /// (bug-prone) name-resolution and trace-type-mapping logic can be unit tested in isolation.
    /// </summary>
    public static class CommentScriptCommandHelper
    {
        /// <summary>
        /// Normalizes a <c>--&gt; USE</c> database argument by trimming surrounding whitespace and
        /// any wrapping double quotes. Returns <c>null</c> when the input is <c>null</c>.
        /// </summary>
        public static string NormalizeDatabaseName(string raw)
            => raw?.Trim().Trim('"').Trim();

        /// <summary>
        /// Resolves the database targeted by a <c>--&gt; USE</c> command against the list of
        /// available databases, matching on either <see cref="DatabaseDetails.Name"/> or
        /// <see cref="DatabaseDetails.Caption"/> (case-insensitive). The Caption is matched as well
        /// because that is the friendly name shown in the metadata pane dropdown. Returns the
        /// matching database, or <c>null</c> when the name is blank or no match is found.
        /// </summary>
        public static DatabaseDetails ResolveDatabase(IEnumerable<DatabaseDetails> databases, string requestedName)
        {
            var name = NormalizeDatabaseName(requestedName);
            if (string.IsNullOrWhiteSpace(name) || databases == null) return null;

            return databases.FirstOrDefault(d =>
                string.Equals(d?.Name, name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(d?.Caption, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Inspects the first batch of a parsed comment-script document for an auto-connect
        /// declaration: a <c>--&gt; CONNECT</c> command (and the optional accompanying
        /// <c>--&gt; USE</c> that selects a database). This is used when opening a file so a file that
        /// already declares its connection can connect automatically instead of prompting with the
        /// connection dialog. Returns <c>true</c> when a CONNECT command was found in the first batch,
        /// with <paramref name="connectCommand"/> set to the first CONNECT and
        /// <paramref name="targetDatabase"/> set to the normalized database from the last USE in that
        /// batch (or <c>null</c> when no USE was present). Returns <c>false</c> (with <c>null</c>
        /// outputs) when there are no batches or the first batch has no CONNECT command.
        /// </summary>
        public static bool TryGetAutoConnectCommand(
            IReadOnlyList<ScriptBatch> batches,
            out ConnectCommand connectCommand,
            out string targetDatabase)
        {
            connectCommand = null;
            targetDatabase = null;

            var firstBatch = batches?.FirstOrDefault();
            if (firstBatch == null) return false;

            connectCommand = firstBatch.Commands.OfType<ConnectCommand>().FirstOrDefault();
            if (connectCommand == null) return false;

            var useCommand = firstBatch.Commands.OfType<UseCommand>().LastOrDefault();
            targetDatabase = NormalizeDatabaseName(useCommand?.DatabaseName);
            return true;
        }

        /// <summary>
        /// Maps a Comment Script <see cref="TraceType"/> to the concrete trace-watcher ViewModel
        /// type that implements it. Returns <c>null</c> for an unknown trace type.
        /// </summary>
        public static Type GetTraceWatcherType(TraceType traceType)
        {
            switch (traceType)
            {
                case TraceType.ServerTimings:
                    return typeof(ServerTimesViewModel);
                case TraceType.QueryPlan:
                    return typeof(QueryPlanTraceViewModel);
                case TraceType.AllQueries:
                    return typeof(AllServerQueriesViewModel);
                default:
                    return null;
            }
        }

        /// <summary>
        /// Determines whether a <c>--&gt; TRACE &lt;type&gt; ON</c> command should clear the trace's
        /// accumulated results when the target trace is already running. This is <c>true</c> for
        /// every trace type except <see cref="TraceType.AllQueries"/>, whose captured queries are
        /// intentionally left to accumulate across runs.
        /// </summary>
        public static bool ShouldClearResultsWhenAlreadyRunning(TraceType traceType)
            => traceType != TraceType.AllQueries;
    }
}
