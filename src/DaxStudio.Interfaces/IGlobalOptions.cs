﻿using DaxStudio.Interfaces.Enums;
using System;
using System.ComponentModel;
using System.Security;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using DaxStudio.Interfaces.Attributes;
using Serilog.Events;
using Serilog.Core;
using Dax.Metadata;

namespace DaxStudio.Interfaces
{
    public interface IGlobalOptions: IGlobalOptionsBase, IVpaOptions
    {
        bool AutoHideMetadataVerticalScrollbars { get; set; }
        bool AutoRefreshMetadataCloud { get; set; }
        bool AutoRefreshMetadataLocalMachine { get; set; }
        bool AutoRefreshMetadataLocalNetwork { get; set; }

        bool BenchmarkColdCacheSwitchedOn { get; set; }
        bool BenchmarkWarmCacheSwitchedOn { get; set; }
        int BenchmarkColdCacheRuns { get;set; }
        int BenchmarkWarmCacheRuns { get;set; }
        bool CanPublishDaxFunctions { get; set; }
        int CodeCompletionWindowWidthIncrease { get; set; }
        Version CurrentDownloadVersion { get; set; }
        CustomCsvDelimiterType CustomCsvDelimiterType { get; set; }
        bool CustomCsvQuoteStringFields { get; set; }
        
        int DaxFormatterRequestTimeout { get; set; }
        string DefaultDateAutoFormat { get; set; }
        DaxFormatStyle DefaultDaxFormatStyle { get; set; }
        DelimiterType DefaultSeparator { get; set; }
        Version DismissedVersion { get; set; }
        bool EditorConvertTabsToSpaces { get; set; }
        bool EditorEnableIntellisense { get; set; }
        string EditorFontFamily { get; set; }
        [JsonIgnore] double EditorFontSizePx { get; }
        int EditorIndentationSize { get; set; }
        MultipleQueriesDetectedOnPaste EditorMultipleQueriesDetectedOnPaste { get; set; }
        bool EditorShowFunctionInsightsOnHover { get; set; }
        bool EditorShowLineNumbers { get; set; }
        bool EditorWordWrap { get; set; }
        bool ExcludeHeadersWhenCopyingResults { get; set; }
        DateTime LastVersionCheckUTC { get; set; }
        int PreviewDataRowLimit { get; set; }
        string ProxyAddress { get; set; }
        bool ProxyUseSystem { get; set; }
        string ProxyUser { get; set; }
        SecureString ProxySecurePassword { get; set; }
        [JsonIgnore] double ResultFontSizePx { get; }
        string ResultFontFamily { get; set; }
        int QueryEndEventTimeout { get; set; }
        int QueryHistoryMaxItems { get; set; }
        bool QueryHistoryShowTraceColumns { get; set; }
        ObservableCollection<IDaxFile> RecentFiles { get; }
        ObservableCollection<string> RecentServers { get; }
        bool ResultAutoFormat { get; set; }
        bool ScaleResultsFontWithEditor { get; set; }
        bool SetClearCacheAsDefaultRunStyle { get; set; }
        bool ShowHelpWatermark { get; set; }
        bool ShowHiddenMetadata { get; set; }
        bool ShowPreReleaseNotifications { get; set; }
        bool ShowUserInTitlebar { get; set; }
        bool ShowTooltipBasicStats { get; set; }
        bool ShowTooltipSampleData { get; set; }
        bool SkipSpaceAfterFunctionName { get; set; }
        bool SortFoldersFirstInMetadata { get; set; }
        UITheme Theme { get; set; }
        UITheme AutoTheme { get; set; }
        int TraceStartupTimeout { get; set; }
        string WindowPosition { get; set; }





        bool PlaySoundAfterLongOperation { get; set; }
        bool PlaySoundIfNotActive { get; set; }
        LongOperationSounds LongOperationSound { get; set; }
        int LongQuerySeconds { get; set; }

        int PowerPivotModelDetectionTimeout { get; set; }

        bool ShowDatabaseDialogOnConnect { get; set; }

        #region Hotkeys

        [JsonIgnore] string HotkeyWarningMessage { get; set; }

        // Hotkeys
        [Hotkey] string HotkeyCommentSelection { get; set; }
        [Hotkey] string HotkeyUnCommentSelection { get; set; }
        [Hotkey] string HotkeyToUpper { get; set; }
        [Hotkey] string HotkeyToLower { get; set; }
        [Hotkey] string HotkeyRunQuery { get; set; }
        [Hotkey] string HotkeyRunQueryAlt { get; set; }
        [Hotkey] string HotkeyNewDocument { get; set; }
        [Hotkey] string HotkeyNewDocumentWithCurrentConnection { get; set; }
        [Hotkey] string HotkeyOpenDocument { get; set; }
        [Hotkey] string HotkeySaveDocument { get; set; }
        [Hotkey] string HotkeyGotoLine { get; set; }
        [Hotkey] string HotkeyFormatQueryStandard { get; set; }
        [Hotkey] string HotkeyFormatQueryAlternate { get; set; }
        [Hotkey] string HotkeyCopySEQuery { get; set; }
        [Hotkey] string HotkeyCopyPasteServerTimings { get; set; }
        [Hotkey] string HotkeyCopyPasteServerTimingsData { get; set; }
        [Hotkey] string HotkeySelectWord { get; set; }
        [Hotkey] string HotkeyToggleComment { get; set; }
        [Hotkey] string HotkeyDebugCommas { get; set; }
        [Hotkey] string HotkeySwapDelimiters { get; set; }
        #endregion

        // Preview Features
        bool ShowCopyMetricsComments { get; set; }
        bool VpaxIncludeTom { get; set; }
        
        
        
        
        bool UseIndentCodeFolding { get; set; }
        bool ShowDebugCommas { get; set; }
        bool ShowXmlaInAllQueries { get; set; }
        bool EnablePasteFileOnExistingWindow { get; set; }
        bool ShowTotalDirectQueryDuration { get; set; }
        bool ShowFEBenchmark { get; set; }
        bool ShowStorageEngineNetParallelDuration { get; set; }

        bool HighlightXmSqlCallbacks { get; set; }
        bool SimplifyXmSqlSyntax { get; set; }
        bool ReplaceXmSqlColumnNames { get; set; }
        bool ReplaceXmSqlTableNames { get; set; }
        bool FormatXmSql { get; set; }
        bool FormatDirectQuerySql { get; set; }
        bool SendMetadataToQueryBuilderIfOpen { get; set; }

        #region Methods

        // Methods
        string GetCustomCsvDelimiter();
        System.Text.Encoding GetCustomCsvEncoding();
        void Initialize();

        // 
        [JsonIgnore] bool IsRunningPortable { get; set; }
        
        bool ShowMetadataRefreshPrompt { get; set; }
        [JsonIgnore] bool BlockAllInternetAccess { get; set; }
        bool BlockVersionChecks { get; set; }
        bool BlockCrashReporting { get; set; }
        bool BlockExternalServices { get; set; }
        bool HasShownQueryBuilderAutoGenerateWarning { get; set; }

        bool AnyExternalAccessAllowed();

        void PlayLongOperationSound(int currentOperationSeconds);

        LogEventLevel LoggingLevel { get; set; }
        LogEventLevel TemporaryLoggingLevel { get; set; }
        LoggingLevelSwitch LoggingLevelSwitch { get; set; }
        #endregion
        [JsonIgnore] bool GettingStartedShown { get; set; }
        bool IncludeHyperlinkOnCopy { get; set; }
        int DefaultTextFileType { get; set; }

        StorageEventTimelineStyle StorageEventHeatmapStyle { get; set; }
        bool ExportServerTimingDetailsToFolder { get; set; }

    }
}
