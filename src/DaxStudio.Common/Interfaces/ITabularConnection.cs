namespace DaxStudio.Common.Interfaces
{
    public interface ITabularConnection
    {
        void Open();
        void Ping();
        void Close();

    }
}
