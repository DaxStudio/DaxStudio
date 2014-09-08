using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ADOTabular;
using Caliburn.Micro;

namespace DaxStudio.UI.ViewModels
{
    public class DmvPaneViewModel:ToolPaneBaseViewModel
    {

        public DmvPaneViewModel(ADOTabularConnection connection, IEventAggregator eventAggregator)
            : base(connection, eventAggregator)
        {}

        public ADOTabularDynamicManagementViewCollection DmvQueries
        {
            get { return Connection == null ? null: Connection.DynamicManagementViews; }
        }

        protected override void OnConnectionChanged()//bool isSameServer)
        {
            base.OnConnectionChanged();//isSameServer);
            //if (isSameServer) return;
            NotifyOfPropertyChange(()=> DmvQueries);
        }
        public override string DefaultDockingPane
        {
            get { return "DockLeft"; }
            set { base.DefaultDockingPane = value; }
        }
        public override string Title
        {
            get { return "DMV"; }
            set { base.Title = value; }
        }
    }
}
