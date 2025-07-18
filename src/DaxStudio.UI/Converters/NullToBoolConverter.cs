using System;
using System.Windows.Data;

namespace DaxStudio.UI.Converters
{
    class NullToBoolConverter : IValueConverter
    {
    
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (value != null)
            ;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}

