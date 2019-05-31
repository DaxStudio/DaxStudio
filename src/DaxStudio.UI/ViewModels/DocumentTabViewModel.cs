using System;
using System.ComponentModel.Composition;
using Xceed.Wpf.AvalonDock;
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
using System.Threading;
using DaxStudio.Interfaces;
using DaxStudio.UI.Utils;

namespace DaxStudio.UI.ViewModels
{
    /*
    public class DocumentFactory
{
   [Export(typeof(Func<DocumentViewModel>))]
   public DocumentViewModel CreateDocument()
   {
       return new DocumentViewModel();
   }
}
    */

    [Export(typeof(IConductor))]
    public class DocumentTabViewModel : Conductor<IScreen>.Collection.OneActive
        , IHandle<AutoSaveRecoveryEvent>
        , IHandle<NewDocumentEvent>
        , IHandle<OpenFileEvent>
        , IHandle<OpenRecentFileEvent>
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

        //private readonly Func<DocumentViewModel> _documentFactory;
        private readonly Func<IWindowManager, IEventAggregator, DocumentViewModel> _documentFactory;
        [ImportingConstructor]
        public DocumentTabViewModel(IWindowManager windowManager, IEventAggregator eventAggregator, Func<IWindowManager,IEventAggregator, DocumentViewModel> documentFactory , IGlobalOptions options)
        {
            _documentFactory = documentFactory;
            _windowManager = windowManager;
            _eventAggregator = eventAggregator;
            _options = options;
            _eventAggregator.Subscribe(this);
        }

        
        public DocumentViewModel ActiveDocument
        {
            get { return _activeDocument; }
            set
            {
                if (_activeDocument == value) 
                    return;  // this item is already active
                if (this.Items.Count == 0)
                {
                    _activeDocument = null;
                    return;  // no items in collection usually means we are shutting down
                }
                Log.Debug("{Class} {Event} {Connection} {Document}", "DocumentTabViewModel", "ActiveDocument:Set", value.DisplayName);
                _activeDocument = value;
                this.ActivateItem(_activeDocument);
                NotifyOfPropertyChange(()=>ActiveDocument);
            }
        }

        public Xceed.Wpf.AvalonDock.Themes.Theme AvalonDockTheme { get {
                if (_options.Theme == "Dark") return new Theme.MonotoneTheme();
                else return null; // new Xceed.Wpf.AvalonDock.Themes.GenericTheme();
            }
        }


        public void NewQueryDocument(string fileName)
        {
            NewQueryDocument(fileName, null);
        }

        public void NewQueryDocument( string fileName, DocumentViewModel sourceDocument)
        {
            // 1 Open BlankDocument
            if (string.IsNullOrEmpty(fileName)) OpenNewBlankDocument(sourceDocument);
            // 2 Open Document in current window (if it's an empty document)
            else if (ActiveDocument != null && !ActiveDocument.IsDiskFileName && !ActiveDocument.IsDirty) OpenFileInActiveDocument(fileName);
            // 3 Open Document in a new window if current window has content
            else OpenFileInNewWindow(fileName);           
            
        }

        private void RecoverAutoSaveFile(AutoSaveIndexEntry file)
        {
            
            var newDoc = _documentFactory(_windowManager, _eventAggregator);
            using (new StatusBarMessage(newDoc, $"Recovering \"{file.DisplayName}\""))
            {
                newDoc.DisplayName = file.DisplayName;
                newDoc.IsDiskFileName = file.IsDiskFileName;
                newDoc.FileName = file.OriginalFileName;
                newDoc.AutoSaveId = file.AutoSaveId;
                newDoc.State = DocumentState.RecoveryPending;

                Items.Add(newDoc);
                ActivateItem(newDoc);
                ActiveDocument = newDoc;
                newDoc.IsDirty = true;

                file.ShouldOpen = false;

                _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Information, $"Recovering File: '{file.DisplayName}'"));

                Log.Information("{class} {method} {message}", "DocumentTabViewModel", "RecoverAutoSaveFile", $"AutoSave Recovery complete for {file.DisplayName} ({file.AutoSaveId})");
            }
        }

        private void OpenFileInNewWindow(string fileName)
        {
            Log.Debug("{class} {method} {message}", "DocumentTabViewModel", "OpenFileInNewWindow", "Opening " + fileName);
            var newDoc = _documentFactory(_windowManager, _eventAggregator);
            Items.Add(newDoc);
            ActivateItem(newDoc);
            ActiveDocument = newDoc;
                       
            newDoc.DisplayName = "Opening...";
            newDoc.FileName = fileName;
            newDoc.State = DocumentState.LoadPending;  // this triggers the DocumentViewModel to open the file
            
        }

        private void OpenFileInActiveDocument(string fileName)
        {
            Log.Debug("{class} {method} {message}", "DocumentTabViewModel", "OpenFileInActiveDocumentWindow", "Opening " + fileName);
            ActiveDocument.FileName = fileName;
            ActiveDocument.LoadFile(fileName);
        }

        private void OpenNewBlankDocument(DocumentViewModel sourceDocument)
        {

            var newDoc = _documentFactory(_windowManager, _eventAggregator);
            
            Items.Add(newDoc);
            ActivateItem(newDoc);
            ActiveDocument = newDoc;
            newDoc.DisplayName = string.Format("Query{0}.dax", _documentCount);
            _documentCount++;
            
            new System.Action(CleanActiveDocument).BeginOnUIThread();

            if (sourceDocument == null 
                || sourceDocument.Connection == null 
                || sourceDocument.Connection.State != System.Data.ConnectionState.Open)
                    new System.Action(ChangeConnection).BeginOnUIThread();
            else {
                _eventAggregator.PublishOnUIThread(new CopyConnectionEvent(sourceDocument));
            }
            
        }

        private void CleanActiveDocument()
        {
            Log.Debug("{Class} {Event} {ActiveDocument} IsDirty: {IsDirty}", "DocumentTabViewModel", "CleanActiveDocument", ActiveDocument.DisplayName, ActiveDocument.IsDirty);
            ActiveDocument.IsDirty = false;
        }

        private void ChangeConnection()
        {
            ActiveDocument.ChangeConnection();
        }

        public void Handle(NewDocumentEvent message)
        {
            NewQueryDocument("",message.ActiveDocument);
        }

        public void Handle(OpenFileEvent message)
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
                _eventAggregator.PublishOnUIThread(new FileOpenedEvent(fileName));
                NewQueryDocument(fileName);
            }
            
        }

        public void Handle(OpenRecentFileEvent message)
        {
            NewQueryDocument(message.FileName);
        }

        public void TabClosing(object sender, DocumentClosingEventArgs args)
        {

            var doc = args.Document.Content as IScreen;
            if (doc == null) return;

            args.Cancel = true; // cancel the default tab close action as we want to call 

            doc.TryClose();     // TryClose and give the document a chance to block the close

            if (this.Items.Count == 0)
            {
                Log.Debug("{class} {method} {message}", "DocumentTabViewModel", "TabClosing", "All documents closed");
                ActiveDocument = null;
                _eventAggregator.PublishOnUIThreadAsync(new AllDocumentsClosedEvent());
            }
        }

        public void Activate(object document)
        {
            var doc = document as DocumentViewModel;
            if (doc != null)
            {
                ActivateItem(doc);
                ActiveDocument = doc;
            }
        }

        public void Handle(UpdateGlobalOptions message)
        {
            NotifyOfPropertyChange(() => AvalonDockTheme);
            foreach (var itm in this.Items)
            {
                var doc = itm as DocumentViewModel;
                doc.UpdateSettings();
                doc.UpdateTheme();
            }
        }

        public void Handle(AutoSaveRecoveryEvent message)
        {
            _autoSaveRecoveryIndex = message.AutoSaveMasterIndex;

            if (!message.RecoveryInProgress)
            {
                // if auto save recovery is not already in progress 
                // prompt the user for which files should be recovered
                var autoSaveRecoveryDialog = new AutoSaveRecoveryDialogViewModel();

                var filesToRecover = message.AutoSaveMasterIndex.Values.Where(i => i.ShouldRecover && i.IsCurrentVersion).SelectMany(entry => entry.Files);

                if (filesToRecover.Count() == 0)
                {
                    // if there are no files to recover then clean up 
                    // the recovery folder and exit here
                    AutoSaver.CleanUpRecoveredFiles();
                    return; 
                }

                autoSaveRecoveryDialog.Files = new ObservableCollection<AutoSaveIndexEntry>(filesToRecover);

                _windowManager.ShowDialogBox(autoSaveRecoveryDialog, settings: new Dictionary<string, object>
                {
                    { "WindowStyle", WindowStyle.None},
                    { "ShowInTaskbar", false},
                    { "ResizeMode", ResizeMode.NoResize},
                    { "Background", System.Windows.Media.Brushes.Transparent},
                    { "AllowsTransparency",true}

                });

                if (autoSaveRecoveryDialog.Result != OpenDialogResult.Cancel)
                {
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
                    // if recovery has been cancelled open a new blank document
                    NewQueryDocument("");
                    // and remove unwanted recovery files
                    AutoSaver.CleanUpRecoveredFiles();
                }

            }

            // TODO - maybe move this to a RecoverNextFile message and store the files to recover in a private var
            //        then at the end of the ViewLoaded event we can trigger this event and run code like the
            //        section below

            
        }

        public void Handle(RecoverNextAutoSaveFileEvent message)
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
                if (Items.Count == 0) OpenNewBlankDocument(null);

                AutoSaver.CleanUpRecoveredFiles();

                // Now that any files have been recovered start the auto save timer
                _eventAggregator.PublishOnUIThreadAsync(new StartAutoSaveTimerEvent());
                
            }
        }
    }
}
