using ADOTabular;
using ADOTabular.Enums;
using ADOTabular.MetadataInfo;
using DaxStudio.Common.Enums;
using TOM = Microsoft.AnalysisServices;
using Adomd = Microsoft.AnalysisServices.AdomdClient;
using System.Collections.Generic;
using System.Data;

namespace DaxStudio.Interfaces
{
    public interface IConnectionManager : IModelIntellisenseProvider
    {
        string ApplicationName { get; }
        string ConnectionString { get; }
        string DatabaseName { get; }
        string FileName { get; }
        bool IsPowerPivot { get; }
        bool IsConnected { get; }
        string SessionId { get; }
        int SPID { get; }
        Dictionary<DaxStudioTraceEventClass, HashSet<TOM.TraceColumn>> SupportedTraceEventClasses { get; }
        AdomdType Type { get; }
        DaxColumnsRemap DaxColumnsRemapInfo { get; }
        void Ping();
        void PingTrace();
        IEnumerable<string> AllFunctions { get; }

        ADOTabularDatabase Database { get; }
        string SelectedModelName { get; }

        ADOTabular.AdomdClientWrappers.AdomdDataReader ExecuteReader(string query, List<Adomd.AdomdParameter> paramList);
        DataTable ExecuteDaxQueryDataTable(string query);
        ServerType ServerType { get; }
        Adomd.AccessToken AccessToken { get; }
    }
}
