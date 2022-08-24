using System;
using System.Windows;
using System.Windows.Data;

namespace DaxStudio.UI.Converters
{
    public class FindResourceFromStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value != null) return Application.Current.FindResource((string)value);
            return Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
