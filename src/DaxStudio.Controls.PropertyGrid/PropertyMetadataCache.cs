using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace DaxStudio.Controls.PropertyGrid
{
    /// <summary>
    /// Caches reflected property metadata and compiled expression-tree delegates per type.
    /// Eliminates repeated reflection in PropertyList.UpdateSource() and OptionsViewModel.GetCategories().
    /// </summary>
    public static class PropertyMetadataCache
    {
        private static readonly ConcurrentDictionary<Type, PropertyMetadataEntry[]> _metadataCache
            = new ConcurrentDictionary<Type, PropertyMetadataEntry[]>();

        private static readonly ConcurrentDictionary<Type, IReadOnlyList<string>> _categoriesCache
            = new ConcurrentDictionary<Type, IReadOnlyList<string>>();

        private static readonly ConcurrentDictionary<Type, HotkeyDefault[]> _hotkeyDefaultsCache
            = new ConcurrentDictionary<Type, HotkeyDefault[]>();

        /// <summary>
        /// Gets cached property metadata entries for the given type.
        /// Only includes properties decorated with [DisplayName].
        /// </summary>
        public static PropertyMetadataEntry[] GetMetadata(Type type)
        {
            return _metadataCache.GetOrAdd(type, BuildMetadata);
        }

        /// <summary>
        /// Gets a cached sorted list of distinct category names for the given type.
        /// </summary>
        public static IReadOnlyList<string> GetCategories(Type type)
        {
            return _categoriesCache.GetOrAdd(type, t =>
            {
                var entries = GetMetadata(t);
                return entries
                    .Where(e => e.Category != null)
                    .Select(e => e.Category)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(c => c, StringComparer.Ordinal)
                    .ToArray();
            });
        }

        private static PropertyMetadataEntry[] BuildMetadata(Type type)
        {
            var results = new List<PropertyMetadataEntry>();

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var dispName = prop.GetCustomAttribute<DisplayNameAttribute>();
                // Skip properties without a DisplayName — they are not shown in the property list
                if (dispName == null) continue;

                var entry = new PropertyMetadataEntry
                {
                    PropertyName = prop.Name,
                    DisplayName = dispName.DisplayName,
                    Category = prop.GetCustomAttribute<CategoryAttribute>()?.Category,
                    Subcategory = prop.GetCustomAttribute<SubcategoryAttribute>()?.Subcategory,
                    Description = prop.GetCustomAttribute<DescriptionAttribute>()?.Description,
                    SortOrder = prop.GetCustomAttribute<SortOrderAttribute>()?.SortOrder ?? int.MaxValue,
                    MinValue = prop.GetCustomAttribute<MinValueAttribute>()?.MinValue ?? 0,
                    MaxValue = prop.GetCustomAttribute<MaxValueAttribute>()?.MaxValue ?? 0,
                    PropertyType = prop.PropertyType,
                    EnumDisplay = prop.GetCustomAttribute<EnumDisplayAttribute>()?.EnumDisplay ?? EnumDisplayOptions.Description,
                    EnvironmentVariableName = prop.GetCustomAttribute<EnvironmentVariableAttribute>()?.VariableName,
                };

                // Compile getter
                entry.CompiledGetter = BuildGetter(type, prop);

                // Compile setter (only for writable properties)
                if (prop.CanWrite)
                {
                    entry.CompiledSetter = BuildSetter(type, prop);
                }

                // Look up companion {Name}Enabled property
                var enabledProp = type.GetProperty(
                    $"{prop.Name}Enabled",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (enabledProp != null)
                {
                    entry.HasEnabledProperty = true;
                    entry.EnabledPropertyName = enabledProp.Name;
                    entry.CompiledEnabledGetter = BuildEnabledGetter(type, enabledProp);
                }

                results.Add(entry);
            }

            return results.ToArray();
        }

        /// <summary>
        /// Builds a compiled getter: (object source) => (object)((TSource)source).Property
        /// </summary>
        private static Func<object, object> BuildGetter(Type sourceType, PropertyInfo prop)
        {
            var param = Expression.Parameter(typeof(object), "source");
            var cast = Expression.Convert(param, sourceType);
            var access = Expression.Property(cast, prop);
            var boxed = Expression.Convert(access, typeof(object));
            var lambda = Expression.Lambda<Func<object, object>>(boxed, param);
            return lambda.Compile();
        }

        /// <summary>
        /// Builds a compiled setter: (object source, object value) => ((TSource)source).Property = (TProp)value
        /// </summary>
        private static Action<object, object> BuildSetter(Type sourceType, PropertyInfo prop)
        {
            var sourceParam = Expression.Parameter(typeof(object), "source");
            var valueParam = Expression.Parameter(typeof(object), "value");
            var castSource = Expression.Convert(sourceParam, sourceType);
            var castValue = Expression.Convert(valueParam, prop.PropertyType);
            var access = Expression.Property(castSource, prop);
            var assign = Expression.Assign(access, castValue);
            var lambda = Expression.Lambda<Action<object, object>>(assign, sourceParam, valueParam);
            return lambda.Compile();
        }

        /// <summary>
        /// Builds a compiled getter for a bool-returning Enabled property:
        /// (object source) => Convert.ToBoolean(((TSource)source).PropertyEnabled)
        /// </summary>
        private static Func<object, bool> BuildEnabledGetter(Type sourceType, PropertyInfo enabledProp)
        {
            var param = Expression.Parameter(typeof(object), "source");
            var cast = Expression.Convert(param, sourceType);
            var access = Expression.Property(cast, enabledProp);
            var toBool = Expression.Convert(access, typeof(bool));
            var lambda = Expression.Lambda<Func<object, bool>>(toBool, param);
            return lambda.Compile();
        }

        /// <summary>
        /// Gets cached hotkey default values for the given type.
        /// Returns entries for properties whose name starts with "Hotkey" and have a [DefaultValue] attribute.
        /// </summary>
        public static HotkeyDefault[] GetHotkeyDefaults(Type type)
        {
            return _hotkeyDefaultsCache.GetOrAdd(type, BuildHotkeyDefaults);
        }

        private static HotkeyDefault[] BuildHotkeyDefaults(Type type)
        {
            var results = new List<HotkeyDefault>();

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.Name.StartsWith("Hotkey", StringComparison.InvariantCultureIgnoreCase)) continue;

                var defaultVal = prop.GetCustomAttribute<DefaultValueAttribute>();
                if (defaultVal == null) continue;

                results.Add(new HotkeyDefault
                {
                    DefaultValue = defaultVal.Value?.ToString(),
                    CompiledSetter = BuildSetter(type, prop)
                });
            }

            return results.ToArray();
        }
    }

    /// <summary>
    /// Holds a cached hotkey default value and its compiled setter delegate.
    /// </summary>
    public class HotkeyDefault
    {
        public string DefaultValue { get; set; }
        public Action<object, object> CompiledSetter { get; set; }
    }
}
