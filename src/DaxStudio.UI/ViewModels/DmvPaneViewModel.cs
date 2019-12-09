using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ADOTabular;
using Caliburn.Micro;
using System.ComponentModel.Composition;
using DaxStudio.UI.Events;
using System.ComponentModel;
using System.Collections;
using System.Windows.Data;
using System.Windows.Threading;
using System.Collections.ObjectModel;

namespace DaxStudio.UI.ViewModels
{
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export]
    public class DmvPaneViewModel : ToolPaneBaseViewModel
    {
        private object _lock = new object();
        [ImportingConstructor]
        public DmvPaneViewModel(ADOTabularConnection connection, IEventAggregator eventAggregator, DocumentViewModel document)
            : base(connection, eventAggregator)
        {
            Document = document;
            DmvQueries = new ObservableCollection<ADOTabularDynamicManagementView>();
            // this makes sure the collection can be updated on a background thread
            BindingOperations.EnableCollectionSynchronization(DmvQueries, _lock);
            SetupDmvs();
            DmvQueriesView = CollectionViewSource.GetDefaultView(DmvQueries);
            DmvQueriesView.Filter = UserFilter;
        }

        private void SetupDmvs()
        {
            if (Document.Connection == null) return;
            DmvQueries.Clear();

            foreach (var dmv in Document.Connection?.DynamicManagementViews)
            {
                DmvQueries.Add(dmv);
            }
        }

        public ObservableCollection<ADOTabularDynamicManagementView> DmvQueries {get;}

        //public ADOTabularDynamicManagementViewCollection DmvQueries
        //{
        //    get { return Connection == null ? null: Connection.DynamicManagementViews; }
        //}

        protected override void OnConnectionChanged()//bool isSameServer)
        {
            base.OnConnectionChanged();//isSameServer);
            //if (isSameServer) return;
            if (Connection == null) return;

            SetupDmvs();

            NotifyOfPropertyChange(()=> DmvQueries);

            // notify the intellisense provider that the dmv list may need updating
            EventAggregator.PublishOnUIThread(new DmvsLoadedEvent(Document, Connection.DynamicManagementViews));
        }

        public bool UserFilter(object dmv)
        {
            if (String.IsNullOrEmpty(SearchCriteria))
                return true;
            else
                return (((ADOTabularDynamicManagementView)dmv).Caption.IndexOf(SearchCriteria, StringComparison.OrdinalIgnoreCase) >= 0);
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

        private string _searchCriteria;
        public string SearchCriteria
        {
            get { return _searchCriteria; }
            set {
                _searchCriteria = value;
                NotifyOfPropertyChange(nameof(SearchCriteria));
                NotifyOfPropertyChange(nameof(HasSearchCriteria));
                DmvQueriesView.Refresh();
            }
        }

        public void ClearSearchCriteria()
        {
            SearchCriteria = string.Empty;
        }

        public bool HasSearchCriteria => !string.IsNullOrEmpty(SearchCriteria);

        public ICollectionView DmvQueriesView { get; }
    }

}
