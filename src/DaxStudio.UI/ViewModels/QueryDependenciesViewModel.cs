using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Interfaces;
using System;
using System.Data;
using System.IO.Packaging;

namespace DaxStudio.UI.ViewModels
{
    public class QueryDependenciesViewModel : ToolPaneBaseViewModel, IZoomable, IToolWindow, ISaveState
    {
        public QueryDependenciesViewModel(IEventAggregator eventAggregator, IGlobalOptions globalOptions, IWindowManager windowManager) : base(eventAggregator){ 

        }

        private DataTable _dependencies;
        public DataTable Dependencies { get => _dependencies;
            set { 
                _dependencies = value;
                NotifyOfPropertyChange(() => Dependencies);
            }
        }


        public override string Title => "Query Dependencies";

        public override string DefaultDockingPane   => "DockBottom"; 
    

        public override bool CanHide => false;

        public override string ContentId => "QueryDependencies";

        public string GetJson()
        {
            throw new NotImplementedException();
        }

        public void Load(string filename)
        {
            throw new NotImplementedException();
        }

        public void LoadJson(string json)
        {
            throw new NotImplementedException();
        }

        public void LoadPackage(Package package)
        {
            throw new NotImplementedException();
        }

        public void Save(string filename)
        {
            throw new NotImplementedException();
        }

        public void SavePackage(Package package)
        {
            throw new NotImplementedException();
        }

        private bool _showFilters = false;
        public bool ShowFilters { 
            get => _showFilters; 
            set { _showFilters = value; NotifyOfPropertyChange(); } 
        }


    }
}
