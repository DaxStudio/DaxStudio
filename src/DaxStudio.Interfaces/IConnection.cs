namespace DaxStudio.Interfaces
{
    public interface IConnection
    {
        int SPID { get; }
        bool IsAdminConnection { get; }
        bool IsPowerPivot { get; }
        bool IsConnected { get;  }
        string SelectedDatabaseName { get; }
        string ServerName { get; }
    }
}
