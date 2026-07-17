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
    }
}
