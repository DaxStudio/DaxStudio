using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Resources;
using System.Collections;
using System.Reflection;
using System.Windows.Data;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Markup;

namespace DaxStudio.Controls.DataGridFilter.Support
{
    /// <summary>
    /// Code from: http://www.ageektrapped.com/blog/the-missing-net-7-displaying-enums-in-wpf/
    /// </summary>
    [ContentProperty("OverriddenDisplayEntries")]
    public class EnumDisplayer : IValueConverter
    {
        private Type type;
        private IDictionary displayValues;
        private IDictionary reverseValues;
        private List<EnumDisplayEntry> overriddenDisplayEntries;

        public EnumDisplayer()
        {
        }

        public EnumDisplayer(Type type)
        {
            this.Type = type;
        }

        public Type Type
        {
            get { return type; }
            set
            {
                if (value == null || !value.IsEnum)
                    throw new ArgumentException("parameter is not an Enumermated type", nameof(value));
                this.type = value;
            }
        }

        public ReadOnlyCollection<string> DisplayNames
        {
            get
            {
                Type displayValuesType = typeof(Dictionary<,>).GetGenericTypeDefinition().MakeGenericType(type, typeof(string));
                this.displayValues = (IDictionary)Activator.CreateInstance(displayValuesType);

                this.reverseValues =
                   (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>)
                            .GetGenericTypeDefinition()
                            .MakeGenericType(typeof(string), type));

                var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static);
                foreach (var field in fields)
                {
                    DisplayStringAttribute[] a = (DisplayStringAttribute[])
                                                field.GetCustomAttributes(typeof(DisplayStringAttribute), false);

                    string displayString = GetDisplayStringValue(a);
                    object enumValue = field.GetValue(null);

                    if (displayString == null)
                    {
                        displayString = GetBackupDisplayStringValue(enumValue);
                    }
                    if (displayString != null)
                    {
                        displayValues.Add(enumValue, displayString);
                        reverseValues.Add(displayString, enumValue);
                    }
                }
                return new List<string>((IEnumerable<string>)displayValues.Values).AsReadOnly();
            }
        }

        private string GetDisplayStringValue(DisplayStringAttribute[] a)
        {
            if (a == null || a.Length == 0) return null;
            DisplayStringAttribute dsa = a[0];
            if (!string.IsNullOrEmpty(dsa.ResourceKey))
            {
                ResourceManager rm = new ResourceManager(type);
                return rm.GetString(dsa.ResourceKey);
            }
            return dsa.Value;
        }

        private string GetBackupDisplayStringValue(object enumValue)
        {
            if (overriddenDisplayEntries != null && overriddenDisplayEntries.Count > 0)
            {
                EnumDisplayEntry foundEntry = overriddenDisplayEntries.Find(delegate(EnumDisplayEntry entry)
                {
                    object e = Enum.Parse(type, entry.EnumValue);
                    return enumValue.Equals(e);
                });
                if (foundEntry != null)
                {
                    if (foundEntry.ExcludeFromDisplay) return null;
                    return foundEntry.DisplayString;

                }
            }
            return Enum.GetName(type, enumValue);
        }

        public List<EnumDisplayEntry> OverriddenDisplayEntries
        {
            get
            {
                if (overriddenDisplayEntries == null)
                    overriddenDisplayEntries = new List<EnumDisplayEntry>();
                return overriddenDisplayEntries;
            }
        }


        object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return displayValues[value];
        }

        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return reverseValues[value];
        }
    }

    public class EnumDisplayEntry
    {
        public string EnumValue { get; set; }
        public string DisplayString { get; set; }
        public bool ExcludeFromDisplay { get; set; }
    }
}
