using ADOTabular.Enums;

namespace DaxStudio.Interfaces
{
    public interface IConnectionManager
    {
        string ApplicationName { get; }
        string ConnectionString { get; }
        string DatabaseName { get; }
        string FileName { get; }
        string SessionId { get; }
        AdomdType Type { get; }

        void Ping();
        void PingTrace();
    }
}
