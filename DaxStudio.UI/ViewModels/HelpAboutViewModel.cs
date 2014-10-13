using System;
using Caliburn.Micro;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Reflection;
using DaxStudio.UI.Events;

namespace DaxStudio.UI.ViewModels
{
    public class HelpAboutViewModel : Screen
    {
        private IEventAggregator _eventAggregator;
        public HelpAboutViewModel(IEventAggregator eventAggregator) {
            _eventAggregator = eventAggregator;
            //_eventAggregator.BeginPublishOnUIThread(new NewVersionEvent("100","http://daxstudio.codeplex.com"));
            DisplayName = "About DaxStudio";
        }

        public string FullVersionNumber
        {
            get { return System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString(); }
        }

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

    }

    public class ReferencedAssembly
    {
        public string Name { get; set; }
        public string Version { get; set; }
    }
}
