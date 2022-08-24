using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DaxStudio.UI.Converters
{

    public class StringPercentToDoubleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var rawValue = value as string;
            
            if (rawValue == null) return Binding.DoNothing;
            Double.TryParse(rawValue, out var percent);
            if (percent < 0) return Binding.DoNothing;
            return percent / 100.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;   
        }
    }
}
