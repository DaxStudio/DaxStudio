using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using ADOTabular;
using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Events;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Model;

namespace DaxStudio.UI.ViewModels
{
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export(typeof(FunctionPaneViewModel))]
    public class FunctionPaneViewModel:ToolPaneBaseViewModel, IMetadataPane
    {
        [ImportingConstructor]
        public FunctionPaneViewModel(ADOTabularConnection connection, IEventAggregator eventAggregator, DocumentViewModel document, IGlobalOptions options) : base(connection, eventAggregator)
        {
            Document = document;
            Connection = connection;
            EventAggregator = eventAggregator;
            Options = options;
        }


        private IEnumerable<FilterableTreeViewItem> _functionGroups;
        public IEnumerable<FilterableTreeViewItem> FunctionGroups
        {
            get
            {
                if (_functionGroups == null)
                {
                    _functionGroups = Connection.TreeViewFunctions(Options, EventAggregator, this);
                }
                return _functionGroups;
            }
        }

        //public ADOTabularFunctionGroupCollection FunctionGroups{
        //    get { return Connection == null ? null : Connection.FunctionGroups; }  
        //}

        protected override void OnConnectionChanged()//bool isSameServer)
        {
            base.OnConnectionChanged();//isSameServer);
            //if (isSameServer) return;
            NotifyOfPropertyChange(()=> FunctionGroups);
            EventAggregator.PublishOnUIThread(new FunctionsLoadedEvent(Document, Connection.FunctionGroups));
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
        public IGlobalOptions Options { get; }
        public bool ShowHiddenObjects { get; set; } = true;

        private string _searchCriteria;
        public string SearchCriteria { get => _searchCriteria;
            set {
                _searchCriteria = value;
                NotifyOfPropertyChange(nameof(SearchCriteria));
                NotifyOfPropertyChange(nameof(HasSearchCriteria));
                ApplyFilter();
            } 
        }

        private void ApplyFilter()
        {
            if (FunctionGroups == null) return;
            foreach (var node in FunctionGroups)
                node.ApplyCriteria(SearchCriteria, new Stack<FilterableTreeViewItem>());
        }

        public bool HasSearchCriteria => !string.IsNullOrEmpty(SearchCriteria);
        public void ClearSearchCriteria()
        {
            SearchCriteria = string.Empty;
        }
    }
}
