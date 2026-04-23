using System;
using System.Globalization;
using System.Windows.Data;
using Microsoft.AnalysisServices;
using System.Text.RegularExpressions;
using System.Linq;

namespace DaxStudio.UI.Converters
{
    class QueryTabToSpacesConverter : IValueConverter
    {
        const int TabSpaces = 4;
        static private string tabSpaces = new string(' ', TabSpaces);

        private const int MaxTooltipLength = 2000;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var sc = value as string;
            if (sc != null)
            {
                // Truncate for tooltip display to avoid WPF layout freezes on very large queries
                bool truncated = sc.Length > MaxTooltipLength;
                if (truncated) sc = sc.Substring(0, MaxTooltipLength);
                var result = sc.Replace("\t", "    ");
                return truncated ? result + "\n\n[… truncated]" : result;
            }
            return System.Windows.Data.Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return System.Windows.Data.Binding.DoNothing;
        }
    }
}
