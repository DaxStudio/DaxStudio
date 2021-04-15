namespace DaxStudio.Common.Interfaces
{
    public interface ILinkedQueryResult
    {
        string DaxQuery { get; set; }
        string TargetSheet { get; set; }
        string ConnectionString { get; set; }
    }
}
