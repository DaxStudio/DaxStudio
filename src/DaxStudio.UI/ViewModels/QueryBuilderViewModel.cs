﻿using ADOTabular.Interfaces;
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

namespace DaxStudio.UI.ViewModels
{
    

    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export]
    [DataContract]
    public sealed class QueryBuilderViewModel : ToolWindowBase
        ,IQueryTextProvider
        ,IHandle<SendColumnToQueryBuilderEvent>
        ,IHandle<DuplicateMeasureEvent>
        ,IDisposable
        ,ISaveState
        ,INotifyPropertyChanged
        ,IHandle<QueryBuilderUpdateEvent>
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
        }

        private bool _autoGenerate;
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

        private void ShowAutoGenerateWarning()
        {
            MessageBox.Show("Enabling this option will overwrite any changes you may have made to the query text.\n\nHowever, if you make any further manual edits to the query this option will be automatically disabled.", "Auto Generate Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        private void AutoGenerateQuery()
        {
            if (AutoGenerate && Columns.Count > 0) SendTextToEditor();
            if (AutoGenerate && Columns.Count == 0) SendTextToEditor(true);
        }

        private void OnFiltersPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            AutoGenerateQuery();
        }

        private void OnVisibilityChanged(object sender, EventArgs e)
        {
            if (IsVisible) EventAggregator.Subscribe(this);
            else EventAggregator.Unsubscribe(this);
        }

        private void OnColumnsPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            NotifyOfPropertyChange(nameof(CanRunQuery));
            NotifyOfPropertyChange(nameof(CanSendTextToEditor));
            NotifyOfPropertyChange(nameof(CanOrderBy));
            NotifyOfPropertyChange(nameof(OrderBy));
            AutoGenerateQuery();
        }


        // ReSharper disable once UnusedMember.Global
        public override string Title => "Query Builder";
        public override string DefaultDockingPane => "DockMidLeft";
        public new bool CanHide => true;
        public override string ContentId => "query-builder";
        public override ImageSource IconSource
        {
            get
            {
                var imgSourceConverter = new ImageSourceConverter();
                // TODO - can I convert FontAwesome to an ImageSource ??
                return imgSourceConverter.ConvertFromInvariantString(
                    @"pack://application:,,,/DaxStudio.UI;component/images/icon-undo.png") as ImageSource;

            }
        }
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
                if (_isEnabled) EventAggregator.Subscribe(this);
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

        public string QueryText { 
            get { 
                try {
                    var modelCaps = GetModelCapabilities();
                    return QueryBuilder.BuildQuery(modelCaps,Columns.Items, Filters.Items); 
                }
                catch (Exception ex)
                {
                    Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(QueryBuilderViewModel), nameof(QueryText), ex.Message);
                    EventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, $"Error generating query: {ex.Message}"));
                }
                return string.Empty;
            } 
        
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
                // todo- we may eventually want to read parameters from filter values, but at the moment we just return an empty collection
                return new List<Microsoft.AnalysisServices.AdomdClient.AdomdParameter>();
            }
        }

        public Dictionary<string, QueryParameter> QueryParameters
        {
            get;
        } = new Dictionary<string, QueryParameter>();

        // ReSharper disable once UnusedMember.Global
        public void RunQuery() {
            if (! CheckForCrossjoins() )
                EventAggregator.PublishOnUIThread(new RunQueryEvent(Document.SelectedTarget, Document.SelectedRunStyle) { QueryProvider = this });
        }

        private bool CheckForCrossjoins()
        {
            bool hasMeasures = this.Columns.Items.Any(c => c.IsMeasure());
            if (hasMeasures) return false;  // we have a measure so that should prevent a large crossjoin
            
            var cols = this.Columns.GroupBy(c => c.TableName);
            if (cols.Count() == 1) return false;  // if all the columns are from one table it will not produce a crossjoin

            return MessageBox.Show("Including columns from multiple tables without a measure is likely to result in a large crossjoin which could use a lot of memory.\n\nAre you sure you want to proceed?", "Potential Crossjoin Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No;
        }

        // ReSharper disable once UnusedMember.Global

        public void SendTextToEditor()
        {
            SendTextToEditor(false);
        }
        public void SendTextToEditor(bool clearText)
        {
            EventAggregator.PublishOnUIThread(new SendTextToEditor(clearText?string.Empty:QueryText,false,true));
        }

        public bool CanRunQuery => Columns.Items.Count > 0;

        public bool CanSendTextToEditor => Columns.Items.Count > 0;

        public bool CanAddNewMeasure
        {
            get {
                try
                {
                    return Document?.Connection?.SelectedModel?.Tables.Count > 0;
                }
                catch (Exception ex)
                {
                    Log.Error(ex,Common.Constants.LogMessageTemplate, nameof(QueryBuilderViewModel), nameof(CanAddNewMeasure), "Error checking if the model has any tables");
                    EventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, $"The following error occurred while checking if your model has any tables:\n{ex.Message}"));                    
                }

                return false;
            }
        }

        // ReSharper disable once UnusedMember.Global
        public void AddNewMeasure()
        {
            if (Document.Connection.SelectedModel.Tables.Count == 0)
            {
                var msg = "Cannot add a new measure if the model has no tables";
                Log.Warning(nameof(QueryBuilderViewModel), nameof(AddNewMeasure), msg);
                EventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Warning,msg ));
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
            Columns.EditMeasure(newMeasure);
            IsEnabled = false;
            //EventAggregator.PublishOnUIThread(new ShowMeasureExpressionEditor(newMeasure));
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

        public void Handle(SendColumnToQueryBuilderEvent message)
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
            
        }

        private void AddColumnToColumns(ITreeviewColumn column)
        {
            if (column == null) return;
            if (column.InternalColumn == null) return;

            if (Columns.Contains(column.InternalColumn))
            {
                // write warning and return
                EventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Warning, $"Cannot add the {column.InternalColumn.Caption} column to the query builder columns a second time"));
                return;
            }
            Columns.Add(column.InternalColumn);
        }

        private void AddColumnToFilters(ITreeviewColumn column)
        {
            if (column == null) return;
            if (column.InternalColumn == null) return;

            if (Filters.Contains(column.InternalColumn))
            {
                // write warning and return
                EventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Warning, $"Cannot add the {column.InternalColumn.Caption} column to the query builder filters a second time"));
                return;
            }
            Filters.Add(column.InternalColumn);
        }

        public void Save(string filename)
        {
            var json = GetJson();
            File.WriteAllText(filename + ".queryBuilder", json);
        }

        internal string GetJson()
        {
            var settings = new JsonSerializerSettings()
            {
                ContractResolver = new InterfaceContractResolver(typeof(IADOTabularColumn))
            };
            string json = JsonConvert.SerializeObject(this, settings);
            return json;
        }

        internal QueryBuilderViewModel LoadJson(string jsontext)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.Converters.Add(new QueryBuilderConverter(this.EventAggregator, this.Document, this.Options));
            //settings.Converters.Add(new ADOTabularColumnCreationConverter());
            var result = JsonConvert.DeserializeObject<QueryBuilderViewModel>(jsontext, settings);
            return result;
        }

        public void Load(string filename)
        {
            filename = filename + ".queryBuilder";
            if (!File.Exists(filename)) return;
            
            string data = File.ReadAllText(filename);
            var model = LoadJson(data);
            LoadViewModel(model);
        }

        private void LoadViewModel(QueryBuilderViewModel model)
        {
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

            this.IsVisible = true;
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
                var model = LoadJson(data);
                LoadViewModel(model);
            }
            
        }

        public void Handle(DuplicateMeasureEvent message)
        {
            Log.Information(Common.Constants.LogMessageTemplate,nameof(QueryBuilderViewModel), "Handle<DuplicateMeasureEvent>", $"Duplicating Measure: {message.Measure.Caption}");
            var meas = new QueryBuilderColumn($"{message.Measure.Caption} - Copy", (ADOTabularTable)message.Measure.SelectedTable, EventAggregator)
                { MeasureExpression = message.Measure.MeasureExpression };
            Columns.Add(meas);
        }

        public void Handle(QueryBuilderUpdateEvent message)
        {
            AutoGenerateQuery();
        }

        public void Clear()
        {
            Columns.Items.Clear();
            Filters.Items.Clear();
            AutoGenerateQuery();
        }
    }
}
