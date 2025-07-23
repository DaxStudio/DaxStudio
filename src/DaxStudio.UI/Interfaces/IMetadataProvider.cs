using ADOTabular;
using System.Collections.Generic;
using System.Threading.Tasks;
using DaxStudio.Interfaces;
using ADOTabular.Interfaces;
using System.Threading;

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

        Task UpdateColumnSampleDataAsync(ITreeviewColumn column, int sampleSize, CancellationToken cancellationToken);
        Task UpdateColumnBasicStatsAsync(ITreeviewColumn column, CancellationToken cancellationToken);
        bool IsPowerPivot { get; }
        bool IsPowerBIorSSDT { get; }
        bool IsConnected { get; }

        List<ADOTabularMeasure> GetAllMeasures(string filterTable = null);
        string DefineFilterDumpMeasureExpression(string tableName, bool allTables);
        string ExpandDependentMeasure(string measureName, bool ignoreNonUniqueMeasureNames);
        List<ADOTabularMeasure> FindDependentMeasures(string measureName);
        IEnumerable<IFilterableTreeViewItem> GetTreeViewTables(IMetadataPane metadataPaneViewModel, IGlobalOptions options);
        Task UpdateTableBasicStatsAsync(DaxStudio.UI.Model.TreeViewTable table);
        void CancelUpdatingTableBasicStats();

        List<string> GetRoles();
        void CancelUpdatingColumnSampleData();
        void CancelUpdatingColumnBasicStats();
    }
}
