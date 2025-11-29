using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace DaxStudio.UI.Converters
{
    public class AncestorRecordsImageResourceConverter : IMultiValueConverter
    {
        private static readonly string increaseKey = "increase_smallDrawingImage";
        private static readonly string decreaseKey = "decrease_smallDrawingImage";
        private static readonly string equalsKey = "equal_smallDrawingImage";
        private static readonly ImageSource increaseImage;
        private static readonly ImageSource decreaseImage;
        private static readonly ImageSource equalsImage;
        static AncestorRecordsImageResourceConverter()
        {
            // Static constructor to initialize any static resources if needed
            increaseImage = Application.Current.TryFindResource(increaseKey) as ImageSource;
            decreaseImage = Application.Current.TryFindResource(decreaseKey) as ImageSource;
            equalsImage = Application.Current.TryFindResource(equalsKey) as ImageSource;
        }

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            long? records = values[0] as long?;
            long? ancestorRecords = values[1] as long?;
            if (records == null || ancestorRecords == null)
            {

                return null; // Default color if values are not set
            }
            if (records > ancestorRecords)
            {
                return increaseImage;
            }
            else if (records < ancestorRecords)
            {
                return Application.Current.TryFindResource(decreaseKey) as ImageSource; // Records are less than ancestor records
            }
            else
            {
                return Application.Current.TryFindResource(equalsKey) as ImageSource; // Records are equal to ancestor records
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return new object[] { Binding.DoNothing };
        }
    }
}
