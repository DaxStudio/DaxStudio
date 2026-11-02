using System;
using System.Collections.Generic;
using System.Data;
using DaxStudio.Parsers.CommentScript;

namespace DaxStudio.Core.Assertions
{
    /// <summary>
    /// A snapshot of one batch's results and Server Timings metrics, captured by a
    /// <c>--&gt; BASELINE ["name"]</c> command so that a later batch can assert against it.
    /// </summary>
    public sealed class CapturedBaseline
    {
        public CapturedBaseline(string name, DataTable results, IReadOnlyDictionary<PerformanceProperty, double> metrics, int runs = 1)
        {
            Name = name;
            Results = results;
            Metrics = metrics ?? new Dictionary<PerformanceProperty, double>();
            Runs = runs;
        }

        /// <summary>The baseline's name (<see cref="BaselineReference.DefaultName"/> when unnamed).</summary>
        public string Name { get; }

        /// <summary>
        /// The captured result set, or <c>null</c> when the batch produced no result table. This is a
        /// private copy - the live results <see cref="DataSet"/> is reused and cleared between batches.
        /// </summary>
        public DataTable Results { get; }

        /// <summary>
        /// The Server Timings metrics captured for the batch. Empty when the trace was not running or
        /// captured no data, in which case a performance assertion against this baseline reports an error.
        /// </summary>
        public IReadOnlyDictionary<PerformanceProperty, double> Metrics { get; }

        /// <summary>The number of executions the metrics were aggregated over. Always 1 today.</summary>
        public int Runs { get; }

        /// <summary>The number of rows in the captured result set (0 when there was no result table).</summary>
        public int RowCount => Results?.Rows.Count ?? 0;
    }

    /// <summary>
    /// The run-scoped set of baselines captured by <c>--&gt; BASELINE</c> commands, keyed by name.
    /// Kept UI-independent (like <see cref="AssertionEngine"/>) so the same store can back the DAX
    /// Studio UI and, later, the <c>dscmd</c> CLI.
    /// </summary>
    /// <remarks>
    /// A store instance covers a single script run: <see cref="Clear"/> is called at the start of each
    /// run so a baseline never leaks from one execution into the next.
    /// </remarks>
    public sealed class BaselineStore
    {
        private readonly Dictionary<string, CapturedBaseline> _baselines =
            new Dictionary<string, CapturedBaseline>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Captures a batch's result set and performance metrics under <paramref name="name"/>
        /// (last write wins). The supplied <paramref name="results"/> table is <b>copied</b>, because
        /// the live results <see cref="DataSet"/> is reused and cleared as later batches run.
        /// </summary>
        public void Capture(string name, DataTable results, IReadOnlyDictionary<PerformanceProperty, double> metrics, int runs = 1)
        {
            var key = string.IsNullOrWhiteSpace(name) ? BaselineReference.DefaultName : name;
            var snapshot = results?.Copy();

            var metricsCopy = new Dictionary<PerformanceProperty, double>();
            if (metrics != null)
            {
                foreach (var kvp in metrics) metricsCopy[kvp.Key] = kvp.Value;
            }

            _baselines[key] = new CapturedBaseline(key, snapshot, metricsCopy, runs);
        }

        /// <summary>Returns the baseline captured under <paramref name="name"/>, if any.</summary>
        public bool TryGet(string name, out CapturedBaseline baseline)
        {
            var key = string.IsNullOrWhiteSpace(name) ? BaselineReference.DefaultName : name;
            return _baselines.TryGetValue(key, out baseline);
        }

        /// <summary>Removes every captured baseline. Called at the start of each run.</summary>
        public void Clear() => _baselines.Clear();

        /// <summary>The number of baselines captured so far in this run.</summary>
        public int Count => _baselines.Count;
    }
}
