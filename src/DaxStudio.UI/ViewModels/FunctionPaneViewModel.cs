using System.ComponentModel.Composition;
using ADOTabular;
using Caliburn.Micro;

namespace DaxStudio.UI.ViewModels
{
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export(typeof(FunctionPaneViewModel))]
    public class FunctionPaneViewModel:ToolPaneBaseViewModel
    {
        [ImportingConstructor]
        public FunctionPaneViewModel(ADOTabularConnection connection, IEventAggregator eventAggregator) : base(connection, eventAggregator)
        {
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
    }
}
