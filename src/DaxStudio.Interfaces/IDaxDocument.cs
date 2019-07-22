using ADOTabular;

namespace DaxStudio.Interfaces
{
    public interface IDaxDocument
    {
        string Title { get; }
        ADOTabularConnection Connection { get; }
        void OutputError(string message);
    }
}
