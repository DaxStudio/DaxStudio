using System;
using System.Windows;

namespace DaxStudio.Controls.PropertyGrid
{
    public interface IPropertyBinding<T>
    {
        string Category { get; set; }
        string Description { get; set; }
        string DisplayName { get; set; }
        Func<T> GetValue { get; set; }
        Type PropertyType { get; set; }
        Action<T> SetValue { get; set; }
        Visibility ShowDescription { get; }
        string Subcategory { get; set; }
        T Value { get; set; }
    }
}