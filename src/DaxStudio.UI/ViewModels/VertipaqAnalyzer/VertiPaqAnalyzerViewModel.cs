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
using System.Threading.Tasks;
using System.Threading;
using DaxStudio.UI.Interfaces;
using System.IO.Packaging;
using DaxStudio.UI.Utils;
using System;
using System.IO;
using Microsoft.AnalysisServices.Tabular;
using ADOTabular.Enums;
using ICSharpCode.AvalonEdit.CodeCompletion;
using Microsoft.AspNet.SignalR.Client;
using System.Windows;
using System.Windows.Forms;
using ADOTabular;

namespace DaxStudio.UI.ViewModels
{


    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export]
    public class VertiPaqAnalyzerViewModel : ToolWindowBase
        , IHandle<DocumentConnectionUpdateEvent>
        , IHandle<UpdateGlobalOptions>
        , IViewAware
        , ISaveState
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
            _eventAggregator.SubscribeOnPublishedThread(this);
            CurrentDocument = currentDocument;

            // configure default sort columns
            RelationshipSortColumn = "UsedSize";
            TableSortColumn = "TotalSize";
            PartitionSortColumn = "RowsCount";
            ColumnSortColumn = "TotalSize";

            Log.Debug("{class} {method} {message}", "VertiPaqAnalyzerViewModel", "ctor", "end");
        }

        public override Task TryCloseAsync(bool? dialogResult = null)
        {
            // unsubscribe from event aggregator
            _eventAggregator.Unsubscribe(this);
            return base.TryCloseAsync(dialogResult);
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
                _eventAggregator.PublishOnUIThreadAsync(new ViewMetricsCompleteEvent());
            }
        }

        private ICollectionView _groupedColumns;
        public ICollectionView GroupedColumns
        {
            get
            {
                if (_groupedColumns == null && ViewModel != null)
                {
                    // Skip the special column "RowNumber-GUID" that is not relevant for the analysis
                    const string ROWNUMBER_COLUMNNAME = "RowNumber-";

                    var cols = ViewModel.Tables.Select(t => new VpaTableViewModel(t, this, VpaSort.Table, _globalOptions )).SelectMany(t => t.Columns.Where(c => !c.ColumnName.StartsWith(ROWNUMBER_COLUMNNAME)));
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
                    var rels = ViewModel.TablesWithFromRelationships.Select(t => new VpaTableViewModel(t, this, VpaSort.Relationship, _globalOptions)).SelectMany(t => t.RelationshipsFrom);
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
                                     select new VpaPartitionViewModel(p, new VpaTableViewModel(t, this, VpaSort.Partition, _globalOptions), this, _globalOptions);

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
                    var cols = ViewModel.Columns.Select(c => new VpaColumnViewModel(c,_globalOptions) { MaxColumnTotalSize = maxSize });

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
        public override string Title => "VertiPaq Analyzer";

        public override string DefaultDockingPane => "DockBottom";
        public override string ContentId => "vertipaq-analyzer";

        public Task HandleAsync(DocumentConnectionUpdateEvent message,CancellationToken cancellationToken)
        {

            // TODO connect VPA data
            Log.Information("VertiPaq Analyzer Handle DocumentConnectionUpdateEvent call");
            return Task.CompletedTask;
        }

        public void MouseDoubleClick(object sender)//, MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("clicked!");
        }

        public Task HandleAsync(UpdateGlobalOptions message, CancellationToken cancellationToken)
        {
            // NotifyOfPropertyChange(() => ShowTraceColumns);
            NotifyOfPropertyChange(nameof(ColumnsShowDashedTableColumn));
            NotifyOfPropertyChange(nameof(ColumnsShowDaxColumnName));
            NotifyOfPropertyChange(nameof(ColumnsShowTwoColumns));
            Log.Information("VertiPaq Analyzer Handle UpdateGlobalOptions call");
            return Task.CompletedTask;
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

        public void Save(string filename)
        {
            return; // we do nothing here since we don't save satellite vpax files
        }

        public void Load(string filename)
        {
            return; // we do nothing here since we don't save satellite vpax files
        }

        public string GetJson()
        {
            return String.Empty;
        }

        public void LoadJson(string json)
        {
            return; // we do nothing here since we don't save satellite vpax files
        }

        public void SavePackage(Package package)
        {
            Uri uriTom = PackUriHelper.CreatePartUri(new Uri(DaxxFormat.VpaxFile, UriKind.Relative));
            try { 
                var serverName = this.ViewModel.Model.ServerName.ToString();
                var databaseName = ViewModel.Model.ModelName.ToString();
                //
                // Get TOM model from the SSAS engine
                //
                //Microsoft.AnalysisServices.Tabular.Database database = _globalOptions.VpaxIncludeTom ? Dax.Metadata.Extractor.TomExtractor.GetDatabase(serverName, databaseName) : null;

                // 
                // Create VertiPaq Analyzer views
                //
                Dax.ViewVpaExport.Model viewVpa = new Dax.ViewVpaExport.Model(ViewModel.Model);

                //model.ModelName = new Dax.Metadata.DaxName(modelName);

                //
                // Save VPAX file to daxx file
                // 

                using (Stream strm = package.CreatePart(uriTom, "application/json", CompressionOption.Maximum).GetStream())
                {
                    Dax.Vpax.Tools.VpaxTools.ExportVpax(strm, ViewModel.Model, viewVpa, Database);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(VertiPaqAnalyzerViewModel), nameof(LoadPackage), "Error saving vpax data to daxx file");
                _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, $"Error saving vpax data to daxx file\n{ex.Message}"));
            }
        }

        public void LoadPackage(Package package)
        {
            var uri = PackUriHelper.CreatePartUri(new Uri(DaxxFormat.VpaxFile, UriKind.Relative));
            if (!package.PartExists(uri)) return;

            var part = package.GetPart(uri);
            try
            {
                using (Stream strm = part.GetStream())
                {
                    var content = Dax.Vpax.Tools.VpaxTools.ImportVpax(strm);
                    if (!CurrentDocument.Connection.IsConnected)
                        Task.Run(async () => { await CurrentDocument.Connection.ConnectAsync(new ConnectEvent(CurrentDocument.Connection.ApplicationName, content)); });

                    var view = new Dax.ViewModel.VpaModel(content.DaxModel);
                    // update view model
                    ViewModel = view;
                    Database = content.TomDatabase;
                }

                Activate();
            }
            catch( Exception ex)
            {
                Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(VertiPaqAnalyzerViewModel), nameof(LoadPackage), "Error loading vpax data from daxx file");
                _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, $"Error loading vpax data from daxx file\n{ex.Message}"));
            }
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

        public Database Database { get; internal set; }

        public class TooltipStruct
        {
            public string Cardinality => "The total number of distinct values in a column";
            public string TableSize => "The total size of a table including all columns, relationships and hierarchies in bytes";
            public string TotalSize => "The total size of the column = Data + Dictionary + Hierarchies in bytes";
            public string DictionarySize => "The size of the column's dictionary in bytes";
            public string DataSize => "The size of the data for the column in bytes";
            public string HierarchySize => "The size of hierarchy structures in bytes";
            public string DataType => "The data type for the column";
            public string Encoding => "The encoding type for the column";
            public string RIViolations => "Indicates the number of Referential Integrity (RI) Violations. Which are relationships where there are values on the 'many' side of a relationship that do not exist on the '1' side";
            public string UserHierarchySize => "The size of user hierarchy structures in bytes";
            public string RelationshipSize => "The size taken up by relationship structures in bytes";
            public string PercentOfTable => "The space taken up by a column as a percentage of the parent table";
            public string PercentOfDatabase => "The space taken up by a table or column as a percentage of the total size of the database";
            public string Segments => "The maximum number of segments of any single column";
            public string TotalSegments => "The total number of segments for all columns";
            public string Pageable => "The number of pageable segments";
            public string Resident => "The number of memory resident segments";
            public string Temperature => "A scaled numeric frequency of segment access";
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
            public string TableName => "The name of the table";
            public string ColumnName => "The name of the column";
        }

        internal async Task ExportAnalysisDataAsync(string fileName)
        {
            try
            {
                if (ViewModel == null || ViewModel?.Model == null)
                {
                    var msg = "There is no Metrics Data to export";
                    Log.Error(Common.Constants.LogMessageTemplate, nameof(VertiPaqAnalyzerViewModel), nameof(ExportAnalysisDataAsync), msg);
                    await _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, msg));
                    return;
                }

                await Task.Run(() =>
                {
                    Dax.ViewVpaExport.Model viewVpa = new Dax.ViewVpaExport.Model(ViewModel.Model);
                    ModelAnalyzer.ExportExistingModelToVPAX(fileName, ViewModel.Model, viewVpa, Database);
                });
            }
            catch (Exception ex)
            {
                var msg = $"The following error occured while trying to export to a vpax file:\n{ex.Message}";
                Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(VertiPaqAnalyzerViewModel), nameof(ExportAnalysisDataAsync), msg);
                await _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, msg));
            }
        }

        public void ExportBimFile()
        {
            if (Database == null)
            {
                System.Windows.MessageBox.Show("No bim file included in metrics", "Export BIM", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var filename = (ViewModel?.Model?.ModelName?.Name ?? "model") + ".bim";

            if (CurrentDocument.IsDiskFileName) filename = (Path.GetFileNameWithoutExtension(CurrentDocument?.DisplayName) ?? "model") + ".bim";

            var saveAsDlg = new SaveFileDialog()
            {
                FileName = filename,
                DefaultExt = "bim",
                Title = "Save .bim file",
                Filter = "Model BIM file (*.bim)|*.bim"
            };
            
            if (saveAsDlg.ShowDialog() == DialogResult.OK)
            {
                System.Diagnostics.Debug.WriteLine($"exporting to {saveAsDlg.FileName}");
                var opts = new SerializeOptions();
                opts.IgnoreInferredObjects = true;
                opts.IgnoreInferredProperties = true;
                
                File.WriteAllText(saveAsDlg.FileName, JsonSerializer.SerializeDatabase(Database, opts));
            }
        }

        public bool ColumnsShowTwoColumns => this._globalOptions.VpaTableColumnDisplay == DaxStudio.Interfaces.Enums.VpaTableColumnDisplay.TwoColumns;
        public bool ColumnsShowDashedTableColumn => this._globalOptions.VpaTableColumnDisplay == DaxStudio.Interfaces.Enums.VpaTableColumnDisplay.TableDashColumn;
        public bool ColumnsShowDaxColumnName => this._globalOptions.VpaTableColumnDisplay == DaxStudio.Interfaces.Enums.VpaTableColumnDisplay.DaxNameFormat;

    }
}
