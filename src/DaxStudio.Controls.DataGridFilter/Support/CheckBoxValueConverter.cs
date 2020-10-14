using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Data;

namespace DaxStudio.Controls.DataGridFilter.Support
{
    public class CheckBoxValueConverter : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool result = false;

            if (value is string)
            {
                if( !bool.TryParse(value.ToString(), out result)) return false;
            }
            else if (value != null)
            {
                result = System.Convert.ToBoolean(value);
            }

            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value;
        }

        #endregion
    }
}
