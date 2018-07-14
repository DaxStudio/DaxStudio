using DaxStudio.Interfaces.Enums;
using System.Security;


namespace DaxStudio.Interfaces
{
    public interface IGlobalOptions
    {
        bool EditorShowLineNumbers { get; set; }
        double EditorFontSize { get; set; }
        string EditorFontFamily { get; set; }
        bool EditorEnableIntellisense { get; set; }
        int QueryHistoryMaxItems { get; set; }
        bool QueryHistoryShowTraceColumns { get; set; }
        bool ProxyUseSystem { get; set; }
        string ProxyAddress { get; set; }
        string ProxyUser { get; set; }
        SecureString ProxySecurePassword { get; set; }
        int QueryEndEventTimeout { get; set; }
        int DaxFormatterRequestTimeout { get; set; }
        DelimiterType DefaultSeparator { get; set; }
        DaxFormatStyle DefaultDaxFormatStyle { get; set; }
        bool TraceDirectQuery { get; set; }
        bool ShowPreReleaseNotifcations { get; set; }
        bool ShowTooltipBasicStats { get; set; }
        bool ShowTooltipSampleData { get; set; }
        bool CanPublishDaxFunctions { get; set; }
        bool ExcludeHeadersWhenCopyingResults { get; set; }

        bool ShowExportMetrics { get; set; }
    }
}
