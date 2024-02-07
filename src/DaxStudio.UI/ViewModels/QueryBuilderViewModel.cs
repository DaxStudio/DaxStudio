using ADOTabular.Interfaces;
using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Events;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Model;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Windows;
using DaxStudio.UI.JsonConverters;
using DaxStudio.UI.Utils;
using System.Windows.Media;
using ADOTabular;
using DaxStudio.UI.Enums;
using DaxStudio.UI.Extensions;
using Newtonsoft.Json;
using System.Windows.Data;
using System.Threading;
using System.Threading.Tasks;
using DaxStudio.Interfaces.Enums;

namespace DaxStudio.UI.ViewModels
{
    

    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export]
    [DataContract]
    public sealed class QueryBuilderViewModel : ToolWindowBase
        ,IQueryTextProvider
        ,IHandle<ActivateDocumentEvent>
        ,IHandle<ConnectionChangedEvent>
        ,IHandle<DuplicateMeasureEvent>
        ,IHandle<QueryBuilderUpdateEvent>
        ,IHandle<RunStyleChangedEvent>
        ,IHandle<SendColumnToQueryBuilderEvent>
        ,IHandle<SendTabularObjectToQueryBuilderEvent>
        ,IDisposable
        ,ISaveState
        ,INotifyPropertyChanged
        
    {
        const string NewMeasurePrefix = "MyMeasure";

        [ImportingConstructor]
        public QueryBuilderViewModel(IEventAggregator eventAggregator, DocumentViewModel document, IGlobalOptions globalOptions)
        {
            EventAggregator = eventAggregator;
            Document = document;
            Options = globalOptions;
            Filters = new QueryBuilderFilterList(EventAggregator ,GetModelCapabilities);
            IsVisible = false;
            Columns = new QueryBuilderFieldList(EventAggregator);
            Columns.PropertyChanged += OnColumnsPropertyChanged;
            Filters.PropertyChanged += OnFiltersPropertyChanged;
            VisibilityChanged += OnVisibilityChanged;
            //RunStyle = document.SelectedRunStyle;
        }

        private bool _autoGenerate;
        [DataMember]
        public bool AutoGenerate { get=> _autoGenerate;
            set {
                _autoGenerate = value;
                NotifyOfPropertyChange();
                if (!Options.HasShownQueryBuilderAutoGenerateWarning) { 
                    ShowAutoGenerateWarning();
                    Options.HasShownQueryBuilderAutoGenerateWarning = true;
                }
                AutoGenerateQuery();
            } 
        }

        public void GotFocus()
        {
            SetRunStyle();
        }

        private void SetRunStyle()
        {
            EventAggregator.PublishOnUIThreadAsync(new SetRunStyleEvent(RunStyleIcons.RunBuilder));
        }

        private void UnsetRunStyle()
        {
            EventAggregator.PublishOnUIThreadAsync(new SetRunStyleEvent(RunStyleIcons.RunOnly));
        }

        private void ShowAutoGenerateWarning()
        {
            MessageBox.Show("Enabling this option will overwrite any changes you may have made to the query text.\n\nHowever, if you make any further manual edits to the query this option will be automatically disabled.", "Auto Generate Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        private void AutoGenerateQuery()
        {
            if (AutoGenerate && Columns.Count > 0) SendTextToEditor();
            if (AutoGenerate && Columns.Count == 0) ClearEditor();
        }

        private void OnFiltersPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            SetRunStyle();
            AutoGenerateQuery();
        }

        private void OnVisibilityChanged(object sender, EventArgs e)
        {
            if (IsVisible)
            {
                EventAggregator.SubscribeOnPublishedThread(this);
                SetRunStyle();
            }
            else
            {
                EventAggregator.Unsubscribe(this);
                UnsetRunStyle();
            }
        }

        private void OnColumnsPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            SetRunStyle();
            NotifyOfPropertyChange(nameof(CanRunQuery));
            NotifyOfPropertyChange(nameof(CanSendTextToEditor));
            NotifyOfPropertyChange(nameof(CanOrderBy));
            NotifyOfPropertyChange(nameof(OrderBy));
            AutoGenerateQuery();
        }


        // ReSharper disable once UnusedMember.Global
        public override string Title => "Query Builder";
        public override string DefaultDockingPane => "DockMidLeft";
        public override bool CanHide => true;
        public override string ContentId => "query-builder";

        public bool CanOrderBy => Columns.Any();
        [DataMember]
        public QueryBuilderFieldList Columns { get; } 
        [DataMember]
        public QueryBuilderFilterList Filters { get; }

        public ICollectionView OrderBy { get {
                //Columns.Where(c => c.ObjectType == ADOTabularObjectType.Column || c.ObjectType == ADOTabularObjectType.Level); 
                ICollectionView view = CollectionViewSource.GetDefaultView(Columns);
                view.Filter = (c) => { return ((QueryBuilderColumn)c).ObjectType == ADOTabularObjectType.Column || ((QueryBuilderColumn)c).ObjectType == ADOTabularObjectType.Level; };
                return view;
            } 
        }


        private bool _isEnabled = true;
        public new bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                if (_isEnabled) EventAggregator.SubscribeOnPublishedThread(this);
                else EventAggregator.Unsubscribe(this);
                NotifyOfPropertyChange();
            }
        }
        public IEventAggregator EventAggregator { get; }
        public DocumentViewModel Document { get; }
        public IGlobalOptions Options { get; }

        private QueryBuilderColumn _selectedColumn;
        public QueryBuilderColumn SelectedColumn { get => _selectedColumn;
            set {
                _selectedColumn = value;
                NotifyOfPropertyChange();
            }
        }

        private int _selectedIndex;
        public int SelectedIndex { get => _selectedIndex;
            set {
                _selectedIndex = value;
                NotifyOfPropertyChange();
            }
        }

        public string EditorText => QueryText;

        public string QueryText { 
            get { 
                try {
                    var modelCaps = GetModelCapabilities();
                    var firstTableName = GetFirstTableName();
                    return QueryBuilder.BuildQuery(modelCaps,Columns.Items, Filters.Items, AutoGenerate, firstTableName, DelimiterType.Comma); 
                }
                catch (Exception ex)
                {
                    Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(QueryBuilderViewModel), nameof(QueryText), ex.Message);
                    EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, $"Error generating query: {ex.Message}"));
                }
                return string.Empty;
            } 
        
        }

        public string QueryTextWithDefaultDelimiter
        {
            get
            {
                try
                {
                    var modelCaps = GetModelCapabilities();
                    var firstTableName = GetFirstTableName();
                    return QueryBuilder.BuildQuery(modelCaps, Columns.Items, Filters.Items, AutoGenerate, firstTableName, Options.DefaultSeparator);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(QueryBuilderViewModel), nameof(QueryText), ex.Message);
                    EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, $"Error generating query: {ex.Message}"));
                }
                return string.Empty;
            }

        }

        private string GetFirstTableName()
        {
            var model = Document.Connection.SelectedModel;
            return model?.Tables.Count > 0? model?.Tables[0].DaxName : string.Empty;
        }

        private IModelCapabilities GetModelCapabilities()
        {
            var model = Document.Connection.SelectedModel;
            return model.Capabilities;
        }

        public List<Microsoft.AnalysisServices.AdomdClient.AdomdParameter> ParameterCollection
        {
            get
            {
                var coll = new List<Microsoft.AnalysisServices.AdomdClient.AdomdParameter>();
                if (QueryInfo?.Parameters?.Values != null)
                {
                    foreach (var p in QueryInfo?.Parameters?.Values)
                    {
                        coll.Add(new Microsoft.AnalysisServices.AdomdClient.AdomdParameter(p.Name, p.Value));
                    }
                }
                return coll;
            }
        }

        public Dictionary<string, QueryParameter> QueryParameters
        {
            get;
        } = new Dictionary<string, QueryParameter>();

        // ReSharper disable once UnusedMember.Global
        public void RunQuery() {
            if (! CheckForCrossjoins() )
                EventAggregator.PublishOnUIThreadAsync(new RunQueryEvent(Document.SelectedTarget, Document.SelectedRunStyle) { QueryProvider = this });
        }

        private bool CheckForCrossjoins()
        {
            bool hasMeasures = this.Columns.Items.Any(c => c.IsMeasure());
            if (hasMeasures) return false;  // we have a measure so that should prevent a large crossjoin
            
            var cols = this.Columns.GroupBy(c => c.TableName);
            if (cols.Count() == 1) return false;  // if all the columns are from one table it will not produce a crossjoin

            return MessageBox.Show("Including columns from multiple tables without a measure is likely to result in a large crossjoin which could use a lot of memory.\n\nAre you sure you want to proceed?", "Potential Crossjoin Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No;
        }

        public void SendTextToEditor()
        {
            EventAggregator.PublishOnUIThreadAsync(new SendTextToEditor(QueryTextWithDefaultDelimiter,false,true));
        }

        public void ClearEditor()
        {
            EventAggregator.PublishOnUIThreadAsync(new SendTextToEditor(string.Empty , false, true));
        }

        public bool CanAutoGenerate => IsConnectedToAModelWithTables;
        public bool CanRunQuery => IsConnectedToAModelWithTables && Columns.Items.Count > 0;

        public bool CanSendTextToEditor => IsConnectedToAModelWithTables && Columns.Items.Count > 0;

        public bool CanAddNewMeasure
        {
            get {
                try
                {
                    return IsConnectedToAModelWithTables;
                }
                catch (Exception ex)
                {
                    Log.Error(ex,Common.Constants.LogMessageTemplate, nameof(QueryBuilderViewModel), nameof(CanAddNewMeasure), "Error checking if the model has any tables");
                    EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, $"The following error occurred while checking if your model has any tables:\n{ex.Message}"));                    
                }

                return false;
            }
        }

        private bool IsConnectedToAModelWithTables
        {
            get
            {
                try
                {
                    return Document?.Connection?.SelectedModel?.Tables.Count > 0;
                }
                catch (Exception ex)
                {
                    var msg = $"The following error occurred while getting count of tables for the selected model: {ex.Message }";
                    Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(QueryBuilderViewModel), nameof(IsConnectedToAModelWithTables), msg);
                    EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, msg));
                }
                return false;
            }
        }

        public RunStyle RunStyle { get => Document.SelectedRunStyle; }
        public QueryInfo QueryInfo { get;set; }

        // ReSharper disable once UnusedMember.Global
        public void AddNewMeasure()
        {
            if (Document.Connection.SelectedModel.Tables.Count == 0)
            {
                var msg = "Cannot add a new measure if the model has no tables";
                Log.Warning(nameof(QueryBuilderViewModel), nameof(AddNewMeasure), msg);
                EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Warning,msg ));
                return;
            }
            
            var firstTable = Document.Connection.SelectedModel.Tables.First();
            // TODO - need to make sure key is unique
            var newMeasureName = GetCustomMeasureName();
            var newMeasure = new QueryBuilderColumn(newMeasureName,firstTable, this.EventAggregator);
            Columns.Add(newMeasure);
            //newMeasure.IsModelItem = false;
            SelectedColumn = newMeasure;
            SelectedIndex = Columns.Count - 1;
            Columns.EditNewMeasure(newMeasure);
            IsEnabled = false;
            //EventAggregator.PublishOnUIThreadAsync(new ShowMeasureExpressionEditor(newMeasure));
            SetRunStyle();
        }

        // Finds a unique name for the new measure
        public string GetCustomMeasureName()
        {
            int customMeasureCnt = Columns.Count(c => c.Caption.StartsWith(NewMeasurePrefix));
            if (customMeasureCnt == 0) return NewMeasurePrefix;
            // if the user has deleted some earlier custom measure numbers we need to loop and keep
            // searching until we find an unused one
            while (Columns.Any(c => c.Caption == $"{NewMeasurePrefix}{customMeasureCnt}" ))
            {
                customMeasureCnt++;
            }
            return $"{NewMeasurePrefix}{customMeasureCnt}";

        }

        protected override void OnVisibilityChanged(EventArgs e)
        {
            base.OnVisibilityChanged(e);
            
        }

        #region IDisposable Support
        private bool _disposedValue; // To detect redundant calls

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // unhook PropertyChanged event
                    Columns.PropertyChanged -= OnColumnsPropertyChanged;
                }

                _disposedValue = true;
            }
        }

        
        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }


        #endregion

        public Task HandleAsync(SendColumnToQueryBuilderEvent message, CancellationToken cancellationToken)
        {
            switch (message.ItemType)
            {
                case QueryBuilderItemType.Column:
                    AddColumnToColumns(message.Column);
                    break;
                case QueryBuilderItemType.Filter:
                    AddColumnToFilters(message.Column);
                    break;
                case QueryBuilderItemType.Both:
                    AddColumnToColumns(message.Column);
                    AddColumnToFilters(message.Column);
                    break;
            }
            return Task.CompletedTask;
        }

        public async Task HandleAsync(SendTabularObjectToQueryBuilderEvent message, CancellationToken cancellationToken)
        {
            if (message.TabularObject is TreeViewColumn col)
            {
                await HandleAsync(new SendColumnToQueryBuilderEvent(col, message.ItemType), cancellationToken);
            }
            return;
        }

        private void AddColumnToColumns(ITreeviewColumn column)
        {
            if (column == null) return;
            if (column.InternalColumn == null) return;

            if (Columns.Contains(column.InternalColumn))
            {
                // write warning and return
                EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Warning, $"Cannot add the {column.InternalColumn.Caption} column to the query builder columns a second time"));
                return;
            }
            Columns.Add(column.InternalColumn);
            SetRunStyle();
        }

        private void AddColumnToFilters(ITreeviewColumn column)
        {
            if (column == null) return;
            if (column.InternalColumn == null) return;

            if (Filters.Contains(column.InternalColumn))
            {
                // write warning and return
                EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Warning, $"Cannot add the {column.InternalColumn.Caption} column to the query builder filters a second time"));
                return;
            }
            Filters.Add(column.InternalColumn);
            SetRunStyle();
        }

        public void Save(string filename)
        {
            var json = GetJson();
            File.WriteAllText(filename + ".queryBuilder", json);
        }

        public string GetJson()
        {
            var settings = new JsonSerializerSettings()
            {
                ContractResolver = new InterfaceContractResolver(typeof(IADOTabularColumn), typeof(IADOTabularObject))
            };
            string json = JsonConvert.SerializeObject(this, settings);
            return json;
        }

        public void LoadJson(string jsontext)
        {
            try
            {
                JsonSerializerSettings settings = new JsonSerializerSettings();
                settings.Converters.Add(new QueryBuilderConverter(this.EventAggregator, this.Document, this.Options));
                //settings.Converters.Add(new ADOTabularColumnCreationConverter());
                var result = JsonConvert.DeserializeObject<QueryBuilderViewModel>(jsontext, settings);
                LoadViewModel(result);
                
            }
            catch (Exception ex)
            {
                var msg = $"The following error occurred while attempting to load the Query Builder from your saved file:\n{ex.Message}";
                Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(QueryBuilderViewModel), nameof(LoadJson), msg);
                EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, msg));
                return;
            }
        }

        public void Load(string filename)
        {
            filename = filename + ".queryBuilder";
            if (!File.Exists(filename)) return;
            
            string data = File.ReadAllText(filename);
            LoadJson(data);
            
        }

        private void LoadViewModel(QueryBuilderViewModel model)
        {
            if (model == null) return;

            this.Columns.Clear();
            foreach (var col in model.Columns)
            {
                this.Columns.Add(col);
            }

            this.Filters.Clear();
            foreach (var filter in model.Filters.Items)
            {
                this.Filters.Add(filter);
            }
            this.AutoGenerate = model.AutoGenerate;
            this.IsVisible = true;

            if (IsConnectedToAModelWithTables) RelinkTabularObjects();
        }

        public void SavePackage(Package package)
        {
           
            Uri uriTom = PackUriHelper.CreatePartUri(new Uri(DaxxFormat.QueryBuilder, UriKind.Relative));
            using (TextWriter tw = new StreamWriter(package.CreatePart(uriTom, "application/json", CompressionOption.Maximum).GetStream(), Encoding.UTF8))
            {
                tw.Write(GetJson());
                tw.Close();
            }
        }

        public void LoadPackage(Package package)
        {
            var uri = PackUriHelper.CreatePartUri(new Uri(DaxxFormat.QueryBuilder, UriKind.Relative));
            if (!package.PartExists(uri)) return;

            var part = package.GetPart(uri);
            using (TextReader tr = new StreamReader(part.GetStream()))
            {
                string data = tr.ReadToEnd();
                LoadJson(data);
            }
            
        }

        public Task HandleAsync(DuplicateMeasureEvent message, CancellationToken cancellationToken)
        {
            Log.Information(Common.Constants.LogMessageTemplate,nameof(QueryBuilderViewModel), "Handle<DuplicateMeasureEvent>", $"Duplicating Measure: {message.Measure.Caption}");
            var meas = new QueryBuilderColumn($"{message.Measure.Caption} - Copy", (IADOTabularObject)message.Measure.SelectedTable, EventAggregator)
                { MeasureExpression = message.Measure.MeasureExpression };
            Columns.Add(meas);
            return Task.CompletedTask;
        }

        public Task HandleAsync(QueryBuilderUpdateEvent message, CancellationToken cancellationToken)
        {
            AutoGenerateQuery();
            return Task.CompletedTask;
        }

        public void Clear()
        {
            Columns.Items.Clear();
            Filters.Items.Clear();
            NotifyOfPropertyChange(nameof(OrderBy));
            AutoGenerateQuery();
        }

        public Task HandleAsync(ActivateDocumentEvent message, CancellationToken cancellationToken)
        {
            RefreshButtonStates();
            return Task.CompletedTask;
        }

        private void RefreshButtonStates()
        {
            NotifyOfPropertyChange(nameof(CanAddNewMeasure));
            NotifyOfPropertyChange(nameof(CanRunQuery));
            NotifyOfPropertyChange(nameof(CanSendTextToEditor));
            NotifyOfPropertyChange(nameof(CanAutoGenerate));
            NotifyOfPropertyChange(nameof(RunStyle));
        }

        public Task HandleAsync(ConnectionChangedEvent message, CancellationToken cancellationToken)
        {
            RefreshButtonStates();
            if (IsConnectedToAModelWithTables) RelinkTabularObjects();
            return Task.CompletedTask;
        }

        private void RelinkTabularObjects()
        {
            foreach (var col in Columns)
            {
                if( ! Document.Connection.TryGetColumn(col.TableName, col.Caption, out var obj)) continue;
                col.TabularObject = obj;
            }

            foreach(var fil in Filters.Items)
            {
                if (!Document.Connection.TryGetColumn(fil.TabularObject.TableName, fil.TabularObject.Caption, out var obj)) continue;
                fil.TabularObject = obj;
            }

        }

        public Task HandleAsync(RunStyleChangedEvent message, CancellationToken cancellationToken)
        {
            //RunStyle = message.RunStyle;
            NotifyOfPropertyChange(nameof(RunStyle));
            return Task.CompletedTask;
        }

        internal void CopyContent(QueryBuilderViewModel queryBuilder)
        {
            var json = queryBuilder.GetJson();
            this.LoadJson(json);
        }
    }
}
