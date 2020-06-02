using System.ComponentModel.Composition;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using DaxStudio.UI.Model;
using DaxStudio.UI.Interfaces;
using System.Windows.Data;
using System;
using System.ComponentModel;
using Serilog;
using System.Windows.Input;
using DaxStudio.Interfaces;
using Dax.ViewModel;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Windows.Navigation;

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

        [ImportingConstructor]
        public VertiPaqAnalyzerViewModel(Dax.ViewModel.VpaModel viewModel, IEventAggregator eventAggregator, DocumentViewModel currentDocument, IGlobalOptions options)
        {
            Log.Debug("{class} {method} {message}", "VertiPaqAnalyzerViewModel", "ctor", "start");
            this.ViewModel = viewModel;
            _globalOptions = options;
            _eventAggregator = eventAggregator;
            _eventAggregator.Subscribe(this);
            CurrentDocument = currentDocument;
            Log.Debug("{class} {method} {message}", "VertiPaqAnalyzerViewModel", "ctor", "end");
        }

        public override void TryClose(bool? dialogResult = null)
        {
            // unsubscribe from event aggregator
            _eventAggregator.Unsubscribe(this);
            base.TryClose(dialogResult);
        }

        private VpaModel _viewModel;
        public Dax.ViewModel.VpaModel ViewModel {
            get { return _viewModel; }
            set {
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
            }
        }

        private ICollectionView _groupedColumns;
        public ICollectionView GroupedColumns {
            get {
                if (_groupedColumns == null)
                {
                    var cols = ViewModel.Tables.Select(t => new VpaTableViewModel(t, this)).SelectMany(t => t.Columns);
                    _groupedColumns = CollectionViewSource.GetDefaultView(cols);
                    _groupedColumns.GroupDescriptions.Add(new TableGroupDescription("Table"));
                    _groupedColumns.SortDescriptions.Add(new SortDescription("TotalSize", ListSortDirection.Descending));
                }
                return _groupedColumns;
            }
        }

        private ICollectionView _groupedRelationships;

        public VpaSummaryViewModel SummaryViewModel { get; private set; }

        public ICollectionView GroupedRelationships {
            get {
                if (_groupedRelationships == null)
                {
                    var rels = ViewModel.TablesWithFromRelationships.Select(t => new VpaTableViewModel(t, this)).SelectMany(t => t.RelationshipsFrom);
                    _groupedRelationships = CollectionViewSource.GetDefaultView(rels);
                    _groupedRelationships.GroupDescriptions.Add(new RelationshipGroupDescription("Table"));
                    _groupedRelationships.SortDescriptions.Add(new SortDescription("UsedSize", ListSortDirection.Descending));
                }
                return _groupedRelationships;
            }
        }

        private ICollectionView _groupedPartitions;
        public ICollectionView GroupedPartitions {
            get {
                if (_groupedPartitions == null)
                {
                    var partitions = from t in ViewModel.Tables
                                     from p in t.Partitions
                                     select new VpaPartitionViewModel(p, new VpaTableViewModel(t, this), this);

                    _groupedPartitions = CollectionViewSource.GetDefaultView(partitions);
                    _groupedPartitions.GroupDescriptions.Add(new PartitionGroupDescription("Table"));
                    _groupedPartitions.SortDescriptions.Add(new SortDescription("RowsCount", ListSortDirection.Descending));
                }
                return _groupedPartitions;
            }
        }

        private ICollectionView _sortedColumns;
        public ICollectionView SortedColumns {
            get {
                if (_sortedColumns == null)
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
        public override string Title {
            get { return "VertiPaq Analyzer Metrics"; }
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

        public void SortTableColumn(System.Windows.Controls.DataGridSortingEventArgs e)
        {
            if (SortColumn == e.Column.SortMemberPath) { SortDirection = SortDirection * -1; }
            else { SortDirection = 1; }
            SortColumn = e.Column.SortMemberPath;
        }

        public string SortColumn { get; set; }
        public int SortDirection { get; set; }
        public DocumentViewModel CurrentDocument { get; }

    }
}
