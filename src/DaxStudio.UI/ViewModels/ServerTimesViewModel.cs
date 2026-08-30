using System;
using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using Caliburn.Micro;
using DaxStudio.Common;
using DaxStudio.Common.Enums;
using DaxStudio.Core;
using DaxStudio.Core.Events;
using DaxStudio.Core.Trace;
using DaxStudio.Interfaces;
using DaxStudio.Parsers;
using DaxStudio.UI.Events;
using DaxStudio.UI.Extensions;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Model;
using DaxStudio.UI.Utils;
using DaxStudio.UI.Views;
using Serilog;

namespace DaxStudio.UI.ViewModels
{

    public class ServerTimesViewModel
        : ServerTimesModel
            , ISaveState
            , IServerTimes
            , ITraceDiagnostics
            , IViewAware
            , IZoomable
            , IHandle<ThemeChangedEvent>
            , IHandle<CopySEQueryEvent>
            , IHandle<CopyPasteServerTimingsEvent>
            , IHaveData
    {
        [ImportingConstructor]
        public ServerTimesViewModel(IEventAggregator eventAggregator, ServerTimingDetailsViewModel serverTimingDetails
            , IGlobalOptions options, IWindowManager windowManager) : base(eventAggregator, serverTimingDetails, options, windowManager)
        {
            this.ViewAttached += ServerTimesViewModel_ViewAttached;
        }

        #region IZoomable
        public event EventHandler OnScaleChanged;
        private double _scale = 1;
        public double Scale
        {
            get => _scale;
            set
            {
                _scale = value;
                NotifyOfPropertyChange();
                OnScaleChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        #endregion

        private void ServerTimesViewModel_ViewAttached(object sender, ViewAttachedEventArgs e)
        {
            var view = e.View as ServerTimesView;
            if (view == null) return;

            DataObject.AddCopyingHandler(view.EventDetails, OnCopyEventDetails);
        }

        private void OnCopyEventDetails(object sender, DataObjectCopyingEventArgs e)
        {
            ClipboardManager.ReplaceLineBreaks(e.DataObject);
        }

        #region ISaveState methods
        void ISaveState.Save(string filename)
        {
            string json = GetJson();
            File.WriteAllText(filename + ".serverTimings", json);
        }

        public override void SavePackage(Package package)
        {
            base.SavePackage(package);
        }

        public void LoadPackage(Package package)
        {
            var uri = PackUriHelper.CreatePartUri(new Uri(DaxxFormat.ServerTimings, UriKind.Relative));
            if (!package.PartExists(uri)) return;
            _eventAggregator.PublishAsync(new ShowTraceWindowEvent(this));
            var part = package.GetPart(uri);
            using (TextReader tr = new StreamReader(part.GetStream()))
            {
                string data = tr.ReadToEnd();
                LoadJson(data);
            }
        }

        void ISaveState.Load(string filename)
        {
            filename = filename + ".serverTimings";
            if (!File.Exists(filename)) return;

            _eventAggregator.PublishAsync(new ShowTraceWindowEvent(this));
            string data = File.ReadAllText(filename);

            LoadJson(data);
        }
        #endregion

        #region Properties to handle layout changes (WPF grid bindings)

        public int TextGridRow { get { return ServerTimingDetails?.LayoutBottom ?? false ? 4 : 2; } }
        public int TextGridRowSpan { get { return ServerTimingDetails?.LayoutBottom ?? false ? 1 : 3; } }
        public int TextGridColumnSpan { get { return ServerTimingDetails?.LayoutBottom ?? false ? 3 : 1; } }
        public int TextGridColumn { get { return ServerTimingDetails?.LayoutBottom ?? false ? 2 : 4; } }

        public GridLength TextColumnWidth { get { return ServerTimingDetails?.LayoutBottom ?? false ? new GridLength(0, GridUnitType.Pixel) : new GridLength(1, GridUnitType.Star); } }

        protected override void ServerTimingDetails_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "LayoutBottom":
                case "LayoutRight":
                    NotifyOfPropertyChange(() => TextGridColumn);
                    NotifyOfPropertyChange(() => TextGridRow);
                    NotifyOfPropertyChange(() => TextGridRowSpan);
                    NotifyOfPropertyChange(() => TextGridColumnSpan);
                    NotifyOfPropertyChange(() => TextColumnWidth);
                    break;
            }
            base.ServerTimingDetails_PropertyChanged(sender, e);
        }

        #endregion

        protected override void OnClearAllStorageEvents()
        {
            StorageEventHeatmap = null;
        }

        public string CopyResultsForComments()
        {
            return CopyResultsData(includeHeader: true, formatTextForComment: true);
        }
        public string CopyResultsForCommentsData()
        {
            return CopyResultsData(includeHeader: false, formatTextForComment: true);
        }
        public override void CopyResults()
        {
            CopyResultsData(true);
        }

        public void CopyResultsData()
        {
            CopyResultsData(false);
        }
        public string CopyResultsData(bool includeHeader, bool formatTextForComment = false)
        {
            var dataObject = new DataObject();
            var headers = string.Empty;
            if (includeHeader) headers = "Query End\tTotal\tFE\tSE\tSE CPU\tSE Par.\tSE Queries\tSE Cache\n";
            var values = $"{QueryEndDateTime.ToString(Constants.IsoDateFormatPaste)}\t{TotalDuration}\t{FormulaEngineDuration}\t{StorageEngineDuration}\t{StorageEngineCpu}\t{StorageEngineCpuFactor}\t{StorageEngineQueryCount}\t{VertipaqCacheMatches}";
            string result = $"{headers}{values}";
            dataObject.SetData(DataFormats.StringFormat, result);
            dataObject.SetData(DataFormats.CommaSeparatedValue, $"{headers.Replace("\t", CultureInfo.CurrentCulture.TextInfo.ListSeparator)}\n{values.Replace("\t", CultureInfo.CurrentCulture.TextInfo.ListSeparator)}");
            if (formatTextForComment)
            {
                var textHeader = includeHeader ? PasteServerTimingsEvent.SERVERTIMINGS_HEADER : string.Empty;
                var textValues = $"-- {TotalDuration,9:#,0}  {FormulaEngineDuration,9:#,0}  {StorageEngineDuration,9:#,0}  {StorageEngineCpu,10:#,0}  x{StorageEngineCpuFactor,4:0.0}";
                result = $"{textHeader}{(string.IsNullOrEmpty(textHeader) ? string.Empty : "\r\n")}{textValues}\r\n";
                dataObject.SetData(DataFormats.Text, result);
            }
            Clipboard.SetDataObject(dataObject);
            return result;
        }

        public void ExportDetails()
        {
            if (Options.ExportServerTimingDetailsToFolder)
            {

                var dialog = new System.Windows.Forms.FolderBrowserDialog();
                dialog.Description = "A file per storage event will be created in the selected folder.";

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) ExportxmSqlFiles(dialog.SelectedPath);
            }
            else
            {
                Export();
            }
        }

        public void CopySEQuery()
        {
            if (SelectedEvent == null)
            {
                Log.Debug("SelectedEvent is null on CopySEQuery");
                return;
            }
            try
            {
                var queryText = SelectedEvent.QueryRichText ?? SelectedEvent.TextData;
                if (string.IsNullOrEmpty(queryText))
                {
                    Clipboard.SetText(SelectedEvent.TextData ?? string.Empty);
                    return;
                }

                var lines = queryText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                var sb = new System.Text.StringBuilder();
                foreach (var line in lines)
                {
                    if (sb.Length == 0 && line.StartsWith("SET DC_KIND", StringComparison.InvariantCulture))
                        continue;
                    if (line.StartsWith("Estimated", StringComparison.InvariantCulture))
                        continue;

                    if (sb.Length > 0) sb.AppendLine();
                    sb.Append(line);
                }

                var result = sb.ToString().TrimEnd('\r', '\n');
                Clipboard.SetText(result);
            }
            catch (Exception ex)
            {
                Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(ServerTimesViewModel), nameof(CopySEQuery), "Error copying SE Query text");
                _eventAggregator.PublishAsync(new OutputMessage(MessageType.Error, $"Error copying SE query text\n{ex.Message}"));
            }
        }

        public async void ShowTraceDiagnostics()
        {
            var traceDiagnosticsViewModel = new RequestInformationViewModel(this);
            await WindowManager.ShowDialogBoxAsync(traceDiagnosticsViewModel, settings: new System.Collections.Generic.Dictionary<string, object>
            {
                { "WindowStyle", WindowStyle.None},
                { "ShowInTaskbar", false},
                { "ResizeMode", ResizeMode.NoResize},
                { "Background", Brushes.Transparent},
                { "AllowsTransparency",true}

            });
        }

        /// <summary>
        /// Shows the Storage Engine Dependencies (ERD) diagram for all SE events.
        /// Opens as a dockable tool window that can be resized, floated, or maximized.
        /// </summary>
        public void ShowQueryDependencies()
        {
            try
            {
                Log.Information("{class} {method} {message}", nameof(ServerTimesViewModel), nameof(ShowQueryDependencies), $"Starting with {AllStorageEngineEvents.Count} events");

                var erdViewModel = new XmSqlErdViewModel(_eventAggregator, ServerTimingDetails);
                erdViewModel.SetNameRemaps(RemapColumnNames, RemapTableNames);

                _eventAggregator.PublishAsync(new ShowToolWindowEvent(erdViewModel));

                erdViewModel.AnalyzeEvents(AllStorageEngineEvents);
            }
            catch (Exception ex)
            {
                Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(ServerTimesViewModel), nameof(ShowQueryDependencies), "Error showing Query Dependencies");
                _eventAggregator.PublishAsync(new OutputMessage(MessageType.Error, $"Error showing Storage Engine Dependencies\n{ex.Message}"));
            }
        }

        /// <summary>
        /// Shows the tables referenced by SE queries in the Model Diagram.
        /// </summary>
        public void ShowInModelDiagram()
        {
            try
            {
                Log.Information("{class} {method} Extracting table names from {count} SE events",
                    nameof(ServerTimesViewModel), nameof(ShowInModelDiagram), AllStorageEngineEvents.Count);

                IXmSqlParser parser = Options.UseAntlrParser
                    ? (IXmSqlParser)new AntlrXmSqlParser()
                    : new XmSqlParser();
                var analysis = new XmSqlAnalysis(RemapColumnNames, RemapTableNames);

                foreach (var evt in AllStorageEngineEvents)
                {
                    if (evt.IsInternalEvent) continue;

                    var metrics = new XmSqlParser.SeEventMetrics
                    {
                        EstimatedRows = evt.EstimatedRows,
                        DurationMs = evt.Duration,
                        IsCacheHit = evt.Class == DaxStudioTraceEventClass.VertiPaqSEQueryCacheMatch,
                        CpuTimeMs = evt.CpuTime
                    };

                    if (evt.IsDirectQueryEvent)
                    {
                        var sqlText = !string.IsNullOrWhiteSpace(evt.TextData) ? evt.TextData : evt.Query;
                        if (!string.IsNullOrWhiteSpace(sqlText))
                            parser.ParseDirectQuerySql(sqlText, analysis, metrics);
                    }
                    else if (evt.IsScanEvent && !string.IsNullOrWhiteSpace(evt.Query))
                    {
                        parser.ParseQueryWithMetrics(evt.Query, analysis, metrics);
                    }
                }

                var tableNames = analysis.Tables.Keys.ToList();

                if (tableNames.Count == 0)
                {
                    _eventAggregator.PublishAsync(new OutputMessage(MessageType.Warning,
                        "No table references found in Storage Engine queries."));
                    return;
                }

                Log.Information("{class} {method} Found {count} tables: {tables}",
                    nameof(ServerTimesViewModel), nameof(ShowInModelDiagram),
                    tableNames.Count, string.Join(", ", tableNames));

                _eventAggregator.PublishAsync(
                    new ShowTablesInModelDiagramEvent(tableNames, includeRelated: false));
            }
            catch (Exception ex)
            {
                Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(ServerTimesViewModel),
                    nameof(ShowInModelDiagram), "Error showing tables in Model Diagram");
                _eventAggregator.PublishAsync(new OutputMessage(MessageType.Error,
                    $"Error showing tables in Model Diagram\n{ex.Message}"));
            }
        }

        /// <summary>
        /// Re-applies formatting to all storage engine events using current Options.
        /// </summary>
        public async void ReformatQueries()
        {
            if (IsBusy) return;

            IsBusy = true;
            BusyMessage = "Reformatting queries...";

            await Task.Delay(50);

            try
            {
                var allEvents = AllStorageEngineEvents;
                var totalEvents = allEvents.Count;
                var events = allEvents.ToList();
                var remapColumns = RemapColumnNames;
                var remapTables = RemapTableNames;
                var dateColIds = DateColumnIds;

                await Task.Run(() =>
                {
                    int processedCount = 0;
                    foreach (var evt in events)
                    {
                        evt.FormatQuery(remapColumns, remapTables, dateColIds);

                        processedCount++;
                        if (processedCount % 50 == 0)
                        {
                            var count = processedCount;
                            Application.Current.Dispatcher.InvokeAsync(() =>
                                BusyMessage = $"Reformatting queries ({count}/{totalEvents})...");
                        }
                    }
                });

                NotifyOfPropertyChange(nameof(StorageEngineEvents));
            }
            finally
            {
                IsBusy = false;
            }
        }

        public Task HandleAsync(CopySEQueryEvent message, CancellationToken cancellationToken)
        {
            CopySEQuery();
            return Task.CompletedTask;
        }
        public Task HandleAsync(CopyPasteServerTimingsEvent message, CancellationToken cancellationToken)
        {
            string textResult;
            if (message.IncludeHeader)
            {
                textResult = CopyResultsForComments();
            }
            else
            {
                textResult = CopyResultsForCommentsData();
            }
            _eventAggregator.PublishAsync(new PasteServerTimingsEvent(message.IncludeHeader, textResult), cancellationToken);
            return Task.CompletedTask;
        }


        public Task HandleAsync(ThemeChangedEvent message, CancellationToken cancellationToken)
        {
            StorageEventHeatmap = null;
            NotifyOfPropertyChange(nameof(StorageEventHeatmap));

            SyntaxHighlightingHelper.SetAllColorThemes(Options.AutoTheme.ToString());

            return Task.CompletedTask;
        }

        private ImageSource _storageEventHeatmap;
        public ImageSource StorageEventHeatmap
        {
            get
            {
                if (_storageEventHeatmap != null) return _storageEventHeatmap;
                if (this.StorageEngineEvents.Count == 0) return new DrawingImage();

                var element = (FrameworkElement)this.GetView();

                Brush scanBrush = (Brush)element.FindResource("Theme.Brush.Accent");
                Brush feBrush = (Brush)element.FindResource("Theme.Brush.Accent2");
                Brush batchBrush = (Brush)element.FindResource("Theme.Brush.Accent1");
                Brush internalBrush = (Brush)element.FindResource("Theme.Brush.Accent3");

                _storageEventHeatmap = TimelineHeatmapImageGenerator.GenerateBitmap(this.StorageEngineEvents.ToList(), 5000, 10, feBrush, scanBrush, batchBrush, internalBrush);
#if DEBUG
                using (StreamWriter writer = File.CreateText("c:\\temp\\heatmap.xaml"))
                {
                    XamlWriter.Save(_storageEventHeatmap, writer);
                }
#endif
                return _storageEventHeatmap;
            }
            set
            {
                _storageEventHeatmap = value;
                NotifyOfPropertyChange();
            }
        }
    }
}
