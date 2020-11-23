using System;
using System.ComponentModel;
using System.Windows;

namespace DaxStudio.Controls.PropertyGrid
{
    public class PropertyBinding<T> : PropertyBindingBase, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public Action<T> SetValue { get; set; }
        public Func<T> GetValue { get; set; }
        public T Value
        {
            get => GetValue();
            set => SetValue(value);
        }
        public Func<bool> GetValueEnabled { get; set; } = () =>  true;
        public bool ValueEnabled => GetValueEnabled();

        public void OnEnabledChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ValueEnabled)));
        }
    }

    public abstract class PropertyBindingBase: IComparable<PropertyBindingBase>
    {
        public int SortOrder { get; set; } = 0;

        public string DisplayName { get; set; }
        public string Category { get; set; }
        private string _subcategory = string.Empty;

        

        public string Subcategory { get => string.IsNullOrEmpty(_subcategory) ? Category : _subcategory; 
            set => _subcategory = value?.Trim()??string.Empty; }

        public Type PropertyType { get; set; }
        
        public string Description { get; internal set; }
        public Visibility ShowDescription => !string.IsNullOrWhiteSpace(Description) ? Visibility.Visible : Visibility.Collapsed;

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
