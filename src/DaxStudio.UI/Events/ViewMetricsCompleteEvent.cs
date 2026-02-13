using Dax.ViewModel;

namespace DaxStudio.UI.Events
{
    /// <summary>
    /// Event published when Vertipaq Analyzer metrics have been loaded.
    /// Carries the VpaModel so subscribers can enrich their views with the stats.
    /// </summary>
    public class ViewMetricsCompleteEvent
    {
        /// <summary>
        /// The Vertipaq Analyzer model with table/column statistics.
        /// May be null if only signaling completion without data.
        /// </summary>
        public VpaModel VpaModel { get; }

        /// <summary>
        /// Creates a ViewMetricsCompleteEvent without VPA data (backward compatible).
        /// </summary>
        public ViewMetricsCompleteEvent()
        {
        }

        /// <summary>
        /// Creates a ViewMetricsCompleteEvent with VPA data for enrichment.
        /// </summary>
        /// <param name="vpaModel">The Vertipaq Analyzer model with statistics.</param>
        public ViewMetricsCompleteEvent(VpaModel vpaModel)
        {
            VpaModel = vpaModel;
        }
    }
}
