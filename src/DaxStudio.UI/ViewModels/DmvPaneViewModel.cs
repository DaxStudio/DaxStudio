using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ADOTabular;
using Caliburn.Micro;
using System.ComponentModel.Composition;
using DaxStudio.UI.Events;

namespace DaxStudio.UI.ViewModels
{
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export]
    public class DmvPaneViewModel:ToolPaneBaseViewModel
    {
        [ImportingConstructor]
        public DmvPaneViewModel(ADOTabularConnection connection, IEventAggregator eventAggregator, DocumentViewModel document)
            : base(connection, eventAggregator)
        {
            Document = document;
        }

        public ADOTabularDynamicManagementViewCollection DmvQueries
        {
            get { return Connection == null ? null: Connection.DynamicManagementViews; }
        }

        protected override void OnConnectionChanged()//bool isSameServer)
        {
            base.OnConnectionChanged();//isSameServer);
            //if (isSameServer) return;
            NotifyOfPropertyChange(()=> DmvQueries);
            EventAggregator.PublishOnUIThread(new DmvsLoadedEvent(Document, Connection.DynamicManagementViews));
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

        public DocumentViewModel Document { get; private set; }
    }
}
