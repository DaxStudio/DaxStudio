using ADOTabular.AdomdClientWrappers;
using System;
using System.Collections.Generic;
using System.Data;

namespace ADOTabular
{
    public interface IADOTabularConnection
    {
        #region Properties
        IEnumerable<string> AllFunctions { get; }
        Dictionary<string, ADOTabularColumn> Columns { get; }
        ADOTabularDatabase Database { get; }
        ADOTabularDatabaseCollection Databases { get; }
        bool IsPowerPivot { get; }
        ADOTabularKeywordCollection Keywords { get; }
        //IEnumerable<string> Keywords { get; }
        string ServerVersion { get; }
        string ServerName { get; }
        string ServerId { get; }
        bool ShowHiddenObjects { get; set; }
        int SPID { get; }

        AdomdType Type { get; }
        IMetaDataVisitor Visitor { get; set; }
        #endregion

        #region Methods
        int ExecuteCommand(string command);
        DataTable ExecuteDaxQueryDataTable(string query);
        DataSet GetSchemaDataSet(string dataset, AdomdRestrictionCollection restrictions);
        DataSet GetSchemaDataSet(string dataset, AdomdRestrictionCollection restrictions, bool throwOnErrors);

        AdomdDataReader ExecuteReader(string command);
        #endregion
    }
}
