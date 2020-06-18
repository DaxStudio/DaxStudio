using ADOTabular;

namespace DaxStudio.UI.Interfaces
{
    public interface IDaxDocument
    {
        string Title { get; }
        ADOTabularConnection Connection { get; }
        void OutputError(string message);
    }
}
