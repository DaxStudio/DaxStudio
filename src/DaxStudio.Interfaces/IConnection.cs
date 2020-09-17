namespace DaxStudio.Interfaces
{
    public interface IConnection
    {
        int Spid { get; }
        bool IsAdminConnection { get; }
        bool IsPowerPivot { get; }
        bool IsConnected { get;  }
        string SelectedDatabase { get; }
        string ServerName { get; }
    }
}
