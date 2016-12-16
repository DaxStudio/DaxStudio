using Caliburn.Micro;
using DaxStudio.Interfaces;
using System.ComponentModel.Composition;
using DaxStudio.UI.Extensions;
using System.Security;
using DaxStudio.Interfaces.Enums;
using System;
using System.Linq;
using System.Collections.Generic;

namespace DaxStudio.UI.ViewModels
{
    [Export(typeof(IGlobalOptions))]
    [Export(typeof(OptionsViewModel))]
    public class OptionsViewModel:Screen, IGlobalOptions
    {
        private string _selectedFontFamily;
        private bool _showLineNumbers;
        private bool _enableIntellisense;
        private double _fontSize;
        private bool _proxyUseSystem;
        private string _proxyAddress;
        private string _proxyUser;
        private string _proxyPassword;
        private SecureString _proxySecurePassword = new SecureString();
        private int _maxQueryHistory;
        private bool _queryHistoryShowTraceColumns;
        private int _queryEndEventTimeout;
        private int _daxFormatterRequestTimeout;
        private bool _traceDirectQuery;

        private IEventAggregator _eventAggregator;
        private DelimiterType _defaultSeparator;
        private bool _showPreReleaseNotifcations;

        //public event EventHandler OptionsUpdated;

        [ImportingConstructor]
        public OptionsViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            EditorFontFamily = RegistryHelper.GetValue<string>("EditorFontFamily","Lucida Console"); 
            EditorFontSize = RegistryHelper.GetValue<double>("EditorFontSize", 11);
            EditorShowLineNumbers = RegistryHelper.GetValue<bool>("EditorShowLineNumbers",true);
            EditorEnableIntellisense = RegistryHelper.GetValue<bool>("EditorEnableIntellisense", true);
            ProxyUseSystem = RegistryHelper.GetValue<bool>("ProxyUseSystem", true);
            ProxyAddress = RegistryHelper.GetValue<string>("ProxyAddress", "");
            ProxyUser = RegistryHelper.GetValue<string>("ProxyUser", "");
            ProxyPassword = RegistryHelper.GetValue<string>("ProxyPassword", "").Decrypt();
            QueryHistoryMaxItems = RegistryHelper.GetValue<int>("QueryHistoryMaxItems", 200);
            QueryHistoryShowTraceColumns = RegistryHelper.GetValue<bool>("QueryHistoryShowTraceColumns", true);
            QueryEndEventTimeout = RegistryHelper.GetValue<int>(nameof(QueryEndEventTimeout), 5);
            DaxFormatterRequestTimeout = RegistryHelper.GetValue<int>(nameof(DaxFormatterRequestTimeout), 10);
            DefaultSeparator = (DelimiterType) RegistryHelper.GetValue<int>(nameof(DefaultSeparator), (int)DelimiterType.Comma);
            TraceDirectQuery = RegistryHelper.GetValue<bool>("TraceDirectQuery", false);
            ShowPreReleaseNotifcations = RegistryHelper.GetValue<bool>("ShowPreReleaseNotifcations", false);
        }

        public string EditorFontFamily { get { return _selectedFontFamily; } 
            set{
                if (_selectedFontFamily == value) return;
                _selectedFontFamily = value;
                NotifyOfPropertyChange(() => EditorFontFamily);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                RegistryHelper.SetValueAsync<string>("EditorFontFamily", value);

            } 
        }

        public double EditorFontSize { get { return _fontSize; } 
            set {
                if (_fontSize == value) return;
                _fontSize = value;
                NotifyOfPropertyChange(() => EditorFontSize);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                RegistryHelper.SetValueAsync<double>("EditorFontSize", value);
            } 
        }
        public bool EditorShowLineNumbers { get { return _showLineNumbers; }
            set
            {
                if (_showLineNumbers == value) return;
                _showLineNumbers = value;
                NotifyOfPropertyChange(() => EditorShowLineNumbers);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                RegistryHelper.SetValueAsync<bool>("EditorShowLineNumbers", value);
            }
        }
        public bool EditorEnableIntellisense
        {
            get { return _enableIntellisense; }
            set
            {
                if (_enableIntellisense == value) return;
                _enableIntellisense = value;
                NotifyOfPropertyChange(() => EditorEnableIntellisense);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                RegistryHelper.SetValueAsync<bool>("EditorEnableIntellisense", value);
            }
        }
        public bool TraceDirectQuery {
            get { return _traceDirectQuery; }
            set {
                if (_traceDirectQuery == value) return;
                _traceDirectQuery = value;
                NotifyOfPropertyChange(() => TraceDirectQuery);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                RegistryHelper.SetValueAsync<bool>("TraceDirectQuery", value);
            }
        }
        #region Http Proxy properties

        public bool ProxyUseSystem
        {
            get { return _proxyUseSystem; }
            set
            {
                if (_proxyUseSystem == value) return;
                _proxyUseSystem = value;
                NotifyOfPropertyChange(() => ProxyUseSystem);
                NotifyOfPropertyChange(() => ProxyDontUseSystem);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                RegistryHelper.SetValueAsync<bool>("ProxyUseSystem", value);
            }
        }

        public bool ProxyDontUseSystem
        {
            get { return !_proxyUseSystem; }
        }

        public string ProxyAddress
        {
            get { return _proxyAddress; }
            set
            {
                if (_proxyAddress == value) return;
                _proxyAddress = value;
                NotifyOfPropertyChange(() => ProxyAddress);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                RegistryHelper.SetValueAsync<string>("ProxyAddress", value);
            }
        }

        public string ProxyUser
        {
            get { return _proxyUser; }
            set
            {
                if (_proxyUser == value) return;
                _proxyUser = value;
                NotifyOfPropertyChange(() => ProxyUser);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                RegistryHelper.SetValueAsync<string>("ProxyUser", value);
            }
        }

        public string ProxyPassword
        {
            get { return _proxyPassword; }
            set
            {
                if (_proxyPassword == value) return;
                _proxyPassword = value;
                NotifyOfPropertyChange(() => ProxyPassword);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                RegistryHelper.SetValueAsync<string>("ProxyPassword", value.Encrypt());
                SetProxySecurePassword(value);
            }
        }

        private void SetProxySecurePassword(string value)
        {
            foreach (char c in value)
            {
                ProxySecurePassword.AppendChar(c);
            }

        }

        public SecureString ProxySecurePassword
        {
            get { return _proxySecurePassword; }
            set
            {
                if (_proxySecurePassword == value) return;
                _proxySecurePassword = value;
                NotifyOfPropertyChange(() => ProxyPassword);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                RegistryHelper.SetValueAsync<string>("ProxyPassword", value.GetInsecureString().Encrypt());
            }
        }

        #endregion




        public int QueryHistoryMaxItems { get { return _maxQueryHistory; }
            set
            {
                if (_maxQueryHistory == value) return;
                _maxQueryHistory = value;
                NotifyOfPropertyChange(() => QueryHistoryMaxItems);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                RegistryHelper.SetValueAsync<int>("QueryHistoryMaxItems", value);
            }

        }


        public bool QueryHistoryShowTraceColumns
        {
            get { return _queryHistoryShowTraceColumns; }
            set
            {
                if (_queryHistoryShowTraceColumns == value) return;
                _queryHistoryShowTraceColumns = value;
                NotifyOfPropertyChange(() => QueryHistoryShowTraceColumns);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                RegistryHelper.SetValueAsync<bool>("QueryHistoryShowTraceColumns", value);
            }

        }

        public int QueryEndEventTimeout
        {
            get
            {
                return _queryEndEventTimeout;
            }

            set
            {
                if (_queryEndEventTimeout == value) return;
                _queryEndEventTimeout = value;
                NotifyOfPropertyChange(() => QueryEndEventTimeout);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                RegistryHelper.SetValueAsync<int>(nameof(QueryEndEventTimeout), value);
            }
        }

        public int DaxFormatterRequestTimeout
        {
            get
            {
                return _daxFormatterRequestTimeout;
            }

            set
            {
                if (_daxFormatterRequestTimeout == value) return;
                _daxFormatterRequestTimeout = value;
                NotifyOfPropertyChange(() => DaxFormatterRequestTimeout);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                RegistryHelper.SetValueAsync<int>(nameof(DaxFormatterRequestTimeout), value);
            }
        }

        public DelimiterType DefaultSeparator
        {
            get
            {
                return _defaultSeparator;
            }

            set
            {
                if (_defaultSeparator == value) return;
                _defaultSeparator = value;
                NotifyOfPropertyChange(() => DefaultSeparator);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                RegistryHelper.SetValueAsync<int>(nameof(DefaultSeparator), (int)value);
            }
        }

        public IEnumerable<DelimiterType> SeparatorTypes
        {
            get {
                var items = Enum.GetValues(typeof(DelimiterType)).Cast<DelimiterType>()
                                .Where(e => e != DelimiterType.Unknown);
                return items;
            }
        }

        public bool ShowPreReleaseNotifcations {
            get { return _showPreReleaseNotifcations; }
            set
            {
                if (_showPreReleaseNotifcations == value) return;
                _showPreReleaseNotifcations = value;
                NotifyOfPropertyChange(() => ShowPreReleaseNotifcations);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                RegistryHelper.SetValueAsync<bool>("ShowPreReleaseNotifcations", value);
            }
        }
    }
}
