using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Extensions
{
    public static class ContextExtensions
    {
        private static readonly string DatabaseNameKey = "DatabaseNameKey";

        public static Context WithDatabaseName(this Context context, string databaseName)
        {
            context[DatabaseNameKey] = databaseName;
            return context;
        }

        public static string GetDatabaseName(this Context context)
        {
            if (context.TryGetValue(DatabaseNameKey, out object databaseName))
            {
                return databaseName as string;
            }
            return null;
        }
    }
}
