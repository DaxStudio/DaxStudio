using ADOTabular;

namespace DaxStudio.Interfaces
{
    public interface IDaxDocument
    {
        ADOTabularConnection Connection { get; }
        void OutputError(string message);
    }
}
