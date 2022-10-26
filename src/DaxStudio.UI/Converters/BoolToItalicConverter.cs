using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows;

namespace DaxStudio.UI.Converters
{
    internal class BoolToItalicConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var b = value as bool?;
            if (b ?? false) return FontStyles.Normal;
            return FontStyles.Italic;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
