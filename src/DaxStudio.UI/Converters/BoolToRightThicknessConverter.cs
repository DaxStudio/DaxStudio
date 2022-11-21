using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DaxStudio.UI.Converters
{
    internal class BoolToRightThicknessConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool )
            {
                if (!(bool)value)
                {
                    var right = System.Convert.ToDouble(parameter);
                    return new Thickness(0, 0, right, 0);
                }
                return new Thickness(0.0);
            }
            return Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
