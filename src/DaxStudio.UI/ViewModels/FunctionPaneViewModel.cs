using System.ComponentModel.Composition;
using ADOTabular;
using Caliburn.Micro;
using DaxStudio.UI.Events;

namespace DaxStudio.UI.ViewModels
{
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export(typeof(FunctionPaneViewModel))]
    public class FunctionPaneViewModel:ToolPaneBaseViewModel
    {
        [ImportingConstructor]
        public FunctionPaneViewModel(ADOTabularConnection connection, IEventAggregator eventAggregator, DocumentViewModel document) : base(connection, eventAggregator)
        {
            Document = document;
            Connection = connection;
            EventAggregator = eventAggregator; 
        }

        public ADOTabularFunctionGroupCollection FunctionGroups{
            get { return Connection == null ? null : Connection.FunctionGroups; }
            
        }

        protected override void OnConnectionChanged()//bool isSameServer)
        {
            base.OnConnectionChanged();//isSameServer);
            //if (isSameServer) return;
            NotifyOfPropertyChange(()=> FunctionGroups);
            EventAggregator.PublishOnUIThread(new FunctionsLoadedEvent(Document, FunctionGroups));
        }

        public override string DefaultDockingPane
        {
            get { return "DockLeft"; }
            set { base.DefaultDockingPane = value; }
        }
        public override string Title
        {
            get { return "Functions"; }
            set { base.Title = value; }
        }

        public DocumentViewModel Document { get; private set; }
    }
}
