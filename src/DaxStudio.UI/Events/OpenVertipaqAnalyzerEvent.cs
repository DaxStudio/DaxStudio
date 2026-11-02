namespace DaxStudio.UI.Events
{
    /// <summary>
    /// Event raised to open the VertiPaq Analyzer (Metrics) view.
    /// Used by the "--> SHOW METRICS" comment-script command; the handler reuses the existing
    /// <see cref="DaxStudio.UI.ViewModels.DocumentViewModel.ViewAnalysisDataAsync"/> logic.
    /// </summary>
    public class OpenVertipaqAnalyzerEvent
    {
    }
}
