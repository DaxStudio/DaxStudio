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
using System.Threading.Tasks;

namespace DaxStudio.UI.ViewModels
{
    [DataContract]
    [Export(typeof(IGlobalOptions))]   
    public class OptionsViewModel:Screen, IGlobalOptions, IDisposable
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
        private bool _isInitializing = false;


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

        [DataMember]
        [DefaultValue(DefaultEditorFontFamily)]
        public string EditorFontFamily { get { return _selectedEditorFontFamily; } 
            set{
                if (_selectedEditorFontFamily == value) return;
                _selectedEditorFontFamily = value;
                NotifyOfPropertyChange(() => EditorFontFamily);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<string>(nameof(EditorFontFamily), value , _isInitializing);

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
                SettingProvider.SetValueAsync<double>(nameof(EditorFontSize), value, _isInitializing);
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
                SettingProvider.SetValueAsync<string>(nameof(ResultFontFamily), value, _isInitializing);

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
                SettingProvider.SetValueAsync<double>(nameof(ResultFontSize), value, _isInitializing);
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
                SettingProvider.SetValueAsync<bool>(nameof(TraceDirectQuery), value, _isInitializing);
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
                SettingProvider.SetValueAsync<bool>(nameof(ProxyUseSystem), value, _isInitializing);
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
                SettingProvider.SetValueAsync<string>(nameof(ProxyAddress), value, _isInitializing);
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
                SettingProvider.SetValueAsync<string>(nameof(ProxyUser), value, _isInitializing);
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
                //SettingProvider.SetValueAsync<string>("ProxyPassword", value.Encrypt(), _isInitializing);
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
                SettingProvider.SetValueAsync<string>("ProxySecurePassword", value.GetInsecureString().Encrypt(), _isInitializing);
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
                SettingProvider.SetValueAsync<int>(nameof(QueryHistoryMaxItems), value, _isInitializing);
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
                SettingProvider.SetValueAsync<int>(nameof(VpaxSampleReferentialIntegrityViolations), value, _isInitializing);
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
                SettingProvider.SetValueAsync<bool>(nameof(QueryHistoryShowTraceColumns), value, _isInitializing);
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
        public bool SkipSpaceAfterFunctionName {
            get { return _skipSpaceAfterFunctionName; }
            set {
                if (_skipSpaceAfterFunctionName == value) return;
                _skipSpaceAfterFunctionName = value;
                NotifyOfPropertyChange(() => SkipSpaceAfterFunctionName);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync<bool>(nameof(SkipSpaceAfterFunctionName), value, _isInitializing);
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
                SettingProvider.SetValueAsync<bool>(nameof(ShowPreReleaseNotifications), value, _isInitializing);
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


        private string _csvDelimiter = ",";
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
                SettingProvider.SetValueAsync<string>(nameof(CustomCsvDelimiter), value, _isInitializing);
                NotifyOfPropertyChange(() => CustomCsvDelimiter);
            }
        }

        private bool _csvQuoteStringFields = true;
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
                SettingProvider.SetValueAsync<bool>(nameof(CustomCsvQuoteStringFields), value, _isInitializing);
                NotifyOfPropertyChange(() => CustomCsvQuoteStringFields);
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

        private CustomCsvDelimiterType _csvCustomDelimiterType = CustomCsvDelimiterType.CultureDefault;
        [DataMember, DefaultValue(CustomCsvDelimiterType.CultureDefault)]
        public CustomCsvDelimiterType CustomCsvDelimiterType
        {
            get => _csvCustomDelimiterType;
            set
            {
                _csvCustomDelimiterType = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValueAsync<CustomCsvDelimiterType>(nameof(CustomCsvDelimiterType), value, _isInitializing);
                NotifyOfPropertyChange(() => CustomCsvDelimiterType);
                NotifyOfPropertyChange(() => UseCommaDelimiter);
                NotifyOfPropertyChange(() => UseCultureDefaultDelimiter);
                NotifyOfPropertyChange(() => UseTabDelimiter);
                NotifyOfPropertyChange(() => UseOtherDelimiter);
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
                SettingProvider.SetValueAsync<string>(nameof(HotkeyCommentSelection), value, _isInitializing);
                NotifyOfPropertyChange(() => HotkeyCommentSelection);
            } 
        }

        private string _hotkeyUncommentSelection;
        [DataMember, DefaultValue("Ctrl + Alt + U"), Hotkey]
        public string HotkeyUnCommentSelection { get => _hotkeyUncommentSelection;
            set {
                _hotkeyUncommentSelection = value;
                if (!_isInitializing) _eventAggregator.PublishOnUIThread(new UpdateHotkeys());
                SettingProvider.SetValueAsync<string>(nameof(HotkeyUnCommentSelection), value, _isInitializing);
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
                SettingProvider.SetValueAsync<string>(nameof(HotkeyToUpper), value, _isInitializing);
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
                SettingProvider.SetValueAsync<string>(nameof(HotkeyToLower), value, _isInitializing);
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
                SettingProvider.SetValueAsync<string>(nameof(HotkeyRunQuery), value, _isInitializing);
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
                SettingProvider.SetValueAsync<string>(nameof(HotkeyRunQueryAlt), value, _isInitializing);
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
                SettingProvider.SetValueAsync<string>(nameof(HotkeyNewDocument), value, _isInitializing);
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
                SettingProvider.SetValueAsync<string>(nameof(HotkeyNewDocumentWithCurrentConnection), value, _isInitializing);
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
                SettingProvider.SetValueAsync<string>(nameof(HotkeySaveDocument), value, _isInitializing);
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
                SettingProvider.SetValueAsync<string>(nameof(HotkeyOpenDocument), value, _isInitializing);
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
                SettingProvider.SetValueAsync<string>(nameof(HotkeyGotoLine), value, _isInitializing);
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
                SettingProvider.SetValueAsync<string>(nameof(HotkeyFormatQueryStandard), value, _isInitializing);
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
                SettingProvider.SetValueAsync<string>(nameof(HotkeyFormatQueryAlternate), value, _isInitializing);
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
                SettingProvider.SetValueAsync(nameof(ShowExportMetrics), value, _isInitializing);
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

        private bool _showExportAllData = false;
        [DataMember, DefaultValue(false)]
        public bool ShowExportAllData { get => _showExportAllData;
            set {
                _showExportAllData = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync(nameof(ShowExportAllData), value, _isInitializing);
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
                SettingProvider.SetValueAsync(nameof(VpaxIncludeTom), value, _isInitializing);
                NotifyOfPropertyChange(() => VpaxIncludeTom);
            }
        }




        private bool _showKeyBindings = false;
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
                SettingProvider.SetValueAsync(nameof(ShowKeyBindings), value, _isInitializing);
                NotifyOfPropertyChange(() => ShowKeyBindings);
            }
        }

        private bool _showPreviewQueryBuilder = false;
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
                SettingProvider.SetValueAsync(nameof(ShowPreviewQueryBuilder), value, _isInitializing);
                NotifyOfPropertyChange(() => ShowPreviewQueryBuilder);
            }
        }

        private bool _showPreviewBenchmark = false;
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
                SettingProvider.SetValueAsync(nameof(ShowPreviewBenchmark), value, _isInitializing);
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
                SettingProvider.SetValueAsync("Theme", value, _isInitializing);

            }
        }

        private bool _ResultAutoFormat = false;
        [DataMember, DefaultValue(false)]
        public bool ResultAutoFormat {
            get => _ResultAutoFormat;
            set {
                _ResultAutoFormat = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync("ResultAutoFormat", value, _isInitializing);
                NotifyOfPropertyChange(() => ResultAutoFormat);
            }
        }

        private bool _scaleResultsFontWithEditor = true;
        [DataMember, DefaultValue(true)]
        public bool ScaleResultsFontWithEditor { 
            get => _scaleResultsFontWithEditor;
            set {
                _scaleResultsFontWithEditor = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync("ScaleResultsFontWithEditor", value, _isInitializing);
                NotifyOfPropertyChange(() => ScaleResultsFontWithEditor);
            } }

        private int _codeCompletionWindowWidthIncrease;
        [DataMember, DefaultValue(100)]
        public int CodeCompletionWindowWidthIncrease { 
            get => _codeCompletionWindowWidthIncrease;
            set {
                if (value < 100) value = 100; // value should not be less than 100% of the default size
                if (value > 300) value = 300; // value cannot be greater than 300% of the default size
                _codeCompletionWindowWidthIncrease = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync("CodeCompletionWindowWidthIncrease", value, _isInitializing);
                NotifyOfPropertyChange(() => CodeCompletionWindowWidthIncrease);
            }
        }

        private bool _keepMetadataSearchOpen;
        [DataMember, DefaultValue(false)]
        public bool KeepMetadataSearchOpen { 
            get => _keepMetadataSearchOpen;
            set {
                _keepMetadataSearchOpen = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync("KeepMetadataSearchOpen", value, _isInitializing);
                NotifyOfPropertyChange(() => KeepMetadataSearchOpen);
            }
        }

        private bool _autoRefreshMetadataLocalMachine = true;
        [DataMember, DefaultValue(true)]
        public bool AutoRefreshMetadataLocalMachine { 
            get => _autoRefreshMetadataLocalMachine;
            set {
                _autoRefreshMetadataLocalMachine = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync("AutoRefreshMetadataLocalMachine", value, _isInitializing);
                NotifyOfPropertyChange(() => AutoRefreshMetadataLocalMachine);
            }
        }

        private bool _autoRefreshMetadataLocalNetwork = true;
        [DataMember, DefaultValue(true)]
        public bool AutoRefreshMetadataLocalNetwork { 
            get => _autoRefreshMetadataLocalNetwork;
            set {
                _autoRefreshMetadataLocalNetwork = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync("AutoRefreshMetadataLocalNetwork", value, _isInitializing);
                NotifyOfPropertyChange(() => AutoRefreshMetadataLocalNetwork);
            }
        }

        private bool _autoRefreshMetadataCloud = true;
        [DataMember, DefaultValue(false)]
        public bool AutoRefreshMetadataCloud { 
            get => _autoRefreshMetadataCloud;
            set {
                _autoRefreshMetadataCloud = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync("AutoRefreshMetadataCloud", value, _isInitializing);
                NotifyOfPropertyChange(() => AutoRefreshMetadataCloud);
            }
        }

        private bool _showMetadataRefreshPrompt = false;
        [DataMember, DefaultValue(false)]
        public bool ShowMetadataRefreshPrompt
        {
            get => _showMetadataRefreshPrompt;
            set
            {
                _showMetadataRefreshPrompt = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync("ShowMetadataRefreshPrompt", value, _isInitializing);
                NotifyOfPropertyChange(() => ShowMetadataRefreshPrompt);
            }
        }

        private bool _showHiddenMetadata = true;
        [DataMember, DefaultValue(true)]
        public bool ShowHiddenMetadata { 
            get => _showHiddenMetadata;
            set {
                _showHiddenMetadata = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync("ShowHiddenMetadata", value, _isInitializing);
                NotifyOfPropertyChange(() => ShowHiddenMetadata);
            } 
        }

        private bool _setClearCacheAndRunAsDefaultRunStyle = false;
        [DataMember, DefaultValue(false)]
        public bool SetClearCacheAsDefaultRunStyle { get => _setClearCacheAndRunAsDefaultRunStyle;
            set
            {
                _setClearCacheAndRunAsDefaultRunStyle = value;
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValueAsync(nameof(SetClearCacheAsDefaultRunStyle), value, _isInitializing);
                NotifyOfPropertyChange(() => SetClearCacheAsDefaultRunStyle);
            }
        }

        private bool _sortFoldersFirstInMetadata = false;
        [DataMember, DefaultValue(true)]
        public bool SortFoldersFirstInMetadata { 
            get => _sortFoldersFirstInMetadata;
            set {
                _sortFoldersFirstInMetadata = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValueAsync(nameof(SortFoldersFirstInMetadata), value, _isInitializing);
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
                SettingProvider.SetValueAsync(nameof(WindowPosition), value, _isInitializing);
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
                SettingProvider.SetValueAsync(nameof(DismissedVersion), value, _isInitializing);
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
                SettingProvider.SetValueAsync(nameof(LastVersionCheckUTC), value, _isInitializing);
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
                SettingProvider.SetValueAsync(nameof(CurrentDownloadVersion), value, _isInitializing);
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

        private bool _editorConvertTabsToSpaces = false;
        [DataMember,DefaultValue(false)]
        public bool EditorConvertTabsToSpaces { get => _editorConvertTabsToSpaces; set {
                _editorConvertTabsToSpaces = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValueAsync<bool>(nameof(EditorConvertTabsToSpaces), value, _isInitializing);
                NotifyOfPropertyChange(() => EditorConvertTabsToSpaces);
            }
        }

        private int _editorIndentationSize = 4;
        [DataMember,DefaultValue(4)]
        public int EditorIndentationSize { 
            get => _editorIndentationSize; 
            set {
                if (value < 1) value = 1; // the value cannot be less than 1
                _editorIndentationSize = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValueAsync<int>(nameof(EditorIndentationSize), value, _isInitializing);
                NotifyOfPropertyChange(() => EditorIndentationSize);
            }
        }

        private bool _editorWordWrap = false;
        [DataMember, DefaultValue(false)]
        public bool EditorWordWrap
        {
            get => _editorWordWrap;
            set {
                _editorWordWrap = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValueAsync<bool>(nameof(EditorWordWrap), value, _isInitializing);
                NotifyOfPropertyChange(() => EditorWordWrap);
            }
        }

        private bool _showUserInTitlebar = false;
        [DataMember, DefaultValue(false)]
        public bool ShowUserInTitlebar
        {
            get => _showUserInTitlebar;
            set
            {
                _showUserInTitlebar = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValueAsync<bool>(nameof(ShowUserInTitlebar), value, _isInitializing);
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

        private bool? _isExcelAddinEnabledForAllUsers = null;
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

        public bool IsRunningPortable { get; set; } = false;

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

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

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
