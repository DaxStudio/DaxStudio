using ADOTabular;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Caliburn.Micro;

namespace DaxStudio.UI.Model
{

    public static class ADOTabularModelExtensions
    {
        public static List<FilterableTreeViewItem> TreeViewTables(this ADOTabularModel model )
        {
            var lst = new List<FilterableTreeViewItem>();
            foreach (var t in model.Tables)
            {
                lst.Add(new TreeViewTable(t, t.TreeViewColumns));
            }
            return lst;   
        }

        public static IEnumerable<FilterableTreeViewItem> TreeViewColumns(this ADOTabularTable table)
        {
            var lst = new List<FilterableTreeViewItem>();
            foreach (var c in table.Columns)
            {
                lst.Add( new TreeViewColumn(c, c.TreeViewColumnChildren));
            }
            return lst;
        }

        public static IEnumerable<FilterableTreeViewItem> TreeViewColumnChildren(this ADOTabularColumn column)
        {
            var lst = new List<FilterableTreeViewItem>();
            var hier = column as ADOTabularHierarchy;
            if (hier != null)
            {
                foreach (var lvl in hier.Levels)
                {
                    lst.Add( new TreeViewColumn(lvl));
                }
            }
            var kpi = column as ADOTabularKpi;
            if (kpi != null)
            {
                foreach (var comp in kpi.Components)
                {
                    lst.Add( new TreeViewColumn(comp));
                }
            }
            return lst;
        }

    }

    public delegate IEnumerable<FilterableTreeViewItem> GetChildrenDelegate();
    public class FilterableTreeViewItem : PropertyChangedBase
    {
        GetChildrenDelegate _getChildren;
        public FilterableTreeViewItem(GetChildrenDelegate getChildren)
        {
            _getChildren = getChildren;
        }

        private IEnumerable<FilterableTreeViewItem> _children;
        public IEnumerable<FilterableTreeViewItem> Children  {
            get
            {
                if (_children == null && _getChildren != null)
                { _children = _getChildren.Invoke(); }
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

        public void ApplyCriteria(string criteria, Stack<FilterableTreeViewItem> ancestors)
        {
            if (IsCriteriaMatched(criteria))
            {
                IsMatch = true;
                foreach (var ancestor in ancestors)
                {
                    ancestor.IsMatch = true;
                    ancestor.IsExpanded = !String.IsNullOrEmpty(criteria);
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

    class TreeViewTable : FilterableTreeViewItem, IADOTabularObject
    {
        private readonly ADOTabularTable _table;
        public TreeViewTable(ADOTabularTable table, GetChildrenDelegate getChildren):base(getChildren)
        {
            _table = table;
        }

        public MetadataImages MetadataImage { get { return _table.MetadataImage; } }
        public string Caption { get { return _table.Caption; } }
        public string Description { get { return _table.Description; } }
        public bool ShowDescription { get { return !string.IsNullOrEmpty(Description); } }
        public override bool IsCriteriaMatched(string criteria)
        {
            return String.IsNullOrEmpty(criteria) ||  Caption.IndexOf(criteria, StringComparison.InvariantCultureIgnoreCase) >= 0 ;
        }

        
        string IADOTabularObject.DaxName
        {
            get { return _table.DaxName; }
        }
        public int ColumnCount
        {
            get { return _table.Columns.Count; }
        }
    }

    public class TreeViewColumn: FilterableTreeViewItem, IADOTabularObject
    {
    /*
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
        private IADOTabularObject _tabularObject;
        private IADOTabularColumn _column;
        private List<string> _sampleData;
        private bool _updatingBasicStats = false;
        private bool _updatingSampleData = false;
        private string _minValue = string.Empty;
        private string _maxValue = string.Empty;
        private long _distinctValues = 0;


        public TreeViewColumn(ADOTabularColumn column, GetChildrenDelegate getChildren):base(getChildren)
        {
            _sampleData = new List<string>();
            _tabularObject = column;
            _column = column;
            Description = column.Description;
            DataTypeName = column.DataTypeName;
            MetadataImage = column.MetadataImage;
            FormatString = column.FormatString;
            MinValue = column.MinValue;
            MaxValue = column.MaxValue;
            DistinctValues = column.DistinctValues;
        }

        public bool HasBasicStats { get { return MaxValue != MinValue && DistinctValues != 1; } }
        public bool HasSampleData { get { return _sampleData.Count > 0; } }

        public bool ShowMinMax { get {
                if ( _column.GetType() != typeof(ADOTabularColumn)) return false;
                if (MinValue == string.Empty && MaxValue == string.Empty) return false;
                return true;
            }
        }

        public bool ShowSampleData
        {
            get
            {
                return HasSampleData && _column.GetType() == typeof(ADOTabularColumn) ;
            }
        }

        public bool ShowDistinctValues { get { return typeof(ADOTabularColumn) == _column.GetType(); } }
        public TreeViewColumn(ADOTabularKpiComponent kpiComponent):base(null)
        {
            _tabularObject = kpiComponent;
            DataTypeName = kpiComponent.DataTypeName;
            
            MetadataImage = MetadataImages.Measure;
        }

        public TreeViewColumn(ADOTabularKpi kpi)
            : base(null)
        {
            _tabularObject = kpi;
            DataTypeName = kpi.DataTypeName;
            MetadataImage = MetadataImages.Kpi;
        }
        public TreeViewColumn(ADOTabularLevel level):base(null)
        {
            _tabularObject = level;
            Description = level.Column.Description;
            DataTypeName = level.Column.DataTypeName;

            MetadataImage = MetadataImages.Column;            
        }

        public TreeViewColumn(ADOTabularHierarchy hier)
            : base(null)
        {
            _tabularObject = hier;
            MetadataImage = MetadataImages.Hierarchy;
        }
        public MetadataImages MetadataImage { get; set; }
        public string Caption { get { return _tabularObject.Caption; } }
        public string Description { get; private set; }
        public string DataTypeName { get; private set; }
        string IADOTabularObject.DaxName { get { return _tabularObject.DaxName; } }

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

        public bool IsMeasure
        {
            get
            {
                return this.MetadataImage == MetadataImages.Measure;
            }
        }

        public IADOTabularObject Column
        {
            get
            {
                return this._tabularObject;
            }
        }

        public override bool IsCriteriaMatched(string criteria)
        {
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
                    _column.UpdateBasicStats(connection);
                    MinValue = _column.MinValue;
                    MaxValue = _column.MaxValue;
                    DistinctValues = _column.DistinctValues;
                });
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
