using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Data;
using System.Globalization;

namespace DaxStudio.Controls.DataGridFilter.Support
{
    public class FontSizeToHeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double height;

            if (value != null)
            {
                if(Double.TryParse(value.ToString(), out height))
                {
                    return height * 2;
                }
                else
                {
                    return Double.NaN;
                }
                
            }
            else
            {
                return value;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
