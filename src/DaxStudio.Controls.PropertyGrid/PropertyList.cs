using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Reflection;
using System.ComponentModel;
using System.Windows.Data;

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

        private async static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
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
            set => SetValue(SearchTextProperty, value);
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
            var text = ((PropertyList)d).SearchText;
            var cat = ((PropertyList)d).CategoryFilter;
            if (!string.IsNullOrWhiteSpace(text))
            {
                return p.DisplayName.Contains(text, StringComparison.OrdinalIgnoreCase);
            }

            if (!string.IsNullOrEmpty(cat)) 
                return p.Category == ((PropertyList)d).CategoryFilter;

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
        protected virtual async Task UpdateSource(object newSource)
        {
            await Task.Delay(100);
            //var cvs = new System.Windows.Data.CollectionViewSource();
            var props = new List<PropertyBindingBase>();

            foreach (var prop in newSource.GetType().GetProperties( ))
            {
                var dispName = prop.GetCustomAttribute(typeof(DisplayNameAttribute)) as DisplayNameAttribute;
                var catName = prop.GetCustomAttribute(typeof(CategoryAttribute)) as CategoryAttribute;
                var subCatName = prop.GetCustomAttribute(typeof(SubcategoryAttribute)) as SubcategoryAttribute;
                var sortOrder = prop.GetCustomAttribute<SortOrderAttribute>();
                var desc = prop.GetCustomAttribute(typeof(DescriptionAttribute)) as DescriptionAttribute;
                var minValue = prop.GetCustomAttribute(typeof(MinValueAttribute)) as MinValueAttribute;
                var maxValue = prop.GetCustomAttribute(typeof(MaxValueAttribute)) as MaxValueAttribute;
                var t = prop.GetType();

                //Type[] typeArgs = { prop.PropertyType };
                //Type d1 = typeof(PropertyBinding<>);
                //Type constructed = d1.MakeGenericType(typeArgs);
                //var o = Activator.CreateInstance(constructed);
                //var binding = (PropertyBindingBase)o;

                var binding = new PropertyBinding<object>();

                //skip properties that do not have a display name defined
                if (dispName == null) continue; 
                    
                binding.DisplayName = dispName.DisplayName;
                
                if (catName != null) binding.Category = catName?.Category;

                binding.Description = desc?.Description;
                binding.Subcategory = subCatName?.Subcategory;
                binding.SortOrder = sortOrder?.SortOrder ?? int.MaxValue;
                binding.MinValue = minValue?.MinValue ?? 0;
                binding.MaxValue = maxValue?.MaxValue ?? 0;
                binding.PropertyType = prop.PropertyType;
                //var setProp = constructed.GetProperty("SetValue");
                //setProp.SetValue(o, (Action)((value) => prop.SetValue(newSource,value));
                //var getProp = o.GetType().GetProperty("GetValue");

                binding.SetValue = (value) => prop.SetValue(newSource, value);
                binding.GetValue = () => prop.GetValue(newSource);

                props.Add(binding);
            }
            _cvs = (ListCollectionView)CollectionViewSource.GetDefaultView( props);
            PropertyGroupDescription groupDescription = new PropertyGroupDescription("Subcategory");
            _cvs.GroupDescriptions.Add(groupDescription);
            _cvs.CustomSort = new GenericComparer<PropertyBindingBase>();
            SetCategoryFilter(this);
            this.ItemsSource = PropertyView;
        }
    }

}
