
using DaxStudio.Interfaces;
using System.Threading.Tasks;

namespace DaxStudio.UI.Model
{
    public interface IResultsTarget
    {
        string Name { get; }
        string Group { get; }
        void OutputResults(IQueryRunner runner );
        Task OutputResultsAsync(IQueryRunner runner);
    }
}
