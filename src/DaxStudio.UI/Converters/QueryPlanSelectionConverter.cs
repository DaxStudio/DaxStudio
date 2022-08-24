using DaxStudio.UI.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace DaxStudio.UI.Converters
{
    internal class QueryPlanSelectionConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            
            if (values.Length != 2) return Visibility.Hidden;
            if (values[0] == null) return Visibility.Hidden;
            if (!(values[0] is IQueryPlanRow qpr)) return Visibility.Hidden;
            if (!(values[1] is int currentRow)) return Visibility.Hidden;
            return qpr.RowNumber < currentRow && qpr.NextSiblingRowNumber > currentRow ? Visibility.Visible : Visibility.Hidden;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            //throw new NotImplementedException();
            return null;
        }
    }
}
