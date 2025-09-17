using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Interfaces;
using System;
using System.Data;
using System.IO.Packaging;

namespace DaxStudio.UI.ViewModels
{
    public class QueryDependenciesViewModel : Screen, IZoomable, IToolWindow, ISaveState
    {
        public QueryDependenciesViewModel(IEventAggregator eventAggregator, IGlobalOptions globalOptions, IWindowManager windowManager){ 
        
        }

        private DataTable _dependencies;
        public DataTable Dependencies { get => _dependencies;
            set { 
                _dependencies = value;
                NotifyOfPropertyChange(() => Dependencies);
            }
        }

        public double Scale { get; set ; }

        public string Title => "Query Dependencies";

        public string DefaultDockingPane   => "DockBottom"; 
    

        public bool CanCloseWindow  { get { return true; }
            set { }
        }

        public bool CanHide => false;

        public int AutoHideMinHeight { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool IsSelected { get; set; }

        public string ContentId => "QueryDependencies";

        public event EventHandler OnScaleChanged;

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
