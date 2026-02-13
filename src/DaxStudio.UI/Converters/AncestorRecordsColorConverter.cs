using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace DaxStudio.UI.Converters
{
    public class AncestorRecordsColorConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            long? records = values[0] as long?;
            long? ancestorRecords = values[1] as long?;
            if (records == null || ancestorRecords == null)
            {
                return Application.Current.TryFindResource("Theme.Brush.Default.Fore") as Brush; // Default color if values are not set
            }
            if (records > ancestorRecords)
            {
                return Application.Current.TryFindResource("Theme.Brush.Icons.Red") as Brush; // Records are greater than ancestor records
            }
            else if (records < ancestorRecords)
            {
                return Application.Current.TryFindResource("Theme.Brush.Log.Success") as Brush; // Records are less than ancestor records
            }
            else
            {
                return Application.Current.TryFindResource("Theme.Brush.Default.Fore") as Brush; // Records are equal to ancestor records
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return new object[] { Binding.DoNothing };
        }
    }
}
