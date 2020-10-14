using System;
using System.Windows.Data;

namespace DaxStudio.Controls.DataGridFilter.Support
{
    public class DatePickerToQueryStringConverter : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            object convertedValue;

            if (string.IsNullOrEmpty(value.ToString() ))
            {
                convertedValue = null;
            }
            else
            {
                if (DateTime.TryParse(
                    value.ToString(),
                    culture.DateTimeFormat,
                    System.Globalization.DateTimeStyles.None,
                    out var dateTime))
                {
                    convertedValue = dateTime;
                }
                else
                {
                    convertedValue = null;
                }
            }

            return convertedValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value;
        }

        #endregion
    }
}
