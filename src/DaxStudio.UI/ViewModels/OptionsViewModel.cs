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
using DaxStudio.UI.Interfaces;

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
        public OptionsViewModel(IEventAggregator eventAggregator, ISettingProvider settingProvider)
        {
            _eventAggregator = eventAggregator;
            SettingProvider = settingProvider;
            
            EditorFontFamily = SettingProvider.GetValue<string>("EditorFontFamily", DefaultEditorFontFamily);
            EditorFontSize = SettingProvider.GetValue<double>("EditorFontSize", DefaultEditorFontSize);
            ResultFontFamily = SettingProvider.GetValue<string>("ResultFontFamily", DefaultResultsFontFamily);
            ResultFontSize = SettingProvider.GetValue<double>("ResultFontSize", DefaultResultsFontSize);
            EditorShowLineNumbers = SettingProvider.GetValue<bool>("EditorShowLineNumbers", true);
            EditorEnableIntellisense = SettingProvider.GetValue<bool>("EditorEnableIntellisense", true);
            ProxyUseSystem = SettingProvider.GetValue<bool>("ProxyUseSystem", true);
            ProxyAddress = SettingProvider.GetValue<string>("ProxyAddress", "");
            ProxyUser = SettingProvider.GetValue<string>("ProxyUser", "");
            ProxyPassword = SettingProvider.GetValue<string>("ProxyPassword", "").Decrypt();
            QueryHistoryMaxItems = SettingProvider.GetValue<int>("QueryHistoryMaxItems", 200);
            QueryHistoryShowTraceColumns = SettingProvider.GetValue<bool>("QueryHistoryShowTraceColumns", true);
            QueryEndEventTimeout = SettingProvider.GetValue<int>(nameof(QueryEndEventTimeout), 15);
            DaxFormatterRequestTimeout = SettingProvider.GetValue<int>(nameof(DaxFormatterRequestTimeout), 10);
            TraceStartupTimeout = SettingProvider.GetValue<int>(nameof(TraceStartupTimeout), 30);
            DefaultSeparator = (DelimiterType)SettingProvider.GetValue<int>(nameof(DefaultSeparator), (int)DelimiterType.Comma);
            TraceDirectQuery = SettingProvider.GetValue<bool>("TraceDirectQuery", false);
            ShowPreReleaseNotifcations = SettingProvider.GetValue<bool>("ShowPreReleaseNotifcations", false);
            ShowTooltipBasicStats = SettingProvider.GetValue<bool>("ShowTooltipBasicStats", true);
            ShowTooltipSampleData = SettingProvider.GetValue<bool>("ShowTooltipSampleData", true);
            ExcludeHeadersWhenCopyingResults = SettingProvider.GetValue<bool>("ExcludeHeadersWhenCopyingResults", true);
            DefaultDaxFormatStyle = (DaxFormatStyle)SettingProvider.GetValue<int>(nameof(DefaultDaxFormatStyle),(int)DaxFormatStyle.LongLine);
            ScaleResultsFontWithEditor = SettingProvider.GetValue<bool>("ScaleResultsFontWithEditor", true);
            // Preview Feature Toggles
            ShowExportMetrics = SettingProvider.GetValue<bool>(nameof(ShowExportMetrics), false);
            ShowExternalTools = SettingProvider.GetValue<bool>(nameof(ShowExternalTools), false);
            ShowExportAllData = SettingProvider.GetValue<bool>(nameof(ShowExportAllData), false);
            Theme = SettingProvider.GetValue<string>("Theme", "Light");
            ResultAutoFormat = SettingProvider.GetValue<bool>("ResultAutoFormat", false);
            CodeCompletionWindowWidthIncrease = SettingProvider.GetValue<int>("CodeCompletionWindowWidthIncrease", 100);
            KeepMetadataSearchOpen = SettingProvider.GetValue<bool>("KeepMetadataSearchOpen", false);
            AutoRefreshMetadataLocalMachine = SettingProvider.GetValue<bool>("AutoRefreshMetadataLocalMachine", true);
            AutoRefreshMetadataLocalNetwork = SettingProvider.GetValue<bool>("AutoRefreshMetadataLocalNetwork", true);
            AutoRefreshMetadataCloud = SettingProvider.GetValue<bool>("AutoRefreshMetadataCloud", false);
            ShowHiddenMetadata = SettingProvider.GetValue<bool>("ShowHiddenMetadata", true);
            SetClearCacheAsDefaultRunStyle = SettingProvider.GetValue<bool>("SetClearCacheAsDefaultRunStyle", false);
        }

        public ISettingProvider SettingProvider { get; }

        public string EditorFontFamily { get { return _selectedEditorFontFamily; } 
            set{
                if (_selectedEditorFontFamily == value) return;
                _selectedEditorFontFamily = value;
                NotifyOfPropertyChange(() => EditorFontFamily);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<string>("EditorFontFamily", value);

            } 
        }

        public double EditorFontSizePx => (double)new FontSizeConverter().ConvertFrom($"{EditorFontSize}pt");

        public double EditorFontSize { private get { return _editorFontSize; } 
            set {
                if (_editorFontSize == value) return;
                _editorFontSize = value;
                NotifyOfPropertyChange(() => EditorFontSize);
                NotifyOfPropertyChange(() => EditorFontSizePx);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<double>("EditorFontSize", value);
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
                SettingProvider.SetValueAsync<string>("ResultFontFamily", value);

            }
        }

        public double ResultFontSizePx => (double)new FontSizeConverter().ConvertFrom($"{ResultFontSize}pt");
        public double ResultFontSize {
            get { return _resultFontSize; }
            set {
                if (_resultFontSize == value) return;
                _resultFontSize = value;
                NotifyOfPropertyChange(() => ResultFontSizePx);
                NotifyOfPropertyChange(() => ResultFontSizePx);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<double>("ResultFontSize", value);
            }
        }

        public bool EditorShowLineNumbers { get { return _showLineNumbers; }
            set
            {
                if (_showLineNumbers == value) return;
                _showLineNumbers = value;
                NotifyOfPropertyChange(() => EditorShowLineNumbers);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<bool>("EditorShowLineNumbers", value);
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
                SettingProvider.SetValueAsync<bool>("EditorEnableIntellisense", value);
            }
        }
        public bool TraceDirectQuery {
            get { return _traceDirectQuery; }
            set {
                if (_traceDirectQuery == value) return;
                _traceDirectQuery = value;
                NotifyOfPropertyChange(() => TraceDirectQuery);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<bool>("TraceDirectQuery", value);
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
                SettingProvider.SetValueAsync<bool>("ProxyUseSystem", value);
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
                SettingProvider.SetValueAsync<string>("ProxyAddress", value);
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
                SettingProvider.SetValueAsync<string>("ProxyUser", value);
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
                SettingProvider.SetValueAsync<string>("ProxyPassword", value.Encrypt());
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
                SettingProvider.SetValueAsync<string>("ProxyPassword", value.GetInsecureString().Encrypt());
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
                SettingProvider.SetValueAsync<int>("QueryHistoryMaxItems", value);
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
                SettingProvider.SetValueAsync<bool>("QueryHistoryShowTraceColumns", value);
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
                SettingProvider.SetValueAsync<int>(nameof(QueryEndEventTimeout), value);
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
                SettingProvider.SetValueAsync<int>(nameof(DaxFormatterRequestTimeout), value);
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
                SettingProvider.SetValueAsync<int>(nameof(DefaultSeparator), (int)value);
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
                SettingProvider.SetValueAsync<int>(nameof(DefaultDaxFormatStyle), (int)value);
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
                SettingProvider.SetValueAsync<bool>("ShowPreReleaseNotifcations", value);
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
                SettingProvider.SetValueAsync<bool>("ShowTooltipBasicStats", value);
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
                SettingProvider.SetValueAsync<bool>("ShowTooltipSampleData", value);
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
                SettingProvider.SetValueAsync<bool>("ExcludeHeadersWhenCopyingResults", value);
                NotifyOfPropertyChange(() => ExcludeHeadersWhenCopyingResults);
            }
        }

        
        private int _traceStartupTimeout = 30;
        public int TraceStartupTimeout { get => _traceStartupTimeout; set {
                _traceStartupTimeout = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<int>("TraceStartupTimeout", value);
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
                SettingProvider.SetValueAsync<bool>("ShowExportMetrics", value);
                NotifyOfPropertyChange(() => ShowExportMetrics);
            }
        }

        private bool _showExternalTools = false;
        public bool ShowExternalTools { get => _showExternalTools;
            set {
                _showExternalTools = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<bool>("ShowExternalTools", value);
                NotifyOfPropertyChange(() => ShowExternalTools);
            }
        }

        private bool _showExportAllData = false;
        public bool ShowExportAllData { get => _showExportAllData;
            set {
                _showExportAllData = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<bool>("ShowExportAllData", value);
                NotifyOfPropertyChange(() => ShowExportAllData);
            }
        }

        private bool _scaleResultsFontWithEditor = true;

        private string _theme = "Light";
        public string Theme
        {
            get { return _theme; }
            set
            {
                if (_theme == value) return;
                _theme = value;
                NotifyOfPropertyChange(() => Theme);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<string>("Theme", value);

            }
        }

        private bool _ResultAutoFormat = false;
        private int _codeCompletionWindowWidthIncrease;
        private bool _keepMetadataSearchOpen;

        public bool ResultAutoFormat {
            get => _ResultAutoFormat;
            set {
                _ResultAutoFormat = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<bool>("ResultAutoFormat", value);
                NotifyOfPropertyChange(() => ResultAutoFormat);
            }
        }

        public bool ScaleResultsFontWithEditor { get => _scaleResultsFontWithEditor;
            set {
                _scaleResultsFontWithEditor = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<bool>("ScaleResultsFontWithEditor", value);
                NotifyOfPropertyChange(() => ScaleResultsFontWithEditor);
            } }

        public int CodeCompletionWindowWidthIncrease { get => _codeCompletionWindowWidthIncrease;
            set {
                if (value < 100) value = 100; // value should not be less than 100% of the default size
                if (value > 300) value = 300; // value cannot be greater than 300% of the default size
                _codeCompletionWindowWidthIncrease = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<int>("CodeCompletionWindowWidthIncrease", value);
                NotifyOfPropertyChange(() => CodeCompletionWindowWidthIncrease);
            }
        }

        public bool KeepMetadataSearchOpen { get => _keepMetadataSearchOpen;
            set {
                _keepMetadataSearchOpen = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<bool>("KeepMetadataSearchOpen", value);
                NotifyOfPropertyChange(() => KeepMetadataSearchOpen);
            }
        }

        private bool _autoRefreshMetadataLocalMachine = true;
        public bool AutoRefreshMetadataLocalMachine { get => _autoRefreshMetadataLocalMachine;
            set {
                _autoRefreshMetadataLocalMachine = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<bool>("AutoRefreshMetadataLocalMachine", value);
                NotifyOfPropertyChange(() => AutoRefreshMetadataLocalMachine);
            }
        }

        private bool _autoRefreshMetadataLocalNetwork = true;
        public bool AutoRefreshMetadataLocalNetwork { get => _autoRefreshMetadataLocalNetwork;
            set {
                _autoRefreshMetadataLocalNetwork = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<bool>("AutoRefreshMetadataLocalNetwork", value);
                NotifyOfPropertyChange(() => AutoRefreshMetadataLocalNetwork);
            }
        }

        private bool _autoRefreshMetadataCloud = true;
        public bool AutoRefreshMetadataCloud { get => _autoRefreshMetadataCloud;
            set {
                _autoRefreshMetadataCloud = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<bool>("AutoRefreshMetadataCloud", value);
                NotifyOfPropertyChange(() => AutoRefreshMetadataCloud);
            }
        }

        private bool _showHiddenMetadata = true;
        public bool ShowHiddenMetadata { get => _showHiddenMetadata;
                set {
                _showHiddenMetadata = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<bool>("ShowHiddenMetadata", value);
                NotifyOfPropertyChange(() => ShowHiddenMetadata);
            } }

        private bool _setClearCacheAndRunAsDefaultRunStyle = false;
        public bool SetClearCacheAsDefaultRunStyle { get => _setClearCacheAndRunAsDefaultRunStyle;
            set
            {
                _setClearCacheAndRunAsDefaultRunStyle = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<bool>(nameof(SetClearCacheAsDefaultRunStyle), value);
                NotifyOfPropertyChange(() => SetClearCacheAsDefaultRunStyle);
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
