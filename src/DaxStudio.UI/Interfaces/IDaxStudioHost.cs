using System;

namespace DaxStudio.UI.Interfaces
{
    public interface IDaxStudioHost : IDisposable
    {
        IDaxStudioProxy Proxy { get; }
        bool IsExcel { get; }

        string CommandLineFileName { get; }
        int Port { get;  }
        bool DebugLogging { get; }
    }
}
