using DaxStudio.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security;
using DaxStudio.Interfaces.Enums;

namespace DaxStudio.Tests.Mocks
{
    class MockGlobalOptions : IGlobalOptions
    {
        public int DaxFormatterRequestTimeout  { get; set; }
        public DelimiterType DefaultSeparator { get; set; }
        public DaxFormatStyle DefaultDaxFormatStyle { get; set; }
        public bool EditorEnableIntellisense { get; set; }
        public string EditorFontFamily { get; set; }
        public double EditorFontSizePx { get; set; }
        public string ResultFontFamily { get; set; }
        public double ResultFontSizePx { get; set; }
        public bool EditorShowLineNumbers { get; set; }
        public string ProxyAddress {get; set;}
        public SecureString ProxySecurePassword { get; set; }
        public string ProxyUser { get; set; }
        public bool ProxyUseSystem { get; set; }
        public int QueryEndEventTimeout { get; set; }
        public int QueryHistoryMaxItems { get; set; }
        public bool QueryHistoryShowTraceColumns { get; set; }
        public bool ShowPreReleaseNotifcations { get;set; }
        public bool TraceDirectQuery { get; set; }
        public bool ShowTooltipSampleData { get; set; }
        public bool ShowTooltipBasicStats { get; set; }
        public bool CanPublishDaxFunctions { get; set; }

        public bool ExcludeHeadersWhenCopyingResults { get; set; }

        public bool ShowExportMetrics { get;set; }
        public int TraceStartupTimeout { get; set; }
        public bool ShowExternalTools { get; set; }
        public bool ShowExportAllData { get; set; }
        public bool ResultAutoFormat { get; set; }
        public bool ShowAggregationRewritesInAllQueries { get; set; }
        public bool ScaleResultsFontWithEditor { get;set; }
        public bool AutoRefreshMetadataLocalMachine { get; set; }
        public bool AutoRefreshMetadataLocalNetwork { get; set; }
        public bool AutoRefreshMetadataCloud { get; set; }
    }
}
