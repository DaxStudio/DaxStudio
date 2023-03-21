using ADOTabular.Enums;
using ADOTabular.MetadataInfo;
using DaxStudio.Common.Enums;
using System.Collections.Generic;

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
        Dictionary<DaxStudioTraceEventClass,List<int>> SupportedTraceEventClasses { get; }
        AdomdType Type { get; }
        DaxColumnsRemap DaxColumnsRemapInfo { get; }
        void Ping();
        void PingTrace();
    }
}
