using System.ComponentModel.Composition;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using DaxStudio.UI.Model;
using System.Windows.Data;
using System.ComponentModel;
using Serilog;
using DaxStudio.Interfaces;
using Dax.ViewModel;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace DaxStudio.UI.ViewModels
{


    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export]
    public class VertiPaqAnalyzerViewModel : ToolWindowBase
        , IHandle<DocumentConnectionUpdateEvent>
        , IHandle<UpdateGlobalOptions>
        , IViewAware
    {

        private readonly IEventAggregator _eventAggregator;
        private readonly IGlobalOptions _globalOptions;

        //Dax.ViewModel.VpaModel viewModel,
        [ImportingConstructor]
        public VertiPaqAnalyzerViewModel(IEventAggregator eventAggregator, DocumentViewModel currentDocument, IGlobalOptions options)
        {
            Log.Debug("{class} {method} {message}", "VertiPaqAnalyzerViewModel", "ctor", "start");
            //this.ViewModel = viewModel;
            IsBusy = true;
            _globalOptions = options;
            _eventAggregator = eventAggregator;
            _eventAggregator.Subscribe(this);
            CurrentDocument = currentDocument;

            // configure default sort columns
            RelationshipSortColumn = "UsedSize";
            TableSortColumn = "TotalSize";
            PartitionSortColumn = "RowsCount";
            ColumnSortColumn = "TotalSize";

            Log.Debug("{class} {method} {message}", "VertiPaqAnalyzerViewModel", "ctor", "end");
        }

        public override void TryClose(bool? dialogResult = null)
        {
            // unsubscribe from event aggregator
            _eventAggregator.Unsubscribe(this);
            base.TryClose(dialogResult);
        }

        private VpaModel _viewModel;
        public Dax.ViewModel.VpaModel ViewModel
        {
            get { return _viewModel; }
            set
            {
                _viewModel = value;
                _groupedColumns = null;
                _sortedColumns = null;
                _groupedRelationships = null;
                _groupedPartitions = null;
                SummaryViewModel = new VpaSummaryViewModel(this);
                NotifyOfPropertyChange(() => ViewModel);
                NotifyOfPropertyChange(() => GroupedColumns);
                NotifyOfPropertyChange(() => SortedColumns);
                NotifyOfPropertyChange(() => GroupedRelationships);
                NotifyOfPropertyChange(() => TreeviewColumns);
                NotifyOfPropertyChange(() => TreeviewRelationships);
                NotifyOfPropertyChange(() => GroupedPartitions);
                NotifyOfPropertyChange(() => SummaryViewModel);
                IsBusy = false;
            }
        }

        private ICollectionView _groupedColumns;
        public ICollectionView GroupedColumns
        {
            get
            {
                if (_groupedColumns == null && ViewModel != null)
                {
                    var cols = ViewModel.Tables.Select(t => new VpaTableViewModel(t, this, VpaSort.Table )).SelectMany(t => t.Columns);
                    _groupedColumns = CollectionViewSource.GetDefaultView(cols);
                    _groupedColumns.GroupDescriptions.Add(new TableGroupDescription("Table"));
                    // sort by TableSize then by TotalSize
                    _groupedColumns.SortDescriptions.Add(new SortDescription("TableSize", ListSortDirection.Descending));
                    _groupedColumns.SortDescriptions.Add(new SortDescription("TotalSize", ListSortDirection.Descending));
                }
                return _groupedColumns;
            }
        }

        private ICollectionView _groupedRelationships;

        public VpaSummaryViewModel SummaryViewModel { get; private set; }

        public ICollectionView GroupedRelationships
        {
            get
            {
                if (_groupedRelationships == null && ViewModel != null)
                {
                    var rels = ViewModel.TablesWithFromRelationships.Select(t => new VpaTableViewModel(t, this, VpaSort.Relationship)).SelectMany(t => t.RelationshipsFrom);
                    _groupedRelationships = CollectionViewSource.GetDefaultView(rels);
                    _groupedRelationships.GroupDescriptions.Add(new RelationshipGroupDescription("Table"));
                    _groupedRelationships.SortDescriptions.Add(new SortDescription("UsedSize", ListSortDirection.Descending));
                }
                return _groupedRelationships;
            }
        }

        private ICollectionView _groupedPartitions;
        public ICollectionView GroupedPartitions
        {
            get
            {
                if (_groupedPartitions == null && ViewModel != null)
                {
                    var partitions = from t in ViewModel.Tables
                                     from p in t.Partitions
                                     select new VpaPartitionViewModel(p, new VpaTableViewModel(t, this, VpaSort.Partition), this);

                    _groupedPartitions = CollectionViewSource.GetDefaultView(partitions);
                    _groupedPartitions.GroupDescriptions.Add(new PartitionGroupDescription("Table"));
                    _groupedPartitions.SortDescriptions.Add(new SortDescription("RowsCount", ListSortDirection.Descending));
                }
                return _groupedPartitions;
            }
        }

        private ICollectionView _sortedColumns;
        public ICollectionView SortedColumns
        {
            get
            {
                if (_sortedColumns == null && ViewModel != null)
                {
                    long maxSize = ViewModel.Columns.Max(c => c.TotalSize);
                    var cols = ViewModel.Columns.Select(c => new VpaColumnViewModel(c) { MaxColumnTotalSize = maxSize });

                    _sortedColumns = CollectionViewSource.GetDefaultView(cols);
                    _sortedColumns.SortDescriptions.Add(new SortDescription(nameof(VpaColumn.TotalSize), ListSortDirection.Descending));
                }
                return _sortedColumns;

            }
        }

        public IEnumerable<VpaTable> TreeviewTables { get { return ViewModel.Tables; } }
        public IEnumerable<VpaColumn> TreeviewColumns { get { return ViewModel.Columns; } }
        public IEnumerable<VpaTable> TreeviewRelationships { get { return ViewModel.TablesWithFromRelationships; } }

        // TODO: we might add the database name here
        public override string Title => "VertiPaq Analyzer Metrics";

        public override string DefaultDockingPane => "DockBottom";
        public override string ContentId => "vertipaq-analyzer";
        public override ImageSource IconSource {
            get
            {
                var imgSourceConverter = new ImageSourceConverter();
                return imgSourceConverter.ConvertFromInvariantString(
                    @"pack://application:,,,/DaxStudio.UI;component/images/icon-view-metrics.png") as ImageSource;

            }
        }

        public void Handle(DocumentConnectionUpdateEvent message)
        {

            // TODO connect VPA data
            Log.Information("VertiPaq Analyzer Handle DocumentConnectionUpdateEvent call");
        }

        public void MouseDoubleClick(object sender)//, MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("clicked!");
        }

        public void Handle(UpdateGlobalOptions message)
        {
            // NotifyOfPropertyChange(() => ShowTraceColumns);
            Log.Information("VertiPaq Analyzer Handle UpdateGlobalOptions call");
        }

        public void OnTableSorting(System.Windows.Controls.DataGridSortingEventArgs e)
        {
            if (e.Column.SortDirection == null) e.Column.SortDirection = ListSortDirection.Ascending;
            if (TableSortColumn == e.Column.SortMemberPath) { TableSortDirection = TableSortDirection * -1; }
            else { TableSortDirection = -1; } 
            TableSortColumn = e.Column.SortMemberPath;
        }

        public string TableSortColumn { get; set; }
        public int TableSortDirection { get; set; } = -1;

        public void OnColumnSorting(System.Windows.Controls.DataGridSortingEventArgs e)
        {
            if (e.Column.SortDirection == null) e.Column.SortDirection = ListSortDirection.Ascending;
            if (ColumnSortColumn == e.Column.SortMemberPath) { ColumnSortDirection = ColumnSortDirection * -1; }
            else { ColumnSortDirection = -1; } 
            ColumnSortColumn = e.Column.SortMemberPath;
        }

        public string ColumnSortColumn { get; set; }
        public int ColumnSortDirection { get; set; } = -1;

        public void OnRelationshipSorting(System.Windows.Controls.DataGridSortingEventArgs e)
        {
            if (e.Column.SortDirection == null) e.Column.SortDirection = ListSortDirection.Ascending;
            if (RelationshipSortColumn == e.Column.SortMemberPath) { RelationshipSortDirection = RelationshipSortDirection * -1; }
            else { RelationshipSortDirection = -1; } 
            RelationshipSortColumn = e.Column.SortMemberPath;
        }

        public string RelationshipSortColumn { get; set; }
        public int RelationshipSortDirection { get; set; } = -1;

        public void OnPartitionSorting(System.Windows.Controls.DataGridSortingEventArgs e)
        {
            if (e.Column.SortDirection == null) e.Column.SortDirection = ListSortDirection.Ascending;
            if (PartitionSortColumn == e.Column.SortMemberPath) { PartitionSortDirection = PartitionSortDirection * -1; }
            else { PartitionSortDirection = -1; }
            PartitionSortColumn = e.Column.SortMemberPath;
        }

        public string PartitionSortColumn { get; set; }
        public int PartitionSortDirection { get; set; } = -1;

        public DocumentViewModel CurrentDocument { get; }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                _isBusy = value;
                NotifyOfPropertyChange(nameof(IsBusy));
            }
        }

        public string BusyMessage => "Loading Model Metrics";

        public TooltipStruct Tooltips => new TooltipStruct();

        public class TooltipStruct
        {
            public string Cardinality => "The total number of distinct values in a column";
            public string TableSize => "The total size of a table including all columns, relationships and hierarchies";
            public string TotalSize => "The total size of the column = Data + Dictionary + Hierarchies";
            public string DictionarySize => "The size of the dictionary";
            public string DataSize => "The size of the data for the column";
            public string HierarchySize => "The size of hierarchy structures";
            public string DataType => "The data type for the column";
            public string Encoding => "The encoding type for the column";
            public string RIViolations => "Indicates the number of relationships where there are values on the 'many' side of a relationship that do not exist on the '1' side";
            public string UserHierarchySize => "The size of user hierarchy structures";
            public string RelationshipSize => "The size taken up by relationship structures";
            public string PercentOfTable => "The space taken up as a percentage of the parent table";
            public string PercentOfDatabase => "The space taken up as a percentage of the total size of the database";
            public string Segments => "The number of segments";
            public string TotalSegments => "The total number of segments";
            public string Pageable => "The number of pageable segments";
            public string Resident => "The number of resident segments";
            public string Temperature => "Scaled numeric feequency of segment access";
            public string LastAccessed => "Last access time of a pageable segment";
            public string Partitions => "The number of partitions";
            public string Columns => "The number of columns in the table";
            public string TableRows => "The total number of rows in the table";
            public string MaxFromCardinality => "The maximum number of distinct values on the 'from' side of the relationship";
            public string MaxToCardinality => "The maximum number of distinct values on the 'to' side of the relationship";
            public string MaxOneToManyRatio => "The maximum ratio of rows between the 'to' and the 'from' side of the relationship (only for 1:M type)";
            public string MissingKeys => "The number of distinct missing key values";
            public string InvalidRows => "The number of rows with missing keys";
            public string SampleViolations => "3 examples of any missing key values\nNote: The 'Sample Violations' data is not saved out to the .vpax file when the metrics are exported";
            public string TableColumn => "A combination of the table and column name in the form '<table>-<column>'";
            public string TableRelationship => "The name of the relationship";
            public string OneToManyRatio => "This is the ratio of the rows on the 1 side of a relationship to the rows on the many side";
        }

    }
}
