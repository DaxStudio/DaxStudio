using ADOTabular;
using DaxStudio.Interfaces;

namespace DaxStudio.UI.Interfaces
{
    public interface IDaxDocument
    {
        string Title { get; }
        IModelIntellisenseProvider Connection { get; }
        void OutputError(string message);
    }
}
