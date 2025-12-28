using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ADOTabular;
using Serilog;

namespace DaxStudio.UI.Services
{
    /// <summary>
    /// Resolves column internal IDs to human-readable names using ADOTabular metadata.
    /// </summary>
    public class ColumnNameResolver : IColumnNameResolver
    {
        private readonly Dictionary<string, string> _cache = new Dictionary<string, string>();
        private ADOTabularColumnCollection _columns;

        // Pattern to match column references like: '$Column12345' or 'Column12345'
        private static readonly Regex ColumnRefPattern = new Regex(
            @"\$?Column\d+",
            RegexOptions.Compiled);

        /// <summary>
        /// Gets whether the resolver has been initialized with column metadata.
        /// </summary>
        public bool IsInitialized => _columns != null;

        /// <summary>
        /// Initializes the resolver with column metadata from the current connection.
        /// </summary>
        /// <param name="columns">Column collection from ADOTabular</param>
        public void Initialize(ADOTabularColumnCollection columns)
        {
            _columns = columns;
            _cache.Clear();
        }

        /// <summary>
        /// Resolves a column internal reference to its display name.
        /// </summary>
        /// <param name="columnRef">Internal column reference/ID</param>
        /// <returns>Resolved column name, or original if not found</returns>
        public string ResolveColumnName(string columnRef)
        {
            if (string.IsNullOrEmpty(columnRef))
                return columnRef;

            // Check cache first
            if (_cache.TryGetValue(columnRef, out var cachedName))
                return cachedName;

            if (_columns == null)
            {
                Log.Debug("ColumnNameResolver: Not initialized, returning original reference: {ColumnRef}", columnRef);
                return columnRef;
            }

            try
            {
                // Try to resolve via ADOTabular
                var column = _columns.GetByPropertyRef(columnRef);
                if (column != null)
                {
                    var resolvedName = $"'{column.Table.Name}'[{column.Name}]";
                    _cache[columnRef] = resolvedName;
                    return resolvedName;
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "ColumnNameResolver: Failed to resolve column reference: {ColumnRef}", columnRef);
            }

            // Return original if not found
            return columnRef;
        }

        /// <summary>
        /// Resolves all column references in an operation string.
        /// </summary>
        /// <param name="operation">Raw operation string with column IDs</param>
        /// <returns>Operation string with resolved column names</returns>
        public string ResolveOperationString(string operation)
        {
            if (string.IsNullOrEmpty(operation))
                return operation;

            if (!IsInitialized)
                return operation;

            // Replace all column references in the operation string
            var result = ColumnRefPattern.Replace(operation, match =>
            {
                var resolved = ResolveColumnName(match.Value);
                return resolved;
            });

            return result;
        }
    }
}
