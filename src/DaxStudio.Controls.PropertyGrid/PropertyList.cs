using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using System.Windows.Data;
using System.Linq;

namespace DaxStudio.Controls.PropertyGrid
{
    public class PropertyList : ListView
    {
        //public event EventHandler SourceChanged;
        private  ListCollectionView _cvs;
        /// <summary>
        /// 
        /// </summary>
        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register("Source", 
                                                                                                typeof(object), 
                                                                                                typeof(PropertyList), 
                                                                                                new FrameworkPropertyMetadata(default(object), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSourceChanged));

        private static async void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            await ((PropertyList)d).UpdateSource(e.NewValue);
        }


        /// <summary>
        /// 
        /// </summary>
        public object Source
        {
            get => GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }


        public static readonly DependencyProperty CategoryFilterProperty = DependencyProperty.Register("CategoryFilter", typeof(string), typeof(PropertyList), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnCategoryFilterChanged));
        public string CategoryFilter
        {
            get => (string)GetValue(CategoryFilterProperty);
            set => SetValue(CategoryFilterProperty, value);
        }

        private static void OnCategoryFilterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (((PropertyList)d).PropertyView == null) return;
            SetCategoryFilter(d);
        }

        public static readonly DependencyProperty SearchTextProperty = DependencyProperty.Register("SearchText", typeof(string), typeof(PropertyList), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSearchTextChanged));
        public string SearchText
        {
            get => (string)GetValue(SearchTextProperty);
            set {
                System.Diagnostics.Debug.WriteLine($"PropertyList.SearchText = {value}");
                SetValue(SearchTextProperty, value); }
        }

        private static void OnSearchTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (((PropertyList)d).PropertyView == null) return;
            SetCategoryFilter(d);
        }

        private static void SetCategoryFilter(DependencyObject d)
        {
            
            ((PropertyList)d).PropertyView.Filter += new Predicate<object>(o => CategoryFilterPredicate(o as PropertyBinding<object>, d));
        }

        private static bool CategoryFilterPredicate( PropertyBinding<object> p, DependencyObject d)
        {
            if (Application.Current.Dispatcher.CheckAccess()) return CategoryFilterContains(p, d);
            return Application.Current.Dispatcher.Invoke(() => CategoryFilterContains(p, d));
        }

        private static bool CategoryFilterContains(PropertyBinding<object> p, DependencyObject d)
        {
            var text = ((PropertyList) d).SearchText;
            var cat = ((PropertyList) d).CategoryFilter;
            if (!string.IsNullOrWhiteSpace(text))
            {
                return p.DisplayName.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                       p.Subcategory.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                       p.Description.Contains(text, StringComparison.OrdinalIgnoreCase);
            }

            if (!string.IsNullOrEmpty(cat))
                return p.Category == ((PropertyList) d).CategoryFilter;

            // if neither filter is set return everything
            return true;
        }

        public ICollectionView PropertyView
        {
            get
            {
                var view = _cvs;
                //if (!string.IsNullOrEmpty(CategoryFilter)) SetCategoryFilter(this);
                return view;
            }
        }

        private Dictionary<string, Action> onEnabledChangedFuncs = new Dictionary<string, Action>();
        protected virtual async Task UpdateSource(object newSource)
        {
            await Task.Run(() =>
            {
                var props = new System.Collections.ObjectModel.ObservableCollection<PropertyBindingBase>();

                var npc = newSource as INotifyPropertyChanged;
                if ( npc != null){
                    npc.PropertyChanged += OnSourcePropertyChanged;
                }

                var cachedEntries = PropertyMetadataCache.GetMetadata(newSource.GetType());

                foreach (var entry in cachedEntries)
                {
                    // Environment variable check remains dynamic (env vars can change between dialog opens)
                    if (!string.IsNullOrEmpty(entry.EnvironmentVariableName))
                    {
                        var envValue = Environment.GetEnvironmentVariable(entry.EnvironmentVariableName);
                        if (string.IsNullOrWhiteSpace(envValue) || envValue.Trim() != "1")
                        {
                            continue;
                        }
                    }

                    var binding = new PropertyBinding<object>();

                    binding.DisplayName = entry.DisplayName;
                    if (entry.Category != null) binding.Category = entry.Category;
                    binding.Description = entry.Description;
                    binding.Subcategory = entry.Subcategory;
                    binding.SortOrder = entry.SortOrder;
                    binding.MinValue = entry.MinValue;
                    binding.MaxValue = entry.MaxValue;
                    binding.PropertyType = entry.PropertyType;
                    binding.EnumDisplay = entry.EnumDisplay;

                    // Use compiled delegates instead of PropertyInfo.GetValue/SetValue
                    var compiledGetter = entry.CompiledGetter;
                    var compiledSetter = entry.CompiledSetter;
                    binding.GetValue = () => compiledGetter(newSource);
                    if (compiledSetter != null)
                    {
                        binding.SetValue = (value) => compiledSetter(newSource, value);
                    }

                    if (entry.HasEnabledProperty)
                    {
                        var compiledEnabledGetter = entry.CompiledEnabledGetter;
                        binding.GetValueEnabled = () => compiledEnabledGetter(newSource);
                        onEnabledChangedFuncs.Add(entry.EnabledPropertyName, binding.OnEnabledChanged);
                    }

                    props.Add(binding);
                }

                _cvs = (ListCollectionView)CollectionViewSource.GetDefaultView(props);
                PropertyGroupDescription groupDescription = new PropertyGroupDescription("Subcategory");
                _cvs.GroupDescriptions.Add(groupDescription);
                _cvs.CustomSort = new GenericComparer<PropertyBindingBase>();
            });

            SetCategoryFilter(this);

            this.ItemsSource = PropertyView;
        }

        private void OnSourcePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Action onEnabledChanged;
            onEnabledChangedFuncs.TryGetValue(e.PropertyName, out onEnabledChanged);
            onEnabledChanged?.Invoke();
        }
    }

}
