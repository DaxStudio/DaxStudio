using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Collections;
using DaxStudio.Controls.DataGridFilter.Support;
using System.Reflection;
using DaxStudio.Controls.DataGridFilter.Querying;
using System.Windows.Controls.Primitives;

namespace DaxStudio.Controls.DataGridFilter
{
    public class DataGridColumnFilter : Control
    {
        static DataGridColumnFilter()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DataGridColumnFilter), new FrameworkPropertyMetadata(typeof(DataGridColumnFilter)));
        }

        #region Overrides

        protected override void OnPropertyChanged(
            DependencyPropertyChangedEventArgs e)
        {
            if (e.Property == DataGridItemsSourceProperty
                && e.OldValue != e.NewValue
                && AssignedDataGridColumn != null && DataGrid != null && AssignedDataGridColumn is DataGridColumn)
            {
                Initialize();

                FilterCurrentData.IsRefresh = true;//query optimization filed

                filterCurrentData_FilterChangedEvent(this, EventArgs.Empty);//init query

                FilterCurrentData.FilterChangedEvent -= new EventHandler<EventArgs>(filterCurrentData_FilterChangedEvent);
                FilterCurrentData.FilterChangedEvent += new EventHandler<EventArgs>(filterCurrentData_FilterChangedEvent);
            }

            base.OnPropertyChanged(e);
        }
        #endregion

        #region Properties
        public FilterData FilterCurrentData
        {
            get { return (FilterData)GetValue(FilterCurrentDataProperty); }
            set { SetValue(FilterCurrentDataProperty, value); }
        }

        public static readonly DependencyProperty FilterCurrentDataProperty =
            DependencyProperty.Register(nameof(FilterCurrentData), typeof(FilterData), typeof(DataGridColumnFilter), new PropertyMetadata(null,OnFilterCurrentDataChanged));

        private static void OnFilterCurrentDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var colFilter = d as DataGridColumnFilter;
            if (colFilter != null)
                if (colFilter.AssignedDataGridColumn is DataGridColumn)
                    if (colFilter.FilterCurrentData != null)
                    {
                        colFilter.FilterCurrentData.FilterChangedEvent += colFilter.FilterChangedEvent;
                        colFilter.FilterCurrentData.FilterClearedEvent += colFilter.FilterClearedEvent;
                    }
                    
        }

        private void FilterChangedEvent(object sender, EventArgs e)
        {
            if (this.FilterCurrentData != null)
            {
                IsFiltered = !string.IsNullOrEmpty(this.FilterCurrentData.QueryString)
                            || !string.IsNullOrEmpty(this.FilterCurrentData.QueryStringTo);
            } else
            {
                IsFiltered = false;
            }
        }

        private void FilterClearedEvent(object sender, EventArgs e)
        {
            IsFiltered = false;
        }

        public DataGridColumnHeader AssignedDataGridColumnHeader
        {
            get { return (DataGridColumnHeader)GetValue(AssignedDataGridColumnHeaderProperty); }
            set { SetValue(AssignedDataGridColumnHeaderProperty, value); }
        }

        public static readonly DependencyProperty AssignedDataGridColumnHeaderProperty =
            DependencyProperty.Register(nameof(AssignedDataGridColumnHeader), typeof(DataGridColumnHeader), typeof(DataGridColumnFilter));

        public DataGridColumn AssignedDataGridColumn
        {
            get { return (DataGridColumn)GetValue(AssignedDataGridColumnProperty); }
            set { SetValue(AssignedDataGridColumnProperty, value); }
        }

        public static readonly DependencyProperty AssignedDataGridColumnProperty =
            DependencyProperty.Register(nameof(AssignedDataGridColumn), typeof(DataGridColumn), typeof(DataGridColumnFilter));

        public DataGrid DataGrid
        {
            get { return (DataGrid)GetValue(DataGridProperty); }
            set { SetValue(DataGridProperty, value); }
        }

        public static readonly DependencyProperty DataGridProperty =
            DependencyProperty.Register(nameof(DataGrid), typeof(DataGrid), typeof(DataGridColumnFilter));

        public IEnumerable DataGridItemsSource
        {
            get { return (IEnumerable)GetValue(DataGridItemsSourceProperty); }
            set { SetValue(DataGridItemsSourceProperty, value); }
        }

        public static readonly DependencyProperty DataGridItemsSourceProperty =
            DependencyProperty.Register(nameof(DataGridItemsSource), typeof(IEnumerable), typeof(DataGridColumnFilter));


        //private bool _isFiltered = false;
        //public bool IsFiltered
        //{
        //    get { return _isFiltered; }
        //    set { if (_isFiltered != value) { _isFiltered = value;   } }
        //}

        public bool IsFiltered
        {
            get { return (bool)GetValue(IsFilteredProperty); }
            set { SetValue(IsFilteredProperty, value); }
        }

        public static readonly DependencyProperty IsFilteredProperty =
            DependencyProperty.Register(nameof(IsFiltered), typeof(bool), typeof(DataGridColumnFilter));

        public bool IsFilteringInProgress
        {
            get { return (bool)GetValue(IsFilteringInProgressProperty); }
            set { SetValue(IsFilteringInProgressProperty, value); }
        }



        public static readonly DependencyProperty IsFilteringInProgressProperty =
            DependencyProperty.Register(nameof(IsFilteringInProgress), typeof(bool), typeof(DataGridColumnFilter));

        public FilterType FilterType { get { return FilterCurrentData != null ? FilterCurrentData.Type : FilterType.Text; } }

        public bool IsTextFilterControl
        {
            get { return (bool)GetValue(IsTextFilterControlProperty); }
            set { SetValue(IsTextFilterControlProperty, value); }
        }

        public static readonly DependencyProperty IsTextFilterControlProperty =
            DependencyProperty.Register(nameof(IsTextFilterControl), typeof(bool), typeof(DataGridColumnFilter));

        public bool IsNumericFilterControl
        {
            get { return (bool)GetValue(IsNumericFilterControlProperty); }
            set { SetValue(IsNumericFilterControlProperty, value); }
        }
        public static readonly DependencyProperty IsNumericFilterControlProperty =
            DependencyProperty.Register("IsNumericFilterControl", typeof(bool), typeof(DataGridColumnFilter));

        public bool IsNumericBetweenFilterControl
        {
            get { return (bool)GetValue(IsNumericBetweenFilterControlProperty); }
            set { SetValue(IsNumericBetweenFilterControlProperty, value); }
        }
        public static readonly DependencyProperty IsNumericBetweenFilterControlProperty =
            DependencyProperty.Register("IsNumericBetweenFilterControl", typeof(bool), typeof(DataGridColumnFilter));

        public bool IsBooleanFilterControl
        {
            get { return (bool)GetValue(IsBooleanFilterControlProperty); }
            set { SetValue(IsBooleanFilterControlProperty, value); }
        }
        public static readonly DependencyProperty IsBooleanFilterControlProperty =
            DependencyProperty.Register("IsBooleanFilterControl", typeof(bool), typeof(DataGridColumnFilter));

        public bool IsListFilterControl
        {
            get { return (bool)GetValue(IsListFilterControlProperty); }
            set { SetValue(IsListFilterControlProperty, value); }
        }
        public static readonly DependencyProperty IsListFilterControlProperty =
            DependencyProperty.Register("IsListFilterControl", typeof(bool), typeof(DataGridColumnFilter));

        public bool IsDateTimeFilterControl
        {
            get { return (bool)GetValue(IsDateTimeFilterControlProperty); }
            set { SetValue(IsDateTimeFilterControlProperty, value); }
        }
        public static readonly DependencyProperty IsDateTimeFilterControlProperty =
            DependencyProperty.Register("IsDateTimeFilterControl", typeof(bool), typeof(DataGridColumnFilter));

        public bool IsDateTimeBetweenFilterControl
        {
            get { return (bool)GetValue(IsDateTimeBetweenFilterControlProperty); }
            set { SetValue(IsDateTimeBetweenFilterControlProperty, value); }
        }
        public static readonly DependencyProperty IsDateTimeBetweenFilterControlProperty =
            DependencyProperty.Register("IsDateTimeBetweenFilterControl", typeof(bool), typeof(DataGridColumnFilter));

        public bool IsFirstFilterControl
        {
            get { return (bool)GetValue(IsFirstFilterControlProperty); }
            set { SetValue(IsFirstFilterControlProperty, value); }
        }
        public static readonly DependencyProperty IsFirstFilterControlProperty =
            DependencyProperty.Register("IsFirstFilterControl", typeof(bool), typeof(DataGridColumnFilter));

        public bool IsControlInitialized
        {
            get { return (bool)GetValue(IsControlInitializedProperty); }
            set { SetValue(IsControlInitializedProperty, value); }
        }
        public static readonly DependencyProperty IsControlInitializedProperty =
            DependencyProperty.Register("IsControlInitialized", typeof(bool), typeof(DataGridColumnFilter));
        #endregion

        #region Initialization
        private void Initialize()
        {
            if (DataGridItemsSource != null && AssignedDataGridColumn != null && DataGrid != null)
            {
                initFilterData();

                initControlType();

                handleListFilterType();

                HookUpCommands();

                IsControlInitialized = true;
            }
        }

        private void initFilterData()
        {
            if (FilterCurrentData == null || !FilterCurrentData.IsTypeInitialized)
            {
                string valuePropertyBindingPath = GetValuePropertyBindingPath(AssignedDataGridColumn);

                Type valuePropertyType = GetValuePropertyType(
                    valuePropertyBindingPath, GetItemSourceElementType(out var typeInitialized));

                FilterType filterType = GetFilterType(
                    valuePropertyType, 
                    IsComboDataGridColumn(),
                    IsBetweenType());

                FilterOperator filterOperator = FilterOperator.Undefined;

                string queryString   = String.Empty;
                string queryStringTo = String.Empty;

                FilterCurrentData = new FilterData(
                    filterOperator, 
                    filterType, 
                    valuePropertyBindingPath, 
                    valuePropertyType, 
                    queryString, 
                    queryStringTo,
                    typeInitialized,
                    DataGridColumnExtensions.GetIsCaseSensitiveSearch(AssignedDataGridColumn),
                    DataGridColumnExtensions.GetIsContainsTextSearch(AssignedDataGridColumn));
            }
        }

        private void initControlType()
        {
            IsFirstFilterControl    = false;

            IsTextFilterControl     = false;
            IsNumericFilterControl  = false;
            IsBooleanFilterControl  = false;
            IsListFilterControl     = false;
            IsDateTimeFilterControl = false;

            IsNumericBetweenFilterControl = false;
            IsDateTimeBetweenFilterControl = false;

            if (FilterType == FilterType.Text)
            {
                IsTextFilterControl = true;
            }
            else if (FilterType == FilterType.Numeric)
            {
                IsNumericFilterControl = true;
            }
            else if (FilterType == FilterType.Boolean)
            {
                IsBooleanFilterControl = true;
            }
            else if (FilterType == FilterType.List)
            {
                IsListFilterControl = true;
            }
            else if (FilterType == FilterType.DateTime)
            {
                IsDateTimeFilterControl = true;
            }
            else if (FilterType == FilterType.NumericBetween)
            {
                IsNumericBetweenFilterControl = true;
            }
            else if (FilterType == FilterType.DateTimeBetween)
            {
                IsDateTimeBetweenFilterControl = true;
            }
        }

        private void handleListFilterType()
        {
            if (FilterCurrentData.Type == FilterType.List)
            {
                ComboBox comboBox             = this.Template.FindName("PART_ComboBoxFilter", this) as ComboBox;
                DataGridComboBoxColumn column = AssignedDataGridColumn as DataGridComboBoxColumn;

                if (comboBox != null && column != null)
                {

                    if (DataGridComboBoxExtensions.GetIsTextFilter(column))
                    {
                        FilterCurrentData.Type = FilterType.Text;
                        initControlType();
                    }
                    else //list filter type
                    {
                        Binding columnItemsSourceBinding = null;
                        columnItemsSourceBinding = BindingOperations.GetBinding(column, DataGridComboBoxColumn.ItemsSourceProperty);

                        if (columnItemsSourceBinding == null)
                        {
                            System.Windows.Setter styleSetter = column.EditingElementStyle.Setters.FirstOrDefault(s => ((System.Windows.Setter)s).Property == DataGridComboBoxColumn.ItemsSourceProperty) as System.Windows.Setter;
                            if (styleSetter != null)
                                columnItemsSourceBinding = styleSetter.Value as Binding;
                        }

                        comboBox.DisplayMemberPath = column.DisplayMemberPath;
                        comboBox.SelectedValuePath = column.SelectedValuePath;

                        if (columnItemsSourceBinding != null)
                        {
                            BindingOperations.SetBinding(comboBox, ComboBox.ItemsSourceProperty, columnItemsSourceBinding);
                        }

                        comboBox.RequestBringIntoView 
                            += new RequestBringIntoViewEventHandler(setComboBindingAndHanldeUnsetValue);
                    }
                }
            }
        }

        private void setComboBindingAndHanldeUnsetValue(object sender, RequestBringIntoViewEventArgs e)
        {
            ComboBox combo = sender as ComboBox;
            DataGridComboBoxColumn column = AssignedDataGridColumn as DataGridComboBoxColumn;

            if (column.ItemsSource == null)
            {
                if (combo.ItemsSource != null)
                {
                    IList list = combo.ItemsSource.Cast<object>().ToList();

                    if (list.Count > 0 && list[0] != DependencyProperty.UnsetValue)
                    {
                        combo.RequestBringIntoView -=
                            new RequestBringIntoViewEventHandler(setComboBindingAndHanldeUnsetValue);

                        list.Insert(0, DependencyProperty.UnsetValue);

                        combo.DisplayMemberPath = column.DisplayMemberPath;
                        combo.SelectedValuePath = column.SelectedValuePath;

                        combo.ItemsSource = list;
                    }
                }
            }
            else
            {
                combo.RequestBringIntoView -=
                    new RequestBringIntoViewEventHandler(setComboBindingAndHanldeUnsetValue);

                IList comboList = null;
                IList columnList = null;

                if (combo.ItemsSource != null)
                {
                    comboList = combo.ItemsSource.Cast<object>().ToList();
                }

                columnList = column.ItemsSource.Cast<object>().ToList();

                if (comboList == null ||
                    (columnList.Count > 0 && columnList.Count + 1 != comboList.Count))
                {
                    columnList = column.ItemsSource.Cast<object>().ToList();
                    columnList.Insert(0, DependencyProperty.UnsetValue);

                    combo.ItemsSource = columnList;
                }

                combo.RequestBringIntoView +=
                    new RequestBringIntoViewEventHandler(setComboBindingAndHanldeUnsetValue);
            }
        }

        private static string GetValuePropertyBindingPath(DataGridColumn column)
        {
            string path = (string)column.GetValue(DataGridColumnExtensions.CustomBindingPathProperty);

            if (!string.IsNullOrEmpty(path)) return path;

            path = String.Empty;

            if (column is DataGridBoundColumn)
            {
                DataGridBoundColumn bc = column as DataGridBoundColumn;
                path = (bc.Binding as Binding).Path.Path;
            }
            else if (column is DataGridTemplateColumn)
            {
                DataGridTemplateColumn tc = column as DataGridTemplateColumn;

                object templateContent = tc.CellTemplate.LoadContent();

                if (templateContent != null && templateContent is TextBlock)
                {
                    TextBlock block = templateContent as TextBlock;

                    BindingExpression binding = block.GetBindingExpression(TextBlock.TextProperty);

                    path = binding.ParentBinding.Path.Path;
                }
            }
            else if (column is DataGridComboBoxColumn)
            {
                DataGridComboBoxColumn comboColumn = column as DataGridComboBoxColumn;

                path = null;

                Binding binding = ((comboColumn.SelectedValueBinding) as Binding);

                if (binding == null)
                {
                    binding = ((comboColumn.SelectedItemBinding) as Binding);
                }

                if (binding == null)
                {
                    binding = comboColumn.SelectedValueBinding as Binding;
                }

                if (binding != null)
                {
                    path = binding.Path.Path;
                }

                if (comboColumn.SelectedItemBinding != null && comboColumn.SelectedValueBinding == null)
                {
                    if (path != null && path.Trim().Length > 0)
                    {
                        if (DataGridComboBoxExtensions.GetIsTextFilter(comboColumn))
                        {
                            path += "." + comboColumn.DisplayMemberPath;
                        }
                        else
                        {
                            path += "." + comboColumn.SelectedValuePath;
                        }
                    }
                }
            }
            
            return path;
        }

        private static Type GetValuePropertyType(string path, Type elementType)
        {
            Type type = typeof(object);

            if (elementType != null)
            {
                string[] properties = path.Split(".".ToCharArray()[0]);

                PropertyInfo pi = null;

                if (properties.Length == 1)
                {
                    pi = elementType.GetProperty(path);
                }
                else
                {
                    pi = elementType.GetProperty(properties[0]);

                    for (int i = 1; i < properties.Length; i++)
                    {
                        if (pi != null)
                        {
                            pi = pi.PropertyType.GetProperty(properties[i]);
                        }
                    }
                }


                if (pi != null)
                {
                    type = pi.PropertyType;
                }
            }

            return type;
        }

        private Type GetItemSourceElementType(out bool typeInitialized)
        {
            typeInitialized = false;

            Type elementType = null;

            IList l = (DataGridItemsSource as IList);

            if (l != null && l.Count > 0)
            {
                object obj = l[0];

                if (obj != null)
                {
                    elementType = l[0].GetType();
                    typeInitialized = true;
                }
                else
                {
                    elementType = typeof(object);
                }
            }
            if (l == null)
            {
                ListCollectionView lw = (DataGridItemsSource as ListCollectionView);

                if (lw != null && lw.Count > 0)
                {
                    object obj = lw.CurrentItem;

                    if (obj != null)
                    {
                        elementType = lw.CurrentItem.GetType();
                        typeInitialized = true;
                    }
                    else
                    {
                        elementType = typeof(object);
                    }
                }
            }

            if (l != null && l.Count == 0)
            {
                //HACK: getting type from empty generic collection
                elementType = DataGridItemsSource.GetType().GetGenericArguments()[0];
                typeInitialized = true;
            }
            return elementType;
        }

        private static FilterType GetFilterType(
            Type valuePropertyType, 
            bool isAssignedDataGridColumnComboDataGridColumn,
            bool isBetweenType)
        {
            FilterType filterType;

            if (isAssignedDataGridColumnComboDataGridColumn)
            {
                filterType = FilterType.List;
            }
            else if (valuePropertyType == typeof(Boolean) || valuePropertyType == typeof(bool?))
            {
                filterType = FilterType.Boolean;
            }
            else if (valuePropertyType == typeof(SByte) || valuePropertyType == typeof(sbyte?))
            {
                filterType = FilterType.Numeric;
            }
            else if (valuePropertyType == typeof(Byte) || valuePropertyType == typeof(byte?))
            {
                filterType = FilterType.Numeric;
            }
            else if (valuePropertyType == typeof(Int16) || valuePropertyType == typeof(short?))
            {
                filterType = FilterType.Numeric;
            }
            else if (valuePropertyType == typeof(UInt16) || valuePropertyType == typeof(ushort?))
            {
                filterType = FilterType.Numeric;
            }
            else if (valuePropertyType == typeof(Int32) || valuePropertyType == typeof(int?))
            {
                filterType = FilterType.Numeric;
            }
            else if (valuePropertyType == typeof(UInt32) || valuePropertyType == typeof(uint?))
            {
                filterType = FilterType.Numeric;
            }
            else if (valuePropertyType == typeof(Int64) || valuePropertyType == typeof(long?))
            {
                filterType = FilterType.Numeric;
            }
            else if (valuePropertyType == typeof(Single) || valuePropertyType == typeof(float?))
            {
                filterType = FilterType.Numeric;
            }
            else if (valuePropertyType == typeof(Int64) || valuePropertyType == typeof(long?))
            {
                filterType = FilterType.Numeric;
            }
            else if (valuePropertyType == typeof(Decimal) || valuePropertyType == typeof(decimal?))
            {
                filterType = FilterType.Numeric;
            }
            else if (valuePropertyType == typeof(float) || valuePropertyType == typeof(float?))
            {
                filterType = FilterType.Numeric;
            }
            else if (valuePropertyType == typeof(Double) || valuePropertyType == typeof(double?))
            {
                filterType = FilterType.Numeric;
            }
            else if (valuePropertyType == typeof(Int64) || valuePropertyType == typeof(long?))
            {
                filterType = FilterType.Numeric;
            }
            else if (valuePropertyType == typeof(DateTime) || valuePropertyType == typeof(DateTime?))
            {
                filterType = FilterType.DateTime;
            }
            else
            {
                filterType = FilterType.Text;
            }

            if (filterType == FilterType.Numeric && isBetweenType)
            {
                filterType = FilterType.NumericBetween;
            }
            else if (filterType == FilterType.DateTime && isBetweenType)
            {
                filterType = FilterType.DateTimeBetween;
            }

            return filterType;
        }

        private bool IsComboDataGridColumn()
        {
            return AssignedDataGridColumn is DataGridComboBoxColumn;
        }

        private bool IsBetweenType()
        {
            return DataGridColumnExtensions.GetIsBetweenFilterControl(AssignedDataGridColumn);
        }

        private void HookUpCommands()
        {
            if (DataGridExtensions.GetClearFilterCommand(DataGrid) == null)
            {
                DataGridExtensions.SetClearFilterCommand(
                    DataGrid, new DataGridFilterCommand(ClearQuery));
            }
        }
        #endregion

        #region Querying
        void filterCurrentData_FilterChangedEvent(object sender, EventArgs e)
        {
            if (DataGrid != null)
            {
                QueryController query = QueryControllerFactory.GetQueryController(
                    DataGrid, FilterCurrentData, DataGridItemsSource);

                AddFilterStateHandlers(query);

                query.DoQuery();

                IsFirstFilterControl = query.IsCurentControlFirstControl;
            }
        }

        private void ClearQuery(object parameter)
        {
            if (DataGrid != null)
            {
                QueryController query = QueryControllerFactory.GetQueryController(
                    DataGrid, FilterCurrentData, DataGridItemsSource);

                query.ClearFilter();
            }
        }

        private void AddFilterStateHandlers(QueryController query)
        {
            query.FilteringStarted -= new EventHandler<EventArgs>(query_FilteringStarted);
            query.FilteringFinished -= new EventHandler<EventArgs>(query_FilteringFinished);

            query.FilteringStarted += new EventHandler<EventArgs>(query_FilteringStarted);
            query.FilteringFinished += new EventHandler<EventArgs>(query_FilteringFinished);
        }

        void query_FilteringFinished(object sender, EventArgs e)
        {
            if (FilterCurrentData == null)
            {
                if ((sender as QueryController).ColumnFilterData != null)
                {
                    this.IsFilteringInProgress = true;
                }
                else
                {
                    this.IsFilteringInProgress = false;
                }
            }
            else
            {
                if (FilterCurrentData.Equals((sender as QueryController).ColumnFilterData))
                {
                    this.IsFilteringInProgress = false;
                }
            }
        }

        void query_FilteringStarted(object sender, EventArgs e)
        {
            if (FilterCurrentData == null) {
                if ((sender as QueryController).ColumnFilterData != null)
                {
                    this.IsFilteringInProgress = true;
                }
            }
            else
            {
                if (FilterCurrentData.Equals((sender as QueryController).ColumnFilterData))
                {
                    this.IsFilteringInProgress = true;
                }
            }
        }
        #endregion
    }
}