using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Caliburn.Micro;
using DaxStudio.Common;
using DaxStudio.Controls.PropertyGrid;
using DaxStudio.Interfaces;
using DaxStudio.Interfaces.Attributes;
using DaxStudio.Interfaces.Enums;
using DaxStudio.UI.Enums;
using DaxStudio.UI.Events;
using DaxStudio.UI.Extensions;
using DaxStudio.UI.Interfaces;
using Newtonsoft.Json;
using DaxStudio.UI.JsonConverters;
using Microsoft.Win32;
using DaxStudio.UI.Utils;
using Serilog;
using Serilog.Events;

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
        private const double Tolerance = 0.1;

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
        private int _longQuerySeconds;
        private int _maxQueryHistory;
        private bool _queryHistoryShowTraceColumns;
        private int _queryEndEventTimeout;
        private int _daxFormatterRequestTimeout;
        private bool _traceDirectQuery;
        private bool _highlightXmSqlCallbacks;
        private bool _simplifyXmSqlSyntax;
        private bool _replaceXmSqlColumnNames;
        private bool _replaceXmSqlTableNames;
        private bool _playSoundAtQueryEnd;
        private bool _playSoundIfNotActive;
        private LongOperationSounds _queryEndSound;
        private readonly IEventAggregator _eventAggregator;

        private DelimiterType _defaultSeparator;
        private DaxFormatStyle _defaultDaxFormatStyle;
        private bool _skipSpaceAfterFunctionName;
        private bool _showPreReleaseNotifications;
        private bool _showTooltipBasicStats;
        private bool _showTooltipSampleData;

        private bool _showFunctionInsightsOnHover;
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
        public string EditorFontFamily { get => _selectedEditorFontFamily;
            set {
                if (_selectedEditorFontFamily == value) return;
                _selectedEditorFontFamily = value;
                NotifyOfPropertyChange(() => EditorFontFamily);
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(EditorFontFamily), value, _isInitializing, this);

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
        public double EditorFontSize { get => _editorFontSize;
            set {
                if (Math.Abs(_editorFontSize - value) < Tolerance) return;
                _editorFontSize = value;
                NotifyOfPropertyChange(() => EditorFontSize);
                NotifyOfPropertyChange(() => EditorFontSizePx);
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(EditorFontSize), value, _isInitializing, this);
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
            get => _selectedResultFontFamily;
            set {
                if (_selectedResultFontFamily == value) return;
                _selectedResultFontFamily = value;
                NotifyOfPropertyChange(() => ResultFontFamily);
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(ResultFontFamily), value, _isInitializing, this);

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
            get => _resultFontSize;
            set {
                if (Math.Abs(_resultFontSize - value) < Tolerance) return;
                _resultFontSize = value;
                NotifyOfPropertyChange(() => ResultFontSize);
                NotifyOfPropertyChange(() => ResultFontSizePx);
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(ResultFontSize), value, _isInitializing, this);
            }
        }

        [Category("Editor")]
        [DisplayName("Show Line Numbers")]
        [SortOrder(30)]
        [DataMember]
        [DefaultValue(true)]
        public bool EditorShowLineNumbers { get => _showLineNumbers;
            set
            {
                if (_showLineNumbers == value) return;
                _showLineNumbers = value;
                NotifyOfPropertyChange(() => EditorShowLineNumbers);
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(EditorShowLineNumbers), value, _isInitializing, this);
            }
        }

        [Category("Editor")]
        [DisplayName("Enable Intellisense")]
        [SortOrder(40)]
        [DataMember]
        [DefaultValue(true)]
        public bool EditorEnableIntellisense
        {
            get => _enableIntellisense;
            set
            {
                if (_enableIntellisense == value) return;
                _enableIntellisense = value;
                NotifyOfPropertyChange(() => EditorEnableIntellisense);
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(EditorEnableIntellisense), value, _isInitializing, this);
            }
        }

        [Category("Editor")]
        [DisplayName("Show Function Tooltips on Hover")]
        [Description("This feature requires a connection to a data source and it needs to move your keyboard cursor position on hover since the toolip is always positioned under the keyboard cursor")]
        [SortOrder(45)]
        [DataMember]
        [DefaultValue(true)]
        public bool EditorShowFunctionInsightsOnHover
        {
            get => _showFunctionInsightsOnHover;
            set
            {
                if (_showFunctionInsightsOnHover == value) return;
                _showFunctionInsightsOnHover = value;
                NotifyOfPropertyChange(() => EditorShowFunctionInsightsOnHover);
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(EditorShowFunctionInsightsOnHover), value, _isInitializing, this);
            }
        }

        [Category("Editor")]
        [DisplayName("Multiple queries detected on paste")]
        [Description("Specifies how to handle code after a \"// Direct Query\" comment when pasting code from Power BI Performance Analyzer")]
        [SortOrder(60)]
        [DataMember]
        [DefaultValue(MultipleQueriesDetectedOnPaste.Prompt)]
        public MultipleQueriesDetectedOnPaste EditorMultipleQueriesDetectedOnPaste
        {
            get => _removeDirectQueryCode;
            set
            {
                if (_removeDirectQueryCode == value) return;
                _removeDirectQueryCode = value;
                NotifyOfPropertyChange(() => EditorMultipleQueriesDetectedOnPaste);
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue<MultipleQueriesDetectedOnPaste>(nameof(EditorMultipleQueriesDetectedOnPaste), value, _isInitializing, this);
            }
        }

        [DisplayName("Legacy DirectQuery Trace")]
        [Category("Trace")]
        [Description("On servers prior to v15 (SSAS 2017) we do not trace DirectQuery events by default in the server timings pane as we have to do expensive client side filtering. Only turn this option on if you explicitly need to trace these events on a v14 or earlier data source and turn off the trace as soon as possible")]
        [DataMember, DefaultValue(false)]
        public bool TraceDirectQuery {
            get => _traceDirectQuery;
            set {
                if (_traceDirectQuery == value) return;
                _traceDirectQuery = value;
                NotifyOfPropertyChange(() => TraceDirectQuery);
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(TraceDirectQuery), value, _isInitializing, this);
            }
        }

        [DisplayName("Highlight VertiPaq callbacks")]
        [Category("Server Timings")]
        [Description("Highlight xmSQL queries containing callbacks that don't store the result in the storage engine cache.")]
        [DataMember, DefaultValue(true)]
        public bool HighlightXmSqlCallbacks
        {
            get => _highlightXmSqlCallbacks;
            set
            {
                if (_highlightXmSqlCallbacks == value) return;
                _highlightXmSqlCallbacks = value;
                NotifyOfPropertyChange(() => HighlightXmSqlCallbacks);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValue<bool>(nameof(HighlightXmSqlCallbacks), value, _isInitializing, this);
            }
        }

        [DisplayName("Simplify SE query syntax")]
        [Category("Server Timings")]
        [Description("Remove internal IDs and verbose syntax from xmSQL queries.")]
        [DataMember, DefaultValue(true)]
        public bool SimplifyXmSqlSyntax
        {
            get => _simplifyXmSqlSyntax;
            set
            {
                if (_simplifyXmSqlSyntax == value) return;
                _simplifyXmSqlSyntax = value;
                NotifyOfPropertyChange(() => SimplifyXmSqlSyntax);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValue<bool>(nameof(SimplifyXmSqlSyntax), value, _isInitializing, this);
            }
        }

        [DisplayName("Replace column ID with name")]
        [Category("Server Timings")]
        [Description("Replace xmSQL column ID with corresponding column name in data model.")]
        [DataMember, DefaultValue(true)]
        public bool ReplaceXmSqlColumnNames
        {
            get => _replaceXmSqlColumnNames;
            set
            {
                if (_replaceXmSqlColumnNames == value) return;
                _replaceXmSqlColumnNames = value;
                NotifyOfPropertyChange(() => ReplaceXmSqlColumnNames);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValue<bool>(nameof(ReplaceXmSqlColumnNames), value, _isInitializing, this);
            }
        }

        [DisplayName("Replace table ID with name")]
        [Category("Server Timings")]
        [Description("Replace xmSQL table ID with corresponding table name in data model.")]
        [DataMember, DefaultValue(true)]
        public bool ReplaceXmSqlTableNames
        {
            get => _replaceXmSqlTableNames;
            set
            {
                if (_replaceXmSqlTableNames == value) return;
                _replaceXmSqlTableNames = value;
                NotifyOfPropertyChange(() => ReplaceXmSqlTableNames);
                _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
                SettingProvider.SetValue<bool>(nameof(ReplaceXmSqlTableNames), value, _isInitializing, this);
            }
        }

        #region Http Proxy properties

        [Category("Proxy")]
        [DisplayName("Use System Proxy")]
        [SortOrder(1)]
        [DataMember, DefaultValue(true)]
        public bool ProxyUseSystem
        {
            get => _proxyUseSystem;
            set
            {
                if (_proxyUseSystem == value) return;
                _proxyUseSystem = value;
                NotifyOfPropertyChange(() => ProxyUseSystem);
                NotifyOfPropertyChange(() => ProxyDontUseSystem);
                NotifyOfPropertyChange(() => ProxySecurePasswordEnabled);
                NotifyOfPropertyChange(() => ProxyUserEnabled);
                NotifyOfPropertyChange(() => ProxyAddressEnabled);
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(ProxyUseSystem), value, _isInitializing, this);
                WebRequestFactory.ResetProxy();
            }
        }

        [JsonIgnore]
        public bool ProxyDontUseSystem => !_proxyUseSystem;

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
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(ProxyAddress), value, _isInitializing, this);
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
                SettingProvider.SetValue<bool>(nameof(ShowHelpWatermark), value, _isInitializing, this);
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
            get => _proxyUser;
            set
            {
                if (_proxyUser == value) return;
                _proxyUser = value;
                NotifyOfPropertyChange(() => ProxyUser);
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(ProxyUser), value, _isInitializing, this);
                WebRequestFactory.ResetProxy();
            }
        }

        [JsonIgnore]
        public bool ProxyUserEnabled => !ProxyUseSystem;


        [JsonIgnore]
        public string ProxyPassword
        {
            get => _proxyPassword;
            set
            {
                if (_proxyPassword == value) return;
                _proxyPassword = value;
                NotifyOfPropertyChange(() => ProxyPassword);
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                //SettingProvider.SetValueAsync<string>("ProxyPassword", value.Encrypt(), _isInitializing);
                SetProxySecurePassword(value);
                WebRequestFactory.ResetProxy();
            }
        }

        [JsonIgnore]
        public bool ProxySecurePasswordEnabled => !ProxyUseSystem;

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
            get => _proxySecurePassword;
            set
            {
                if (_proxySecurePassword == value) return;
                _proxySecurePassword = value;
                NotifyOfPropertyChange(() => ProxyPassword);
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue("ProxySecurePassword", value.GetInsecureString().Encrypt(), _isInitializing, this);
            }
        }

        #endregion

        [Category("Query History")]
        [DisplayName("History items to keep")]
        [DataMember, DefaultValue(200), MinValue(0), MaxValue(500)]
        public int QueryHistoryMaxItems { get => _maxQueryHistory;
            set
            {
                if (_maxQueryHistory == value) return;
                _maxQueryHistory = value;
                NotifyOfPropertyChange(() => QueryHistoryMaxItems);
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(QueryHistoryMaxItems), value, _isInitializing, this);
            }

        }

        private bool _vpaxReadStatisticsFromData = true;
        [DataMember, DefaultValue(true)]
        public bool VpaxReadStatisticsFromData
        {
            get => _vpaxReadStatisticsFromData;
            set
            {
                if (_vpaxReadStatisticsFromData == value) return;
                _vpaxReadStatisticsFromData = value;
                NotifyOfPropertyChange(() => VpaxReadStatisticsFromData);
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(VpaxReadStatisticsFromData), value, _isInitializing, this);
            }

        }

        private int _vpaxSampleReferentialIntegrityViolations = 3;
        [DataMember, DefaultValue(3)]
        public int VpaxSampleReferentialIntegrityViolations {
            get => _vpaxSampleReferentialIntegrityViolations;
            set {
                if (_vpaxSampleReferentialIntegrityViolations == value) return;
                _vpaxSampleReferentialIntegrityViolations = value;
                NotifyOfPropertyChange(() => VpaxSampleReferentialIntegrityViolations);
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(VpaxSampleReferentialIntegrityViolations), value, _isInitializing, this);
            }

        }

        [Category("Query History")]
        [DisplayName("Show Trace Timings (SE/FE)")]
        [DataMember, DefaultValue(true)]
        public bool QueryHistoryShowTraceColumns
        {
            get => _queryHistoryShowTraceColumns;
            set
            {
                if (_queryHistoryShowTraceColumns == value) return;
                _queryHistoryShowTraceColumns = value;
                NotifyOfPropertyChange(() => QueryHistoryShowTraceColumns);
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(QueryHistoryShowTraceColumns), value, _isInitializing, this);
            }

        }

        [Category("Timeouts")]
        [DisplayName("Server Timings End Event Timeout (sec)")]
        [DataMember, DefaultValue(30), MinValue(0), MaxValue(999)]
        public int QueryEndEventTimeout
        {
            get => _queryEndEventTimeout;

            set
            {
                if (_queryEndEventTimeout == value) return;
                _queryEndEventTimeout = value;
                NotifyOfPropertyChange(() => QueryEndEventTimeout);
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(QueryEndEventTimeout), value, _isInitializing, this);
            }
        }

        [Category("Timeouts")]
        [DisplayName("DaxFormatter Request timeout (sec)")]
        [DataMember, DefaultValue(10)]
        [MinValue(0)]
        [MaxValue(999)]
        public int DaxFormatterRequestTimeout
        {
            get => _daxFormatterRequestTimeout;

            set
            {
                if (_daxFormatterRequestTimeout == value) return;
                _daxFormatterRequestTimeout = value;
                NotifyOfPropertyChange(() => DaxFormatterRequestTimeout);
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(DaxFormatterRequestTimeout), value, _isInitializing, this);
            }
        }

        [Category("Defaults")]
        [DisplayName("Separators")]
        [DataMember, DefaultValue(DelimiterType.Comma)]
        [Description("Sets the separator style used within DAX queries and expressions")]
        public DelimiterType DefaultSeparator
        {
            get => _defaultSeparator;

            set
            {
                if (_defaultSeparator == value) return;
                _defaultSeparator = value;
                NotifyOfPropertyChange(() => DefaultSeparator);
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(DefaultSeparator), (int)value, _isInitializing, this);
            }
        }


        [Category("Sounds")]
        [DisplayName("Play a sound after long running operations")]
        [DataMember, DefaultValue(false)]
        [Description("Enable this to play a sound after long running operations (export/benchmark/view metrics/queries)")]
        [SortOrder(50)]
        public bool PlaySoundAfterLongOperation
        {
            get => _playSoundAtQueryEnd;

            set
            {
                if (_playSoundAtQueryEnd == value) return;
                _playSoundAtQueryEnd = value;
                NotifyOfPropertyChange(() => PlaySoundAfterLongOperation);
                NotifyOfPropertyChange(nameof(LongOperationSoundEnabled));
                NotifyOfPropertyChange(nameof(LongQuerySecondsEnabled));
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(PlaySoundAfterLongOperation), (bool)value, _isInitializing, this);
            }
        }
        
        
        [Category("Sounds")]
        [DisplayName("Only Play a sound if not active")]
        [DataMember, DefaultValue(false)]
        [Description("Only play a sound after long running operations if DAX Studio is not the active application")]
        [SortOrder(52)]
        public bool PlaySoundIfNotActive
        {
            get => _playSoundIfNotActive;

            set
            {
                if (_playSoundIfNotActive == value) return;
                _playSoundIfNotActive = value;
                NotifyOfPropertyChange(() => PlaySoundIfNotActive);
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(PlaySoundIfNotActive), (bool)value, _isInitializing, this);
            }
        }


        [Category("Sounds")]
        [DisplayName("Long Operation Sound")]
        [DataMember, DefaultValue(LongOperationSounds.Beep)]
        [Description("The sounds used here are taken from your Windows system sound settings")]
        [SortOrder(55)]
        public LongOperationSounds LongOperationSound
        {
            get => _queryEndSound;

            set
            {
                if (_queryEndSound == value) return;
                _queryEndSound = value;
                NotifyOfPropertyChange(() => LongOperationSound);
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(LongOperationSound), value, _isInitializing, this);
            }
        }

        [Category("Sounds")]
        [DisplayName("Long Query Seconds")]
        [DataMember, DefaultValue(10), MinValue(0),MaxValue(600)]
        [Description("If a query takes more than this number of seconds play the 'Long Operation' sound (use 0 to play a sound after all queries)")]
        [SortOrder(60)]
        public int LongQuerySeconds
        {
            get => _longQuerySeconds;

            set
            {
                if (_longQuerySeconds == value) return;
                _longQuerySeconds = value;
                NotifyOfPropertyChange(() => LongQuerySeconds);
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(LongQuerySeconds), value, _isInitializing, this);
            }
        }

        public bool LongOperationSoundEnabled => PlaySoundAfterLongOperation;
        public bool LongQuerySecondsEnabled => PlaySoundAfterLongOperation;

        [Category("Dax Formatter")]
        [DisplayName("Default Format Style")]
        [DataMember, DefaultValue(DaxFormatStyle.LongLine)]
        public DaxFormatStyle DefaultDaxFormatStyle {
            get => _defaultDaxFormatStyle;

            set {
                if (_defaultDaxFormatStyle == value) return;
                _defaultDaxFormatStyle = value;
                NotifyOfPropertyChange(() => DefaultDaxFormatStyle);
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(DefaultDaxFormatStyle), (int)value, _isInitializing, this);
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
            get => _skipSpaceAfterFunctionName;
            set {
                if (_skipSpaceAfterFunctionName == value) return;
                _skipSpaceAfterFunctionName = value;
                NotifyOfPropertyChange(() => SkipSpaceAfterFunctionName);
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(SkipSpaceAfterFunctionName), value, _isInitializing, this);
            }
        }

        [DataMember, DefaultValue(false)]
        public bool ShowPreReleaseNotifications {
            get => _showPreReleaseNotifications;
            set
            {
                if (_showPreReleaseNotifications == value) return;
                _showPreReleaseNotifications = value;
                NotifyOfPropertyChange(() => ShowPreReleaseNotifications);
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(ShowPreReleaseNotifications), value, _isInitializing, this);
            }
        }

        [DisplayName("Show Basic Statistics")]
        [Category("Metadata Pane")]
        [Subcategory("Tooltips")]
        [Description("Shows basic information like a count of distinct values and the min and max value")]
        [DataMember, DefaultValue(true)]
        public bool ShowTooltipBasicStats
        {
            get => _showTooltipBasicStats;
            set
            {
                if (_showTooltipBasicStats == value) return;
                _showTooltipBasicStats = value;
                NotifyOfPropertyChange(() => ShowTooltipBasicStats);
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue("ShowTooltipBasicStats", value, _isInitializing, this);
            }
        }

        [DisplayName("Show Sample Data")]
        [Category("Metadata Pane")]
        [Subcategory("Tooltips")]
        [Description("Shows the top 10 values for the column")]
        [DataMember, DefaultValue(true)]
        public bool ShowTooltipSampleData
        {
            get => _showTooltipSampleData;
            set
            {
                if (_showTooltipSampleData == value) return;
                _showTooltipSampleData = value;
                NotifyOfPropertyChange(() => ShowTooltipSampleData);
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue("ShowTooltipSampleData", value, _isInitializing, this);
            }
        }

        private bool _canPublishDaxFunctions = true;
        [JsonIgnore]
        public bool CanPublishDaxFunctions
        {
            get => _canPublishDaxFunctions && !BlockExternalServices;

            set
            {
                _canPublishDaxFunctions = value;
                NotifyOfPropertyChange(() => CanPublishDaxFunctions);
            }
        }
        
        [JsonIgnore]
        public string CanPublishDaxFunctionsMessage {
            get
            {
                if (BlockExternalServices) return "Access to External Services blocked in Options";
                if (!_canPublishDaxFunctions) return "Publish Functions is currently running...";
                return string.Empty;
            }
        } 

        private bool _excludeHeadersWhenCopyingResults;
        [Category("Results")]
        [DisplayName("Exclude Headers when Copying Data")]
        [Description("Setting this option will just copy the raw data from the results pane")]
        [DataMember, DefaultValue(true)]
        public bool ExcludeHeadersWhenCopyingResults
        {
            get => _excludeHeadersWhenCopyingResults;

            set
            {
                _excludeHeadersWhenCopyingResults = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue("ExcludeHeadersWhenCopyingResults", value, _isInitializing, this);
                NotifyOfPropertyChange(() => ExcludeHeadersWhenCopyingResults);
            }
        }


        private string _csvDelimiter = ",";
        [Category("Custom Export Format")]
        [DisplayName("'Other' Delimiter")]
        [DataMember, DefaultValue(",")]
        public string CustomCsvDelimiter
        {
            get => _csvDelimiter;

            set
            {
                _csvDelimiter = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(CustomCsvDelimiter), value, _isInitializing, this);
                NotifyOfPropertyChange(() => CustomCsvDelimiter);
            }
        }

        private bool CustomCsvDelimiterEnabled => CustomCsvDelimiterType == CustomCsvDelimiterType.Other;

        private bool _csvQuoteStringFields = true;
        [Category("Custom Export Format")]
        [DisplayName("Quote String Fields")]
        [DataMember, DefaultValue(true)]
        public bool CustomCsvQuoteStringFields
        {
            get => _csvQuoteStringFields;

            set
            {
                _csvQuoteStringFields = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(CustomCsvQuoteStringFields), value, _isInitializing, this);
                NotifyOfPropertyChange(() => CustomCsvQuoteStringFields);
            }
        }

        private int _traceStartupTimeout = 30;
        [Category("Timeouts")]
        [DisplayName("Trace Startup Timeout (secs)")]
        [DataMember, DefaultValue(30), MinValue(0),MaxValue(999)]
        public int TraceStartupTimeout { get => _traceStartupTimeout; set {
                _traceStartupTimeout = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue("TraceStartupTimeout", value, _isInitializing, this);
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
                SettingProvider.SetValue(nameof(CustomCsvDelimiterType), value, _isInitializing, this);
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
                Thread.Sleep(5000);
                HotkeyWarningMessage = string.Empty;
            });
        }

        private string _hotkeyCommentSelection;
        [DataMember, DefaultValue("Ctrl + Alt + C"),Hotkey]
        public string HotkeyCommentSelection { get => _hotkeyCommentSelection;
                set {
                _hotkeyCommentSelection = value;
                if (!_isInitializing) _eventAggregator.PublishOnUIThread(new UpdateHotkeys());
                SettingProvider.SetValue(nameof(HotkeyCommentSelection), value, _isInitializing, this);
                NotifyOfPropertyChange(() => HotkeyCommentSelection);
            } 
        }

        private string _hotkeyUncommentSelection;
        [DataMember, DefaultValue("Ctrl + Alt + U"), Hotkey]
        public string HotkeyUnCommentSelection { get => _hotkeyUncommentSelection;
            set {
                _hotkeyUncommentSelection = value;
                if (!_isInitializing) _eventAggregator.PublishOnUIThread(new UpdateHotkeys());
                SettingProvider.SetValue(nameof(HotkeyUnCommentSelection), value, _isInitializing, this);
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
                SettingProvider.SetValue(nameof(HotkeyToUpper), value, _isInitializing, this);
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
                SettingProvider.SetValue(nameof(HotkeyToLower), value, _isInitializing, this);
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
                SettingProvider.SetValue(nameof(HotkeyRunQuery), value, _isInitializing, this);
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
                SettingProvider.SetValue(nameof(HotkeyRunQueryAlt), value, _isInitializing, this);
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
                SettingProvider.SetValue(nameof(HotkeyNewDocument), value, _isInitializing, this);
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
                SettingProvider.SetValue(nameof(HotkeyNewDocumentWithCurrentConnection), value, _isInitializing, this);
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
                SettingProvider.SetValue(nameof(HotkeySaveDocument), value, _isInitializing, this);
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
                SettingProvider.SetValue(nameof(HotkeyOpenDocument), value, _isInitializing, this);
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
                SettingProvider.SetValue(nameof(HotkeyGotoLine), value, _isInitializing, this);
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
                SettingProvider.SetValue(nameof(HotkeyFormatQueryStandard), value, _isInitializing, this);
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
                SettingProvider.SetValue(nameof(HotkeyFormatQueryAlternate), value, _isInitializing, this);
                NotifyOfPropertyChange(() => HotkeyFormatQueryAlternate);
            }
        }

        private string _hotkeySelectWord;
        [DataMember, DefaultValue("Ctrl + W"), Hotkey]
        public string HotkeySelectWord
        {
            get => _hotkeySelectWord;
            set
            {
                _hotkeySelectWord = value;
                if (!_isInitializing) _eventAggregator.PublishOnUIThread(new UpdateHotkeys());
                SettingProvider.SetValue(nameof(HotkeySelectWord), value, _isInitializing, this);
                NotifyOfPropertyChange(() => HotkeySelectWord);
            }
        }

        private string _hotkeyToggleComment;
        [DataMember, DefaultValue("Ctrl + OemQuestion"), Hotkey]
        public string HotkeyToggleComment
        {
            get => _hotkeyToggleComment;
            set
            {
                _hotkeyToggleComment = value;
                if (!_isInitializing) _eventAggregator.PublishOnUIThread(new UpdateHotkeys());
                SettingProvider.SetValue(nameof(HotkeyToggleComment), value, _isInitializing, this);
                NotifyOfPropertyChange(() => HotkeyToggleComment);
            }
        }


        public void ResetKeyBindings()
        {
            //try
            //{
            //    _isInitializing = true;
                var props = typeof(OptionsViewModel).GetProperties(BindingFlags.Public | BindingFlags.Instance);
                foreach (var prop in props)
                {
                    if (!prop.Name.StartsWith("Hotkey", StringComparison.InvariantCultureIgnoreCase)) continue;

                    foreach (var att in prop.GetCustomAttributes(false))
                    {
                        if (att is DefaultValueAttribute val)
                        {
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
                case CustomCsvDelimiterType.CultureDefault: return CultureInfo.CurrentUICulture.TextInfo.ListSeparator;
                case CustomCsvDelimiterType.Comma: return ",";
                case CustomCsvDelimiterType.Tab: return "\t";
                case CustomCsvDelimiterType.Other: return CustomCsvDelimiter;
                default: return CultureInfo.CurrentUICulture.TextInfo.ListSeparator;

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

        public string CultureSpecificListDelimiter => CultureInfo.CurrentUICulture.TextInfo.ListSeparator;

        #endregion

        #region "Preview Toggles"

        // Preview Feature Toggles

        private bool _showExportMetrics;
        [DataMember, DefaultValue(false)]
        public bool ShowExportMetrics
        {
            get => _showExportMetrics;

            set
            {
                _showExportMetrics = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(ShowExportMetrics), value, _isInitializing, this);
                NotifyOfPropertyChange(() => ShowExportMetrics);
            }
        }

        //private bool _showExternalTools = false;
        //[DataMember, DefaultValue(false)]
        //public bool ShowExternalTools { get => _showExternalTools;
        //    set {
        //        _showExternalTools = value;
        //        _eventAggregator.PublishOnUIThread(new Events.UpdateGlobalOptions());
        //        SettingProvider.SetValueAsync(nameof(ShowExternalTools), value, _isInitializing, this);
        //        NotifyOfPropertyChange(() => ShowExternalTools);
        //    }
        //}

        private bool _showExportAllData;
        [DataMember, DefaultValue(false)]
        public bool ShowExportAllData { get => _showExportAllData;
            set {
                _showExportAllData = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(ShowExportAllData), value, _isInitializing, this);
                NotifyOfPropertyChange(() => ShowExportAllData);
            }
        }

        private bool _vpaxIncludeTom;
        [DataMember, DefaultValue(false)]
        public bool VpaxIncludeTom {
            get => _vpaxIncludeTom;
            set {
                _vpaxIncludeTom = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(VpaxIncludeTom), value, _isInitializing, this);
                NotifyOfPropertyChange(() => VpaxIncludeTom);
            }
        }




        private bool _showKeyBindings;
        [DataMember, DefaultValue(false)]
        public bool ShowKeyBindings
        {
            get => _showKeyBindings;

            set
            {
                _showKeyBindings = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(ShowKeyBindings), value, _isInitializing, this);
                NotifyOfPropertyChange(() => ShowKeyBindings);
            }
        }

        private bool _showDebugCommas;
        [DataMember, DefaultValue(false)]
        public bool ShowDebugCommas
        {
            get => _showDebugCommas;

            set
            {
                _showDebugCommas = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(ShowDebugCommas), value, _isInitializing, this);
                NotifyOfPropertyChange(() => ShowDebugCommas);

            }
        }

        private bool _showXmlaInAllQueries;
        [DataMember, DefaultValue(false)]
        public bool ShowXmlaInAllQueries
        {
            get => _showXmlaInAllQueries;

            set
            {
                _showXmlaInAllQueries = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(ShowXmlaInAllQueries), value, _isInitializing, this);
                NotifyOfPropertyChange(() => ShowXmlaInAllQueries);
            }
        }

        #endregion


        private string _theme = "Light";
        [DataMember, DefaultValue("Light")]
        public string Theme
        {
            get => _theme;
            set
            {
                if (_theme == value) return;
                _theme = value;
                NotifyOfPropertyChange(() => Theme);
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue("Theme", value, _isInitializing, this);

            }
        }

        private bool _resultAutoFormat;
        [Category("Results")]
        [SortOrder(10)]
        [DisplayName("Automatic Format Results")]
        [Description("Setting this option will automatically format numbers in the query results pane if a format string is not available for a measure with the same name as the column in the output")]
        [DataMember, DefaultValue(false)]
        public bool ResultAutoFormat {
            get => _resultAutoFormat;
            set {
                _resultAutoFormat = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue("ResultAutoFormat", value, _isInitializing, this);
                NotifyOfPropertyChange(() => ResultAutoFormat);
            }
        }
                
        private string _defaultDateAutoFormat;
        [Category("Results")]
        [SortOrder(20)]
        [DisplayName("Default Date Automatic Format")]
        [Description("The automatic format result will use this setting to format dates column, keep it empty to get the default format.")]
        [DataMember]
        [DefaultValue("yyyy-MM-dd")]
        public string DefaultDateAutoFormat
        {
            get => _defaultDateAutoFormat;
            set
            {
                _defaultDateAutoFormat = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue("DefaultDateAutoFormat", value, _isInitializing, this);
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
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue("ScaleResultsFontWithEditor", value, _isInitializing, this);
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
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue("CodeCompletionWindowWidthIncrease", value, _isInitializing, this);
                NotifyOfPropertyChange(() => CodeCompletionWindowWidthIncrease);
            }
        }

        private bool _keepMetadataSearchOpen;
        [Category("Metadata Pane")]
        [Subcategory("Search")]
        [DisplayName("Keep Metadata Search Open")]
        [DataMember, DefaultValue(true)]
        public bool KeepMetadataSearchOpen { 
            get => _keepMetadataSearchOpen;
            set {
                _keepMetadataSearchOpen = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue("KeepMetadataSearchOpen", value, _isInitializing, this);
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
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue("AutoRefreshMetadataLocalMachine", value, _isInitializing, this);
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
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue("AutoRefreshMetadataLocalNetwork", value, _isInitializing, this);
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
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue("AutoRefreshMetadataCloud", value, _isInitializing, this);
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
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(ShowMetadataRefreshPrompt), value, _isInitializing, this);
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
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(ShowHiddenMetadata), value, _isInitializing, this);
                NotifyOfPropertyChange(() => ShowHiddenMetadata);
            } 
        }

        private int _previewDataRowLimit = 500;
        [Category("Metadata Pane")]
        [Subcategory("Preview Data")]
        [DisplayName("Row Limit for Preview Data")]
        [Description("This limits the amount of rows when previewing data, setting this to 0 will display all the data. (setting to 0 is not recommended as this can result in memory and performance issues with large tables)")]
        [DataMember, DefaultValue(500), MaxValue(2000000000), MinValue(0)]
        public int PreviewDataRowLimit
        {
            get => _previewDataRowLimit;
            set
            {
                _previewDataRowLimit = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(PreviewDataRowLimit), value, _isInitializing, this);
                NotifyOfPropertyChange(nameof(PreviewDataRowLimit));
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
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(SetClearCacheAsDefaultRunStyle), value, _isInitializing, this);
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
                SettingProvider.SetValue(nameof(SortFoldersFirstInMetadata), value, _isInitializing, this);
                NotifyOfPropertyChange(() => SortFoldersFirstInMetadata);
            }
        }


        private string _windowPosition = string.Empty;
        [DataMember, DefaultValue(DefaultWindowPosition)]
        public string WindowPosition {
            get => _windowPosition;
            set {
                _windowPosition = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(WindowPosition), value, _isInitializing, this);
                NotifyOfPropertyChange(() => WindowPosition);
            }
        }

        private Version _dismissedVersion = new Version();
        [DataMember, DefaultValue("0.0.0.0")]
        [JsonConverter(typeof(VersionConverter))]
        public Version DismissedVersion
        {
            get => _dismissedVersion;
            set
            {
                _dismissedVersion = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(DismissedVersion), value, _isInitializing, this);
                NotifyOfPropertyChange(() => DismissedVersion);
            }
        }

        private DateTime _lastVersionCheck = DateTime.MinValue;
        [DataMember, DefaultValue("1900-01-01 00:00:00Z")]
        public DateTime LastVersionCheckUTC
        {
            get => _lastVersionCheck;
            set
            {
                _lastVersionCheck = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(LastVersionCheckUTC), value, _isInitializing, this);
                NotifyOfPropertyChange(() => LastVersionCheckUTC);
            }
        }

        private Version _currentDownloadVersion = new Version(0,0,0,0);
        [DataMember, DefaultValue("0.0.0.0")]
        public Version CurrentDownloadVersion
        {
            get => _currentDownloadVersion;
            set
            {
                _currentDownloadVersion = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(CurrentDownloadVersion), value, _isInitializing, this);
                NotifyOfPropertyChange(() => CurrentDownloadVersion);
            }
        }

        private ObservableCollection<IDaxFile> _recentFiles;
        [DataMember]
        //[JsonConverter(typeof(DaxFileConverter))]
        public ObservableCollection<IDaxFile> RecentFiles
        {
            get => _recentFiles ?? (_recentFiles = new ObservableCollection<IDaxFile>());
            set
            {
                if (value != null) {
                    _recentFiles = value;
                }

            }
        }


        private ObservableCollection<string> _recentServers;
        [DataMember]
        public ObservableCollection<string> RecentServers { get => _recentServers ?? (_recentServers = new ObservableCollection<string>());
            set {
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
                SettingProvider.SetValue(nameof(EditorConvertTabsToSpaces), value, _isInitializing, this);
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
                SettingProvider.SetValue(nameof(EditorIndentationSize), value, _isInitializing, this);
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
                SettingProvider.SetValue(nameof(EditorWordWrap), value, _isInitializing, this);
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
                SettingProvider.SetValue(nameof(ShowUserInTitlebar), value, _isInitializing, this);
                NotifyOfPropertyChange(() => ShowUserInTitlebar);
            }
        }

        private bool _blockAllInternetAccess;
        // this setting is set by the installer writing a non-zero value to HKLM:\Software\DaxStudio\BlockAllInternetAccess
        [Category("Privacy")]
        [DisplayName("Block All Internet Access")]
        [Description("[NOT RECOMMENDED] Stops DAX Studio from all external access. This option can only be set by an administrator during an 'All Users' install and overrides all the other options below. (and they will show up as disabled when this option has been set)")]
        [DataMember, DefaultValue(false)]
        public bool BlockAllInternetAccess { get => _blockAllInternetAccess;
            set
            {
                _blockAllInternetAccess = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                NotifyOfPropertyChange();
                // NOTE: we do not write this change to the provider since it is 
                //       saved in HKEY_LOCAL_MACHINE and can only be updated by the setup program
            }
        }

        public bool BlockAllInternetAccessEnabled => false;

        private bool _blockVersionChecks;
        [Category("Privacy")]
        [DisplayName("Block Version Checks")]
        [Description("[NOT RECOMMENDED] Stops DAX Studio from checking for and notifying of available updates")]
        [DataMember, DefaultValue(false)]
        public bool BlockVersionChecks
        {
            get => _blockVersionChecks || BlockAllInternetAccess;
            set
            {
                _blockVersionChecks = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(BlockVersionChecks), value, _isInitializing, this);
                NotifyOfPropertyChange(() => BlockVersionChecks);
            }
        }

        public bool BlockVersionChecksEnabled => !BlockAllInternetAccess;

        private bool _blockCrashReporting;
        [Category("Privacy")]
        [DisplayName("Block Crash Reporting")]
        [Description("[NOT RECOMMENDED] Stops DAX Studio from sending crash reports to the developer. There is a small chance that the screenshot of the crash could include personal information. Although you can untick the option to include the screenshot in the report if this is the case.")]
        [DataMember, DefaultValue(false)]
        public bool BlockCrashReporting
        {
            get => _blockCrashReporting || BlockAllInternetAccess;
            set
            {
                _blockCrashReporting = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(BlockCrashReporting), value, _isInitializing, this);
                NotifyOfPropertyChange(() => BlockCrashReporting);
            }
        }

        public bool BlockCrashReportingEnabled => !BlockAllInternetAccess;

        private bool _blockExternalServices;
        [Category("Privacy")]
        [DisplayName("Block External Services")]
        [Description("[NOT RECOMMENDED] Stops DAX Studio from accessing external services (such as DaxFormatter.com). We never send any data externally, but there is a small chance that query text might contain personal information if you were writing queries that filtered for specific information like Customer Names")]
        [DataMember, DefaultValue(false)]
        public bool BlockExternalServices
        {
            get => _blockExternalServices || BlockAllInternetAccess;
            set
            {
                _blockExternalServices = value;
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(BlockExternalServices), value, _isInitializing, this);
                NotifyOfPropertyChange(() => BlockExternalServices);
                NotifyOfPropertyChange(() => CanPublishDaxFunctions);
                NotifyOfPropertyChange(() => CanPublishDaxFunctionsMessage);
            }
        }

        public bool BlockExternalServicesEnabled => !BlockAllInternetAccess;

        private LogEventLevel _loggingLevel = LogEventLevel.Warning; 
        [Category("Logging")]
        [DisplayName("Logging Level")]
        [Description("Sets the minimum level of information recorded in the log file (eg Error would log Error and Fatal events)")]
        [DataMember, DefaultValue(LogEventLevel.Warning)]
        public LogEventLevel LoggingLevel { get => _loggingLevel;
            set {
                _loggingLevel = value;
                if (LoggingLevelSwitch != null)
                {
                    LoggingLevelSwitch.MinimumLevel = LogEventLevel.Information;
                    Log.Information(Constants.LogMessageTemplate, nameof(OptionsViewModel), nameof(LoggingLevel), $"Setting Logging Level to {_loggingLevel} base on user options");
                    LoggingLevelSwitch.MinimumLevel = _loggingLevel;
                }
                _eventAggregator.PublishOnUIThread(new UpdateGlobalOptions());
                SettingProvider.SetValue(nameof(LoggingLevel), value, _isInitializing, this);
                NotifyOfPropertyChange();
            }
        }


        private Serilog.Core.LoggingLevelSwitch _loggingLevelSwitch;
        public Serilog.Core.LoggingLevelSwitch LoggingLevelSwitch { get => _loggingLevelSwitch;
            set {
                _loggingLevelSwitch = value;
                _loggingLevelSwitch.MinimumLevel = _loggingLevel;
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

        public bool AnyExternalAccessAllowed()
        {
            return !BlockCrashReporting || !BlockExternalServices || !BlockVersionChecks;
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
            get
            {
                if (IsExcelAddinEnabledForCurrentUser())
                    return "Disable Excel Addin";
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
            return Path.GetDirectoryName(Assembly.GetEntryAssembly().Location).Replace("\\","/");
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

        private bool _hasShowQueryBuilderAutoGenerateWarning;
        [DataMember, DefaultValue(false)]
        public bool HasShownQueryBuilderAutoGenerateWarning { get => _hasShowQueryBuilderAutoGenerateWarning;
            set {
                _hasShowQueryBuilderAutoGenerateWarning = value;
                SettingProvider.SetValue(nameof(HasShownQueryBuilderAutoGenerateWarning), value, _isInitializing, this);
                NotifyOfPropertyChange(() => HasShownQueryBuilderAutoGenerateWarning);
            } 
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
                Debug.WriteLine($"OptionsViewModel.SearchText = {value}");
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

            foreach (var prop in GetType().GetProperties())
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


        public void PlaySound(PropertyBinding<object> param)
        {
            if (param?.Value is LongOperationSounds sound) PlaySound(sound);
        }

        public void PlayLongOperationSound(int currentOperationSeconds)
        {
            // if the time thresholds have not been breached exit here
            if (LongQuerySeconds > 0
                && currentOperationSeconds >= 0
                && currentOperationSeconds < LongQuerySeconds) return;

            // if the app is active and sounds should only be played when inactive then exit here
            if (PlaySoundIfNotActive && ApplicationHelper.IsApplicationActive()) return;
            
            // otherwise play the selected sound
            PlaySound(LongOperationSound);
        }


        public void PlaySound(LongOperationSounds value)
        {
            try
            {
                switch (value)
                {
                    case LongOperationSounds.Asterisk:
                        SystemSounds.Asterisk.Play();
                        break;
                    case LongOperationSounds.Beep:
                        SystemSounds.Beep.Play();
                        break;
                    case LongOperationSounds.Exclamation:
                        SystemSounds.Exclamation.Play();
                        break;
                    case LongOperationSounds.Hand:
                        SystemSounds.Hand.Play();
                        break;
                    case LongOperationSounds.Question:
                        SystemSounds.Question.Play();
                        break;
                    default:
                        SystemSounds.Beep.Play();
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(OptionsViewModel), nameof(PlaySound), "Error while trying to play a SystemSound");
            }
        }

        protected override void OnActivate()
        {
            base.OnActivate();
            this.Refresh();
            // trigger an update of the current properties pane by faking a category change
            NotifyOfPropertyChange(nameof(SelectedCategory));
        }

        #region IDisposable Support
        private bool _disposedValue; // To detect redundant calls
        private MultipleQueriesDetectedOnPaste _removeDirectQueryCode;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    if (_proxySecurePassword != null)
                    {
                        _proxySecurePassword.Dispose();
                        _proxySecurePassword = null;
                    }
                }


                _disposedValue = true;
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
