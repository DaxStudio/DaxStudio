using System;
using System.Windows;

namespace DaxStudio.Controls.PropertyGrid
{
    public class PropertyBinding<T> : PropertyBindingBase
    {
        public Action<T> SetValue { get; set; }
        public Func<T> GetValue { get; set; }
        public T Value
        {
            get => GetValue();
            set => SetValue(value);
        }
    }

    public abstract class PropertyBindingBase: IComparable<PropertyBindingBase>
    {
        public int SortOrder { get; set; } = 0;

        public string DisplayName { get; set; }
        public string Category { get; set; }
        public string Subcategory { get; set; }

        public Type PropertyType { get; set; }
        
        public string Description { get; internal set; }
        public Visibility ShowDescription { get => !string.IsNullOrWhiteSpace(Description) ? Visibility.Visible : Visibility.Collapsed; }

        public double MinValue { get; set; }
        public double MaxValue { get; set; }
        public int CompareTo(PropertyBindingBase other)
        {
            if (SortOrder == other.SortOrder) return string.Compare(DisplayName, other.DisplayName);
            if (SortOrder > other.SortOrder) return 1;
            return -1;
        }
    }
}
