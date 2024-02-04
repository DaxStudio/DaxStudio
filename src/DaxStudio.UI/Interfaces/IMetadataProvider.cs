using ADOTabular;
using System.Collections.Generic;
using System.Threading.Tasks;
using DaxStudio.Interfaces;
using ADOTabular.Interfaces;

namespace DaxStudio.UI.Interfaces
{
    public interface IMetadataProvider
    {
        void Refresh();

        void SetSelectedDatabase(IDatabaseReference database);
        Task SetSelectedModelAsync(ADOTabularModel model);

        string FileName { get; }
        string ShortFileName { get; }
        ADOTabularDatabaseCollection GetDatabases();
        ADOTabularModelCollection GetModels();
        ADOTabularTableCollection GetTables();
        string DatabaseName { get; }
        ADOTabularDatabase Database { get; }
        ADOTabularModel SelectedModel { get; }
        string SelectedModelName { get; }

        Task UpdateColumnSampleData(ITreeviewColumn column, int sampleSize);
        Task UpdateColumnBasicStats(ITreeviewColumn column);
        bool IsPowerPivot { get; }
        bool IsPowerBIorSSDT { get; }
        bool IsConnected { get; }

        List<ADOTabularMeasure> GetAllMeasures(string filterTable = null);
        string DefineFilterDumpMeasureExpression(string tableName, bool allTables);
        string ExpandDependentMeasure(string measureName, bool ignoreNonUniqueMeasureNames);
        List<ADOTabularMeasure> FindDependentMeasures(string measureName);
        IEnumerable<IFilterableTreeViewItem> GetTreeViewTables(IMetadataPane metadataPaneViewModel, IGlobalOptions options);
        void UpdateTableBasicStats(DaxStudio.UI.Model.TreeViewTable table);

        List<string> GetRoles();
    }
}
