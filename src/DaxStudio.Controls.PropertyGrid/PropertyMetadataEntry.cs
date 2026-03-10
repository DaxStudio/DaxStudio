using System;

namespace DaxStudio.Controls.PropertyGrid
{
    /// <summary>
    /// Holds pre-extracted attribute metadata and compiled delegates for a single property.
    /// Built once per type via PropertyMetadataCache, eliminating repeated reflection.
    /// </summary>
    public class PropertyMetadataEntry
    {
        public string PropertyName { get; set; }
        public string DisplayName { get; set; }
        public string Category { get; set; }
        public string Subcategory { get; set; }
        public string Description { get; set; }
        public int SortOrder { get; set; } = int.MaxValue;
        public double MinValue { get; set; }
        public double MaxValue { get; set; }
        public Type PropertyType { get; set; }
        public EnumDisplayOptions EnumDisplay { get; set; } = EnumDisplayOptions.Description;
        public string EnvironmentVariableName { get; set; }

        /// <summary>Compiled getter: (source) => source.Property</summary>
        public Func<object, object> CompiledGetter { get; set; }

        /// <summary>Compiled setter: (source, value) => source.Property = value</summary>
        public Action<object, object> CompiledSetter { get; set; }

        /// <summary>Whether a companion {PropertyName}Enabled property exists</summary>
        public bool HasEnabledProperty { get; set; }

        /// <summary>The name of the companion Enabled property (for INotifyPropertyChanged tracking)</summary>
        public string EnabledPropertyName { get; set; }

        /// <summary>Compiled getter for the companion Enabled property: (source) => (bool)source.PropertyEnabled</summary>
        public Func<object, bool> CompiledEnabledGetter { get; set; }
    }
}
