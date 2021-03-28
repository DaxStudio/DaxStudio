using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Media;
using ADOTabular;
using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Enums;
using DaxStudio.UI.Events;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Model;
using Serilog;
using FocusManager = DaxStudio.UI.Utils.FocusManager;

namespace DaxStudio.UI.ViewModels
{
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export(typeof(FunctionPaneViewModel))]
    public class FunctionPaneViewModel:ToolPaneBaseViewModel
        , IMetadataPane
        , IHandle<ConnectionChangedEvent>
    {
        private IFunctionProvider _functionProvider;
        [ImportingConstructor]
        public FunctionPaneViewModel(IFunctionProvider functionProvider, IEventAggregator eventAggregator, DocumentViewModel document, IGlobalOptions options) : base( eventAggregator)
        {
            Document = document;
            _functionProvider = functionProvider;
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
                    _functionGroups = _functionProvider.TreeViewFunctions(Options, EventAggregator, this);
                }
                return _functionGroups;
            }
        }

        public override string DefaultDockingPane => "DockLeft";
        public override string ContentId => "functions";
        public override ImageSource IconSource
        {
            get
            {
                var imgSourceConverter = new ImageSourceConverter();
                return imgSourceConverter.ConvertFromInvariantString(
                    @"pack://application:,,,/DaxStudio.UI;component/images/Metadata/Function.png") as ImageSource;

            }
        }

        public override string Title => "Functions";

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
            try
            {
                if (FunctionGroups == null) return;
                foreach (var node in FunctionGroups)
                    node.ApplyCriteria(SearchCriteria, new Stack<IFilterableTreeViewItem>());
            }
            catch (Exception ex)
            {
                Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(FunctionPaneViewModel), nameof(ApplyFilter), ex.Message);
                EventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, $"Error Filtering Functions: {ex.Message}"));
            }
        }

        public bool HasSearchCriteria => !string.IsNullOrEmpty(SearchCriteria);
        public void ClearSearchCriteria()
        {
            SearchCriteria = string.Empty;
        }

        public void Handle(ConnectionChangedEvent message)
        {
            NotifyOfPropertyChange(() => FunctionGroups);
            //EventAggregator.PublishOnUIThread(new FunctionsLoadedEvent(Document, _functionProvider.FunctionGroups));
        }

        public void MetadataKeyUp(IFilterableTreeViewItem selectedItem, KeyEventArgs args)
        {
            switch (args.Key)
            {
                case Key.F1:
                    
                    LaunchDaxGuide(selectedItem);
                    break;
            }
        }

        public void LaunchDaxGuide(IFilterableTreeViewItem selectedItem)
        {
            if (!(selectedItem is ADOTabularFunctionsExtensions.TreeViewFunction func)) return;
            Process.Start(new ProcessStartInfo($"https://dax.guide/{func.Name}/?aff=dax-studio"));
        }
    }
}
