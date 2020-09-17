using ADOTabular;

namespace DaxStudio.Interfaces
{
    public interface IFunctionProvider
    {
        ADOTabularFunctionGroupCollection FunctionGroups { get; }
    }
}
