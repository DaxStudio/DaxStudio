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
using System.Windows.Media;
using DaxStudio.Interfaces;
using ADOTabular.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace DaxStudio.UI.ViewModels
{
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export]
    public class DmvPaneViewModel : ToolPaneBaseViewModel 
        , IHandle<ConnectionChangedEvent>
        , IHandle<UpdateGlobalOptions>
    {
        private IDmvProvider _dmvProvider;
        private object _lock = new object();
        [ImportingConstructor]
        public DmvPaneViewModel(IDmvProvider dmvProvider, IEventAggregator eventAggregator, DocumentViewModel document, IGlobalOptions options)
            : base(eventAggregator)
            
        {
            _dmvProvider = dmvProvider;
            Document = document;
            Options = options;
            DmvQueries = new ObservableCollection<IDmv>();
            // this makes sure the collection can be updated on a background thread
            BindingOperations.EnableCollectionSynchronization(DmvQueries, _lock);
            SetupDmvs();
            DmvQueriesView = CollectionViewSource.GetDefaultView(DmvQueries);
            DmvQueriesView.Filter = UserFilter;
        }

        private void SetupDmvs()
        {
            if (!Document.Connection.IsConnected) return;
            DmvQueries.Clear();

            foreach (var dmv in _dmvProvider.DynamicManagementViews)
            {
                DmvQueries.Add(dmv);
            }
        }

        public ObservableCollection<IDmv> DmvQueries {get;}

        //public ADOTabularDynamicManagementViewCollection DmvQueries
        //{
        //    get { return Connection == null ? null: Connection.DynamicManagementViews; }
        //}

        public Task HandleAsync(ConnectionChangedEvent message, CancellationToken cancellationToken)
        {
            SetupDmvs();
            NotifyOfPropertyChange(() => DmvQueries);

            // notify the intellisense provider that the dmv list may need updating
            //EventAggregator.PublishOnUIThreadAsync(new DmvsLoadedEvent(Document, _dmvProvider.DynamicManagementViews));
            return Task.CompletedTask;
        }

        public bool UserFilter(object dmv)
        {
            if (String.IsNullOrEmpty(SearchCriteria))
                return true;
            else
                return (((ADOTabularDynamicManagementView)dmv).Caption.IndexOf(SearchCriteria, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public override string DefaultDockingPane => "DockLeft";
        public override string ContentId => "dmv";
        public override ImageSource IconSource
        {
            get
            {
                var imgSourceConverter = new ImageSourceConverter();
                return imgSourceConverter.ConvertFromInvariantString(
                    @"pack://application:,,,/DaxStudio.UI;component/images/Metadata/DmvTable.png") as ImageSource;

            }
        }
        public override string Title => "DMV";

        public DocumentViewModel Document { get; private set; }
        public IGlobalOptions Options { get; }

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

        public Task HandleAsync(UpdateGlobalOptions message, CancellationToken cancellationToken)
        {
            NotifyOfPropertyChange(nameof(AutoHideMetadataVerticalScrollbars));
            return Task.CompletedTask;
        }

        public bool HasSearchCriteria => !string.IsNullOrEmpty(SearchCriteria);

        public ICollectionView DmvQueriesView { get; }
        public bool AutoHideMetadataVerticalScrollbars => Options.AutoHideMetadataVerticalScrollbars;
    }

}
