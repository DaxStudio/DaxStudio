using Dax.Metadata;
using DaxStudio.Interfaces.Enums;

namespace DaxStudio.Interfaces
{
    public interface IVpaOptions
    {
        DirectLakeExtractionMode VpaxDirectLakeExtractionMode { get; set; }
        bool VpaxReadStatisticsFromDirectQuery { get; set; }
        bool VpaxReadStatisticsFromData { get; set; }
        bool VpaxAdjustSegmentsMetrics { get; set; }
        bool VpaxDontShowOptionsDialog { get; set; }
        int VpaxSampleReferentialIntegrityViolations { get; set; }
        VpaTableColumnDisplay VpaTableColumnDisplay { get; set; }
    }
}
