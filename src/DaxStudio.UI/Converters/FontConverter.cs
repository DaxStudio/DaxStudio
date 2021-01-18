using System;
using System.Diagnostics;
using System.Windows.Media;
using System.Globalization;
using System.Windows.Data;
using Serilog;

namespace DaxStudio.UI.Converters
{
    /// <summary>
    /// This converter takes a font name and converts it to a FontFamily object and back again
    /// </summary>
    public class FontConverter : IValueConverter
    {
        public object Convert(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            FontFamily f;
            try
            {
                f = new FontFamily(value.ToString());
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{class} {method} {message}", nameof(FontConverter), nameof(Convert), $"Error converting {value} to a FontFamily Object: {ex.Message}");
                // try setting a default font family if we hit an error
                f = new FontFamily("Courier New, Segoe UI, Arial");
            }
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
