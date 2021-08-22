using System.Collections.Generic;
using ADOTabular.Enums;
using ADOTabular.MetadataInfo;
using DaxStudio.Common.Enums;

namespace DaxStudio.Common.Interfaces
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
        HashSet<DaxStudioTraceEventClass> SupportedTraceEventClasses { get; }
        AdomdType Type { get; }
        DaxColumnsRemap DaxColumnsRemapInfo { get; }

        void Ping();
        void PingTrace();
    }
}
