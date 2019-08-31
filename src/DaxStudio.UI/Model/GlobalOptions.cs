using DaxStudio.Interfaces;
using DaxStudio.Interfaces.Enums;
using System;
using System.Security;

namespace DaxStudio.UI.Model
{
    public class GlobalOptions : IGlobalOptions
    {
        public bool EditorShowLineNumbers { get; set; }

        public double EditorFontSizePx => throw new NotImplementedException();

        public string EditorFontFamily { get; set; }

        public double ResultFontSizePx => throw new NotImplementedException();

        public string ResultFontFamily { get; set; }
        public bool EditorEnableIntellisense { get;set; }
        public int QueryHistoryMaxItems { get;set; }
        public bool QueryHistoryShowTraceColumns { get; set; }
        public bool ProxyUseSystem { get; set; }
        public string ProxyAddress { get; set; }
        public string ProxyUser { get; set; }
        public SecureString ProxySecurePassword { get; set; }
        public int QueryEndEventTimeout { get; set; }
        public int DaxFormatterRequestTimeout { get; set; }
        public int TraceStartupTimeout { get; set; }
        public DelimiterType DefaultSeparator { get; set; }
        public DaxFormatStyle DefaultDaxFormatStyle { get; set; }
        public bool TraceDirectQuery { get; set; }
        public bool ShowPreReleaseNotifcations { get; set; }
        public bool ShowTooltipBasicStats { get; set; }
        public bool ShowTooltipSampleData { get; set; }
        public bool CanPublishDaxFunctions { get; set; }
        public bool ExcludeHeadersWhenCopyingResults { get; set; }
        public bool ShowExportMetrics { get; set; }
        public bool ShowExternalTools { get; set; }
        public bool ShowExportAllData { get; set; }
        public bool VpaxIncludeTom { get; set; }
        public bool ResultAutoFormat { get; set; }
        public bool ScaleResultsFontWithEditor { get; set; }
        public int CodeCompletionWindowWidthIncrease { get; set; }
        public bool KeepMetadataSearchOpen { get; set; }
        public string Theme { get; set; }
        public bool AutoRefreshMetadataLocalMachine { get; set; }
        public bool AutoRefreshMetadataLocalNetwork { get; set; }
        public bool AutoRefreshMetadataCloud { get; set; }
        public bool ShowHiddenMetadata { get; set; }
        public bool SetClearCacheAsDefaultRunStyle { get; set; }
        public bool SortFoldersFirstInMetadata { get; set; }
        public string WindowPosition { get; set; }
        public Version DismissedVersion { get; set; }
        public DateTime LastVersionCheckUTC { get; set; }
    }
}
