using System;
using System.Reflection;
using Caliburn.Micro;
using System.ComponentModel.Composition;

namespace DaxStudio.UI.ViewModels {
    [Export(typeof (IShell))]
    public class ShellViewModel : Screen, IShell
    {
        private readonly IWindowManager windowManager; 
        private DocumentTabViewModel _tabs;

        [ImportingConstructor]
        public ShellViewModel(IWindowManager windowManager, IEventAggregator eventAggregator ,RibbonViewModel ribbonViewModel, IConductor conductor)
        {
            this.Ribbon = ribbonViewModel;
            this.windowManager = windowManager;
            Tabs = (DocumentTabViewModel) conductor;
            Tabs.ConductWith(this);
            //Tabs = new DocumentTabViewModel(windowManager,eventAggregator) ;    
            //var tabs =  (Conductor<IScreen>.Collection.OneActive)conductor;
            //Tabs = tabs.Items;
            DisplayName = string.Format("DaxStudio - v{0}.{1}", Version.Major, Version.Minor); 
        }


        public Version Version { get { return Assembly.GetExecutingAssembly().GetName().Version; } }
        public DocumentTabViewModel Tabs { get; set; }
        public RibbonViewModel Ribbon { get; set; }
        public void ContentRendered()
        { }

        // Used for Global Keyboard Hooks
        public void RunQuery()
        {
            Ribbon.RunQuery();
        }

    }
}