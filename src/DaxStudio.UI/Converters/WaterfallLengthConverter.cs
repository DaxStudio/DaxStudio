using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DaxStudio.UI.Converters
{
    internal class WaterfallLengthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 3 && values.Length !=4) throw new ArgumentException($"The {nameof(WaterfallLengthConverter)} needs 3-4 parameters");
            var cellWidth = (double)values[0];
            var length = (long)values[1];
            var totalWidth = (long)values[2];
            // restrict offset and totalwidth to positive values
            if (length < 0) length = 0;
            if (totalWidth < 0) totalWidth = 0;
            var minWidth = 1.0;
            if (values.Length==4) minWidth = (double)values[3];
            //add minWidth on so that any 0 length operations at the end of the waterfall are visible
            var calcLength = (cellWidth / totalWidth) * (length + minWidth);
            if (calcLength < minWidth) calcLength = minWidth;
            return calcLength;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}