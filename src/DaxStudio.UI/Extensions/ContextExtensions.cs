using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DaxStudio.UI.Extensions
{
    public static class ContextExtensions
    {
        private static readonly string DatabaseNameKey = "DatabaseNameKey";
        private static readonly string TextKey = "TextKey";
        private static readonly string TextDataFormatKey = "TextDataFormatKey";

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

        public static Context WithText(this Context context, string text)
        {
            context[TextKey] = text;
            return context;
        }

        public static string GetText(this Context context)
        {
            if (context.TryGetValue(TextKey, out object text))
            {
                return text as string;
            }
            return null;
        }

        public static Context WithTextDataFormat(this Context context, TextDataFormat format)
        {
            context[TextDataFormatKey] = format;
            return context;
        }

        public static TextDataFormat GetTextDataFormat(this Context context)
        {
            if (context.TryGetValue(TextKey, out object format))
            {
                return (TextDataFormat)format;
            }
            return TextDataFormat.Text;
        }
    }
}
