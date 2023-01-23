using DaxStudio.Common;
using Serilog;
using System;
using System.Diagnostics;
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
            try
            {
                var cellWidth = (double)(values[0] ?? 0.0);
                var length = (long)(values[1] ?? 0L);
                var totalWidth = (long)(values[2] ?? 0L);

                // restrict offset and totalwidth to positive values
                if (length < 0L) length = 0;
                if (totalWidth <= 1L) totalWidth = 0;
                var minWidth = 1.0;
                if (values.Length == 4) minWidth = (double)(values[3] ?? 0.0);
                // force a small minWidth so that 0 duration events are visible
                if (length == 0L) return minWidth;
                // calculate a proportional width
                var calcLength = (cellWidth / totalWidth) * (length);
                if (calcLength < minWidth) calcLength = minWidth;
                return calcLength;
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate,nameof(WaterfallLengthConverter),nameof(Convert),"Error calculating waterfall length");
                Debug.Assert(false, $"Error calculating waterfall length: {ex.Message}");
                return Binding.DoNothing;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}