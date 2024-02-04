using DaxStudio.Interfaces;
using DaxStudio.UI.Interfaces;
using Serilog;
using System.Diagnostics;

namespace DaxStudio.CommandLine
{
    internal class CmdLineDocument : IDocumentToExport, IDaxDocument
    {
        public CmdLineDocument(IConnectionManager connMgr, IMetadataPane metadataPane)
        {
            Connection = connMgr;
            QueryStopWatch = new Stopwatch();
            MetadataPane = metadataPane;
        }

        public IConnectionManager Connection { get; }

        public bool IsQueryRunning { get ; set ; }

        public Stopwatch QueryStopWatch { get; }

        public IMetadataPane MetadataPane { get; }

        public string Title => throw new System.NotImplementedException();

        public IDaxStudioHost Host => throw new System.NotImplementedException();

        public void OutputError(string message)
        {
            throw new System.NotImplementedException();
        }

        public void OutputMessage(string message)
        {
            Log.Information(message);   
        }

        public void OutputMessage(string message, double duration)
        {
            Log.Information($"{message} ({duration}ms");
        }

        public void OutputWarning(string message)
        {
            Log.Warning(message);
        }

        public void RefreshElapsedTime()
        {
            // do nothing
        }

        public void SetStatusBarMessage(string message)
        {
            // do nothing
        }
    }
}
