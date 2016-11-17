using System;
using System.Globalization;
using System.Windows.Data;
using Microsoft.AnalysisServices;
using System.Text.RegularExpressions;

namespace DaxStudio.UI.Converters {
    class QueryMultiLineConverter : IValueConverter {
        private const int SPACE_PER_LEVEL = 4;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var sc = value as string;
            if (sc != null) {
                return sc.Replace("\t", new string(' ', SPACE_PER_LEVEL));
            }
            return System.Windows.Data.Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return System.Windows.Data.Binding.DoNothing;
        }

    }
}
