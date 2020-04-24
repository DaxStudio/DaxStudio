using System.ComponentModel;

namespace ADOTabular.Enums
{
    // Note the description values are passed to DaxFormatter.com
    public enum ServerType
    {
        [Description("SSAS")]
        AnalysisServices,
        [Description("PBI Desktop")]
        PowerBIDesktop,
        [Description("PBI Report Server")]
        PowerBIReportServer,
        [Description("PowerPivot")]
        PowerPivot,
        [Description("SSDT")]
        SSDT
    }
    
}
