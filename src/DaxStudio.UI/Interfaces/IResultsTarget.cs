
using DaxStudio.Interfaces;
using System;
using System.Threading.Tasks;

namespace DaxStudio.UI.Interfaces
{
    public interface IResultsTarget
    {
        string Name { get; }
        string Group { get; }
        //void OutputResults(IQueryRunner runner );
        Task OutputResultsAsync(IQueryRunner runner);
        bool IsDefault { get; }
        bool IsAvailable { get; }
        bool IsEnabled { get; }
        string DisabledReason { get; }
        int DisplayOrder { get; }

        string Message { get; }
        OutputTargets Icon { get; }
    }
}
