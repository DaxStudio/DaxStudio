using System.Data;
using DaxStudio.UI.ViewModels;

namespace DaxStudio.UI.Model
{
    public interface IResultsTarget
    {
        string Name { get; }
        string Group { get; }
        void OutputResults(IQueryRunner runner );
    }
}
