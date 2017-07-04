using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DaxStudio.Controls.DataGridFilter.Support
{
    public enum FilterOperator
    {
        Undefined,
        LessThan,
        LessThanOrEqual,
        GreaterThan,
        GreaterThanOrEqual,
        Equals,
        Like,
        Between,
        Contains
    }
}
