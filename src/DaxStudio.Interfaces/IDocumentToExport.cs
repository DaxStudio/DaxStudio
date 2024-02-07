using System.Diagnostics;

namespace DaxStudio.Interfaces
{
    public interface IDocumentToExport : IHaveStatusBar
    {
        IConnectionManager Connection { get; }
        bool IsQueryRunning { get; set; }
        Stopwatch QueryStopWatch { get; }
        IMetadataPane MetadataPane { get; }

        void OutputMessage(string message);
        void OutputMessage(string message, double duration);
        void RefreshElapsedTime();

    }
}
