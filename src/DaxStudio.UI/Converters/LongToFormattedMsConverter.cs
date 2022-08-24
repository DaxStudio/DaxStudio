using System;
using System.Windows.Data;

namespace DaxStudio.UI.Converters
{
    class LongToFormattedMsConverter: IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is long @long)
            {
                return @long >= 0 ? string.Format("{0:n0}ms", @long) : "...";
            }
            return Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
