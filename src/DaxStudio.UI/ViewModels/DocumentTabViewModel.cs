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
using AvalonDock.Controls;
using System.Text.RegularExpressions;

namespace DaxStudio.UI.ViewModels
{

    [Export(typeof(IConductor))]
    public class DocumentTabViewModel : Conductor<IScreen>.Collection.OneActive
        , IHandle<AutoSaveRecoveryEvent>
        , IHandle<NewDocumentEvent>
        , IHandle<OpenFileEvent>
        , IHandle<OpenDaxFileEvent>
        , IHandle<PasteDaxFileEvent>
        , IHandle<RecoverNextAutoSaveFileEvent>
        , IHandle<UpdateGlobalOptions>
        , IDocumentWorkspace
    {
        private readonly IWindowManager _windowManager;
        private readonly IEventAggregator _eventAggregator;
        private int _documentCount = 1;
        private Dictionary<int,AutoSaveIndex> _autoSaveRecoveryIndex;
        private readonly IGlobalOptions _options;
        private readonly object _activeDocumentLock = new object();
        private readonly IDaxStudioHost _host;
        private readonly RibbonViewModel Ribbon;
        private readonly Application _app;
        private static Regex generatedNameRegEx = new Regex(@"(?<=Query)(\d+)(?=\.)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
            try
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
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(DocumentTabViewModel), nameof(OnInitializeAsync), "Error Initializing DocumentTabs");

            }
        }

        private async Task RecoverAutoSavedFilesAsync(Dictionary<int, AutoSaveIndex> autoSaveInfo)
        {
            Log.Information("{class} {method} {message}", nameof(DocumentTabViewModel), nameof(RecoverAutoSavedFilesAsync), $"Found {autoSaveInfo.Values.Count} auto save index files");
            // show recovery dialog
            await _eventAggregator.PublishOnUIThreadAsync(new AutoSaveRecoveryEvent(autoSaveInfo));

        }

        public DocumentViewModel ActiveDocument => this.ActiveItem as DocumentViewModel;

        //public DocumentViewModel ActiveDocument
        //{
        //    get => _activeDocument;
        //    set
        //    {
        //        try
        //        {
        //            if (_activeDocument == value)
        //                return;  // this item is already active
        //            if (this.Items.Count == 0)
        //            {
        //                _activeDocument = null;
        //                return;  // no items in collection usually means we are shutting down
        //            }
        //            Log.Debug("{Class} {Event} {Document}", nameof(DocumentTabViewModel), "ActiveDocument:Set", value?.DisplayName);
        //            lock (_activeDocumentLock)
        //            {
        //                _activeDocument = value;

        //                NotifyOfPropertyChange(() => ActiveDocument);

        //                if (ActiveDocument == null) return;

        //                var docs = GetChildren();
        //                docs.Apply(i => ((DocumentViewModel)i).IsFocused = false);

        //                _eventAggregator.PublishOnUIThreadAsync(new SetFocusEvent());
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            Log.Error(ex, Constants.LogMessageTemplate, nameof(DocumentTabViewModel), "ActiveDocument.Set", "error setting ActiveDocument");
        //        }
        //    }
        //}

        protected override Task ChangeActiveItemAsync(IScreen newItem, bool closePrevious, CancellationToken cancellationToken)
        {
            try
            {
                Log.Verbose(Constants.LogMessageTemplate, nameof(DocumentTabViewModel), nameof(ChangeActiveItemAsync), "Starting setting ActiveDocument");
                //ActiveDocument = newItem as DocumentViewModel;
                var docs = GetChildren();
                docs.Apply(i => ((DocumentViewModel)i).IsFocused = false);
                _eventAggregator.PublishOnUIThreadAsync(new SetFocusEvent());

                Log.Verbose(Constants.LogMessageTemplate, nameof(DocumentTabViewModel), nameof(ChangeActiveItemAsync), "Finished setting ActiveDocument");
                return base.ChangeActiveItemAsync(newItem, closePrevious, cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(DocumentTabViewModel), nameof(ChangeActiveItemAsync), "Error Changing Active Item");
                _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, $"Error Setting Active Document\n{ex.Message}"));
                return Task.CompletedTask;
            }
            
        }

        public AvalonDock.Themes.Theme AvalonDockTheme => new AvalonDock.Themes.GenericTheme();
        //if (_options.Theme == "Dark") return new Theme.MonotoneTheme();
        //else return new Theme.DaxStudioLightTheme();

        public IAutoSaver AutoSaver { get; }

        public async Task PasteQueryDocumentAsync(string fileName )
        {
            await PasteQueryDocumentAsync(fileName, null);
        }

        public async Task PasteQueryDocumentAsync(string fileName, DocumentViewModel sourceDocument)
        {
            try
            {
                // 1 Open BlankDocument
                if (string.IsNullOrEmpty(fileName)) await OpenNewBlankDocumentAsync(sourceDocument);
                // 2 Open Document in current window even if it's not empty!
                else if (ActiveDocument != null) OpenFileInActiveDocument(fileName);
                // 3 Open Document in a new window if there is no current window
                else await OpenFileInNewWindowAsync(fileName);
            }
            catch (Exception ex)
            {
                var msg = $"Error pasting DAX file in current document: {ex.Message}";
                Log.Error(ex, Constants.LogMessageTemplate, nameof(DocumentTabViewModel), nameof(PasteQueryDocumentAsync), msg);
                await _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, msg));
            }
        }

        public async Task NewQueryDocumentAsync(string fileName)
        {
            await NewQueryDocumentAsync(fileName, null);
        }

        public async Task NewQueryDocumentAsync( string fileName, DocumentViewModel sourceDocument, bool copyContent = false)
        {
            try
            {
                // 1 Open BlankDocument
                if (string.IsNullOrEmpty(fileName)) await OpenNewBlankDocumentAsync(sourceDocument, copyContent);
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

        private async Task RecoverAutoSaveFileAsync(AutoSaveIndexEntry file)
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
                    await DeactivateItemAsync(ActiveItem,false);
                    await ActivateItemAsync(newDoc);
                    //ActiveDocument = newDoc;
                    newDoc.IsDirty = true;

                    file.ShouldOpen = false;

                    await _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Information, $"Recovering File: '{file.DisplayName}'"));

                    Log.Debug("{class} {method} {message}", "DocumentTabViewModel", "RecoverAutoSaveFile", $"Finished AutoSave Recovery for {file.DisplayName} ({file.AutoSaveId})");
                }
                catch (Exception ex)
                {
                    await _eventAggregator.PublishOnUIThreadAsync( new  OutputMessage( MessageType.Error, $"Error recovering: '{file.OriginalFileName}({file.AutoSaveId})'\n{ex.Message}"));
                    Log.Error(ex, Constants.LogMessageTemplate, nameof(DocumentTabViewModel), nameof(RecoverAutoSaveFileAsync), $"Error recovering: '{file.OriginalFileName}({file.AutoSaveId})'\n{ex.Message}");
                }
            }
        }

        private async Task OpenFileInNewWindowAsync(string fileName)
        {
            Log.Debug("{class} {method} {message}", "DocumentTabViewModel", "OpenFileInNewWindow", "Opening " + fileName);
            try
            {
                var newDoc = _documentFactory(_windowManager, _eventAggregator);
                newDoc.DisplayName = "Opening...";
                newDoc.FileName = fileName;
                newDoc.State = DocumentState.LoadPending;  // this triggers the DocumentViewModel to open the file

                await ActivateItemAsync(newDoc);
                //ActiveDocument = newDoc;

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

        private async Task OpenNewBlankDocumentAsync(DocumentViewModel sourceDocument, bool copyContent = false)
        {
            try
            {
                Log.Debug(Constants.LogMessageTemplate, nameof(DocumentTabViewModel), nameof(OpenNewBlankDocumentAsync), "Requesting new document from document factory");
                var newDoc = _documentFactory(_windowManager, _eventAggregator);
                _documentCount = GetMaxDocNumber();
                _documentCount++;
                newDoc.DisplayName = $"Query{_documentCount}.dax";
                
                //newDoc.Parent = this;
                //newDoc.ConductWith(this);

                Log.Debug(Constants.LogMessageTemplate, nameof(DocumentTabViewModel), nameof(OpenNewBlankDocumentAsync), "Adding new document to tabs collection");

                await ActivateItemAsync(newDoc);
                //ActiveDocument = newDoc;

                new System.Action(CleanActiveDocument).BeginOnUIThread();

                

                if (sourceDocument == null
                    || sourceDocument.Connection.IsConnected == false)
                {
                    await ConnectToServerAsync();
                }
                else
                {
                    await ActiveDocument.CopyConnectionAsync(sourceDocument);
                    if (copyContent) ActiveDocument.CopyContent(sourceDocument);
                }

                
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(DocumentTabViewModel), nameof(OpenNewBlankDocumentAsync), "Error opening new document");
                await _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, $"Error opening new document\n{ex.Message}"));
            }

        }

        private int GetMaxDocNumber()
        {
            int maxDocNumber = 0;
            foreach (DocumentViewModel doc in Items)
            {
                if (doc.IsDiskFileName) continue; // if this is previously saved file then skip it
                var m = generatedNameRegEx.Matches(doc.DisplayName);
                if (m.Count == 0) continue; // if we did not find any digits after the word query then skip this file
                var docNum = int.Parse(m[0].Value);
                if (docNum > maxDocNumber) maxDocNumber = docNum;
            }
            return maxDocNumber;
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
                                                                        database,
                                                                        server.Trim().StartsWith("localhost:",StringComparison.OrdinalIgnoreCase) ? ADOTabular.Enums.ServerType.PowerBIDesktop: ADOTabular.Enums.ServerType.AnalysisServices,
                                                                        server.Trim().StartsWith("localhost:",StringComparison.OrdinalIgnoreCase),
                                                                        _app.Args().Database??string.Empty));
                await _eventAggregator.PublishOnUIThreadAsync(new SetFocusEvent());
              
            }
            else
                await ChangeConnectionAsync();
        }

        private void CleanActiveDocument()
        {
            Log.Debug("{Class} {Event} {ActiveDocument} IsDirty: {IsDirty}", "DocumentTabViewModel", "CleanActiveDocument", ActiveDocument.DisplayName, ActiveDocument.IsDirty);
            ActiveDocument.IsDirty = false;
        }

        private async Task ChangeConnectionAsync()
        {
            await ActiveDocument.ChangeConnectionAsync();
        }

        public async Task HandleAsync(NewDocumentEvent message, CancellationToken cancellationToken)
        {
            if (ActiveDocument?.IsConnectionDialogOpen??false)
            {
                await _eventAggregator.PublishOnUIThreadAsync(new RefreshConnectionDialogEvent());
                return;
            }
            await NewQueryDocumentAsync("", message.ActiveDocument, message.CopyContent);
        }

        public async Task HandleAsync(OpenFileEvent message, CancellationToken cancellationToken)
        {
            // Configure open file dialog box
            var dlg = new OpenFileDialog
            {
                FileName = "Document",
                DefaultExt = ".dax",
                Filter = "DAX documents|*.dax;*.msdax;*.daxx;*.vpax|DAX Studio documents|*.dax;*.daxx;*.vpax|SSMS DAX documents|*.msdax"
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

        public async Task HandleAsync(PasteDaxFileEvent message, CancellationToken cancellationToken)
        {
            await PasteQueryDocumentAsync(message.FileName);
        }

        public async Task TabClosing(object sender, DocumentClosingEventArgs args)
        {
            Log.Verbose(Constants.LogMessageTemplate, nameof(DocumentTabViewModel), nameof(TabClosing), "Starting");
            try
            {
                var doc = args.Document.Content as IScreen;
                if (doc == null) return;

                args.Cancel = true; // cancel the default tab close action as we want to call 

                await doc.TryCloseAsync();     // TryClose and give the document a chance to block the close

                if (this.Items.Count == 0)
                {
                    Log.Debug("{class} {method} {message}", "DocumentTabViewModel", "TabClosing", "All documents closed");
                    //ActiveDocument = null;
                    await _eventAggregator.PublishOnUIThreadAsync(new AllDocumentsClosedEvent());
                }

                // remove this document from the autosave index
                if (doc is DocumentViewModel docModel)
                {
                    AutoSaver.Remove(docModel);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(DocumentTabViewModel), nameof(TabClosing), "Error while closing a tab");
                await _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, $"Error closing document:\n{ex.Message}"));
            }
            finally
            {
                Log.Verbose(Constants.LogMessageTemplate, nameof(DocumentTabViewModel), nameof(TabClosing), "Finished");
            }
        }

        public void Activate(object document)
        {
            Log.Verbose(Constants.LogMessageTemplate, nameof(DocumentTabViewModel), nameof(Activate), "Starting");
            try
            {
                if (document is DocumentViewModel doc)
                {
                    ActivateItemAsync(doc).Wait();
                    //ActiveDocument = doc;
                }
            }
            catch (Exception ex)
            {
                Log.Error(Constants.LogMessageTemplate, nameof(DocumentTabViewModel), nameof(Activate), $"Error Activating document: {ex.Message}");
            }
            Log.Verbose(Constants.LogMessageTemplate, nameof(DocumentTabViewModel), nameof(Activate), "Finished");
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

                    var fileToOpen = _autoSaveRecoveryIndex.Values.Where(i=>i.ShouldRecover).SelectMany(index => index.Files).Where(x => x.ShouldOpen).FirstOrDefault();

                    if (fileToOpen != null)
                    {
                        // the first file will mark itself as opened then re-publish the message 
                        // to open the next file (if there is one)
                        await RecoverAutoSaveFileAsync(fileToOpen);
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

            await RecoverNextAutoSaveFileAsync();

        }

        private async Task RecoverNextAutoSaveFileAsync()
        {
            var fileToOpen = _autoSaveRecoveryIndex.Values.Where(i => i.ShouldRecover).SelectMany(index => index.Files).Where(x => x.ShouldOpen).FirstOrDefault();

            if (fileToOpen != null)
            {
                // change the ShouldOpen flag to prevent the file being opened twice
                fileToOpen.ShouldOpen = false;
                // the first file will mark itself as opened then re-publish the message 
                // to open the next file (if there is one)
                await RecoverAutoSaveFileAsync(fileToOpen);
            }
            else
            {

                // if no files have been opened open a new blank document
                if (Items.Count == 0) await OpenNewBlankDocumentAsync(null);

                // Only the last tab should be active.
                // make sure all items except the last tab are not subscribed to the EventAggregator
                if (Items.Count > 1)
                {
                    for (var i = 0 ; i < Items.Count-1;i++)
                    {
                        (Items[i] as DocumentViewModel)?.UnsubscribeAll();
                    }
                }

                AutoSaver.CleanUpRecoveredFiles();

                // Now that any files have been recovered start the auto save timer
                await _eventAggregator.PublishOnUIThreadAsync(new StartAutoSaveTimerEvent());

            }
        }

        

        public async Task DuplicateTab(object tab)
        {
            if( tab is LayoutDocumentItem item)
            {
                if (item.Model is DocumentViewModel doc)
                {
                    await OpenNewBlankDocumentAsync(doc, copyContent: true);
                }
            }
            // todo get tab.Model to get at DocumentViewModel
            System.Diagnostics.Debug.WriteLine("duplicate tab");
        }

    }
}
