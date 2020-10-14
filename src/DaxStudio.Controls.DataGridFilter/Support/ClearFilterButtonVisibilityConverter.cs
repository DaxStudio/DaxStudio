using System;
using System.Windows.Data;
using System.Globalization;

namespace DaxStudio.Controls.DataGridFilter.Support
{
    public class ClearFilterButtonVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));

            if ((bool)values[0] && (bool)values[1])
            {
                return System.Windows.Visibility.Visible;
            }
            else
            {
                return System.Windows.Visibility.Collapsed;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
