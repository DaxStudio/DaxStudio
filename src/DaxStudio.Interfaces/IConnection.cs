namespace DaxStudio.Interfaces
{
    public interface IConnection
    {
        int SPID { get; }
        bool IsAdminConnection { get; }
        bool IsPowerPivot { get; }
        bool IsConnected { get;  }
        string DatabaseName { get; }
        string ServerName { get; }
    }
}
