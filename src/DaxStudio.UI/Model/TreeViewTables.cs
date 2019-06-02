using ADOTabular;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Events;
using ADOTabular.Utils;
using DaxStudio.UI.Interfaces;

namespace DaxStudio.UI.Model
{

    public static class ADOTabularModelExtensions
    {

        public static List<FilterableTreeViewItem> TreeViewTables(this ADOTabularModel model, IGlobalOptions options, IEventAggregator eventAggregator , IMetadataPane metadataPane)
        {
            var lst = new List<FilterableTreeViewItem>();
            foreach (var t in model.Tables)
            {
                if (t.Private && !metadataPane.ShowHiddenObjects) continue; // skip Private tables
                if (t.ShowAsVariationsOnly && !metadataPane.ShowHiddenObjects) continue; // skip Variation tables
                if (!metadataPane.ShowHiddenObjects && !t.IsVisible) continue; // skip hidden tables

                lst.Add(new TreeViewTable(t, t.TreeViewColumns,options, eventAggregator, metadataPane));
            }
            return lst;   
        }

        public static IEnumerable<FilterableTreeViewItem> TreeViewColumns(this ADOTabularTable table, ADOTabularTable table2, IGlobalOptions options, IEventAggregator eventAggregator, IMetadataPane metadataPane)
        {
            var lst = new SortedList<string, FilterableTreeViewItem>();
            foreach (var c in table.Columns)
            {

                if (!metadataPane.ShowHiddenObjects && !c.IsVisible) continue; // skip hidden columns

                if (!c.IsInDisplayFolder)
                {
                    var col = new TreeViewColumn(c, c.TreeViewColumnChildren, options, eventAggregator, metadataPane);
                    
                    var lstItem = lst.FirstOrDefault(x => x.Value.Name == col.Name).Value;
                    if (lstItem != null && lstItem.ObjectType == lstItem.ObjectType)
                    {
                        // todo add this col as a child of lstItem
                        throw new NotSupportedException();
                    }
                    else
                    {
                        lst.Add(col.Caption, col);
                    }
                }
            }

            foreach( IADOTabularObjectReference f in table.FolderItems)
            {
                var folder = new TreeViewColumn(f, f.TreeViewFolderChildren, table, options, eventAggregator, metadataPane);

                lst.Add(folder.Caption, folder);
            }
            return lst.Values;
        }

        public static IEnumerable<FilterableTreeViewItem> TreeViewColumnChildren(this ADOTabularColumn column, ADOTabularTable table, IGlobalOptions options,IEventAggregator eventAggregator, IMetadataPane metadataPane)
        {
            var lst = new List<FilterableTreeViewItem>();
            var hier = column as ADOTabularHierarchy;
            if (hier != null)
            {
                foreach (var lvl in hier.Levels)
                {
                    lst.Add( new TreeViewColumn(lvl,options,eventAggregator, metadataPane));
                }
            }
            var kpi = column as ADOTabularKpi;
            if (kpi != null)
            {
                foreach (var comp in kpi.Components)
                {
                    lst.Add( new TreeViewColumn(comp,options,eventAggregator,metadataPane));
                }
            }
            return lst;
        }

        public static IEnumerable<FilterableTreeViewItem> TreeViewFolderChildren(this IADOTabularObjectReference objRef, ADOTabularTable table, IGlobalOptions options, IEventAggregator eventAggregator, IMetadataPane metadataPane)
        {
            var lst = new List<FilterableTreeViewItem>();

            var folder = objRef as IADOTabularFolderReference;

            if (folder != null)
            {
                foreach (var folderItem in folder.FolderItems)
                {
                    GetChildrenDelegate getChildren = null;
                    if (folderItem is IADOTabularFolderReference) getChildren = ((IADOTabularObjectReference)folderItem).TreeViewFolderChildren;
                    lst.Add(new TreeViewColumn(folderItem, getChildren, table, options, eventAggregator, metadataPane));
                }
            }
            else
            {
                var col = table.Columns.GetByPropertyRef(objRef.InternalReference);
                lst.Add(new TreeViewColumn(col, null, options, eventAggregator,metadataPane));
            }
            

            return lst;
        }


    }

    //public delegate IEnumerable<FilterableTreeViewItem> GetChildrenDelegate(IGlobalOptions options, IEventAggregator eventAggregator);
    public delegate IEnumerable<FilterableTreeViewItem> GetChildrenDelegate(ADOTabularTable table, IGlobalOptions options, IEventAggregator eventAggregator, IMetadataPane metadataPane);
    
    public abstract class FilterableTreeViewItem : PropertyChangedBase
    {
        protected GetChildrenDelegate _getChildren;
        protected IGlobalOptions _options;
        protected IEventAggregator _eventAggregator;
        protected ADOTabularTable _table;

        public FilterableTreeViewItem(ADOTabularTable table, GetChildrenDelegate getChildren, IGlobalOptions options, IEventAggregator eventAggregator, IMetadataPane metadataPane)
        {
            _eventAggregator = eventAggregator;
            _options = options;
            _table = table;
            _getChildren = getChildren;
            MetadataPane = metadataPane;
        }

        private IEnumerable<FilterableTreeViewItem> _children;
        public IEnumerable<FilterableTreeViewItem> Children  {
            get
            {
                if (_children == null && _getChildren != null)
                { _children = _getChildren.Invoke(_table, _options, _eventAggregator,MetadataPane); }
                return _children;
            }
        }

        private bool match = true;
        public bool IsMatch
        {
            get { return match; }
            set
            {
                if (value == match) return;
                match = value;
                NotifyOfPropertyChange(()=> IsMatch);
            }
        }

        public abstract string Name { get; }
        public abstract ADOTabularObjectType ObjectType { get; }

        public abstract bool IsVisible { get;  }

        public virtual bool IsCriteriaMatched(string criteria)
        {return false;}

        private bool _isExpanded ;
        public bool IsExpanded { 
            get { return _isExpanded; }
            set
            {
                if (value == _isExpanded) return;
                _isExpanded = value;
                NotifyOfPropertyChange(()=> IsExpanded);
            }
        }

        public IMetadataPane MetadataPane { get; }

        public IGlobalOptions Options
        {
            get { return _options; }
            set { _options = value; }
        }

        public void ApplyCriteria(string criteria, Stack<FilterableTreeViewItem> ancestors)
        {
            if (IsCriteriaMatched(criteria))
            {
                IsMatch = true;
                foreach (var ancestor in ancestors)
                {
                    //if (!MetadataPane.ShowHiddenObjects && !ancestor.IsVisible)
                    //{
                    //    ancestor.IsMatch = false;
                    //}
                    //else
                    //{
                        ancestor.IsMatch = true;
                        ancestor.IsExpanded = !String.IsNullOrEmpty(criteria);
                    //}
                }
            }
            else
                IsMatch = false;

            //NotifyOfPropertyChange(() => IsMatch);

            if (_getChildren == null) return; // if there are no children then finish here

            ancestors.Push(this);
            foreach (var child in Children)
                child.ApplyCriteria(criteria, ancestors);

            ancestors.Pop();
        }
    }

    public class TreeViewTable : FilterableTreeViewItem, IADOTabularObject
    {
        //private readonly ADOTabularTable _table;
        public TreeViewTable(ADOTabularTable table, GetChildrenDelegate getChildren, IGlobalOptions options, IEventAggregator eventAggregator, IMetadataPane metadataPane ):base(table, getChildren,options,eventAggregator, metadataPane)
        {
            _table = table;
        }

        public MetadataImages MetadataImage { get { return _table.MetadataImage; } }

        // the Caption is affected by translations, it is visible in resultsets, but is not used in queries
        public string Caption => _table.Caption; 
        // the Name property is the untranslated object name used in queries and DAX expressions
        public override string Name => _table.Name;
        public override ADOTabularObjectType ObjectType => ADOTabularObjectType.Table;
        public string Description { get { return _table.Description; } }
        public override bool IsVisible => _table.IsVisible;

        public bool ShowDescription { get { return !string.IsNullOrEmpty(Description); } }
        public override bool IsCriteriaMatched(string criteria)
        {
        //    if (!this.MetadataPane.ShowHiddenObjects && !this.IsVisible) return false;
            return String.IsNullOrEmpty(criteria) ||  Caption.IndexOf(criteria, StringComparison.InvariantCultureIgnoreCase) >= 0 ;
        }

        // this the fully qualified (and possibly quoted)
        // so for a column it would be something like 'table name'[column name]
        // but for a table it would be 'table name'
        string IADOTabularObject.DaxName
        {
            get { return _table.DaxName; }
        }
        public int ColumnCount
        {
            get { return _table.Columns.Count; }
        }
    }

    public static class TreeViewColumnFactory
    {
        public static FilterableTreeViewItem Create(ADOTabularColumn col) {
            //TODO create folder hierarchy if DisplayFolder is not empty string
            // else return raw column
            return null;
        }
    }

    public class TreeViewColumn: FilterableTreeViewItem, IADOTabularObject
    {
        /*
        Folder

        Hierarchy
          Caption
          Description
        Level -> Column
          Caption
          DataTypeName
          Description
        Column
          Caption
          DataTypeName
          Description
          MetadataImage
        KPIComponent -> Measure (Column)
          Caption
          DataTypeName
          Description
        */

        private IADOTabularColumn _column;
        private List<string> _sampleData;
        private bool _updatingBasicStats = false;
        private bool _updatingSampleData = false;
        private string _minValue = string.Empty;
        private string _maxValue = string.Empty;
        private long _distinctValues = 0;

        #region Constructors
        public TreeViewColumn(ADOTabularColumn column, GetChildrenDelegate getChildren, IGlobalOptions options, IEventAggregator eventAggregator, IMetadataPane metadataPane):base(column.Table, getChildren, options,eventAggregator,metadataPane)
        {
            _eventAggregator = eventAggregator;
            _sampleData = new List<string>();
            Column = column;
            _column = column;
            Options = options;
            Description = column.Description;
            DataTypeName = column.DataTypeName;
            MetadataImage = column.MetadataImage;
            FormatString = column.FormatString;
            MinValue = column.MinValue;
            MaxValue = column.MaxValue;
            DistinctValues = column.DistinctValues;
        }

        //public TreeViewColumn(string displayFolder, GetChildrenDelegate getChildren, IGlobalOptions options, IEventAggregator eventAggregator) : base(getChildren, options, eventAggregator)
        //{
        //    _eventAggregator = eventAggregator;
        //    _sampleData = new List<string>();
        //    //Column = column;
        //    //_column = column;
        //    Options = options;
            
        //    MetadataImage = MetadataImages.Folder;
            
        //}

        public TreeViewColumn(ADOTabularKpiComponent kpiComponent, IGlobalOptions options, IEventAggregator eventAggregator, IMetadataPane metadataPane):base(null, null,null,eventAggregator, metadataPane)
        {
            Options = options;
            Column = kpiComponent;
            DataTypeName = kpiComponent.DataTypeName;
            
            MetadataImage = MetadataImages.Measure;
        }
        public TreeViewColumn(ADOTabularKpi kpi, IGlobalOptions options, IEventAggregator eventAggregator, IMetadataPane metadataPane)
            : base(null, null, options,eventAggregator, metadataPane)
        {
            Options = options;
            Column = kpi;
            DataTypeName = kpi.DataTypeName;
            MetadataImage = MetadataImages.Kpi;
        }
        public TreeViewColumn(ADOTabularLevel level, IGlobalOptions options,IEventAggregator eventAggregator, IMetadataPane metadataPane):base(null, null, options,eventAggregator,metadataPane)
        {
            Options = options;
            Column = level;
            Description = level.Column.Description;
            DataTypeName = level.Column.DataTypeName;

            MetadataImage = MetadataImages.Column;            
        }

        public TreeViewColumn(ADOTabularHierarchy hier, IGlobalOptions options,IEventAggregator eventAggregator, IMetadataPane metadataPane)
            : base(null, null,options,eventAggregator,metadataPane)
        {
            Options = options;
            Column = hier;
            MetadataImage = MetadataImages.Hierarchy;
        }

        public TreeViewColumn(IADOTabularObjectReference reference, GetChildrenDelegate getChildren, ADOTabularTable table, IGlobalOptions options, IEventAggregator eventAggregator, IMetadataPane metadataPane)
            : base(table, getChildren, options, eventAggregator, metadataPane)
        {
            Options = options;
            IADOTabularFolderReference folder = reference as IADOTabularFolderReference;
            if (folder == null)
            {
                Column = table.Columns.GetByPropertyRef(reference.InternalReference);
                MetadataImage = Column.GetMetadataImage();
            }
            else
            {
                _caption = reference.Name;
                MetadataImage = MetadataImages.Folder;
            }
        }


        #endregion

        public bool HasBasicStats { get { return MaxValue != MinValue && DistinctValues != 1; } }
        public bool HasSampleData { get { return _sampleData != null && _sampleData.Count > 0; } }

        public bool ShowMinMax { get {
                if (!Options.ShowTooltipBasicStats) return false;
                if ( _column != null &&_column.GetType() != typeof(ADOTabularColumn)) return false;
                if (MinValue == string.Empty && MaxValue == string.Empty) return false;
                return true;
            }
        }

        public bool ShowSampleData
        {
            get
            {
                if (!Options.ShowTooltipSampleData) return false;
                return HasSampleData && _column != null && _column.GetType() == typeof(ADOTabularColumn) ;
            }
        }

        public bool ShowDistinctValues { get {
                if (!Options.ShowTooltipBasicStats) return false;
                return _column != null && typeof(ADOTabularColumn) == _column.GetType(); }
        }

        public MetadataImages MetadataImage { get; set; }

        private string _caption = string.Empty;
        public string Caption => Column?.Caption??_caption;
        public override string Name => Column?.Name??_caption;
        public override ADOTabularObjectType ObjectType => Column.ObjectType;
        public string Description { get; private set; }
        public string DataTypeName { get; private set; }
        string IADOTabularObject.DaxName => Column?.DaxName??string.Empty;

        public bool ShowDescription { get { return !string.IsNullOrEmpty(Description); } }
        public bool ShowDataType { get { return !string.IsNullOrEmpty(DataTypeName); } }
        public string FormatString { get; private set; }
        public string MinValue { get { return _minValue; }
            private set { _minValue = value;
                NotifyOfPropertyChange(() => MinValue);
            }
        }
        public string MaxValue { get { return _maxValue; }
            private set { _maxValue = value;
                NotifyOfPropertyChange(() => MaxValue);
            }
        }

        public long DistinctValues { get { return _distinctValues; }
            private set { _distinctValues = value;
                NotifyOfPropertyChange(() => DistinctValues);
            }
        }


        public override bool IsVisible => _column?.IsVisible ?? true;

        public bool IsColumnOrMeasure => IsColumn || IsMeasure;

        public bool IsColumn =>  this.MetadataImage == MetadataImages.Column
                              || this.MetadataImage == MetadataImages.HiddenColumn;
 

        public bool IsMeasure => MetadataImage == MetadataImages.Measure 
                              || MetadataImage == MetadataImages.HiddenMeasure;
         

        public bool IsTable => MetadataImage == MetadataImages.Table
                            || MetadataImage == MetadataImages.HiddenTable;
         

        public IADOTabularObject Column { get; }

        public override bool IsCriteriaMatched(string criteria)
        {
            if (!this.MetadataPane.ShowHiddenObjects && !this.IsVisible) return false;
            return String.IsNullOrEmpty(criteria) || Caption.IndexOf(criteria, StringComparison.InvariantCultureIgnoreCase) >= 0;
        }

        public async void GetSampleDataAsync(ADOTabularConnection connection, int sampleSize)
        {
            UpdatingSampleData = true;
            try
            {
                await Task.Run(() => {                    
                    using (var newConn = connection.Clone())
                    {
                        SampleData = _column.GetSampleData(newConn, sampleSize);
                    }
                });
            }
            catch (Exception ex)
            {
                await _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Warning, $"Error populating tooltip sample data: {ex.Message}"));
            }
            finally
            {
                UpdatingSampleData = false;
            }
        }

        public List<string> SampleData
        {
            get { return _sampleData; }
            set { _sampleData = value;
                NotifyOfPropertyChange(() => SampleData);
            }
        }

        public async void UpdateBasicStatsAsync(ADOTabularConnection connection )
        {
            UpdatingBasicStats = true;
            try {
                await Task.Run(() => {
                    using (var newConn = connection.Clone())
                    {
                        _column.UpdateBasicStats(newConn);
                        MinValue = _column.MinValue;
                        MaxValue = _column.MaxValue;
                        DistinctValues = _column.DistinctValues;   
                    }
                });
            }
            catch (Exception ex)
            {
                await _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Warning, $"Error populating tooltip basic statistics data: {ex.Message}"));
            }
            finally
            {
                UpdatingBasicStats = false;
            }
        }
        public bool UpdatingSampleData
        {
            get { return _updatingSampleData; }
            private set
            {
                _updatingSampleData = value;
                NotifyOfPropertyChange(() => UpdatingSampleData);
                NotifyOfPropertyChange(() => ShowSampleData);
            }
        }

        public bool UpdatingBasicStats {
            get { return _updatingBasicStats; }
            private set {
                _updatingBasicStats = value;
                NotifyOfPropertyChange(() => UpdatingBasicStats);
                NotifyOfPropertyChange(() => ShowDistinctValues);
                NotifyOfPropertyChange(() => ShowMinMax);
            }
        }

        
    }
}
