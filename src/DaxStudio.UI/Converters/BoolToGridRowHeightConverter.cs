using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DaxStudio.UI.Converters
{
    [ValueConversion(typeof(bool), typeof(GridLength))]
    public class BoolToGridRowHeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // if a converter parameter is provided, it should be an integer representing the height in stars
            int starHeight = parameter switch
            {
                int intValue => intValue,
                _ => 1
            };
            return ((bool)value == true) ? new GridLength(starHeight, GridUnitType.Star) : new GridLength(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {    // Don't need any convert back
            return null;
        }
    }
}
