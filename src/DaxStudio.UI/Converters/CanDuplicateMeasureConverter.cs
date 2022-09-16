using System;
using System.Globalization;
using System.Windows.Data;
using DaxStudio.UI.Model;

namespace DaxStudio.UI.Converters
{
    class CanDuplicateMeasureConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is QueryBuilderColumn column)
            {
                try
                {
                    return !string.IsNullOrEmpty(column.MeasureExpression);
                }
                catch
                {
                    return false;   
                }
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
