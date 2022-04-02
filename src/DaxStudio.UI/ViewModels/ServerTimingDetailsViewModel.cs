using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Caliburn.Micro;

namespace DaxStudio.UI.ViewModels
{
    [Export(typeof(ServerTimingDetailsViewModel))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class ServerTimingDetailsViewModel:PropertyChangedBase
    {
        private bool _showScan = true; // show scans by default
        public bool ShowScan { get {return _showScan;} set {_showScan = value; NotifyOfPropertyChange(() => ShowScan);} }
        private bool _showInternal;
        public bool ShowInternal { get { return _showInternal; } set { _showInternal = value; NotifyOfPropertyChange(() => ShowInternal); } }
        private bool _showBatch = true; // show batch by default
        public bool ShowBatch { get { return _showBatch; } set { _showBatch = value; NotifyOfPropertyChange(() => ShowBatch); } }
        private bool _showCache;
        public bool ShowCache { get { return _showCache; } set { _showCache = value; NotifyOfPropertyChange(() => ShowCache); } }

        private bool _showRewriteAttempts = true; // default to true
        public bool ShowRewriteAttempts { get { return _showRewriteAttempts; } set { _showRewriteAttempts = value; NotifyOfPropertyChange(() => ShowRewriteAttempts); } }


        public bool LayoutRight { get { return !LayoutBottom; } set {LayoutBottom = !value; }}
        private bool _layoutBottom;
        public bool LayoutBottom
        {
            get { return _layoutBottom; }
            set { _layoutBottom = value; 
                NotifyOfPropertyChange(() => LayoutBottom);
                NotifyOfPropertyChange(() => LayoutRight);
            }
        }
    }
}
