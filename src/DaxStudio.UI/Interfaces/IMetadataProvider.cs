using ADOTabular;
using System.Collections.Generic;
using System.Threading.Tasks;
using DaxStudio.Interfaces;

namespace DaxStudio.UI.Interfaces
{
    public interface IMetadataProvider
    {
        void Refresh();

        void SetSelectedDatabase(IDatabaseReference database);
        void SetSelectedModel(ADOTabularModel model);

        string FileName { get; }
        string ShortFileName { get; }
        ADOTabularDatabaseCollection GetDatabases();
        ADOTabularModelCollection GetModels();
        ADOTabularTableCollection GetTables();
        string SelectedDatabaseName { get; }
        ADOTabularDatabase SelectedDatabase { get; }
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
        IEnumerable<IFilterableTreeViewItem> GetTreeViewTables(IMetadataPane metadataPaneViewModel, IGlobalOptions options);
        void UpdateTableBasicStats(DaxStudio.UI.Model.TreeViewTable table);

        List<string> GetRoles();
    }
}
