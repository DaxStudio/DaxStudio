﻿using DaxStudio.Interfaces;
using DaxStudio.Interfaces.Enums;
using System;
using System.Collections.ObjectModel;
using System.Security;

namespace DaxStudio.SignalR
{
    class StubGlobalOptions : IGlobalOptions
    {
        public int DaxFormatterRequestTimeout { get; set; }

        public DelimiterType DefaultSeparator { get; set; }

        public DaxFormatStyle DefaultDaxFormatStyle { get; set; }

        public bool EditorEnableIntellisense { get; set; }

        public string EditorFontFamily { get; set; }

        public double EditorFontSizePx { get; set; }

        public string ResultFontFamily { get; set; }

        public double ResultFontSizePx { get; set; }

        public bool EditorShowLineNumbers { get; set; }

        public bool CanPublishDaxFunctions { get; set; }

        public string ProxyAddress { get; set; }
        public bool ShowHelpWatermark { get; set; }

        public SecureString ProxySecurePassword { get; set; }

        public string ProxyUser { get; set; }

        public bool ProxyUseSystem { get; set; }

        public int QueryEndEventTimeout { get; set; }

        public int QueryHistoryMaxItems { get; set; }

        public bool QueryHistoryShowTraceColumns { get; set; }

        public bool ShowPreReleaseNotifications { get; set; }

        public bool ShowTooltipBasicStats { get; set; }

        public bool ShowTooltipSampleData { get; set; }

        public bool TraceDirectQuery
        {
            get => false;

            set => throw new NotImplementedException();
        }

        public bool ExcludeHeadersWhenCopyingResults { get; set; }

        public bool ShowExportMetrics { get; set; }
        public int TraceStartupTimeout { get; set; }
        public bool ShowExportAllData { get ; set ; }
        public bool ShowAggregationRewritesInAllQueries { get; set ; }
        public string Theme { get; set; }
        public bool ResultAutoFormat { get; set; }
        public bool ScaleResultsFontWithEditor { get; set; }
        public int CodeCompletionWindowWidthIncrease { get;set; }
        public bool KeepMetadataSearchOpen { get;set; }
        public bool AutoRefreshMetadataLocalMachine { get; set; }
        public bool AutoRefreshMetadataLocalNetwork { get; set; }
        public bool AutoRefreshMetadataCloud { get; set; }
        public bool ShowHiddenMetadata { get; set; }
        public bool SetClearCacheAsDefaultRunStyle { get; set; }
        public bool ShowAutoDateTables { get; set; }
        public bool SortFoldersFirstInMetadata { get;set; }
        public string WindowPosition { get; set; }
        public Version DismissedVersion { get; set; }
        public DateTime LastVersionCheckUTC { get;set; }
        public bool VpaxIncludeTom { get;set; }
        public int VpaxSampleReferentialIntegrityViolations { get; set; }

        public bool CustomCsvQuoteStringFields { get; set; }
        public CustomCsvDelimiterType CustomCsvDelimiterType { get; set; }
        public ObservableCollection<IDaxFile> RecentFiles { get; set; }
        public ObservableCollection<string> RecentServers { get; set; }
        public bool EditorConvertTabsToSpaces { get; set; }
        public int EditorIndentationSize { get; set; }
        public bool IsRunningPortable { get; set; }
        public string HotkeyCommentSelection { get;set; }
        public string HotkeyUnCommentSelection { get; set; }
        public string HotkeyToUpper { get; set; }
        public string HotkeyToLower { get; set; }
        public string HotkeyRunQuery { get; set; }
        public string HotkeyRunQueryAlt { get; set; }
        public string HotkeyNewDocument { get; set; }
        public string HotkeyNewDocumentWithCurrentConnection { get; set; }
        public string HotkeyOpenDocument { get; set; }
        public string HotkeySaveDocument { get; set; }
        public string HotkeyGotoLine { get; set; }
        public string HotkeyFormatQueryStandard { get; set; }
        public string HotkeyFormatQueryAlternate { get; set; }
        public bool ShowUserInTitlebar { get; set; }
        public bool EditorWordWrap { get; set; }
        public bool SkipSpaceAfterFunctionName { get; set; }
        public bool ShowPreviewQueryBuilder { get; set; }
        public bool ShowPreviewBenchmark { get; set; }
        public Version CurrentDownloadVersion { get; set; }
        public bool ShowMetadataRefreshPrompt { get; set; }
        public bool BlockAllInternetAccess { get; set; }
        public bool BlockVersionChecks { get; set; }
        public bool BlockCrashReporting { get; set; }
        public bool BlockExternalServices { get; set; }
        public string HotkeyWarningMessage { get; set; }
        public string DefaultDateAutoFormat { get; set; }
        public MultipleQueriesDetectedOnPaste EditorMultipleQueriesDetectedOnPaste { get; set; }
        public bool HighlightXmSqlCallbacks { get; set; }
        public bool SimplifyXmSqlSyntax { get; set; }
        public bool ReplaceXmSqlColumnNames { get; set; }

        public string GetCustomCsvDelimiter()
        {
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ListSeparator;
        }

        public void Initialize() { }
    }
}
