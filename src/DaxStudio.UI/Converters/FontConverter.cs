using System;
using System.Diagnostics;
using System.Windows.Media;
using System.Globalization;
using System.Windows.Data;

namespace DaxStudio.UI.Converters
{
    /// <summary>
    /// This converter does nothing except breaking the
    /// debugger into the convert method
    /// </summary>
    public class FontConverter : IValueConverter
    {
        public object Convert(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            var f = new FontFamily(value.ToString());
            return f;
        }

        public object ConvertBack(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            var f = value as FontFamily;
            return f.Source;
        }
    }
}
