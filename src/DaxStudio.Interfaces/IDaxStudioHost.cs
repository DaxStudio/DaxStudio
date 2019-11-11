using System;

namespace DaxStudio.Interfaces
{
    public interface IDaxStudioHost : IDisposable
    {
        IDaxStudioProxy Proxy { get; }
        bool IsExcel { get; }
        ADOTabular.AdomdClientWrappers.AdomdType ConnectionType { get; }

        string CommandLineFileName { get; }
        int Port { get;  }
        bool DebugLogging { get; }
    }
}
