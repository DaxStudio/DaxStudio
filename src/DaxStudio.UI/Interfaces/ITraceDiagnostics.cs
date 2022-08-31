using System;

namespace DaxStudio.UI.Interfaces
{
    public interface ITraceDiagnostics
    {
        string ActivityID { get; set; }
        DateTime StartDatetime { get; }
        string CommandText { get; set; }
        string Parameters { get;set; }
    }
}
