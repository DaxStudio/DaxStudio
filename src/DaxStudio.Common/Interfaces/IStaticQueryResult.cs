using System.Data;

namespace DaxStudio.Common.Interfaces
{
    public interface IStaticQueryResult
    {
        DataTable QueryResults { get; set; }
        string TargetSheet { get; set; }
        
    }
}
