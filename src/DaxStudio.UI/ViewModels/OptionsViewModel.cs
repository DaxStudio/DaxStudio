using Caliburn.Micro;
using DaxStudio.Interfaces;
using System.ComponentModel.Composition;
using DaxStudio.UI.Extensions;
using System.Security;
using DaxStudio.Interfaces.Enums;
using System;
using System.Linq;
using System.Collections.Generic;
using DaxStudio.UI.Events;
using System.Windows;
using DaxStudio.UI.Utils;

namespace DaxStudio.UI.ViewModels
{
    [Export(typeof(IGlobalOptions))]
    [Export(typeof(OptionsViewModel))]
    public class OptionsViewModel:Screen, IGlobalOptions
    {
        private const string DefaultEditorFontFamily = "Lucida Console";
        private const int DefaultEditorFontSize = 11;
        private const string DefaultResultsFontFamily = "Segoe UI";
        private const int DefaultResultsFontSize = 11;

        private string _selectedEditorFontFamily;
        private string _selectedResultFontFamily;
        private bool _showLineNumbers;
        private bool _enableIntellisense;
        private double _editorFontSize;
        private double _resultFontSize;
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
        private DaxFormatStyle _defaultDaxFormatStyle;
        private bool _showPreReleaseNotifcations;
        private bool _showTooltipBasicStats;
        private bool _showTooltipSampleData;

        //public event EventHandler OptionsUpdated;

        [ImportingConstructor]
        public OptionsViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            

            EditorFontFamily = RegistryHelper.GetValue<string>("EditorFontFamily", DefaultEditorFontFamily);
            EditorFontSize = RegistryHelper.GetValue<double>("EditorFontSize", DefaultEditorFontSize);
            ResultFontFamily = RegistryHelper.GetValue<string>("ResultFontFamily", DefaultResultsFontFamily);
            ResultFontSize = RegistryHelper.GetValue<double>("ResultFontSize", DefaultResultsFontSize);
            EditorShowLineNumbers = RegistryHelper.GetValue<bool>("EditorShowLineNumbers", true);
            EditorEnableIntellisense = RegistryHelper.GetValue<bool>("EditorEnableIntellisense", true);
            ProxyUseSystem = RegistryHelper.GetValue<bool>("ProxyUseSystem", true);
            ProxyAddress = RegistryHelper.GetValue<string>("ProxyAddress", "");
            ProxyUser = RegistryHelper.GetValue<string>("ProxyUser", "");
            ProxyPassword = RegistryHelper.GetValue<string>("ProxyPassword", "").Decrypt();
            QueryHistoryMaxItems = RegistryHelper.GetValue<int>("QueryHistoryMaxItems", 200);
            QueryHistoryShowTraceColumns = RegistryHelper.GetValue<bool>("QueryHistoryShowTraceColumns", true);
            QueryEndEventTimeout = RegistryHelper.GetValue<int>(nameof(QueryEndEventTimeout), 5);
            DaxFormatterRequestTimeout = RegistryHelper.GetValue<int>(nameof(DaxFormatterRequestTimeout), 10);
            TraceStartupTimeout = RegistryHelper.GetValue<int>(nameof(TraceStartupTimeout), 30);
            DefaultSeparator = (DelimiterType)RegistryHelper.GetValue<int>(nameof(DefaultSeparator), (int)DelimiterType.Comma);
            TraceDirectQuery = RegistryHelper.GetValue<bool>("TraceDirectQuery", false);
            ShowPreReleaseNotifcations = RegistryHelper.GetValue<bool>("ShowPreReleaseNotifcations", false);
            ShowTooltipBasicStats = RegistryHelper.GetValue<bool>("ShowTooltipBasicStats", true);
            ShowTooltipSampleData = RegistryHelper.GetValue<bool>("ShowTooltipSampleData", true);
            ExcludeHeadersWhenCopyingResults = RegistryHelper.GetValue<bool>("ExcludeHeadersWhenCopyingResults", true);
            DefaultDaxFormatStyle = (DaxFormatStyle)RegistryHelper.GetValue<int>(nameof(DefaultDaxFormatStyle),(int)DaxFormatStyle.LongLine);

            // Preview Feature Toggles
            ShowExportMetrics = RegistryHelper.GetValue<bool>("ShowExportMetrics", false);
            ShowExternalTools = RegistryHelper.GetValue<bool>("ShowExternalTools", false);
            ShowAggregationRewritesInAllQueries = RegistryHelper.GetValue<bool>("ShowAggregationRewritesInAllQueries", false);
            ResultAutoFormat = RegistryHelper.GetValue<bool>("ResultAutoFormat", false);
        }

        public string EditorFontFamily { get { return _selectedEditorFontFamily; } 
            set{
                if (_selectedEditorFontFamily == value) return;
                _selectedEditorFontFamily = value;
                NotifyOfPropertyChange(() => EditorFontFamily);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                RegistryHelper.SetValueAsync<string>("EditorFontFamily", value);

            } 
        }

        public double EditorFontSize { get { return _editorFontSize; } 
            set {
                if (_editorFontSize == value) return;
                _editorFontSize = value;
                NotifyOfPropertyChange(() => EditorFontSize);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                RegistryHelper.SetValueAsync<double>("EditorFontSize", value);
            } 
        }

        public void ResetEditorFont()
        {
            EditorFontFamily = DefaultEditorFontFamily;
            EditorFontSize = DefaultEditorFontSize;
        }

        public void ResetResultsFont()
        {
            ResultFontFamily = DefaultResultsFontFamily;
            ResultFontSize = DefaultResultsFontSize;
        }

        public string ResultFontFamily {
            get { return _selectedResultFontFamily; }
            set {
                if (_selectedResultFontFamily == value) return;
                _selectedResultFontFamily = value;
                NotifyOfPropertyChange(() => ResultFontFamily);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                RegistryHelper.SetValueAsync<string>("ResultFontFamily", value);

            }
        }

        public double ResultFontSize {
            get { return _resultFontSize; }
            set {
                if (_resultFontSize == value) return;
                _resultFontSize = value;
                NotifyOfPropertyChange(() => ResultFontSize);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                RegistryHelper.SetValueAsync<double>("ResultFontSize", value);
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
                WebRequestFactory.ResetProxy();
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
                WebRequestFactory.ResetProxy();
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
                WebRequestFactory.ResetProxy();
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
                WebRequestFactory.ResetProxy();
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

        public DaxFormatStyle DefaultDaxFormatStyle {
            get {
                return _defaultDaxFormatStyle;
            }

            set {
                if (_defaultDaxFormatStyle == value) return;
                _defaultDaxFormatStyle = value;
                NotifyOfPropertyChange(() => DefaultDaxFormatStyle);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                RegistryHelper.SetValueAsync<int>(nameof(DefaultDaxFormatStyle), (int)value);
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

        public IEnumerable<DaxFormatStyle> DaxFormatStyles {
            get {
                var items = Enum.GetValues(typeof(DaxFormatStyle)).Cast<DaxFormatStyle>();
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

        public bool ShowTooltipBasicStats
        {
            get { return _showTooltipBasicStats; }
            set
            {
                if (_showTooltipBasicStats == value) return;
                _showTooltipBasicStats = value;
                NotifyOfPropertyChange(() => ShowTooltipBasicStats);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                RegistryHelper.SetValueAsync<bool>("ShowTooltipBasicStats", value);
            }
        }

        public bool ShowTooltipSampleData
        {
            get { return _showTooltipSampleData; }
            set
            {
                if (_showTooltipSampleData == value) return;
                _showTooltipSampleData = value;
                NotifyOfPropertyChange(() => ShowTooltipSampleData);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                RegistryHelper.SetValueAsync<bool>("ShowTooltipSampleData", value);
            }
        }

        private bool _canPublishDaxFunctions = true; 
        public bool CanPublishDaxFunctions
        {
            get
            {
                return _canPublishDaxFunctions;
            }

            set
            {
                _canPublishDaxFunctions = value;
                NotifyOfPropertyChange(() => CanPublishDaxFunctions);
            }
        }

        private bool _excludeHeadersWhenCopyingResults = false;
        public bool ExcludeHeadersWhenCopyingResults
        {
            get
            {
                return _excludeHeadersWhenCopyingResults;
            }

            set
            {
                _excludeHeadersWhenCopyingResults = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                RegistryHelper.SetValueAsync<bool>("ExcludeHeadersWhenCopyingResults", value);
                NotifyOfPropertyChange(() => ExcludeHeadersWhenCopyingResults);
            }
        }

        
        private int _traceStartupTimeout = 30;
        public int TraceStartupTimeout { get => _traceStartupTimeout; set {
                _traceStartupTimeout = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                RegistryHelper.SetValueAsync<int>("TraceStartupTimeout", value);
                NotifyOfPropertyChange(() => TraceStartupTimeout);
            }
        }

        #region "Preview Toggles"

        // Preview Feature Toggles

        private bool _showExportMetrics = false;
        public bool ShowExportMetrics
        {
            get
            {
                return _showExportMetrics;
            }

            set
            {
                _showExportMetrics = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                RegistryHelper.SetValueAsync<bool>("ShowExportMetrics", value);
                NotifyOfPropertyChange(() => ShowExportMetrics);
            }
        }

        private bool _showExternalTools = false;
        public bool ShowExternalTools { get => _showExternalTools;
            set {
                _showExternalTools = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                RegistryHelper.SetValueAsync<bool>("ShowExternalTools", value);
                NotifyOfPropertyChange(() => ShowExternalTools);
            }
        }

        private bool _showExportAllData = false;
        public bool ShowExportAllData { get => _showExportAllData;
            set {
                _showExportAllData = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                RegistryHelper.SetValueAsync<bool>("ShowExportAllData", value);
                NotifyOfPropertyChange(() => ShowExportAllData);
            }
        }

        private bool _showAggregationRewritesInAllQueries = false;
        public bool ShowAggregationRewritesInAllQueries { get => _showAggregationRewritesInAllQueries;
            set
            {
                _showAggregationRewritesInAllQueries = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                RegistryHelper.SetValueAsync<bool>("ShowAggregationRewritesInAllQueries", value);
                NotifyOfPropertyChange(() => ShowAggregationRewritesInAllQueries);
            }
        }

        private bool _ResultAutoFormat = false;
        public bool ResultAutoFormat {
            get => _ResultAutoFormat;
            set {
                _ResultAutoFormat = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                RegistryHelper.SetValueAsync<bool>("ResultAutoFormat", value);
                NotifyOfPropertyChange(() => ResultAutoFormat);
            }
        }
        #endregion

        public void ExportDaxFunctions()
        {
            _eventAggregator.PublishOnUIThread(new ExportDaxFunctionsEvent());
        }

        public void PublishDaxFunctions()
        {
            _eventAggregator.PublishOnUIThread(new ExportDaxFunctionsEvent(true));
        }
    }
}
