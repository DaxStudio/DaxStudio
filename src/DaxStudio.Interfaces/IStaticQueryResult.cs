using System.Data;

namespace DaxStudio.Interfaces
{
    public interface IStaticQueryResult
    {
        DataTable QueryResults { get; set; }
        string TargetSheet { get; set; }
        
    }
}
