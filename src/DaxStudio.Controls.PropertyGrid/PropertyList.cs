using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Threading;
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
            var pl = (PropertyList)d;
            Debug.WriteLine($"PropertyList.OnCategoryFilterChanged: '{e.OldValue}' -> '{e.NewValue}'");

            // If a search debounce is pending, skip this refresh — the debounce timer
            // will pick up both the new search text and the new category when it fires.
            if (pl._searchDebouncePending)
            {
                Debug.WriteLine("PropertyList.OnCategoryFilterChanged: Skipped (search debounce pending)");
                return;
            }
            pl.RefreshFilter();
        }

        public static readonly DependencyProperty SearchTextProperty = DependencyProperty.Register("SearchText", typeof(string), typeof(PropertyList), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSearchTextChanged));
        public string SearchText
        {
            get => (string)GetValue(SearchTextProperty);
            set {
                Debug.WriteLine($"PropertyList.SearchText = {value}");
                SetValue(SearchTextProperty, value); }
        }

        /// <summary>
        /// The search text that is currently active in the filter.
        /// Only updated when the debounced filter actually runs.
        /// Bind TextBlockHighlighter.Selection to this instead of the TextBox directly.
        /// </summary>
        public static readonly DependencyProperty ActiveSearchTextProperty = DependencyProperty.Register("ActiveSearchText", typeof(string), typeof(PropertyList), new PropertyMetadata(string.Empty));
        public string ActiveSearchText
        {
            get => (string)GetValue(ActiveSearchTextProperty);
            private set => SetValue(ActiveSearchTextProperty, value);
        }

        private DispatcherTimer _searchDebounceTimer;
        private bool _searchDebouncePending;

        private static void OnSearchTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            Debug.WriteLine($"PropertyList.OnSearchTextChanged: '{e.OldValue}' -> '{e.NewValue}'");
            var pl = (PropertyList)d;

            // Debounce search input — only refresh after 300ms of no typing
            if (pl._searchDebounceTimer == null)
            {
                pl._searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
                pl._searchDebounceTimer.Tick += (s, args) =>
                {
                    pl._searchDebounceTimer.Stop();
                    pl._searchDebouncePending = false;
                    Debug.WriteLine("PropertyList: Debounce timer fired, refreshing filter");
                    pl.RefreshFilter();
                };
            }

            pl._searchDebouncePending = true;
            pl._searchDebounceTimer.Stop();
            pl._searchDebounceTimer.Start();
        }

        // Cached filter values — captured once per refresh, read per item
        private string _currentSearchText;
        private string _currentCategoryFilter;

        private const int MinSearchLength = 2;

        private void RefreshFilter()
        {
            if (PropertyView == null) return;

            var sw = Stopwatch.StartNew();

            // Capture current values once, avoiding repeated DependencyProperty reads per item
            _currentSearchText = SearchText;
            _currentCategoryFilter = CategoryFilter;

            _filterItemCount = 0;

            // Use assignment (not +=) to replace any previous filter with a single predicate
            PropertyView.Filter = FilterPredicate;

            // Only update highlighting when search text meets minimum length
            ActiveSearchText = (_currentSearchText?.Length ?? 0) >= MinSearchLength
                ? _currentSearchText
                : string.Empty;

            sw.Stop();
            Debug.WriteLine($"PropertyList.RefreshFilter: {sw.ElapsedMilliseconds}ms, " +
                            $"items evaluated: {_filterItemCount}, " +
                            $"search: '{_currentSearchText}', category: '{_currentCategoryFilter}'");
        }

        private int _filterItemCount;

        private bool FilterPredicate(object o)
        {
            _filterItemCount++;

            if (!(o is PropertyBinding<object> p)) return false;

            if (!string.IsNullOrWhiteSpace(_currentSearchText) && _currentSearchText.Length >= MinSearchLength)
            {
                return p.DisplayName.Contains(_currentSearchText, StringComparison.OrdinalIgnoreCase) ||
                       p.Subcategory.Contains(_currentSearchText, StringComparison.OrdinalIgnoreCase) ||
                       (p.Description != null && p.Description.Contains(_currentSearchText, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(_currentCategoryFilter))
                return p.Category == _currentCategoryFilter;

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
            var totalSw = Stopwatch.StartNew();

            await Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();
                var props = new System.Collections.ObjectModel.ObservableCollection<PropertyBindingBase>();

                var npc = newSource as INotifyPropertyChanged;
                if ( npc != null){
                    npc.PropertyChanged += OnSourcePropertyChanged;
                }

                var cachedEntries = PropertyMetadataCache.GetMetadata(newSource.GetType());
                Debug.WriteLine($"PropertyList.UpdateSource: GetMetadata took {sw.ElapsedMilliseconds}ms, {cachedEntries.Length} entries");

                sw.Restart();
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
                Debug.WriteLine($"PropertyList.UpdateSource: Building {props.Count} bindings took {sw.ElapsedMilliseconds}ms");

                sw.Restart();
                _cvs = (ListCollectionView)CollectionViewSource.GetDefaultView(props);
                PropertyGroupDescription groupDescription = new PropertyGroupDescription("Subcategory");
                _cvs.GroupDescriptions.Add(groupDescription);
                _cvs.CustomSort = new GenericComparer<PropertyBindingBase>();
                Debug.WriteLine($"PropertyList.UpdateSource: CollectionView setup took {sw.ElapsedMilliseconds}ms");
            });

            var filterSw = Stopwatch.StartNew();
            RefreshFilter();
            filterSw.Stop();

            var itemsSw = Stopwatch.StartNew();
            this.ItemsSource = PropertyView;
            itemsSw.Stop();

            totalSw.Stop();
            Debug.WriteLine($"PropertyList.UpdateSource: RefreshFilter took {filterSw.ElapsedMilliseconds}ms, " +
                            $"ItemsSource assignment took {itemsSw.ElapsedMilliseconds}ms, " +
                            $"total: {totalSw.ElapsedMilliseconds}ms");
        }

        private void OnSourcePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Action onEnabledChanged;
            onEnabledChangedFuncs.TryGetValue(e.PropertyName, out onEnabledChanged);
            onEnabledChanged?.Invoke();
        }
    }

}
