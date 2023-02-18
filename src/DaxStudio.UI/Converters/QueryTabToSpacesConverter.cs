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

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var sc = value as string;
            if (sc != null)
            {
                return sc.Replace("\t", "    ");
            }
            return System.Windows.Data.Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return System.Windows.Data.Binding.DoNothing;
        }
    }
}
