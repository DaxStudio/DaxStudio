using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DaxStudio.Controls.DataGridFilter.Support
{
    /// <summary>
    /// Corresponds to the FilterCurrentData templates (DataTemplate) 
    /// of the DataGridColumnFilter defined in the Generic.xaml>
    /// </summary>
    public enum FilterType
    {
        Numeric,
        NumericBetween,
        Text,
        List,
        Boolean,
        DateTime,
        DateTimeBetween
    }
}
