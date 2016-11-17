using System;
using System.Globalization;
using System.Windows.Data;
using Microsoft.AnalysisServices;
using System.Text.RegularExpressions;

namespace DaxStudio.UI.Converters {
    class QuerySingleLineConverter : IValueConverter {
        const string searchSet = @"SET.*;";
        const string searchTab = @"\r\n\t+|\n\t+|\r\t+|\r\n|\r|\n|\t+";

        static Regex setRemoval = new Regex(searchSet, RegexOptions.Compiled);
        static Regex tabRemoval = new Regex(searchTab, RegexOptions.Compiled);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var sc = value as string;
            if (sc != null) {
                string s1 = setRemoval.Replace(sc, "");
                string s2 = tabRemoval.Replace(s1, " ");
                return s2.Trim();
            }
            return System.Windows.Data.Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return System.Windows.Data.Binding.DoNothing;
        }
    }
}
