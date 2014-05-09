using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows;
using AvalonDock;
using Caliburn.Micro;
using DaxStudio.UI.Events;
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
        , IHandle<NewDocumentEvent>
        , IHandle<OpenFileEvent>
        , IDocumentWorkspace
    {
        private readonly IWindowManager _windowManager;
        private readonly IEventAggregator _eventAggregator;
        private int _documentCount = 1;
        private DocumentViewModel _activeDocument;

        //private readonly Func<DocumentViewModel> _documentFactory;
        private readonly Func<IWindowManager, IEventAggregator, DocumentViewModel> _documentFactory;
        [ImportingConstructor]
        public DocumentTabViewModel(IWindowManager windowManager, IEventAggregator eventAggregator, Func<IWindowManager,IEventAggregator, DocumentViewModel> documentFactory )
        {
            _documentFactory = documentFactory;
            _windowManager = windowManager;
            _eventAggregator = eventAggregator;
            NewQueryDocument(); // load a blank query window at startup
            _eventAggregator.Subscribe(this);
        }


        //[Import(typeof (Func<DocumentViewModel>))] 
        //private Func<DocumentViewModel> CreateDocument;

        //private readonly Func<DocumentViewModel> createDocument;


        public DocumentViewModel ActiveContent { get; set; }

        public DocumentViewModel ActiveDocument
        {
            get { return _activeDocument; }
            set
            {
                if (_activeDocument == value) 
                    return;
                _activeDocument = value;
                this.ActivateItem(_activeDocument);
                NotifyOfPropertyChange(()=>ActiveDocument);
                _eventAggregator.Publish(new UpdateConnectionEvent(ActiveDocument.Connection));
            }
        }

        
        public void NewQueryDocument()
        {
            NewQueryDocument(false);
        }

        public void NewQueryDocument( bool loadFromDisk)
        {
            //var newDoc = new DocumentViewModel();
            var newDoc = _documentFactory(_windowManager, _eventAggregator);
            //newDoc.Init(_windowManager, _eventAggregator);
            
            Items.Add(newDoc);
            

            ActivateItem(newDoc);
            ActiveDocument = newDoc;

            if (loadFromDisk)
            {
            //    _eventAggregator.Publish(new LoadFileEvent(fileName));
                newDoc.DisplayName = "<New>";
                newDoc.OpenFile();
            }
            else
            {
                var fileName = string.Format("Query{0}.dax", _documentCount);
                _documentCount++;
                newDoc.DisplayName = fileName;
                new System.Action(ChangeConnection).BeginOnUIThread();
                
            }
            //newDoc.IsDirty = false;
            new System.Action(CleanActiveDocument).BeginOnUIThread();
        }

        private void CleanActiveDocument()
        {
            ActiveDocument.IsDirty = false;
        }

        private void ChangeConnection()
        {
            ActiveDocument.ChangeConnection();
        }

        public void Handle(NewDocumentEvent message)
        {
            NewQueryDocument();
        }

        public void Handle(OpenFileEvent message)
        {
            NewQueryDocument(true);
        }

        public void TabClosing(object sender, DocumentClosingEventArgs args)
        {
            
            var doc = args.Document.Content as IScreen;
            if (doc == null) return;

            args.Cancel = true; // cancel the default tab close action as we want to call 

            doc.TryClose();     // TryClose and give the document a chance to block the close
        }

        /*
        public IEnumerable<IResult> DocumentClosing(DocumentViewModel document, DocumentClosingEventArgs e)
        {
            return HandleScriptClosing(document, () => e.Cancel = true);
        }

        public void DocumentClosed(DocumentViewModel document)
        {
            Items.Remove(document);
        }

        private IEnumerable<IResult> HandleScriptClosing(DocumentViewModel document)
        {
            return HandleScriptClosing(document, null);
        }
        
        private IEnumerable<IResult> HandleScriptClosing(DocumentViewModel document, Action cancelCallback)
        {
            if (document.IsDirty)
            {
                var message = Result.ShowMessageBox(document.FileName, string.Format("Do you want to save changes to {0}", document.FileName), MessageBoxButton.YesNoCancel);
                yield return message;

                if (message.Result == MessageBoxResult.Cancel)
                {
                    yield return Result.Cancel(cancelCallback);
                }
                else if (message.Result == MessageBoxResult.Yes)
                {
                    foreach (var result in scriptDialogStrategy.SaveAs(document, true, path => fileSystem.WriteAllText(path, document.FileContent)))
                        yield return result;
                }
            }
        }
        */

        public void Activate(object document)
        {
            var doc = document as DocumentViewModel;
            if (doc != null)
            {
                ActivateItem(doc);
                ActiveDocument = doc;
            }
        }
    }
}
