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
using DaxStudio.UI.Enums;
using System.Collections.ObjectModel;
using DaxStudio.UI.Model;
using DaxStudio.UI.JsonConverters;
using System.Collections.Specialized;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.IO;
using DaxStudio.Interfaces.Attributes;
using DaxStudio.Controls.PropertyGrid;
using System.Reflection;
using System.Drawing;
using System.Threading.Tasks;

namespace DaxStudio.UI.ViewModels
{
    [DataContract]
    [Export(typeof(IGlobalOptions))]
    public class OptionsViewModel : Screen, IGlobalOptions, IDisposable
    {
        private const string DefaultEditorFontFamily = "Lucida Console";
        private const double DefaultEditorFontSize = 11.0;
        private const string DefaultResultsFontFamily = "Segoe UI";
        private const double DefaultResultsFontSize = 11.0;
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

        private readonly IEventAggregator _eventAggregator;

        private DelimiterType _defaultSeparator;
        private DaxFormatStyle _defaultDaxFormatStyle;
        private bool _skipSpaceAfterFunctionName;
        private bool _showPreReleaseNotifications;
        private bool _showTooltipBasicStats;
        private bool _showTooltipSampleData;

        //public event EventHandler OptionsUpdated;
        private bool _isInitializing;


        [ImportingConstructor]
        public OptionsViewModel(IEventAggregator eventAggregator, ISettingProvider settingProvider)
        {
            _eventAggregator = eventAggregator;
            SettingProvider = settingProvider;

        }

        public void Initialize()
        {
            _isInitializing = true;
            SettingProvider.Initialize(this);
            _isInitializing = false;
        }

        [JsonIgnore]
        public ISettingProvider SettingProvider { get; }

        [Category("Editor")]
        [DisplayName("Editor Font Family")]
        [SortOrder(10)]
        [DataMember]
        [DefaultValue(DefaultEditorFontFamily)]
        public string EditorFontFamily { get { return _selectedEditorFontFamily; }
            set {
                if (_selectedEditorFontFamily == value) return;
                _selectedEditorFontFamily = value;
                NotifyOfPropertyChange(() => EditorFontFamily);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValue<string>(nameof(EditorFontFamily), value, _isInitializing);

            }
        }

        [JsonIgnore]
        public double EditorFontSizePx => (double)new FontSizeConverter().ConvertFrom($"{EditorFontSize}pt");

        [Category("Editor")]
        [DisplayName("Editor Font Size")]
        [SortOrder(20)]
        [MinValue(6),MaxValue(120)]
        [DataMember]
        [DefaultValue(DefaultEditorFontSize)]
        public double EditorFontSize { get { return _editorFontSize; }
            set {
                if (_editorFontSize == value) return;
                _editorFontSize = value;
                NotifyOfPropertyChange(() => EditorFontSize);
                NotifyOfPropertyChange(() => EditorFontSizePx);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValue<double>(nameof(EditorFontSize), value, _isInitializing);
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

        [Category("Results")]
        [DisplayName("Results Font Family")]
        [DataMember]
        [DefaultValue(DefaultResultsFontFamily)]
        public string ResultFontFamily {
            get { return _selectedResultFontFamily; }
            set {
                if (_selectedResultFontFamily == value) return;
                _selectedResultFontFamily = value;
                NotifyOfPropertyChange(() => ResultFontFamily);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValue<string>(nameof(ResultFontFamily), value, _isInitializing);

            }
        }

        [JsonIgnore]
        public double ResultFontSizePx => (double)new FontSizeConverter().ConvertFrom($"{ResultFontSize}pt");

        [Category("Results")]
        [DisplayName("Results Font Size")]
        [DataMember]
        [DefaultValue(DefaultResultsFontSize)]
        [MinValue(4.0)]
        [MaxValue(256.0)]
        public double ResultFontSize {
            get { return _resultFontSize; }
            set {
                if (_resultFontSize == value) return;
                _resultFontSize = value;
                NotifyOfPropertyChange(() => ResultFontSize);
                NotifyOfPropertyChange(() => ResultFontSizePx);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValue<double>(nameof(ResultFontSize), value, _isInitializing);
            }
        }

        [Category("Editor")]
        [DisplayName("Show Line Numbers")]
        [SortOrder(30)]
        [DataMember]
        [DefaultValue(true)]
        public bool EditorShowLineNumbers { get { return _showLineNumbers; }
            set
            {
                if (_showLineNumbers == value) return;
                _showLineNumbers = value;
                NotifyOfPropertyChange(() => EditorShowLineNumbers);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValue<bool>(nameof(EditorShowLineNumbers), value, _isInitializing);
            }
        }

        [Category("Editor")]
        [DisplayName("Enable Intellisense")]
        [SortOrder(40)]
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
                SettingProvider.SetValue<bool>(nameof(EditorEnableIntellisense), value, _isInitializing);
            }
        }

        [DisplayName("Legacy DirectQuery Trace")]
        [Category("Trace")]
        [Description("On servers prior to v15 (SSAS 2017) we do not trace DirectQuery events by default in the server timings pane as we have to do expensive client side filtering. Only turn this option on if you explicitly need to trace these events on a v14 or earlier data source and turn off the trace as soon as possible")]
        [DataMember, DefaultValue(false)]
        public bool TraceDirectQuery {
            get { return _traceDirectQuery; }
            set {
                if (_traceDirectQuery == value) return;
                _traceDirectQuery = value;
                NotifyOfPropertyChange(() => TraceDirectQuery);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValue<bool>(nameof(TraceDirectQuery), value, _isInitializing);
            }
        }
        #region Http Proxy properties

        [Category("Proxy")]
        [DisplayName("Use System Proxy")]
        [SortOrder(1)]
        [DataMember, DefaultValue(true)]
        public bool ProxyUseSystem
        {
            get { return _proxyUseSystem; }
            set
            {
                if (_proxyUseSystem == value) return;
                _proxyUseSystem = value;
                NotifyOfPropertyChange(() => ProxyUseSystem);
                NotifyOfPropertyChange(() => ProxyDontUseSystem);
                NotifyOfPropertyChange(() => ProxySecurePasswordEnabled);
                NotifyOfPropertyChange(() => ProxyUserEnabled);
                NotifyOfPropertyChange(() => ProxyAddressEnabled);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValue<bool>(nameof(ProxyUseSystem), value, _isInitializing);
                WebRequestFactory.ResetProxy();
            }
        }

        [JsonIgnore]
        public bool ProxyDontUseSystem
        {
            get { return !_proxyUseSystem; }
        }

        [Category("Proxy")]
        [DisplayName("Proxy Address")]
        [Description("eg. http://myproxy.com:8080")]
        [SortOrder(2)]
        [DataMember, DefaultValue("")]
        public string ProxyAddress
        {
            get => _proxyAddress;
            set
            {
                if (_proxyAddress == value) return;
                _proxyAddress = value;
                NotifyOfPropertyChange(() => ProxyAddress);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValue<string>(nameof(ProxyAddress), value, _isInitializing);
                WebRequestFactory.ResetProxy();
            }
        }

        private bool _neverShowHelpWatermark;
        [Category("Editor")]
        [DisplayName("Show Help Text on Empty Document")]
        [SortOrder(120)]
        [DataMember, DefaultValue(true)]
        public bool ShowHelpWatermark { get => _neverShowHelpWatermark;
            set
            {
                if (_neverShowHelpWatermark == value) return;
                _neverShowHelpWatermark = value;
                NotifyOfPropertyChange(() => ShowHelpWatermark);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValue<bool>(nameof(ShowHelpWatermark), value, _isInitializing);
            }
        }

        [JsonIgnore]
        public bool ProxyAddressEnabled => !ProxyUseSystem;

        [Category("Proxy")]
        [DisplayName("Proxy User")]
        [SortOrder(3)]
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
                SettingProvider.SetValue<string>(nameof(ProxyUser), value, _isInitializing);
                WebRequestFactory.ResetProxy();
            }
        }

        [JsonIgnore]
        public bool ProxyUserEnabled => !ProxyUseSystem;


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
                //SettingProvider.SetValueAsync<string>("ProxyPassword", value.Encrypt(), _isInitializing);
                SetProxySecurePassword(value);
                WebRequestFactory.ResetProxy();
            }
        }

        [JsonIgnore]
        public bool ProxySecurePasswordEnabled { get => !ProxyUseSystem; }

        private void SetProxySecurePassword(string value)
        {
            foreach (char c in value)
            {
                ProxySecurePassword.AppendChar(c);
            }

        }

        [Category("Proxy")]
        [DisplayName("Proxy Password")]
        [SortOrder(4)]
        [DefaultValue(null)]
        [JsonConverter(typeof(SecureStringConverter))]
        [DataMember]
        public SecureString ProxySecurePassword
        {
            get { return _proxySecurePassword; }
            set
            {
                if (_proxySecurePassword == value) return;
                _proxySecurePassword = value;
                NotifyOfPropertyChange(() => ProxyPassword);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValue<string>("ProxySecurePassword", value.GetInsecureString().Encrypt(), _isInitializing);
            }
        }

        #endregion

        [Category("Query History")]
        [DisplayName("History items to keep")]
        [DataMember, DefaultValue(200), MinValue(0), MaxValue(500)]
        public int QueryHistoryMaxItems { get { return _maxQueryHistory; }
            set
            {
                if (_maxQueryHistory == value) return;
                _maxQueryHistory = value;
                NotifyOfPropertyChange(() => QueryHistoryMaxItems);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValue<int>(nameof(QueryHistoryMaxItems), value, _isInitializing);
            }

        }


        private int _vpaxSampleReferentialIntegrityViolations = 3;
        [DataMember, DefaultValue(3)]
        public int VpaxSampleReferentialIntegrityViolations {
            get { return _vpaxSampleReferentialIntegrityViolations; }
            set {
                if (_vpaxSampleReferentialIntegrityViolations == value) return;
                _vpaxSampleReferentialIntegrityViolations = value;
                NotifyOfPropertyChange(() => VpaxSampleReferentialIntegrityViolations);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValue<int>(nameof(VpaxSampleReferentialIntegrityViolations), value, _isInitializing);
            }

        }

        [Category("Query History")]
        [DisplayName("Show Trace Timings (SE/FE)")]
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
                SettingProvider.SetValue<bool>(nameof(QueryHistoryShowTraceColumns), value, _isInitializing);
            }

        }

        [Category("Timeouts")]
        [DisplayName("Server Timings End Event Timeout (sec)")]
        [DataMember, DefaultValue(30), MinValue(0), MaxValue(999)]
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
                SettingProvider.SetValue<int>(nameof(QueryEndEventTimeout), value, _isInitializing);
            }
        }

        [Category("Timeouts")]
        [DisplayName("DaxFormatter Request timeout (sec)")]
        [DataMember, DefaultValue(10)]
        [MinValue(0)]
        [MaxValue(999)]
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
                SettingProvider.SetValue<int>(nameof(DaxFormatterRequestTimeout), value, _isInitializing);
            }
        }

        [Category("Defaults")]
        [DisplayName("Separators")]
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
                SettingProvider.SetValue<int>(nameof(DefaultSeparator), (int)value, _isInitializing);
            }
        }

        [Category("Dax Formatter")]
        [DisplayName("Default Format Style")]
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
                SettingProvider.SetValue<int>(nameof(DefaultDaxFormatStyle), (int)value, _isInitializing);
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
        [Category("Dax Formatter")]
        [DisplayName("Skip space after function name")]
        [DataMember, DefaultValue(false)]
        public bool SkipSpaceAfterFunctionName {
            get { return _skipSpaceAfterFunctionName; }
            set {
                if (_skipSpaceAfterFunctionName == value) return;
                _skipSpaceAfterFunctionName = value;
                NotifyOfPropertyChange(() => SkipSpaceAfterFunctionName);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValue<bool>(nameof(SkipSpaceAfterFunctionName), value, _isInitializing);
            }
        }

        [DataMember, DefaultValue(false)]
        public bool ShowPreReleaseNotifications {
            get { return _showPreReleaseNotifications; }
            set
            {
                if (_showPreReleaseNotifications == value) return;
                _showPreReleaseNotifications = value;
                NotifyOfPropertyChange(() => ShowPreReleaseNotifications);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValue<bool>(nameof(ShowPreReleaseNotifications), value, _isInitializing);
            }
        }

        [DisplayName("Show Basic Statistics")]
        [Category("Metadata Pane")]
        [Subcategory("Tooltips")]
        [Description("Shows basic information like a count of distinct values and the min and max value")]
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
                SettingProvider.SetValue<bool>("ShowTooltipBasicStats", value, _isInitializing);
            }
        }

        [DisplayName("Show Sample Data")]
        [Category("Metadata Pane")]
        [Subcategory("Tooltips")]
        [Description("Shows the top 10 values for the column")]
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
                SettingProvider.SetValue<bool>("ShowTooltipSampleData", value, _isInitializing);
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

        private bool _excludeHeadersWhenCopyingResults;
        [Category("Results")]
        [DisplayName("Exclude Headers when Copying Data")]
        [Description("Setting this option will just copy the raw data from the results pane")]
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
                SettingProvider.SetValue<bool>("ExcludeHeadersWhenCopyingResults", value, _isInitializing);
                NotifyOfPropertyChange(() => ExcludeHeadersWhenCopyingResults);
            }
        }


        private string _csvDelimiter = ",";
        [Category("Custom Export Format")]
        [DisplayName("'Other' Delimiter")]
        [DataMember, DefaultValue(",")]
        public string CustomCsvDelimiter
        {
            get
            {
                return _csvDelimiter;
            }

            set
            {
                _csvDelimiter = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValue<string>(nameof(CustomCsvDelimiter), value, _isInitializing);
                NotifyOfPropertyChange(() => CustomCsvDelimiter);
            }
        }

        private bool CustomCsvDelimiterEnabled {get => CustomCsvDelimiterType == CustomCsvDelimiterType.Other; }

        private bool _csvQuoteStringFields = true;
        [Category("Custom Export Format")]
        [DisplayName("Quote String Fields")]
        [DataMember, DefaultValue(true)]
        public bool CustomCsvQuoteStringFields
        {
            get
            {
                return _csvQuoteStringFields;
            }

            set
            {
                _csvQuoteStringFields = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValue<bool>(nameof(CustomCsvQuoteStringFields), value, _isInitializing);
                NotifyOfPropertyChange(() => CustomCsvQuoteStringFields);
            }
        }

        private int _traceStartupTimeout = 30;
        [Category("Timeouts")]
        [DisplayName("Trace Startup Timeout (secs)")]
        [DataMember, DefaultValue(30), MinValue(0),MaxValue(999)]
        public int TraceStartupTimeout { get => _traceStartupTimeout; set {
                _traceStartupTimeout = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValue<int>("TraceStartupTimeout", value, _isInitializing);
                NotifyOfPropertyChange(() => TraceStartupTimeout);
            }
        }

        private CustomCsvDelimiterType _csvCustomDelimiterType = CustomCsvDelimiterType.CultureDefault;
        [Category("Custom Export Format")]
        [DisplayName("CSV Delimiter")]
        [DataMember, DefaultValue(CustomCsvDelimiterType.CultureDefault)]
        public CustomCsvDelimiterType CustomCsvDelimiterType
        {
            get => _csvCustomDelimiterType;
            set
            {
                _csvCustomDelimiterType = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue<CustomCsvDelimiterType>(nameof(CustomCsvDelimiterType), value, _isInitializing);
                NotifyOfPropertyChange(() => CustomCsvDelimiterType);
                NotifyOfPropertyChange(() => UseCommaDelimiter);
                NotifyOfPropertyChange(() => UseCultureDefaultDelimiter);
                NotifyOfPropertyChange(() => UseTabDelimiter);
                NotifyOfPropertyChange(() => UseOtherDelimiter);
                NotifyOfPropertyChange(() => CustomCsvDelimiterEnabled);
            }
        }

        #region Hotkeys
        private string _hotkeyWarningMessage = string.Empty;
        [JsonIgnore]
        public string HotkeyWarningMessage { get => _hotkeyWarningMessage; 
            set
            {
                _hotkeyWarningMessage = value;
                NotifyOfPropertyChange(nameof(HotkeyWarningMessage));
                NotifyOfPropertyChange(nameof(ShowHotkeyWarningMessage));
                TimeoutHotkeyWarning();
            }
        }

        public bool ShowHotkeyWarningMessage => !string.IsNullOrEmpty(HotkeyWarningMessage);

        private void TimeoutHotkeyWarning()
        {
            if (string.IsNullOrEmpty(HotkeyWarningMessage)) return;
            // if the warning message is not empty wait 5 sec then clear it
            Task.Factory.StartNew(() => {
                System.Threading.Thread.Sleep(5000);
                HotkeyWarningMessage = string.Empty;
            });
        }

        private string _hotkeyCommentSelection;
        [DataMember, DefaultValue("Ctrl + Alt + C"),Hotkey]
        public string HotkeyCommentSelection { get => _hotkeyCommentSelection;
                set {
                _hotkeyCommentSelection = value;
                if (!_isInitializing) _eventAggregator.PublishOnUIThread(new UpdateHotkeys());
                SettingProvider.SetValue<string>(nameof(HotkeyCommentSelection), value, _isInitializing);
                NotifyOfPropertyChange(() => HotkeyCommentSelection);
            } 
        }

        private string _hotkeyUncommentSelection;
        [DataMember, DefaultValue("Ctrl + Alt + U"), Hotkey]
        public string HotkeyUnCommentSelection { get => _hotkeyUncommentSelection;
            set {
                _hotkeyUncommentSelection = value;
                if (!_isInitializing) _eventAggregator.PublishOnUIThread(new UpdateHotkeys());
                SettingProvider.SetValue<string>(nameof(HotkeyUnCommentSelection), value, _isInitializing);
                NotifyOfPropertyChange(() => HotkeyUnCommentSelection);
            } 
        }

        private string _hotkeyToUpper;
        [DataMember, DefaultValue("Ctrl + Shift + U"), Hotkey]
        public string HotkeyToUpper
        {
            get => _hotkeyToUpper;
            set
            {
                _hotkeyToUpper = value;
                if (!_isInitializing) _eventAggregator.PublishOnUIThread(new UpdateHotkeys());
                SettingProvider.SetValue<string>(nameof(HotkeyToUpper), value, _isInitializing);
                NotifyOfPropertyChange(() => HotkeyToUpper);
            }
        }

        private string _hotkeyToLower;
        [DataMember, DefaultValue("Ctrl + Shift + L"), Hotkey]
        public string HotkeyToLower
        {
            get => _hotkeyToLower;
            set
            {
                _hotkeyToLower = value;
                if (!_isInitializing) _eventAggregator.PublishOnUIThread(new UpdateHotkeys());
                SettingProvider.SetValue<string>(nameof(HotkeyToLower), value, _isInitializing);
                NotifyOfPropertyChange(() => HotkeyToLower);
            }
        }

        private string _hotkeyRunQuery;
        [DataMember, DefaultValue("F5"), Hotkey]
        public string HotkeyRunQuery
        {
            get => _hotkeyRunQuery;
            set
            {
                _hotkeyRunQuery = value;
                if (!_isInitializing) _eventAggregator.PublishOnUIThread(new UpdateHotkeys());
                SettingProvider.SetValue<string>(nameof(HotkeyRunQuery), value, _isInitializing);
                NotifyOfPropertyChange(() => HotkeyRunQuery);
            }
        }

        private string _hotkeyRunQueryAlt;
        [DataMember, DefaultValue("Ctrl + E"), Hotkey]
        public string HotkeyRunQueryAlt
        {
            get => _hotkeyRunQueryAlt;
            set
            {
                _hotkeyRunQueryAlt = value;
                if (!_isInitializing) _eventAggregator.PublishOnUIThread(new UpdateHotkeys());
                SettingProvider.SetValue<string>(nameof(HotkeyRunQueryAlt), value, _isInitializing);
                NotifyOfPropertyChange(() => HotkeyRunQueryAlt);
            }
        }

        private string _hotkeyNewDocument;
        [DataMember, DefaultValue("Ctrl + N"),Hotkey]
        public string HotkeyNewDocument
        {
            get => _hotkeyNewDocument;
            set
            {
                _hotkeyNewDocument = value;
                if (!_isInitializing) _eventAggregator.PublishOnUIThread(new UpdateHotkeys());
                SettingProvider.SetValue<string>(nameof(HotkeyNewDocument), value, _isInitializing);
                NotifyOfPropertyChange(() => HotkeyNewDocument);
            }
        }

        private string _hotkeyNewDocumentWithCurrentConnection;
        [DataMember, DefaultValue("Ctrl + Shift + N"),Hotkey]
        public string HotkeyNewDocumentWithCurrentConnection
        {
            get => _hotkeyNewDocumentWithCurrentConnection;
            set
            {
                _hotkeyNewDocumentWithCurrentConnection = value;
                if (!_isInitializing) _eventAggregator.PublishOnUIThread(new UpdateHotkeys());
                SettingProvider.SetValue<string>(nameof(HotkeyNewDocumentWithCurrentConnection), value, _isInitializing);
                NotifyOfPropertyChange(() => HotkeyNewDocumentWithCurrentConnection);
            }
        }

        private string _hotkeySaveDocument;
        [DataMember, DefaultValue("Ctrl + S"), Hotkey]
        public string HotkeySaveDocument
        {
            get => _hotkeySaveDocument;
            set
            {
                _hotkeySaveDocument = value;
                if (!_isInitializing) _eventAggregator.PublishOnUIThread(new UpdateHotkeys());
                SettingProvider.SetValue<string>(nameof(HotkeySaveDocument), value, _isInitializing);
                NotifyOfPropertyChange(() => HotkeySaveDocument);
            }
        }

        private string _hotkeyOpenDocument;
        [DataMember, DefaultValue("Ctrl + O"), Hotkey]
        public string HotkeyOpenDocument
        {
            get => _hotkeyOpenDocument;
            set
            {
                _hotkeyOpenDocument = value;
                if (!_isInitializing) _eventAggregator.PublishOnUIThread(new UpdateHotkeys());
                SettingProvider.SetValue<string>(nameof(HotkeyOpenDocument), value, _isInitializing);
                NotifyOfPropertyChange(() => HotkeyOpenDocument);
            }
        }

        private string _hotkeyGotoLine;
        [DataMember, DefaultValue("Ctrl + G"), Hotkey]
        public string HotkeyGotoLine
        {
            get => _hotkeyGotoLine;
            set
            {
                _hotkeyGotoLine = value;
                if (!_isInitializing) _eventAggregator.PublishOnUIThread(new UpdateHotkeys());
                SettingProvider.SetValue<string>(nameof(HotkeyGotoLine), value, _isInitializing);
                NotifyOfPropertyChange(() => HotkeyGotoLine);
            }
        }

        private string _hotkeyFormatQueryStandard;
        [DataMember, DefaultValue("F6"), Hotkey]
        public string HotkeyFormatQueryStandard
        {
            get => _hotkeyFormatQueryStandard;
            set
            {
                _hotkeyFormatQueryStandard = value;
                if (!_isInitializing) _eventAggregator.PublishOnUIThread(new UpdateHotkeys());
                SettingProvider.SetValue<string>(nameof(HotkeyFormatQueryStandard), value, _isInitializing);
                NotifyOfPropertyChange(() => HotkeyFormatQueryStandard);
            }
        }



        private string _hotkeyFormatQueryAlternate;
        [DataMember, DefaultValue("Ctrl + F6"), Hotkey]
        public string HotkeyFormatQueryAlternate
        {
            get => _hotkeyFormatQueryAlternate;
            set
            {
                _hotkeyFormatQueryAlternate = value;
                if (!_isInitializing) _eventAggregator.PublishOnUIThread(new UpdateHotkeys());
                SettingProvider.SetValue<string>(nameof(HotkeyFormatQueryAlternate), value, _isInitializing);
                NotifyOfPropertyChange(() => HotkeyFormatQueryAlternate);
            }
        }


        public void ResetKeyBindings()
        {
            //try
            //{
            //    _isInitializing = true;
                var props = typeof(OptionsViewModel).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                foreach (var prop in props)
                {
                    if (!prop.Name.StartsWith("Hotkey")) continue;

                    foreach (var att in prop.GetCustomAttributes(false))
                    {
                        if (att is DefaultValueAttribute)
                        {
                            var val = att as DefaultValueAttribute;

                            prop.SetValue(this, val.Value.ToString());
                        }
                    }
                }


            //}
            //finally
            //{
            //    _isInitializing = false;
                
            //}
            
        }
        #endregion


        #region methods
        public string GetCustomCsvDelimiter()
        {
            switch (CustomCsvDelimiterType)
            {
                case CustomCsvDelimiterType.CultureDefault: return System.Globalization.CultureInfo.CurrentUICulture.TextInfo.ListSeparator;
                case CustomCsvDelimiterType.Comma: return ",";
                case CustomCsvDelimiterType.Tab: return "\t";
                case CustomCsvDelimiterType.Other: return CustomCsvDelimiter;
                default: return System.Globalization.CultureInfo.CurrentUICulture.TextInfo.ListSeparator;

            }
        }
        #endregion

        #region View Specific Properties
        public bool UseCultureDefaultDelimiter
        {
            get => CustomCsvDelimiterType == CustomCsvDelimiterType.CultureDefault; 
            set { if (value) CustomCsvDelimiterType = CustomCsvDelimiterType.CultureDefault; }
        }
        public bool UseCommaDelimiter
        {
            get => CustomCsvDelimiterType == CustomCsvDelimiterType.Comma;
            set { if (value)  CustomCsvDelimiterType = CustomCsvDelimiterType.Comma; }
        }
        
        public bool UseTabDelimiter
        {
            get => CustomCsvDelimiterType == CustomCsvDelimiterType.Tab;
            set { if (value) CustomCsvDelimiterType = CustomCsvDelimiterType.Tab;}
        }

        public bool UseOtherDelimiter
        {
            get => CustomCsvDelimiterType == CustomCsvDelimiterType.Other;
            set { if (value) CustomCsvDelimiterType = CustomCsvDelimiterType.Other; }
        }

        public string CultureSpecificListDelimiter
        { get => System.Globalization.CultureInfo.CurrentUICulture.TextInfo.ListSeparator; }

        #endregion

        #region "Preview Toggles"

        // Preview Feature Toggles

        private bool _showExportMetrics;
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
                SettingProvider.SetValue(nameof(ShowExportMetrics), value, _isInitializing);
                NotifyOfPropertyChange(() => ShowExportMetrics);
            }
        }

        //private bool _showExternalTools = false;
        //[DataMember, DefaultValue(false)]
        //public bool ShowExternalTools { get => _showExternalTools;
        //    set {
        //        _showExternalTools = value;
        //        _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
        //        SettingProvider.SetValueAsync(nameof(ShowExternalTools), value, _isInitializing);
        //        NotifyOfPropertyChange(() => ShowExternalTools);
        //    }
        //}

        private bool _showExportAllData;
        [DataMember, DefaultValue(false)]
        public bool ShowExportAllData { get => _showExportAllData;
            set {
                _showExportAllData = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(ShowExportAllData), value, _isInitializing);
                NotifyOfPropertyChange(() => ShowExportAllData);
            }
        }

        private bool _vpaxIncludeTom;
        [DataMember, DefaultValue(false)]
        public bool VpaxIncludeTom {
            get => _vpaxIncludeTom;
            set {
                _vpaxIncludeTom = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(VpaxIncludeTom), value, _isInitializing);
                NotifyOfPropertyChange(() => VpaxIncludeTom);
            }
        }




        private bool _showKeyBindings;
        [DataMember, DefaultValue(false)]
        public bool ShowKeyBindings
        {
            get
            {
                return _showKeyBindings;
            }

            set
            {
                _showKeyBindings = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(ShowKeyBindings), value, _isInitializing);
                NotifyOfPropertyChange(() => ShowKeyBindings);
            }
        }

        private bool _showPreviewQueryBuilder;
        [DataMember, DefaultValue(false)]
        public bool ShowPreviewQueryBuilder
        {
            get
            {
                return _showPreviewQueryBuilder;
            }

            set
            {
                _showPreviewQueryBuilder = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(ShowPreviewQueryBuilder), value, _isInitializing);
                NotifyOfPropertyChange(() => ShowPreviewQueryBuilder);
            }
        }

        private bool _showPreviewBenchmark;
        [DataMember, DefaultValue(false)]
        public bool ShowPreviewBenchmark
        {
            get
            {
                return _showPreviewBenchmark;
            }

            set
            {
                _showPreviewBenchmark = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(ShowPreviewBenchmark), value, _isInitializing);
                NotifyOfPropertyChange(() => ShowPreviewBenchmark);
            }
        }

        //private bool _showDatabaseIdStatus = true;
        //[DataMember, DefaultValue(true)]
        //public bool ShowDatabaseIdStatus {

        //    get
        //    {
        //        return _showDatabaseIdStatus;
        //    }

        //    set
        //    {
        //        _showDatabaseIdStatus = value;
        //        _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
        //        SettingProvider.SetValueAsync(nameof(ShowDatabaseIdStatus), value, _isInitializing);
        //        NotifyOfPropertyChange(() => ShowDatabaseIdStatus);
        //    }

        //}

        #endregion




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
                SettingProvider.SetValue("Theme", value, _isInitializing);

            }
        }

        private bool _ResultAutoFormat;
        [Category("Results")]
        [SortOrder(10)]
        [DisplayName("Automatic Format Results")]
        [Description("Setting this option will automatically format numbers in the query results pane if a format string is not available for a measure with the same name as the column in the output")]
        [DataMember, DefaultValue(false)]
        public bool ResultAutoFormat {
            get => _ResultAutoFormat;
            set {
                _ResultAutoFormat = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValue("ResultAutoFormat", value, _isInitializing);
                NotifyOfPropertyChange(() => ResultAutoFormat);
            }
        }
                
        private string _DefaultDateAutoFormat;
        [Category("Results")]
        [SortOrder(20)]
        [DisplayName("Default Date Automatic Format")]
        [Description("The automatic format result will use this setting to format dates column, keep it empty to get the default format.")]
        [DataMember]
        [DefaultValue("yyyy-MM-dd")]
        public string DefaultDateAutoFormat
        {
            get => _DefaultDateAutoFormat;
            set
            {
                _DefaultDateAutoFormat = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValue("DefaultDateAutoFormat", value, _isInitializing);
                NotifyOfPropertyChange(() => DefaultDateAutoFormat);
            }
        }
        
        private bool _scaleResultsFontWithEditor = true;
        [Category("Results")]
        [DisplayName("Scale Results Font with Editor")]
        [Description("Setting this option will cause the results font to scale when you change the zoom percentage on the editor")]
        [DataMember, DefaultValue(true)]
        public bool ScaleResultsFontWithEditor { 
            get => _scaleResultsFontWithEditor;
            set {
                _scaleResultsFontWithEditor = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValue("ScaleResultsFontWithEditor", value, _isInitializing);
                NotifyOfPropertyChange(() => ScaleResultsFontWithEditor);
            } }

        private int _codeCompletionWindowWidthIncrease;
        [Category("Editor")]
        [DisplayName("Intellisense Width %")]
        [Description("100%-300%")]
        [MinValue(100), MaxValue(300)]
        [SortOrder(80)]
        [DataMember, DefaultValue(100)]
        public int CodeCompletionWindowWidthIncrease { 
            get => _codeCompletionWindowWidthIncrease;
            set {
                if (value < 100) value = 100; // value should not be less than 100% of the default size
                if (value > 300) value = 300; // value cannot be greater than 300% of the default size
                _codeCompletionWindowWidthIncrease = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValue("CodeCompletionWindowWidthIncrease", value, _isInitializing);
                NotifyOfPropertyChange(() => CodeCompletionWindowWidthIncrease);
            }
        }

        private bool _keepMetadataSearchOpen;
        [Category("Metadata Pane")]
        [Subcategory("Search")]
        [DisplayName("Keep Metadata Search Open")]
        [DataMember, DefaultValue(false)]
        public bool KeepMetadataSearchOpen { 
            get => _keepMetadataSearchOpen;
            set {
                _keepMetadataSearchOpen = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValue("KeepMetadataSearchOpen", value, _isInitializing);
                NotifyOfPropertyChange(() => KeepMetadataSearchOpen);
            }
        }

        private bool _autoRefreshMetadataLocalMachine = true;
        [Category("Metadata Pane")]
        [Subcategory("Detect Metadata Changes")]
        [DisplayName("Local Connections (PBI Desktop / SSDT)")]
        [SortOrder(1)]
        [DataMember, DefaultValue(true)]
        public bool AutoRefreshMetadataLocalMachine { 
            get => _autoRefreshMetadataLocalMachine;
            set {
                _autoRefreshMetadataLocalMachine = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValue("AutoRefreshMetadataLocalMachine", value, _isInitializing);
                NotifyOfPropertyChange(() => AutoRefreshMetadataLocalMachine);
            }
        }

        private bool _autoRefreshMetadataLocalNetwork = true;
        [Category("Metadata Pane")]
        [Subcategory("Detect Metadata Changes")]
        [DisplayName("Network Connections (SSAS on-prem)")]
        [SortOrder(2)]
        [DataMember, DefaultValue(true)]
        public bool AutoRefreshMetadataLocalNetwork { 
            get => _autoRefreshMetadataLocalNetwork;
            set {
                _autoRefreshMetadataLocalNetwork = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValue("AutoRefreshMetadataLocalNetwork", value, _isInitializing);
                NotifyOfPropertyChange(() => AutoRefreshMetadataLocalNetwork);
            }
        }

        private bool _autoRefreshMetadataCloud = true;
        [Category("Metadata Pane")]
        [Subcategory("Detect Metadata Changes")]
        [DisplayName("Cloud Connections (asazure:// or powerbi://)")]
        [SortOrder(3)]
        [DataMember, DefaultValue(false)]
        public bool AutoRefreshMetadataCloud { 
            get => _autoRefreshMetadataCloud;
            set {
                _autoRefreshMetadataCloud = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValue("AutoRefreshMetadataCloud", value, _isInitializing);
                NotifyOfPropertyChange(() => AutoRefreshMetadataCloud);
            }
        }

        private bool _showMetadataRefreshPrompt;
        [DataMember, DefaultValue(false),DisplayName("Prompt before refreshing Metadata"),
            Category("Metadata Pane"), Subcategory("Automatic Metadata Refresh"), SortOrder(0)]
        public bool ShowMetadataRefreshPrompt
        {
            get => _showMetadataRefreshPrompt;
            set
            {
                _showMetadataRefreshPrompt = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValue("ShowMetadataRefreshPrompt", value, _isInitializing);
                NotifyOfPropertyChange(() => ShowMetadataRefreshPrompt);
            }
        }

        private bool _showHiddenMetadata = true;
        [Category("Metadata Pane")]
        [Subcategory("Hidden Objects")]
        [DisplayName("Show Hidden Columns, Tables and Measures")]
        [DataMember, DefaultValue(true)]
        public bool ShowHiddenMetadata { 
            get => _showHiddenMetadata;
            set {
                _showHiddenMetadata = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValue("ShowHiddenMetadata", value, _isInitializing);
                NotifyOfPropertyChange(() => ShowHiddenMetadata);
            } 
        }

        private bool _setClearCacheAndRunAsDefaultRunStyle;
        [Category("Defaults")]
        [DisplayName("Set 'Clear Cache and Run' as default")]
        [Description("This option affects the default run style that is selected when DAX Studio starts up. Any changes will take effect the next time DAX Studio starts up.")]
        [DataMember, DefaultValue(false)]
        public bool SetClearCacheAsDefaultRunStyle { get => _setClearCacheAndRunAsDefaultRunStyle;
            set
            {
                _setClearCacheAndRunAsDefaultRunStyle = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(SetClearCacheAsDefaultRunStyle), value, _isInitializing);
                NotifyOfPropertyChange(() => SetClearCacheAsDefaultRunStyle);
            }
        }

        private bool _sortFoldersFirstInMetadata;
        [Category("Metadata Pane")]
        [Subcategory("Sorting")]
        [DisplayName("Sort Folders first in metadata pane")]
        [DataMember, DefaultValue(true)]
        public bool SortFoldersFirstInMetadata { 
            get => _sortFoldersFirstInMetadata;
            set {
                _sortFoldersFirstInMetadata = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(SortFoldersFirstInMetadata), value, _isInitializing);
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
                SettingProvider.SetValue(nameof(WindowPosition), value, _isInitializing);
                NotifyOfPropertyChange(() => WindowPosition);
            }
        }

        private Version _dismissedVersion = new Version();
        [DataMember, DefaultValue("0.0.0.0")]
        [JsonConverter(typeof(VersionConverter))]
        public Version DismissedVersion
        {
            get { return _dismissedVersion; }
            set
            {
                _dismissedVersion = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(DismissedVersion), value, _isInitializing);
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
                SettingProvider.SetValue(nameof(LastVersionCheckUTC), value, _isInitializing);
                NotifyOfPropertyChange(() => LastVersionCheckUTC);
            }
        }

        private Version _currentDownloadVersion = new Version(0,0,0,0);
        [DataMember, DefaultValue("0.0.0.0")]
        public Version CurrentDownloadVersion
        {
            get { return _currentDownloadVersion; }
            set
            {
                _currentDownloadVersion = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(CurrentDownloadVersion), value, _isInitializing);
                NotifyOfPropertyChange(() => CurrentDownloadVersion);
            }
        }

        private ObservableCollection<IDaxFile> _recentFiles;
        [DataMember]
        //[JsonConverter(typeof(DaxFileConverter))]
        public ObservableCollection<IDaxFile> RecentFiles
        {
            get
            {
                if (_recentFiles == null)
                {
                    _recentFiles = new ObservableCollection<IDaxFile>();
                }
                return _recentFiles;
            }
            set
            {
                if (value != null) {
                    _recentFiles = value;
                }

            }
        }


        private ObservableCollection<string> _recentServers;
        [DataMember]
        public ObservableCollection<string> RecentServers { get {
                if (_recentServers == null)
                {
                    _recentServers = new ObservableCollection<string>();
                }
                return _recentServers;
            } set {
                if (value != null) _recentServers = value;
            }
        }

        private bool _editorConvertTabsToSpaces;
        [DataMember,DefaultValue(false)]
        [DisplayName("Convert tabs to spaces"), Category("Editor")]
        [SortOrder(60)]
        public bool EditorConvertTabsToSpaces { get => _editorConvertTabsToSpaces; set {
                _editorConvertTabsToSpaces = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue<bool>(nameof(EditorConvertTabsToSpaces), value, _isInitializing);
                NotifyOfPropertyChange(() => EditorConvertTabsToSpaces);
            }
        }

        private int _editorIndentationSize = 4;
        [Category("Editor")]
        [DisplayName("Indentation Size")]
        [SortOrder(70)]
        [MinValue(1), MaxValue(25)]
        [DataMember,DefaultValue(4)]
        public int EditorIndentationSize { 
            get => _editorIndentationSize; 
            set {
                if (value < 1) value = 1; // the value cannot be less than 1
                _editorIndentationSize = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue<int>(nameof(EditorIndentationSize), value, _isInitializing);
                NotifyOfPropertyChange(() => EditorIndentationSize);
            }
        }

        private bool _editorWordWrap;
        [Category("Editor")]
        [DisplayName("Enable Word Wrapping")]
        [SortOrder(40)]
        [DataMember, DefaultValue(false)]
        public bool EditorWordWrap
        {
            get => _editorWordWrap;
            set {
                _editorWordWrap = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue<bool>(nameof(EditorWordWrap), value, _isInitializing);
                NotifyOfPropertyChange(() => EditorWordWrap);
            }
        }

        private bool _showUserInTitlebar;
        [Category("Defaults")]
        [DisplayName("Show Username in Titlebar")]
        [DataMember, DefaultValue(false)]
        public bool ShowUserInTitlebar
        {
            get => _showUserInTitlebar;
            set
            {
                _showUserInTitlebar = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue<bool>(nameof(ShowUserInTitlebar), value, _isInitializing);
                NotifyOfPropertyChange(() => ShowUserInTitlebar);
            }
        }

        #region Export Function Methods

        public void ExportDaxFunctions()
        {
            _eventAggregator.PublishOnUIThread(new ExportDaxFunctionsEvent());
        }

        public void PublishDaxFunctions()
        {
            _eventAggregator.PublishOnUIThread(new ExportDaxFunctionsEvent(true));
        }

        #endregion

        private bool? _isExcelAddinEnabledForAllUsers;
        public bool CanToggleExcelAddin
        {
            get {
                if (IsRunningPortable) return false;
                if (_isExcelAddinEnabledForAllUsers == null) _isExcelAddinEnabledForAllUsers = IsExcelAddinEnabledForAllUsers();
                return (bool)!_isExcelAddinEnabledForAllUsers;
            }
        }

        private string ExcelAddinKey
        {
            get
            {
                var wowKey = "";
                if (!Is64BitExcelFromRegisteredExe()) wowKey = @"\Wow6432Node";
                return $@"Software{wowKey}\Microsoft\Office\Excel\Addins\DaxStudio.ExcelAddIn";
            }
        }
        private bool IsExcelAddinEnabledForAllUsers()
        {
            // check the registry
            var key = Registry.LocalMachine.OpenSubKey(ExcelAddinKey);
            return key != null;
        }

        private bool IsExcelAddinEnabledForCurrentUser()
        {
            // check the registry
            var key = Registry.CurrentUser.OpenSubKey(ExcelAddinKey);
            return key != null;
        }

        public string ToggleExcelAddinCaption
        {
            get {
                if (IsExcelAddinEnabledForCurrentUser())
                    return "Disable Excel Addin";
                else
                    return "Enable Excel Addin";
            }
        }

        public string ToggleExcelAddinDescription
        {
            get {
                if (IsRunningPortable)
                    return "DAX Studio is currently running in Portable mode. You need to install DAX Studio using the installer in order to activate the Excel Addin";

                if (IsExcelAddinEnabledForAllUsers())
                    return "The Excel add-in was installed using the 'All Users' option so it cannot be enabled/disable using this button. If you want to enable/disable the addin using this button you need to re-run the installer and choose the 'Current User' option for the Addin";
                
                return "This button will enable/disable the DAX Studio Excel addin. If Excel is already open you will need to close and re-open it for this setting to take effect.";
            }
        }

        public bool IsRunningPortable { get; set; }

        public void ToggleExcelAddin()
        {
            // TODO
            if (IsExcelAddinEnabledForCurrentUser())
                DeleteExcelAddinKeys();
            else
                WriteExcelAddinKeys();

            NotifyOfPropertyChange(nameof(CanToggleExcelAddin));
            NotifyOfPropertyChange(nameof(ToggleExcelAddinCaption));
        }

        // Gets the path to the currently running DAXStudio.exe, but swaps back slashes for forward slashes
        private string exePathForRegistry()
        {
            return Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location).Replace("\\","/");
        }


        private void WriteExcelAddinKeys()
        {

            var key = Registry.CurrentUser.CreateSubKey(ExcelAddinKey);
            key.SetValue("LoadBehavior", 3, RegistryValueKind.DWord);
            key.SetValue("Description", "DAX Studio Excel Add-In", RegistryValueKind.String);
            key.SetValue("FriendlyName", "DAX Studio Excel Add-In", RegistryValueKind.String);
            key.SetValue("Manifest", $"file:///{exePathForRegistry()}/bin/DaxStudio.vsto|vstolocal", RegistryValueKind.String);

        }

        private void DeleteExcelAddinKeys()
        {
            Registry.CurrentUser.DeleteSubKeyTree(ExcelAddinKey, false);
        }
        

        private bool Is64BitExcelFromRegisteredExe()
        {
            return false;
            
            // read the "ExcelBitness" value that should be written by the installer
            //var key = Registry.CurrentUser.GetValue(@"HKEY_CURRENT_USER\Software\DaxStudio\ExcelBitness");
            //if (key== null)
            //var key = Registry.LocalMachine.OpenSubKey(@"Software\DaxStudio", false);
            //if (key == null) return false;
            //return (string)(key?.GetValue("ExcelBitness", "32Bit")??"32Bit") == "64Bit";
        }


        #region PropertyList support properties

        private IEnumerable<string> _categories;
        public IEnumerable<string> Categories
        {
            get
            {
                if (_categories == null) _categories = GetCategories();
                return _categories;
            }
        }

        private string _selectedCategory;

        //public event PropertyChangedEventHandler PropertyChanged;

        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                _selectedCategory = value;
                NotifyOfPropertyChange(nameof(SelectedCategory));
                //PropertyChanged(this, new PropertyChangedEventArgs(nameof(SelectedCategory)));
                if (_selectedCategory != null) SearchText = string.Empty;
            }
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (value == _searchText) return;
                System.Diagnostics.Debug.WriteLine($"OptionsViewModel.SearchText = {value}");
                _searchText = value;
                NotifyOfPropertyChange(nameof(SearchText));
                NotifyOfPropertyChange(nameof(HasSearchText));
                //PropertyChanged(this, new PropertyChangedEventArgs(nameof(SearchText)));
                if (!string.IsNullOrEmpty(_searchText)) SelectedCategory = null;
                else SelectedCategory = Categories.FirstOrDefault();
            }
        }

        public bool HasSearchText => !string.IsNullOrWhiteSpace(SearchText);

        public void ClearSearchText() {
            SearchText = string.Empty;
        }

        private IEnumerable<string> GetCategories()
        {
            var lst = new SortedList<string, string>();

            foreach (var prop in this.GetType().GetProperties())
            {
                var catAttrib = prop.GetCustomAttribute<CategoryAttribute>();
                var cat = catAttrib?.Category;
                if (cat == null) continue;
                if (lst.ContainsKey(cat)) continue;
                lst.Add(cat, cat);
            }
            SelectedCategory = lst.Keys.FirstOrDefault();
            return lst.Keys;
        }

        #endregion


        #region IDisposable Support
        private bool disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (_proxySecurePassword != null)
                    {
                        _proxySecurePassword.Dispose();
                        _proxySecurePassword = null;
                    }
                }


                disposedValue = true;
            }
        }


        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion


    }
}
