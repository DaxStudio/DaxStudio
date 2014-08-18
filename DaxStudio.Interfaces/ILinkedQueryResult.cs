using System.Data;

namespace DaxStudio.Interfaces
{
    public interface ILinkedQueryResult
    {
        string DaxQuery { get; set; }
        string TargetSheet { get; set; }
        
    }
}
