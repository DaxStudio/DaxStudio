using System.Data.Common;

namespace ADOTabular.Utils
{
    /// <summary>
    /// Cross-platform helpers for building and editing Analysis Services connection
    /// strings. These use <see cref="DbConnectionStringBuilder"/> (in-framework,
    /// available on every platform) instead of the Windows-only
    /// <c>System.Data.OleDb.OleDbConnectionStringBuilder</c>, so the shared assemblies
    /// can be compiled and run cross-platform (e.g. a net8.0 <c>dscmd</c>).
    ///
    /// For the keys used by DAX Studio, <see cref="DbConnectionStringBuilder"/> produces
    /// output equivalent to the OLE DB builder (identical value quoting/escaping); the
    /// only observable difference is that parsed keys are lower-cased, which is
    /// inconsequential because AS connection-string keys are case-insensitive.
    /// </summary>
    public static class ConnectionStringBuilderExtensions
    {
        /// <summary>
        /// Creates a <see cref="DbConnectionStringBuilder"/> from an existing connection
        /// string. A null or empty input produces an empty builder (matching the
        /// behaviour of <c>new OleDbConnectionStringBuilder(null)</c>).
        /// </summary>
        public static DbConnectionStringBuilder ToConnectionStringBuilder(this string connectionString)
        {
            var builder = new DbConnectionStringBuilder();
            if (!string.IsNullOrEmpty(connectionString))
            {
                builder.ConnectionString = connectionString;
            }
            return builder;
        }

        /// <summary>
        /// Returns the "Data Source" value from the builder, or an empty string when the
        /// key is not present. Replacement for the OLE DB builder's
        /// <c>DataSource</c> property.
        /// </summary>
        public static string GetDataSource(this DbConnectionStringBuilder builder)
        {
            return builder.TryGetValue("Data Source", out var value)
                ? value?.ToString() ?? string.Empty
                : string.Empty;
        }
    }
}
