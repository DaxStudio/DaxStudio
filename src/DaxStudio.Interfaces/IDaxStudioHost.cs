namespace DaxStudio.Interfaces
{
    public interface IDaxStudioHost
    {
        IDaxStudioProxy Proxy { get; }
        bool IsExcel { get; }
        ADOTabular.AdomdClientWrappers.AdomdType ConnectionType { get; }

        string CommandLineFileName { get; }
        int Port { get;  }
        bool DebugLogging { get; }
    }
}
