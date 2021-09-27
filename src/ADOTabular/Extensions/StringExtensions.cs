using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADOTabular.Extensions
{
    public static class StringExtensions
    {
        public static bool IsPowerBIService(this string connectionString)
        {
            if (connectionString == null) return false;
            return connectionString.StartsWith("powerbi://", StringComparison.OrdinalIgnoreCase)
                   || connectionString.StartsWith("pbiazure://", StringComparison.OrdinalIgnoreCase)
                   || connectionString.StartsWith("pbidedicated://", StringComparison.OrdinalIgnoreCase);
        }
    }
}
