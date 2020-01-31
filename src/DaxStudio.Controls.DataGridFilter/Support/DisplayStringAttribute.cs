using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DaxStudio.Controls.DataGridFilter.Support
{
    /// <summary>
    /// Code from: http://www.ageektrapped.com/blog/the-missing-net-7-displaying-enums-in-wpf/
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class DisplayStringAttribute : Attribute
    {
        private readonly string value;
        public string Value
        {
            get { return value; }
        }

        public string ResourceKey { get; set; }

        public DisplayStringAttribute(string displayString)
        {
            this.value = displayString;
        }

        public DisplayStringAttribute()
        {
        }
    }
}
