using System;
using System.Globalization;
using System.Windows.Data;
using DaxStudio.UI.Extensions;

namespace DaxStudio.UI.Converters
{
    class StringWhitespaceRemovalConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var sc = value as string;
            if (sc != null)
            {
                return sc.StripLineBreaks();
            }
            return System.Windows.Data.Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return System.Windows.Data.Binding.DoNothing;
        }
    }
}

