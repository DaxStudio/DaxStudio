﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Caliburn.Micro;
using Windows.Storage.Search;

namespace DaxStudio.UI.ViewModels
{
    [Export(typeof(ServerTimingDetailsViewModel))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class ServerTimingDetailsViewModel:PropertyChangedBase
    {
        private bool _showScan = true; // show scans by default
        public bool ShowScan { get {return _showScan;} set {_showScan = value; NotifyOfPropertyChange();} }
        private bool _showInternal;
        public bool ShowInternal { get { return _showInternal; } set { _showInternal = value; NotifyOfPropertyChange(); } }
        private bool _showBatch = true; // show batch by default
        public bool ShowBatch { get { return _showBatch; } set { _showBatch = value; NotifyOfPropertyChange(); } }
        private bool _showCache;
        public bool ShowCache { get { return _showCache; } set { _showCache = value; NotifyOfPropertyChange(); } }

        private bool _showRewriteAttempts = true; // default to true
        public bool ShowRewriteAttempts { get { return _showRewriteAttempts; } set { _showRewriteAttempts = value; NotifyOfPropertyChange(); } }
        private bool _showMetrics = true;  // default to true
        public bool ShowMetrics { get { return _showMetrics; } set { _showMetrics = value; NotifyOfPropertyChange(); } }
        private bool _showObjectName = false;
        public bool ShowObjectName { get { return _showObjectName; } set { _showObjectName = value;NotifyOfPropertyChange(); } }
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
        private bool _showSql = true; // show SQL by default
        public bool ShowSql
        {
            get { return _showSql; }
            set
            {
                _showSql = value;
                NotifyOfPropertyChange(() => ShowSql);
            }
        }
        private bool _showTabularQueries = true; // show Tabular queries by default
        public bool ShowTabularQueries
        {
            get { return _showTabularQueries; }
            set
            {
                _showTabularQueries = value;
                NotifyOfPropertyChange(() => ShowTabularQueries);
            }
        }
    }
}
