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
using Newtonsoft.Json;
using System.ComponentModel;
using System.Runtime.Serialization;

namespace DaxStudio.UI.ViewModels
{
    [DataContract]
    [Export(typeof(IGlobalOptions))]
    [Export(typeof(OptionsViewModel))]
    
    public class OptionsViewModel:Screen, IGlobalOptions
    {
        private const string DefaultEditorFontFamily = "Lucida Console";
        private const int DefaultEditorFontSize = 11;
        private const string DefaultResultsFontFamily = "Segoe UI";
        private const int DefaultResultsFontSize = 11;
        private const string DefaultWindowPosition = @"﻿﻿<?xml version=""1.0"" encoding=""utf-8""?><WINDOWPLACEMENT xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""><length>44</length><flags>0</flags><showCmd>1</showCmd><minPosition><X>0</X><Y>0</Y></minPosition><maxPosition><X>-1</X><Y>-1</Y></maxPosition><normalPosition><Left>5</Left><Top>5</Top><Right>1125</Right><Bottom>725</Bottom></normalPosition></WINDOWPLACEMENT>";

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
        private bool _isInitializing = false;


        [ImportingConstructor]
        public OptionsViewModel(IEventAggregator eventAggregator, ISettingProvider settingProvider)
        {
            _eventAggregator = eventAggregator;
            SettingProvider = settingProvider;

            

            // Iterate through each property
            /*
            _isInitializing = true; 
            foreach (PropertyDescriptor prop in TypeDescriptor.GetProperties(this))
            {
                // Set default value if DefaultValueAttribute is present
                DefaultValueAttribute attr = prop.Attributes[typeof(DefaultValueAttribute)]
                                                                 as DefaultValueAttribute;
                if (attr != null)
                    prop.SetValue(this, attr.Value);
            }
            _isInitializing = false;
            */

            // Set Default Values
            /*
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
            Theme = SettingProvider.GetValue<string>(nameof(Theme), "Light");
            ResultAutoFormat = SettingProvider.GetValue<bool>("ResultAutoFormat", false);
            CodeCompletionWindowWidthIncrease = SettingProvider.GetValue<int>(nameof(CodeCompletionWindowWidthIncrease), 100);
            KeepMetadataSearchOpen = SettingProvider.GetValue<bool>(nameof(KeepMetadataSearchOpen), false);
            AutoRefreshMetadataLocalMachine = SettingProvider.GetValue<bool>("AutoRefreshMetadataLocalMachine", true);
            AutoRefreshMetadataLocalNetwork = SettingProvider.GetValue<bool>("AutoRefreshMetadataLocalNetwork", true);
            AutoRefreshMetadataCloud = SettingProvider.GetValue<bool>("AutoRefreshMetadataCloud", false);
            ShowHiddenMetadata = SettingProvider.GetValue<bool>("ShowHiddenMetadata", true);
            SetClearCacheAsDefaultRunStyle = SettingProvider.GetValue<bool>("SetClearCacheAsDefaultRunStyle", false);
            SortFoldersFirstInMetadata = SettingProvider.GetValue<bool>(nameof(SortFoldersFirstInMetadata), true);
            WindowPosition = SettingProvider.GetValue<string>(nameof(WindowPosition), DefaultWindowPosition);
            */
        }

        public void Initialize()
        {

            _isInitializing = true;
            SettingProvider.Initialize(this);
            _isInitializing = false;
        }

        [JsonIgnore]
        public ISettingProvider SettingProvider { get; }

        [DataMember]
        [DefaultValue(DefaultEditorFontFamily)]
        public string EditorFontFamily { get { return _selectedEditorFontFamily; } 
            set{
                if (_selectedEditorFontFamily == value) return;
                _selectedEditorFontFamily = value;
                NotifyOfPropertyChange(() => EditorFontFamily);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<string>("EditorFontFamily", value , _isInitializing);

            } 
        }

        [JsonIgnore]
        public double EditorFontSizePx => (double)new FontSizeConverter().ConvertFrom($"{EditorFontSize}pt");

        [DataMember]
        [DefaultValue(DefaultEditorFontSize)]
        public double EditorFontSize { get { return _editorFontSize; } 
            set {
                if (_editorFontSize == value) return;
                _editorFontSize = value;
                NotifyOfPropertyChange(() => EditorFontSize);
                NotifyOfPropertyChange(() => EditorFontSizePx);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<double>("EditorFontSize", value, _isInitializing);
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

        [DataMember]
        [DefaultValue(DefaultResultsFontFamily)]
        public string ResultFontFamily {
            get { return _selectedResultFontFamily; }
            set {
                if (_selectedResultFontFamily == value) return;
                _selectedResultFontFamily = value;
                NotifyOfPropertyChange(() => ResultFontFamily);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<string>("ResultFontFamily", value, _isInitializing);

            }
        }

        [JsonIgnore]
        public double ResultFontSizePx => (double)new FontSizeConverter().ConvertFrom($"{ResultFontSize}pt");

        [DataMember]
        [DefaultValue(DefaultResultsFontSize)]
        public double ResultFontSize {
            get { return _resultFontSize; }
            set {
                if (_resultFontSize == value) return;
                _resultFontSize = value;
                NotifyOfPropertyChange(() => ResultFontSize);
                NotifyOfPropertyChange(() => ResultFontSizePx);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<double>("ResultFontSize", value, _isInitializing);
            }
        }

        [DataMember]
        [DefaultValue(true)]
        public bool EditorShowLineNumbers { get { return _showLineNumbers; }
            set
            {
                if (_showLineNumbers == value) return;
                _showLineNumbers = value;
                NotifyOfPropertyChange(() => EditorShowLineNumbers);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<bool>(nameof(EditorShowLineNumbers), value, _isInitializing);
            }
        }

        [DataMember]
        [DefaultValue(true)]
        public bool EditorEnableIntellisense
        {
            get { return _enableIntellisense; }
            set
            {
                if (_enableIntellisense == value) return;
                _enableIntellisense = value;
                NotifyOfPropertyChange(() => EditorEnableIntellisense);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<bool>(nameof(EditorEnableIntellisense), value, _isInitializing);
            }
        }

        [DataMember,DefaultValue(false)]
        public bool TraceDirectQuery {
            get { return _traceDirectQuery; }
            set {
                if (_traceDirectQuery == value) return;
                _traceDirectQuery = value;
                NotifyOfPropertyChange(() => TraceDirectQuery);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<bool>("TraceDirectQuery", value, _isInitializing);
            }
        }
        #region Http Proxy properties

        [DataMember,DefaultValue(true)]
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
                SettingProvider.SetValueAsync<bool>("ProxyUseSystem", value, _isInitializing);
                WebRequestFactory.ResetProxy();
            }
        }

        [JsonIgnore]
        public bool ProxyDontUseSystem
        {
            get { return !_proxyUseSystem; }
        }

        [DataMember,DefaultValue("")]
        public string ProxyAddress
        {
            get { return _proxyAddress; }
            set
            {
                if (_proxyAddress == value) return;
                _proxyAddress = value;
                NotifyOfPropertyChange(() => ProxyAddress);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<string>("ProxyAddress", value, _isInitializing);
                WebRequestFactory.ResetProxy();
            }
        }

        [DataMember, DefaultValue("")]
        public string ProxyUser
        {
            get { return _proxyUser; }
            set
            {
                if (_proxyUser == value) return;
                _proxyUser = value;
                NotifyOfPropertyChange(() => ProxyUser);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<string>("ProxyUser", value, _isInitializing);
                WebRequestFactory.ResetProxy();
            }
        }

        [DefaultValue("")]
        [JsonIgnore]
        public string ProxyPassword
        {
            get { return _proxyPassword; }
            set
            {
                if (_proxyPassword == value) return;
                _proxyPassword = value;
                NotifyOfPropertyChange(() => ProxyPassword);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<string>("ProxyPassword", value.Encrypt(), _isInitializing);
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

        [DataMember, DefaultValue("")]
        public SecureString ProxySecurePassword
        {
            get { return _proxySecurePassword; }
            set
            {
                if (_proxySecurePassword == value) return;
                _proxySecurePassword = value;
                NotifyOfPropertyChange(() => ProxyPassword);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<string>("ProxyPassword", value.GetInsecureString().Encrypt(), _isInitializing);
            }
        }

        #endregion



        [DataMember, DefaultValue(200)]
        public int QueryHistoryMaxItems { get { return _maxQueryHistory; }
            set
            {
                if (_maxQueryHistory == value) return;
                _maxQueryHistory = value;
                NotifyOfPropertyChange(() => QueryHistoryMaxItems);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<int>("QueryHistoryMaxItems", value, _isInitializing);
            }

        }

        [DataMember, DefaultValue(true)]
        public bool QueryHistoryShowTraceColumns
        {
            get { return _queryHistoryShowTraceColumns; }
            set
            {
                if (_queryHistoryShowTraceColumns == value) return;
                _queryHistoryShowTraceColumns = value;
                NotifyOfPropertyChange(() => QueryHistoryShowTraceColumns);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<bool>("QueryHistoryShowTraceColumns", value, _isInitializing);
            }

        }

        [DataMember, DefaultValue(30)]
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
                SettingProvider.SetValueAsync<int>(nameof(QueryEndEventTimeout), value, _isInitializing);
            }
        }

        [DataMember, DefaultValue(10)]
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
                SettingProvider.SetValueAsync<int>(nameof(DaxFormatterRequestTimeout), value, _isInitializing);
            }
        }

        [DataMember, DefaultValue(DelimiterType.Comma)]
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
                SettingProvider.SetValueAsync<int>(nameof(DefaultSeparator), (int)value, _isInitializing);
            }
        }

        [DataMember, DefaultValue(DaxFormatStyle.LongLine)]
        public DaxFormatStyle DefaultDaxFormatStyle {
            get {
                return _defaultDaxFormatStyle;
            }

            set {
                if (_defaultDaxFormatStyle == value) return;
                _defaultDaxFormatStyle = value;
                NotifyOfPropertyChange(() => DefaultDaxFormatStyle);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<int>(nameof(DefaultDaxFormatStyle), (int)value, _isInitializing);
            }
        }

        [JsonIgnore]
        public IEnumerable<DelimiterType> SeparatorTypes
        {
            get {
                var items = Enum.GetValues(typeof(DelimiterType)).Cast<DelimiterType>()
                                .Where(e => e != DelimiterType.Unknown);
                return items;
            }
        }

        [JsonIgnore]
        public IEnumerable<DaxFormatStyle> DaxFormatStyles {
            get {
                var items = Enum.GetValues(typeof(DaxFormatStyle)).Cast<DaxFormatStyle>();
                return items;
            }
        }

        [DataMember, DefaultValue(false)]
        public bool ShowPreReleaseNotifcations {
            get { return _showPreReleaseNotifcations; }
            set
            {
                if (_showPreReleaseNotifcations == value) return;
                _showPreReleaseNotifcations = value;
                NotifyOfPropertyChange(() => ShowPreReleaseNotifcations);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<bool>("ShowPreReleaseNotifcations", value, _isInitializing);
            }
        }

        [DataMember, DefaultValue(true)]
        public bool ShowTooltipBasicStats
        {
            get { return _showTooltipBasicStats; }
            set
            {
                if (_showTooltipBasicStats == value) return;
                _showTooltipBasicStats = value;
                NotifyOfPropertyChange(() => ShowTooltipBasicStats);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<bool>("ShowTooltipBasicStats", value, _isInitializing);
            }
        }

        [DataMember, DefaultValue(true)]
        public bool ShowTooltipSampleData
        {
            get { return _showTooltipSampleData; }
            set
            {
                if (_showTooltipSampleData == value) return;
                _showTooltipSampleData = value;
                NotifyOfPropertyChange(() => ShowTooltipSampleData);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<bool>("ShowTooltipSampleData", value, _isInitializing);
            }
        }

        private bool _canPublishDaxFunctions = true;
        [JsonIgnore]
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
        [DataMember, DefaultValue(true)]
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
                SettingProvider.SetValueAsync<bool>("ExcludeHeadersWhenCopyingResults", value, _isInitializing);
                NotifyOfPropertyChange(() => ExcludeHeadersWhenCopyingResults);
            }
        }

        
        private int _traceStartupTimeout = 30;
        [DataMember, DefaultValue(30)]
        public int TraceStartupTimeout { get => _traceStartupTimeout; set {
                _traceStartupTimeout = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<int>("TraceStartupTimeout", value, _isInitializing);
                NotifyOfPropertyChange(() => TraceStartupTimeout);
            }
        }

        #region "Preview Toggles"

        // Preview Feature Toggles

        private bool _showExportMetrics = false;
        [DataMember, DefaultValue(false)]
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
                SettingProvider.SetValueAsync<bool>("ShowExportMetrics", value, _isInitializing);
                NotifyOfPropertyChange(() => ShowExportMetrics);
            }
        }

        private bool _showExternalTools = false;
        [DataMember, DefaultValue(false)]
        public bool ShowExternalTools { get => _showExternalTools;
            set {
                _showExternalTools = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<bool>("ShowExternalTools", value, _isInitializing);
                NotifyOfPropertyChange(() => ShowExternalTools);
            }
        }

        private bool _showExportAllData = false;
        [DataMember, DefaultValue(false)]
        public bool ShowExportAllData { get => _showExportAllData;
            set {
                _showExportAllData = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<bool>("ShowExportAllData", value, _isInitializing);
                NotifyOfPropertyChange(() => ShowExportAllData);
            }
        }

        private bool _vpaxIncludeTom = false;
        [DataMember, DefaultValue(false)]
        public bool VpaxIncludeTom {
            get => _vpaxIncludeTom;
            set {
                _vpaxIncludeTom = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<bool>("VpaxIncludeTom", value, _isInitializing);
                NotifyOfPropertyChange(() => VpaxIncludeTom);
            }
        }

        private bool _scaleResultsFontWithEditor = true;

        private string _theme = "Light";
        [DataMember, DefaultValue("Light")]
        public string Theme
        {
            get { return _theme; }
            set
            {
                if (_theme == value) return;
                _theme = value;
                NotifyOfPropertyChange(() => Theme);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<string>("Theme", value, _isInitializing);

            }
        }

        private bool _ResultAutoFormat = false;
        private int _codeCompletionWindowWidthIncrease;
        private bool _keepMetadataSearchOpen;
        [DataMember, DefaultValue(false)]
        public bool ResultAutoFormat {
            get => _ResultAutoFormat;
            set {
                _ResultAutoFormat = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<bool>("ResultAutoFormat", value, _isInitializing);
                NotifyOfPropertyChange(() => ResultAutoFormat);
            }
        }

        [DataMember, DefaultValue(true)]
        public bool ScaleResultsFontWithEditor { get => _scaleResultsFontWithEditor;
            set {
                _scaleResultsFontWithEditor = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<bool>("ScaleResultsFontWithEditor", value, _isInitializing);
                NotifyOfPropertyChange(() => ScaleResultsFontWithEditor);
            } }

        [DataMember, DefaultValue(100)]
        public int CodeCompletionWindowWidthIncrease { get => _codeCompletionWindowWidthIncrease;
            set {
                if (value < 100) value = 100; // value should not be less than 100% of the default size
                if (value > 300) value = 300; // value cannot be greater than 300% of the default size
                _codeCompletionWindowWidthIncrease = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<int>("CodeCompletionWindowWidthIncrease", value, _isInitializing);
                NotifyOfPropertyChange(() => CodeCompletionWindowWidthIncrease);
            }
        }

        [DataMember, DefaultValue(false)]
        public bool KeepMetadataSearchOpen { get => _keepMetadataSearchOpen;
            set {
                _keepMetadataSearchOpen = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<bool>("KeepMetadataSearchOpen", value, _isInitializing);
                NotifyOfPropertyChange(() => KeepMetadataSearchOpen);
            }
        }

        private bool _autoRefreshMetadataLocalMachine = true;
        [DataMember, DefaultValue(true)]
        public bool AutoRefreshMetadataLocalMachine { get => _autoRefreshMetadataLocalMachine;
            set {
                _autoRefreshMetadataLocalMachine = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<bool>("AutoRefreshMetadataLocalMachine", value, _isInitializing);
                NotifyOfPropertyChange(() => AutoRefreshMetadataLocalMachine);
            }
        }

        private bool _autoRefreshMetadataLocalNetwork = true;
        [DataMember, DefaultValue(true)]
        public bool AutoRefreshMetadataLocalNetwork { get => _autoRefreshMetadataLocalNetwork;
            set {
                _autoRefreshMetadataLocalNetwork = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<bool>("AutoRefreshMetadataLocalNetwork", value, _isInitializing);
                NotifyOfPropertyChange(() => AutoRefreshMetadataLocalNetwork);
            }
        }

        private bool _autoRefreshMetadataCloud = true;
        [DataMember, DefaultValue(false)]
        public bool AutoRefreshMetadataCloud { get => _autoRefreshMetadataCloud;
            set {
                _autoRefreshMetadataCloud = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<bool>("AutoRefreshMetadataCloud", value, _isInitializing);
                NotifyOfPropertyChange(() => AutoRefreshMetadataCloud);
            }
        }

        private bool _showHiddenMetadata = true;
        [DataMember, DefaultValue(true)]
        public bool ShowHiddenMetadata { get => _showHiddenMetadata;
                set {
                _showHiddenMetadata = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<bool>("ShowHiddenMetadata", value, _isInitializing);
                NotifyOfPropertyChange(() => ShowHiddenMetadata);
            } }

        private bool _setClearCacheAndRunAsDefaultRunStyle = false;
        [DataMember, DefaultValue(false)]
        public bool SetClearCacheAsDefaultRunStyle { get => _setClearCacheAndRunAsDefaultRunStyle;
            set
            {
                _setClearCacheAndRunAsDefaultRunStyle = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<bool>(nameof(SetClearCacheAsDefaultRunStyle), value, _isInitializing);
                NotifyOfPropertyChange(() => SetClearCacheAsDefaultRunStyle);
            }
        }

        private bool _sortFoldersFirstInMetadata = false;
        [DataMember, DefaultValue(true)]
        public bool SortFoldersFirstInMetadata { get => _sortFoldersFirstInMetadata;
            set {
                _sortFoldersFirstInMetadata = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValueAsync<bool>(nameof(SortFoldersFirstInMetadata), value, _isInitializing);
                NotifyOfPropertyChange(() => SortFoldersFirstInMetadata);
            }
        }


        private string _windowPosition = string.Empty;
        [DataMember, DefaultValue(DefaultWindowPosition)]
        public string WindowPosition {
            get { return _windowPosition; }
            set {
                _windowPosition = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValueAsync<string>(nameof(WindowPosition), value, _isInitializing);
                NotifyOfPropertyChange(() => WindowPosition);
            }
        }

        private Version _dismissedVersion = new Version();
        [DataMember, DefaultValue("0.0.0.0")]
        public Version DismissedVersion
        {
            get { return _dismissedVersion; }
            set
            {
                _dismissedVersion = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValueAsync<Version>(nameof(DismissedVersion), value, _isInitializing);
                NotifyOfPropertyChange(() => DismissedVersion);
            }
        }

        private DateTime _lastVersionCheck = DateTime.MinValue;
        [DataMember, DefaultValue("1900-01-01 00:00:00Z")]
        public DateTime LastVersionCheckUTC
        {
            get { return _lastVersionCheck; }
            set
            {
                _lastVersionCheck = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValueAsync<DateTime>(nameof(LastVersionCheckUTC), value, _isInitializing);
                NotifyOfPropertyChange(() => LastVersionCheckUTC);
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
