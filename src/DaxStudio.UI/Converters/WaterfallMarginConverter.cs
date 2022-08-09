using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DaxStudio.UI.Converters
{
    internal class WaterfallMarginConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 3 && values.Length != 4) throw new ArgumentException($"The {nameof(WaterfallMarginConverter)} needs 3-4 parameters");
            var cellWidth = (double)values[0];
            var offset = (long)values[1];
            var totalWidth = (long)values[2];
            // restrict offset and totalWidth to positive values
            if (offset < 0) offset = 0;
            if (totalWidth < 0) totalWidth = 0;

            var verticalMargin = 0.0;
            if (values.Length == 4) verticalMargin = (double)values[3];

            return new Thickness((cellWidth / totalWidth) * offset, verticalMargin, 0, verticalMargin);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
