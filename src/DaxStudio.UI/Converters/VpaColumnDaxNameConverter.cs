using Dax.ViewModel;
using DaxStudio.UI.ViewModels;
using System;
using System.Globalization;
using System.Windows.Data;

namespace DaxStudio.UI.Converters
{
    public class VpaColumnDaxNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var col = value as VpaColumnViewModel;
            if (col == null) return Binding.DoNothing;
            return $"'{col.TableName}'[{col.ColumnName}]";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
