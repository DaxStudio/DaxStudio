using ADOTabular;

namespace DaxStudio.Common.Interfaces
{
    public interface IFunctionProvider
    {
        ADOTabularFunctionGroupCollection FunctionGroups { get; }
    }
}
