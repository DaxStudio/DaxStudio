using DaxStudio.Interfaces.Enums;
using System;
using System.ComponentModel;
using System.Security;


namespace DaxStudio.Interfaces
{
    public interface IGlobalOptions
    {
        [DefaultValue(true)]
        bool EditorShowLineNumbers { get; set; }
        double EditorFontSizePx { get; }
        string EditorFontFamily { get; set; }
        double ResultFontSizePx { get; }
        string ResultFontFamily { get; set; }
        bool EditorEnableIntellisense { get; set; }
        int QueryHistoryMaxItems { get; set; }
        bool QueryHistoryShowTraceColumns { get; set; }
        bool ProxyUseSystem { get; set; }
        string ProxyAddress { get; set; }
        string ProxyUser { get; set; }
        SecureString ProxySecurePassword { get; set; }
        int QueryEndEventTimeout { get; set; }
        int DaxFormatterRequestTimeout { get; set; }
        int TraceStartupTimeout { get; set; }
        DelimiterType DefaultSeparator { get; set; }
        DaxFormatStyle DefaultDaxFormatStyle { get; set; }
        bool TraceDirectQuery { get; set; }
        bool ShowPreReleaseNotifcations { get; set; }
        bool ShowTooltipBasicStats { get; set; }
        bool ShowTooltipSampleData { get; set; }
        bool CanPublishDaxFunctions { get; set; }
        bool ExcludeHeadersWhenCopyingResults { get; set; }

        bool ResultAutoFormat { get; set; }
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

        // Preview Features
        bool ShowExportMetrics { get; set; }
        bool ShowExternalTools { get; set; }
        bool ShowExportAllData { get; set; }
        bool VpaxIncludeTom { get; set; }

        // Methods
        string GetCustomCsvDelimiter();
    }
}
