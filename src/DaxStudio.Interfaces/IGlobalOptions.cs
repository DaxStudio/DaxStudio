using DaxStudio.Interfaces.Enums;
using System;
using System.ComponentModel;
using System.Security;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using DaxStudio.Interfaces.Attributes;

namespace DaxStudio.Interfaces
{
    public interface IGlobalOptions: IGlobalOptionsBase
    {
        [DefaultValue(true)] bool EditorShowLineNumbers { get; set; }
        [JsonIgnore] double EditorFontSizePx { get; }
        string EditorFontFamily { get; set; }
        [JsonIgnore] double ResultFontSizePx { get; }
        string ResultFontFamily { get; set; }
        bool EditorEnableIntellisense { get; set; }
        int QueryHistoryMaxItems { get; set; }
        bool QueryHistoryShowTraceColumns { get; set; }
        bool ProxyUseSystem { get; set; }
        string ProxyAddress { get; set; }
        bool ShowHelpWatermark { get; set; }
        string ProxyUser { get; set; }
        SecureString ProxySecurePassword { get; set; }
        int QueryEndEventTimeout { get; set; }
        int DaxFormatterRequestTimeout { get; set; }
        int TraceStartupTimeout { get; set; }
        DelimiterType DefaultSeparator { get; set; }
        DaxFormatStyle DefaultDaxFormatStyle { get; set; }
        bool SkipSpaceAfterFunctionName { get; set; }

        bool ShowPreReleaseNotifications { get; set; }
        bool ShowTooltipBasicStats { get; set; }
        bool ShowTooltipSampleData { get; set; }
        bool CanPublishDaxFunctions { get; set; }
        bool ExcludeHeadersWhenCopyingResults { get; set; }

        bool ResultAutoFormat { get; set; }
        string DefaultDateAutoFormat { get; set; }
        bool ScaleResultsFontWithEditor { get; set; }
        int CodeCompletionWindowWidthIncrease { get; set; }
        bool KeepMetadataSearchOpen { get; set; }
        bool SortFoldersFirstInMetadata { get; set; }
        bool ShowHiddenMetadata { get; set; }
        bool SetClearCacheAsDefaultRunStyle { get; set; }
        string WindowPosition { get; set; }
        bool AutoRefreshMetadataCloud { get; set; }
        bool AutoRefreshMetadataLocalNetwork { get; set; }
        bool AutoRefreshMetadataLocalMachine { get; set; }
        DateTime LastVersionCheckUTC { get; set; }
        Version DismissedVersion { get; set; }
        string Theme { get; set; }
        bool CustomCsvQuoteStringFields { get; set; }
        CustomCsvDelimiterType CustomCsvDelimiterType { get; set; }
        ObservableCollection<IDaxFile> RecentFiles { get; }
        ObservableCollection<string> RecentServers { get; }

        bool EditorConvertTabsToSpaces { get; set; }
        int EditorIndentationSize { get; set; }
        MultipleQueriesDetectedOnPaste EditorMultipleQueriesDetectedOnPaste { get; set; }
        bool ShowUserInTitlebar { get; set; }

        Version CurrentDownloadVersion { get; set; }
        
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

        #endregion

        // Preview Features
        bool ShowExportMetrics { get; set; }
        bool ShowExportAllData { get; set; }
        bool VpaxIncludeTom { get; set; }
        int VpaxSampleReferentialIntegrityViolations { get; set; }

        bool HighlightXmSqlCallbacks { get; set; }
        bool SimplifyXmSqlSyntax { get; set; }
        bool ReplaceXmSqlColumnNames { get; set; }

        #region Methods

        // Methods
        string GetCustomCsvDelimiter();
        void Initialize();

        // 
        [JsonIgnore] bool IsRunningPortable { get; set; }
        bool EditorWordWrap { get; set; }
        bool ShowMetadataRefreshPrompt { get; set; }
        [JsonIgnore] bool BlockAllInternetAccess { get; set; }
        bool BlockVersionChecks { get; set; }
        bool BlockCrashReporting { get; set; }
        bool BlockExternalServices { get; set; }

        bool AnyExternalAccessAllowed();

        #endregion
    }
}
