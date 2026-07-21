using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using DaxStudio.Parsers.CommentScript;

namespace DaxStudio.Core.Assertions
{
    /// <summary>
    /// UI-independent evaluator for the comment-script assertion commands
    /// (<c>--&gt; ASSERT ROWCOUNT</c>, <c>--&gt; ASSERT</c> performance, <c>--&gt; ASSERT TABLE</c>).
    /// Kept free of any UI / <c>DocumentViewModel</c> state so it can be shared by the DAX Studio
    /// UI (Test Results pane) and the <c>dscmd</c> CLI (console summary + exit code), and unit
    /// tested in isolation without a live Analysis Services connection.
    /// </summary>
    public static class AssertionEngine
    {
        // Numbers parsed from result data and expected values can differ by tiny amounts due to
        // floating point representation, so equality comparisons use a small relative tolerance.
        private const double Epsilon = 1e-9;

        #region ROWCOUNT

        /// <summary>
        /// Evaluates a <c>--&gt; ASSERT ROWCOUNT &lt;op&gt; &lt;value&gt;</c> command against the number of
        /// rows returned by the query.
        /// </summary>
        public static TestResult EvaluateRowCount(AssertRowcountCommand command, long actualRowCount, string testName = null)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            var result = new TestResult
            {
                TestName = testName,
                Kind = AssertionKind.RowCount,
                Description = DescribeRowCount(command),
                Expected = ExpectedRowCount(command),
                Actual = actualRowCount.ToString(CultureInfo.InvariantCulture),
            };

            try
            {
                var passed = Compare(actualRowCount, command.Comparison, command.Value);
                result.Outcome = passed ? TestOutcome.Passed : TestOutcome.Failed;
                result.Message = passed
                    ? string.Empty
                    : $"Expected row count {command.Comparison} {command.Value} but got {actualRowCount}";
            }
            catch (Exception ex)
            {
                result.Outcome = TestOutcome.Error;
                result.Message = ex.Message;
            }

            return result;
        }

        #endregion

        #region Performance (DURATION / SE_CPU / SE_QUERIES)

        /// <summary>
        /// Evaluates a <c>--&gt; ASSERT &lt;DURATION|SE_CPU|SE_QUERIES&gt; &lt;op&gt; &lt;value&gt;</c> command
        /// against the captured Server Timings metrics. The metric value for the command's
        /// <see cref="AssertCommand.Property"/> must be present in <paramref name="metrics"/>;
        /// when it is missing the result is an error (the required trace was not running).
        /// </summary>
        public static TestResult EvaluatePerformance(
            AssertCommand command,
            IReadOnlyDictionary<PerformanceProperty, double> metrics,
            string testName = null)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            var expectedValue = GetExpectedValue(command);
            var result = new TestResult
            {
                TestName = testName,
                Kind = AssertionKind.Performance,
                Description = DescribePerformance(command),
                Expected = ExpectedPerformance(command),
            };

            try
            {
                if (metrics == null || !metrics.TryGetValue(command.Property, out var actual))
                {
                    result.Outcome = TestOutcome.Error;
                    result.Actual = "n/a";
                    result.Message = $"No '{command.Property}' metric was captured - is the Server Timings trace running?";
                    return result;
                }

                result.Actual = FormatNumber(actual);
                var passed = Compare(actual, command.Comparison, expectedValue);
                result.Outcome = passed ? TestOutcome.Passed : TestOutcome.Failed;
                result.Message = passed
                    ? string.Empty
                    : $"Expected {command.Property} {command.Comparison} {FormatNumber(expectedValue)} but got {FormatNumber(actual)}";
            }
            catch (Exception ex)
            {
                result.Outcome = TestOutcome.Error;
                result.Message = ex.Message;
            }

            return result;
        }

        private static double GetExpectedValue(AssertCommand command)
        {
            // The listener sets either IntegerValue or DoubleValue depending on the literal type;
            // the unused one is left at 0, so prefer the non-zero one (both 0 => expected 0).
            return command.DoubleValue != 0.0 ? command.DoubleValue : command.IntegerValue;
        }

        #endregion

        #region TABLE

        /// <summary>
        /// Evaluates a <c>--&gt; ASSERT TABLE</c> command by comparing the query's result table to the
        /// inline expected table. The comparison honors the command's <see cref="AssertTableMode"/>:
        /// <list type="bullet">
        /// <item><c>Ordered</c> - same columns, same rows, in the same order.</item>
        /// <item><c>Unordered</c> - same columns and the same multiset of rows, any order.</item>
        /// <item><c>Partial</c> - every expected row must be found in the actual table (which may
        /// contain extra rows and extra columns); order is ignored.</item>
        /// </list>
        /// </summary>
        public static TestResult EvaluateTable(AssertTableCommand command, DataTable actual, string testName = null, string baseDirectory = null)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            var result = new TestResult
            {
                TestName = testName,
                Kind = AssertionKind.Table,
                Description = DescribeTable(command),
                Expected = ExpectedTable(command),
                Actual = $"{actual?.Rows.Count ?? 0} row(s)",
            };

            try
            {
                // For file-based ASSERT TABLE the expected rows are loaded lazily here so that
                // missing-file / bad-format errors surface as an assertion error alongside the query.
                if (command.Format != AssertTableFormat.Inline)
                {
                    AssertTableFileLoader.LoadInto(command, baseDirectory);
                    result.Expected = ExpectedTable(command);
                }

                var expected = command.Data;
                if (expected == null)
                {
                    result.Outcome = TestOutcome.Error;
                    result.Message = "ASSERT TABLE has no expected data";
                    return result;
                }
                if (actual == null)
                {
                    result.Outcome = TestOutcome.Failed;
                    result.Message = "The query returned no result table to compare against";
                    return result;
                }

                if (!TryMapColumns(expected, actual, command.Mode, out var columnMap, out var columnError))
                {
                    result.Outcome = TestOutcome.Failed;
                    result.Message = columnError;
                    return result;
                }

                string rowError;
                bool matched;
                switch (command.Mode)
                {
                    case AssertTableMode.Ordered:
                        matched = CompareOrdered(expected, actual, columnMap, out rowError);
                        break;
                    case AssertTableMode.Unordered:
                        matched = CompareUnordered(expected, actual, columnMap, requireSameCount: true, out rowError);
                        break;
                    case AssertTableMode.Partial:
                        matched = CompareUnordered(expected, actual, columnMap, requireSameCount: false, out rowError);
                        break;
                    default:
                        result.Outcome = TestOutcome.Error;
                        result.Message = $"Unsupported ASSERT TABLE mode '{command.Mode}'";
                        return result;
                }

                result.Outcome = matched ? TestOutcome.Passed : TestOutcome.Failed;
                result.Message = matched ? string.Empty : rowError;
            }
            catch (Exception ex)
            {
                result.Outcome = TestOutcome.Error;
                result.Message = ex.Message;
            }

            return result;
        }

        // Maps each expected column to the matching actual column (by name, case-insensitive).
        // For Ordered/Unordered the two tables must have the same set of columns; for Partial the
        // expected columns must be a subset of the actual columns.
        private static bool TryMapColumns(DataTable expected, DataTable actual, AssertTableMode mode, out int[] columnMap, out string error)
        {
            columnMap = new int[expected.Columns.Count];
            error = string.Empty;

            for (int i = 0; i < expected.Columns.Count; i++)
            {
                var name = expected.Columns[i].ColumnName;
                // The results DataTable stores the display name in Caption (ColumnName is escaped -
                // e.g. spaces become backticks - as a grid-sorting workaround), so match on the
                // friendly Caption first, falling back to the (possibly escaped) ColumnName.
                var actualCol = actual.Columns
                    .Cast<DataColumn>()
                    .FirstOrDefault(c => string.Equals(FriendlyColumnName(c), name, StringComparison.OrdinalIgnoreCase)
                                      || string.Equals(c.ColumnName, name, StringComparison.OrdinalIgnoreCase));
                if (actualCol == null)
                {
                    error = $"Expected column '{name}' was not found in the result";
                    return false;
                }
                columnMap[i] = actualCol.Ordinal;
            }

            if (mode != AssertTableMode.Partial && actual.Columns.Count != expected.Columns.Count)
            {
                error = $"Expected {expected.Columns.Count} column(s) but the result has {actual.Columns.Count}";
                return false;
            }

            return true;
        }

        // The results DataTable stores the friendly display name in Caption; ColumnName may be an
        // escaped variant (spaces/commas replaced by backticks) used as a grid-sorting workaround.
        private static string FriendlyColumnName(DataColumn column)
        {
            return string.IsNullOrEmpty(column.Caption) ? column.ColumnName : column.Caption;
        }

        private static bool CompareOrdered(DataTable expected, DataTable actual, int[] columnMap, out string error)
        {
            if (expected.Rows.Count != actual.Rows.Count)
            {
                error = $"Expected {expected.Rows.Count} row(s) but the result has {actual.Rows.Count}";
                return false;
            }

            for (int r = 0; r < expected.Rows.Count; r++)
            {
                if (!RowsMatch(expected.Rows[r], actual.Rows[r], columnMap))
                {
                    error = $"Row {r + 1} does not match: expected [{DescribeRow(expected.Rows[r], columnMap.Length)}] but got [{DescribeActualRow(actual.Rows[r], columnMap)}]";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool CompareUnordered(DataTable expected, DataTable actual, int[] columnMap, bool requireSameCount, out string error)
        {
            if (requireSameCount && expected.Rows.Count != actual.Rows.Count)
            {
                error = $"Expected {expected.Rows.Count} row(s) but the result has {actual.Rows.Count}";
                return false;
            }

            // Track which actual rows have already been matched so duplicate expected rows must be
            // backed by an equal number of actual rows (multiset comparison).
            var used = new bool[actual.Rows.Count];

            foreach (DataRow expectedRow in expected.Rows)
            {
                var matchIndex = -1;
                for (int a = 0; a < actual.Rows.Count; a++)
                {
                    if (used[a]) continue;
                    if (RowsMatch(expectedRow, actual.Rows[a], columnMap))
                    {
                        matchIndex = a;
                        break;
                    }
                }

                if (matchIndex < 0)
                {
                    error = $"Expected row [{DescribeRow(expectedRow, columnMap.Length)}] was not found in the result";
                    return false;
                }

                used[matchIndex] = true;
            }

            error = string.Empty;
            return true;
        }

        private static bool RowsMatch(DataRow expectedRow, DataRow actualRow, int[] columnMap)
        {
            for (int i = 0; i < columnMap.Length; i++)
            {
                if (!ValuesEqual(expectedRow[i], actualRow[columnMap[i]]))
                    return false;
            }
            return true;
        }

        // Compares two cell values tolerantly: nulls match nulls, numbers compare with a small
        // tolerance (regardless of int/double/decimal boxing), DateTimes compare by value, and
        // everything else compares via invariant-culture string equality.
        private static bool ValuesEqual(object expected, object actual)
        {
            var expectedNull = expected == null || expected == DBNull.Value;
            var actualNull = actual == null || actual == DBNull.Value;
            if (expectedNull || actualNull) return expectedNull && actualNull;

            if (IsNumeric(expected) && IsNumeric(actual))
            {
                var e = Convert.ToDouble(expected, CultureInfo.InvariantCulture);
                var a = Convert.ToDouble(actual, CultureInfo.InvariantCulture);
                var scale = Math.Max(1.0, Math.Max(Math.Abs(e), Math.Abs(a)));
                return Math.Abs(e - a) <= Epsilon * scale;
            }

            if (expected is DateTime ed && actual is DateTime ad)
                return ed == ad;

            if (expected is bool eb && actual is bool ab)
                return eb == ab;

            return string.Equals(
                Convert.ToString(expected, CultureInfo.InvariantCulture),
                Convert.ToString(actual, CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
        }

        private static bool IsNumeric(object value)
        {
            switch (value)
            {
                case byte _:
                case sbyte _:
                case short _:
                case ushort _:
                case int _:
                case uint _:
                case long _:
                case ulong _:
                case float _:
                case double _:
                case decimal _:
                    return true;
                default:
                    return false;
            }
        }

        private static string DescribeRow(DataRow row, int columnCount)
        {
            var cells = new string[columnCount];
            for (int i = 0; i < columnCount; i++)
                cells[i] = FormatCell(row[i]);
            return string.Join(", ", cells);
        }

        private static string DescribeActualRow(DataRow row, int[] columnMap)
        {
            var cells = new string[columnMap.Length];
            for (int i = 0; i < columnMap.Length; i++)
                cells[i] = FormatCell(row[columnMap[i]]);
            return string.Join(", ", cells);
        }

        private static string FormatCell(object value)
        {
            if (value == null || value == DBNull.Value) return "(null)";
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        #endregion

        #region Discovery

        /// <summary>
        /// Builds a "pending" <see cref="TestResult"/> for every assertion found in the supplied
        /// script batches, without evaluating them against any query result. Used to populate the
        /// Test Results pane as the user types (greyed-out rows with a clock icon) before the query
        /// has been run. The rows are produced in the same per-batch order the run uses
        /// (row count, then table, then performance) so the pending rows line up with the results
        /// that replace them once the query is executed. Any enclosing
        /// <c>--&gt; TEST "name"</c> header is applied to every assertion in that batch.
        /// </summary>
        public static IReadOnlyList<TestResult> DiscoverTests(IReadOnlyList<ScriptBatch> batches)
        {
            var results = new List<TestResult>();
            if (batches == null) return results;

            for (int batchIndex = 0; batchIndex < batches.Count; batchIndex++)
            {
                var batch = batches[batchIndex];
                if (batch?.Commands == null) continue;

                var testName = batch.Commands.OfType<TestCommand>().FirstOrDefault()?.TestName;

                foreach (var cmd in batch.Commands.OfType<AssertRowcountCommand>())
                    results.Add(CreatePending(AssertionKind.RowCount, DescribeRowCount(cmd), ExpectedRowCount(cmd), testName, batchIndex));

                foreach (var cmd in batch.Commands.OfType<AssertTableCommand>())
                    results.Add(CreatePending(AssertionKind.Table, DescribeTable(cmd), ExpectedTable(cmd), testName, batchIndex));

                foreach (var cmd in batch.Commands.OfType<AssertCommand>())
                    results.Add(CreatePending(AssertionKind.Performance, DescribePerformance(cmd), ExpectedPerformance(cmd), testName, batchIndex));
            }

            return results;
        }

        private static TestResult CreatePending(AssertionKind kind, string description, string expected, string testName, int batchIndex)
        {
            return new TestResult
            {
                TestName = testName,
                Kind = kind,
                Description = description,
                Expected = expected,
                Actual = string.Empty,
                Message = string.Empty,
                Outcome = TestOutcome.Pending,
                BatchIndex = batchIndex,
            };
        }

        // Description / expected-value formatting shared by the Evaluate* methods and DiscoverTests so
        // a discovered (pending) row and the row that later replaces it have identical text.
        private static string DescribeRowCount(AssertRowcountCommand c) => $"ROWCOUNT {c.Comparison} {c.Value}";
        private static string ExpectedRowCount(AssertRowcountCommand c) => $"{c.Comparison} {c.Value}";

        private static string DescribePerformance(AssertCommand c) => $"{c.Property} {c.Comparison} {FormatNumber(GetExpectedValue(c))}";
        private static string ExpectedPerformance(AssertCommand c) => $"{c.Comparison} {FormatNumber(GetExpectedValue(c))}";

        private static string DescribeTable(AssertTableCommand c) => $"TABLE {c.Mode}";
        private static string ExpectedTable(AssertTableCommand c) => $"{c.Data?.Rows.Count ?? 0} row(s)";

        #endregion

        #region Helpers

        // Applies one of the comment-script comparison operators (=, >, <, >=, <=). Note the
        // grammar does not define a not-equal operator, so only these five are supported.
        private static bool Compare(double actual, string op, double expected)
        {
            switch (op)
            {
                case "=":  return Math.Abs(actual - expected) <= Epsilon * Math.Max(1.0, Math.Max(Math.Abs(actual), Math.Abs(expected)));
                case ">":  return actual > expected;
                case "<":  return actual < expected;
                case ">=": return actual > expected || Math.Abs(actual - expected) <= Epsilon * Math.Max(1.0, Math.Max(Math.Abs(actual), Math.Abs(expected)));
                case "<=": return actual < expected || Math.Abs(actual - expected) <= Epsilon * Math.Max(1.0, Math.Max(Math.Abs(actual), Math.Abs(expected)));
                default:
                    throw new ArgumentException($"Unsupported comparison operator '{op}'");
            }
        }

        private static string FormatNumber(double value)
        {
            return value == Math.Floor(value)
                ? ((long)value).ToString(CultureInfo.InvariantCulture)
                : value.ToString(CultureInfo.InvariantCulture);
        }

        #endregion
    }
}
