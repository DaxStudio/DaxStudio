
using ADOTabular.Enums;
using System.Collections.Generic;
using System.Data;
using ADOTabular.AdomdClientWrappers;

namespace ADOTabular.Interfaces
{
    public interface IADOTabularConnection
    {
        #region Properties
        IEnumerable<string> AllFunctions { get; }
        Dictionary<string, ADOTabularColumn> Columns { get; }
        ADOTabularDatabase Database { get; }
        ADOTabularDatabaseCollection Databases { get; }
        ADOTabularDynamicManagementViewCollection DynamicManagementViews { get; }
        bool IsPowerPivot { get; }
        ADOTabularKeywordCollection Keywords { get; }
        //IEnumerable<string> Keywords { get; }
        string ServerVersion { get; }
        string ServerName { get; }
        string ServerId { get; }
        bool ShowHiddenObjects { get; set; }
        int SPID { get; }
        bool IsTestingRls { get; }

        AdomdType Type { get; }
        IMetaDataVisitor Visitor { get; set; }
        bool IsAdminConnection { get; }
        #endregion

        #region Methods
        int ExecuteCommand(string command);
        DataTable ExecuteDaxQueryDataTable(string query);
        DataSet GetSchemaDataSet(string dataSet);
        DataSet GetSchemaDataSet(string dataSet, AdomdRestrictionCollection restrictions);
        DataSet GetSchemaDataSet(string dataSet, AdomdRestrictionCollection restrictions, bool throwOnErrors);

        AdomdDataReader ExecuteReader(string command, List<Microsoft.AnalysisServices.AdomdClient.AdomdParameter> paramList);
        #endregion
    }
}
