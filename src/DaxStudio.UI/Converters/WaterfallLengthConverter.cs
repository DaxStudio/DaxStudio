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

        const double MinWidth = 0.001;
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 3 && values.Length !=4) throw new ArgumentException($"The {nameof(WaterfallLengthConverter)} needs 3-4 parameters");
            try
            {
                double.TryParse(values[0]?.ToString(), out var cellWidth);
                long.TryParse(values[1]?.ToString(), out var length);
                long.TryParse(values[2]?.ToString(), out var totalWidth);

                // restrict offset and totalwidth to positive values
                if (length < 0L) length = 0;
                if (totalWidth <= 1L) totalWidth = 1L;

                var minWidth = values.Length == 4 ? (double)(values[3] ?? 0.0) : MinWidth;
                // force a small minWidth so that 0 duration events are visible
                if (length == 0L) return minWidth;
                // calculate a proportional width
                var calcLength = (cellWidth / totalWidth) * (length);
                // Marco 2023-01-25 disabled the minimum width for the visualization,
                // we might consider an exception for the waterfall display if required,
                // but it should not be applied to the StorageEngineTime visual
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