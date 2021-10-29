using System;
using System.ComponentModel.Composition;
using AvalonDock;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using Microsoft.Win32;
using Serilog;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Enums;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using DaxStudio.UI.Model;
using DaxStudio.UI.Extensions;
using System.Windows;
using System.Linq;
using DaxStudio.Interfaces;
using DaxStudio.Common;
using System.Threading;
using System.Threading.Tasks;

namespace DaxStudio.UI.ViewModels
{

    [Export(typeof(IConductor))]
    public class DocumentTabViewModel : Conductor<IScreen>.Collection.OneActive
        , IHandle<AutoSaveRecoveryEvent>
        , IHandle<NewDocumentEvent>
        , IHandle<OpenFileEvent>
        , IHandle<OpenDaxFileEvent>
        , IHandle<RecoverNextAutoSaveFileEvent>
        , IHandle<UpdateGlobalOptions>
        , IDocumentWorkspace
    {
        private readonly IWindowManager _windowManager;
        private readonly IEventAggregator _eventAggregator;
        private int _documentCount = 1;
        private DocumentViewModel _activeDocument;
        private Dictionary<int,AutoSaveIndex> _autoSaveRecoveryIndex;
        private readonly IGlobalOptions _options;
        private readonly object _activeDocumentLock = new object();
        private readonly IDaxStudioHost _host;
        private readonly RibbonViewModel Ribbon;
        private readonly Application _app;

        //private readonly Func<DocumentViewModel> _documentFactory;
        private readonly Func<IWindowManager, IEventAggregator, DocumentViewModel> _documentFactory;
        [ImportingConstructor]
        public DocumentTabViewModel(IWindowManager windowManager
            , IEventAggregator eventAggregator
            , Func<IWindowManager,IEventAggregator, DocumentViewModel> documentFactory
            , RibbonViewModel ribbonViewModel
            , IDaxStudioHost host
            , IGlobalOptions options
            , IAutoSaver autoSaver)
        {
            _documentFactory = documentFactory;
            _windowManager = windowManager;
            _eventAggregator = eventAggregator;
            _options = options;
            _eventAggregator.SubscribeOnPublishedThread(this);
            AutoSaver = autoSaver;
            Ribbon = ribbonViewModel;
            _host = host;
            _app = Application.Current;
        }

        protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            var recoveringFiles = false;

            // get master auto save indexes and only get crashed index files...
            var autoSaveInfo = AutoSaver.LoadAutoSaveMasterIndex();
            var filesToRecover = autoSaveInfo.Values.Where(idx => idx.IsCurrentVersion && idx.ShouldRecover).SelectMany(entry => entry.Files);

            // check for auto-saved files and offer to recover them
            if (filesToRecover.Any())
            {
                Log.Debug(Constants.LogMessageTemplate, nameof(ShellViewModel), "ctor", "Found auto-save files, beginning recovery");
                recoveringFiles = true;
                await RecoverAutoSavedFilesAsync(autoSaveInfo);
            }
            else
            {
                // if there are no auto-save files to recover, start the auto save timer
                Log.Debug(Constants.LogMessageTemplate, nameof(ShellViewModel), "ctor", "Starting auto-save timer");
                await _eventAggregator.PublishOnUIThreadAsync(new StartAutoSaveTimerEvent());
            }

            // if a filename was passed in on the command line open it
            if (!string.IsNullOrEmpty(_host.CommandLineFileName))
            {
                Log.Debug(Constants.LogMessageTemplate, nameof(ShellViewModel), "ctor", $"Opening file from command line: '{_host.CommandLineFileName}'");
                await NewQueryDocumentAsync(_host.CommandLineFileName);
            }

            // if no tabs are open at this point and we are not recovering auto-save file then, open a blank document
            if (Items.Count == 0 && !recoveringFiles)
            {
                Log.Debug(Constants.LogMessageTemplate, nameof(ShellViewModel), "ctor", "Opening a new blank query window");
                await _eventAggregator.PublishOnUIThreadAsync(new NewDocumentEvent(Ribbon.SelectedTarget));
            }
        }

        public void ActiveDocumentChanged()
        {

        }

        private async Task RecoverAutoSavedFilesAsync(Dictionary<int, AutoSaveIndex> autoSaveInfo)
        {
            Log.Information("{class} {method} {message}", nameof(DocumentTabViewModel), nameof(RecoverAutoSavedFilesAsync), $"Found {autoSaveInfo.Values.Count} auto save index files");
            // show recovery dialog
            await _eventAggregator.PublishOnUIThreadAsync(new AutoSaveRecoveryEvent(autoSaveInfo));

        }

        public DocumentViewModel ActiveDocument
        {
            get => _activeDocument;
            set
            {
                if (_activeDocument == value) 
                    return;  // this item is already active
                if (this.Items.Count == 0)
                {
                    _activeDocument = null;
                    return;  // no items in collection usually means we are shutting down
                }
                Log.Debug("{Class} {Event} {Document}", nameof(DocumentTabViewModel), "ActiveDocument:Set", value?.DisplayName);
                lock (_activeDocumentLock)
                {
                    _activeDocument = value;
                    //this.ActivateItemAsync(_activeDocument);//.FireAndForget();
                    NotifyOfPropertyChange(() => ActiveDocument);

                    if (ActiveDocument == null) return;
                    //Log.Debug("{class} {method} {message}", nameof(DocumentTabViewModel), nameof(ActiveDocumentChanged), $"ActiveDocument changed: {ActiveDocument?.DisplayName}");
                    Items.Apply(i => ((DocumentViewModel)i).IsFocused = false);

                    //ActiveDocument.IsFocused = true;
                    //this.ActivateAsync(value);
                    _eventAggregator.PublishOnUIThreadAsync(new SetFocusEvent());
                }
            }
        }

        protected override Task ChangeActiveItemAsync(IScreen newItem, bool closePrevious, CancellationToken cancellationToken)
        {
            ActiveDocument = newItem as DocumentViewModel;
            return base.ChangeActiveItemAsync(newItem, closePrevious, cancellationToken);
        }

        public AvalonDock.Themes.Theme AvalonDockTheme => new AvalonDock.Themes.GenericTheme();
        //if (_options.Theme == "Dark") return new Theme.MonotoneTheme();
        //else return new Theme.DaxStudioLightTheme();

        public IAutoSaver AutoSaver { get; }

        public async Task NewQueryDocumentAsync(string fileName)
        {
            await NewQueryDocumentAsync(fileName, null);
        }

        public async Task NewQueryDocumentAsync( string fileName, DocumentViewModel sourceDocument)
        {
            try
            {
                // 1 Open BlankDocument
                if (string.IsNullOrEmpty(fileName)) await OpenNewBlankDocumentAsync(sourceDocument);
                // 2 Open Document in current window (if it's an empty document)
                else if (ActiveDocument != null && !ActiveDocument.IsDiskFileName && !ActiveDocument.IsDirty) OpenFileInActiveDocument(fileName);
                // 3 Open Document in a new window if current window has content
                else await OpenFileInNewWindowAsync(fileName);
            }
            catch (Exception ex)
            {
                var msg = $"Error creating new document: {ex.Message}";
                Log.Error(ex, Constants.LogMessageTemplate, nameof(DocumentTabViewModel), nameof(NewQueryDocumentAsync), msg);
                await _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, msg));
            }
        }

        private void RecoverAutoSaveFile(AutoSaveIndexEntry file)
        {
            Log.Debug("{class} {method} {message}", "DocumentTabViewModel", "RecoverAutoSaveFile", $"Starting AutoSave Recovery for {file.DisplayName} ({file.AutoSaveId})");
            var newDoc = _documentFactory(_windowManager, _eventAggregator);
            using (new StatusBarMessage(newDoc, $"Recovering \"{file.DisplayName}\""))
            {
                try
                {
                    newDoc.DisplayName = file.DisplayName;
                    newDoc.IsDiskFileName = file.IsDiskFileName;
                    newDoc.FileName = file.OriginalFileName;
                    newDoc.AutoSaveId = file.AutoSaveId;
                    newDoc.State = DocumentState.RecoveryPending;

                    Items.Add(newDoc);
                    ActivateItemAsync(newDoc);
                    ActiveDocument = newDoc;
                    newDoc.IsDirty = true;

                    file.ShouldOpen = false;

                    _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Information, $"Recovering File: '{file.DisplayName}'"));

                    Log.Debug("{class} {method} {message}", "DocumentTabViewModel", "RecoverAutoSaveFile", $"Finished AutoSave Recovery for {file.DisplayName} ({file.AutoSaveId})");
                }
                catch (Exception ex)
                {
                    _eventAggregator.PublishOnUIThreadAsync( new  OutputMessage( MessageType.Error, $"Error recovering: '{file.OriginalFileName}({file.AutoSaveId})'\n{ex.Message}"));
                    Log.Error(ex, Constants.LogMessageTemplate, nameof(DocumentTabViewModel), nameof(RecoverAutoSaveFile), $"Error recovering: '{file.OriginalFileName}({file.AutoSaveId})'\n{ex.Message}");
                }
            }
        }

        private async Task OpenFileInNewWindowAsync(string fileName)
        {
            Log.Debug("{class} {method} {message}", "DocumentTabViewModel", "OpenFileInNewWindow", "Opening " + fileName);
            try
            {
                var newDoc = _documentFactory(_windowManager, _eventAggregator);
                Items.Add(newDoc);
                await ActivateItemAsync(newDoc);
                ActiveDocument = newDoc;
                ActiveItem = newDoc;

                newDoc.DisplayName = "Opening...";
                newDoc.FileName = fileName;
                newDoc.State = DocumentState.LoadPending;  // this triggers the DocumentViewModel to open the file
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{class} {method} {message}", "DocumentTabViewModel", "OpenFileInNewWindow", ex.Message);
                await _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, $"The following error occurred while attempting to open '{fileName}': {ex.Message}"));
            }
        }

        private void OpenFileInActiveDocument(string fileName)
        {
            Log.Debug("{class} {method} {message}", "DocumentTabViewModel", "OpenFileInActiveDocumentWindow", "Opening " + fileName);
            ActiveDocument.FileName = fileName;
            ActiveDocument.LoadFile(fileName);
        }

        private async Task OpenNewBlankDocumentAsync(DocumentViewModel sourceDocument)
        {
            Log.Debug(Constants.LogMessageTemplate,nameof(DocumentTabViewModel),nameof(OpenNewBlankDocumentAsync), "Requesting new document from document factory");
            var newDoc = _documentFactory(_windowManager, _eventAggregator);
            newDoc.DisplayName = $"Query{_documentCount}.dax";
            _documentCount++;
            //newDoc.Parent = this;
            //newDoc.ConductWith(this);

            Log.Debug(Constants.LogMessageTemplate, nameof(DocumentTabViewModel), nameof(OpenNewBlankDocumentAsync), "Adding new document to tabs collection");

            //           Items.Add(newDoc);
            await ActivateItemAsync(newDoc);
            ActiveDocument = newDoc;

            new System.Action(CleanActiveDocument).BeginOnUIThread();

            if (sourceDocument == null
                || sourceDocument.Connection.IsConnected == false)
            {
                await ConnectToServerAsync();
            }
            else
            {
                await _eventAggregator.PublishOnUIThreadAsync(new CopyConnectionEvent(sourceDocument));
            }

        }

        private async Task ConnectToServerAsync()
        {
            if (!string.IsNullOrEmpty(_app.Args().Server) && !_app.Properties.Contains("InitialQueryConnected"))
            {
                // we only want to run this code to default connection to the server name and database arguments
                // on the first window that is connected. After that the user can use the copy connection option
                // so if they start a new window chances are that they want to connect to another source
                // Setting this property on the app means this code should only run once
                _app.Properties.Add("InitialQueryConnected", true);

                var server = _app.Args().Server;
                var database = _app.Args().Database;
                var initialCatalog = string.Empty;
                if (!string.IsNullOrEmpty(_app.Args().Database)) initialCatalog = $";Initial Catalog={database}";
                Log.Information("{class} {method} {message}", nameof(DocumentTabViewModel), nameof(OpenNewBlankDocumentAsync), $"Connecting to Server: {server} Database:{database}");
                await _eventAggregator.PublishOnUIThreadAsync(new ConnectEvent($"Data Source={server}{initialCatalog}", 
                                                                        false, 
                                                                        string.Empty,
                                                                        string.Empty, 
                                                                        database,
                                                                        server.Trim().StartsWith("localhost:",StringComparison.OrdinalIgnoreCase) ? ADOTabular.Enums.ServerType.PowerBIDesktop: ADOTabular.Enums.ServerType.AnalysisServices,
                                                                        server.Trim().StartsWith("localhost:",StringComparison.OrdinalIgnoreCase)
                                                                        ));
                await _eventAggregator.PublishOnUIThreadAsync(new SetFocusEvent());
            }
            else
                await ChangeConnection();
        }

        private void CleanActiveDocument()
        {
            Log.Debug("{Class} {Event} {ActiveDocument} IsDirty: {IsDirty}", "DocumentTabViewModel", "CleanActiveDocument", ActiveDocument.DisplayName, ActiveDocument.IsDirty);
            ActiveDocument.IsDirty = false;
        }

        private async Task ChangeConnection()
        {
            await ActiveDocument.ChangeConnectionAsync();
        }

        public async Task HandleAsync(NewDocumentEvent message, CancellationToken cancellationToken)
        {
            await NewQueryDocumentAsync("",message.ActiveDocument);
        }

        public async Task HandleAsync(OpenFileEvent message, CancellationToken cancellationToken)
        {
            // Configure open file dialog box
            var dlg = new OpenFileDialog
            {
                FileName = "Document",
                DefaultExt = ".dax",
                Filter = "DAX documents|*.dax;*.msdax|DAX Studio documents|*.dax|SSMS DAX documents|*.msdax"
            };

            // Show open file dialog box
            var result = dlg.ShowDialog();

            // Process open file dialog box results 
            if (result == true)
            {
                // Open document 
                var fileName = dlg.FileName;
                await _eventAggregator.PublishOnUIThreadAsync(new FileOpenedEvent(fileName));
                await NewQueryDocumentAsync(fileName);
            }
            
        }

        public async Task HandleAsync(OpenDaxFileEvent message, CancellationToken cancellationToken)
        {
            await NewQueryDocumentAsync(message.FileName);
        }

        public async void TabClosing(object sender, DocumentClosingEventArgs args)
        {

            var doc = args.Document.Content as IScreen;
            if (doc == null) return;

            args.Cancel = true; // cancel the default tab close action as we want to call 

            await doc.TryCloseAsync();     // TryClose and give the document a chance to block the close

            if (this.Items.Count == 0)
            {
                Log.Debug("{class} {method} {message}", "DocumentTabViewModel", "TabClosing", "All documents closed");
                ActiveDocument = null;
                await _eventAggregator.PublishOnUIThreadAsync(new AllDocumentsClosedEvent());
            }
        }

        public async void ActivateAsync(object document)
        {
            if (document is DocumentViewModel doc)
            {
                await ActivateItemAsync (doc);
                ActiveDocument = doc;
            }
        }

        public Task HandleAsync(UpdateGlobalOptions message, CancellationToken cancellationToken)
        {
            NotifyOfPropertyChange(() => AvalonDockTheme);
            foreach (var itm in this.Items)
            {
                if (!(itm is DocumentViewModel doc)) continue;
                doc.UpdateSettings();
                doc.UpdateTheme();
            }
            return Task.CompletedTask;
        }

        public async Task HandleAsync(AutoSaveRecoveryEvent message, CancellationToken cancellationToken)
        {
            _autoSaveRecoveryIndex = message.AutoSaveMasterIndex;

            if (!message.RecoveryInProgress)
            {
                Log.Information(Constants.LogMessageTemplate, nameof(DocumentViewModel), "Handle<AutoSaveRecoveryEvent>", "Checking if any files need to be recovered from a previous crash");
                
                var filesToRecover = message.AutoSaveMasterIndex.Values.Where(i => i.ShouldRecover && i.IsCurrentVersion).SelectMany(entry => entry.Files).ToList();

                if (!filesToRecover.Any())
                {
                    Log.Information(Constants.LogMessageTemplate, nameof(DocumentViewModel), "Handle<AutoSaveRecoveryEvent>", "no files found that need to be recovered");

                    // if there are no files to recover then clean up 
                    // the recovery folder and exit here
                    AutoSaver.CleanUpRecoveredFiles();
                    //return Task.CompletedTask; 
                }

                Log.Information(Constants.LogMessageTemplate, nameof(DocumentViewModel), "Handle<AutoSaveRecoveryEvent>", $"found {filesToRecover.Count} file(s) that may need to be recovered, showing recovery dialog");

                // if auto save recovery is not already in progress 
                // prompt the user for which files should be recovered
                var autoSaveRecoveryDialog = new AutoSaveRecoveryDialogViewModel
                {
                    Files = new ObservableCollection<AutoSaveIndexEntry>(filesToRecover)
                };


                await _windowManager.ShowDialogBoxAsync(autoSaveRecoveryDialog, settings: new Dictionary<string, object>
                {
                    { "WindowStyle", WindowStyle.None},
                    { "ShowInTaskbar", false},
                    { "ResizeMode", ResizeMode.NoResize},
                    { "Background", System.Windows.Media.Brushes.Transparent},
                    { "AllowsTransparency",true}

                });

                if (autoSaveRecoveryDialog.Result != OpenDialogResult.Cancel)
                {
                    Log.Information(Constants.LogMessageTemplate, nameof(DocumentViewModel), "Handle<AutoSaveRecoveryEvent>", "Recovery Started - recovering selected files.");
                    message.RecoveryInProgress = true;

                    var fileToOpen = _autoSaveRecoveryIndex.Values.Where(i=>i.ShouldRecover).FirstOrDefault().Files.Where(x => x.ShouldOpen).FirstOrDefault();

                    if (fileToOpen != null)
                    {
                        // the first file will mark itself as opened then re-publish the message 
                        // to open the next file (if there is one)
                        RecoverAutoSaveFile(fileToOpen);
                    }
                }
                else
                {
                    Log.Information(Constants.LogMessageTemplate, nameof(DocumentViewModel), "Handle<AutoSaveRecoveryEvent>", "Recovery Cancelled - cleaning up unwanted recovery files.");
                    // if recovery has been cancelled open a new blank document
                    await NewQueryDocumentAsync("");
                    // and remove unwanted recovery files
                    AutoSaver.CleanUpRecoveredFiles();
                }

            }

            //return Task.CompletedTask;
            // TODO - maybe move this to a RecoverNextFile message and store the files to recover in a private var
            //        then at the end of the ViewLoaded event we can trigger this event and run code like the
            //        section below

            
        }

        public async Task HandleAsync(RecoverNextAutoSaveFileEvent message, CancellationToken cancellationToken)
        {
            
            var fileToOpen = _autoSaveRecoveryIndex.Values.Where(i=>i.ShouldRecover).FirstOrDefault().Files.Where(x => x.ShouldOpen).FirstOrDefault();

            if (fileToOpen != null)
            {
                // the first file will mark itself as opened then re-publish the message 
                // to open the next file (if there is one)
                RecoverAutoSaveFile(fileToOpen);
            }
            else
            {

                // if no files have been opened open a new blank document
                if (Items.Count == 0) await OpenNewBlankDocumentAsync(null);

                AutoSaver.CleanUpRecoveredFiles();

                // Now that any files have been recovered start the auto save timer
                await _eventAggregator.PublishOnUIThreadAsync(new StartAutoSaveTimerEvent());
                
            }
            
        }
    }
}
