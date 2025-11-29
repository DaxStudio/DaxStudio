namespace DaxStudio.UI.Interfaces
{
    public interface IQueryPlanRow
    {
        int RowNumber { get; set; }
        int Level { get; set; }
        int NextSiblingRowNumber { get; set; }
    }
}
