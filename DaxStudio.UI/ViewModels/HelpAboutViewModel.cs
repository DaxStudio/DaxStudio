using System;
using Caliburn.Micro;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Reflection;
using DaxStudio.UI.Events;
using DaxStudio.Interfaces;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace DaxStudio.UI.ViewModels
{
    [Export]
    public class HelpAboutViewModel : Screen
    {
        private IEventAggregator _eventAggregator;

        [ImportingConstructor]
        public HelpAboutViewModel(IEventAggregator eventAggregator, IVersionCheck checker) {
            _eventAggregator = eventAggregator;
            DisplayName = "About DaxStudio";
            CheckingUpdateStatus = true;
            UpdateStatus = "Checking for Updates...";
            NotifyOfPropertyChange(() => UpdateStatus);

            // start version check async
            VersionChecker = checker;
            VersionChecker.PropertyChanged += VersionChecker_PropertyChanged;
            Task.Factory.StartNew(() => 
                {
                    this.VersionChecker.Update(); 
                })
                .ContinueWith((previous)=> {
                    CheckingUpdateStatus = false;
                    UpdateStatus = VersionChecker.VersionStatus;
                    NotifyOfPropertyChange(() => UpdateStatus);
                    NotifyOfPropertyChange(() => CheckingUpdateStatus);
                });
        }

        void VersionChecker_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "VersionStatus")
            {
                NotifyOfPropertyChange(() => UpdateStatus);
            }
        }

        public string FullVersionNumber
        {
            get { return System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString(3); }
        }

        public string BuildNumber
        {
            get { return System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString(); }
        }

        //[Import(typeof(IVersionCheck))]
        public IVersionCheck VersionChecker { get; set; }

        public SortedList<string,string> ReferencedAssemblies
        {
            get
            {
                var l = new SortedList<string,string>();
                var ass = System.Reflection.Assembly.GetExecutingAssembly();
                foreach (var a in ass.GetReferencedAssemblies())
                {
                    if (!l.ContainsKey(a.Name))
                    {
                        l.Add(a.Name, a.Version.ToString());
                    }
                }
                return l; 
            }
        }
        public void Ok()
        {
            TryClose();
        }

        public bool CheckingUpdateStatus
        {
            get;
            set;
        }

        public string UpdateStatus
        {
            get;
            set;
        }
    }

    public class ReferencedAssembly
    {
        public string Name { get; set; }
        public string Version { get; set; }
    }
}
