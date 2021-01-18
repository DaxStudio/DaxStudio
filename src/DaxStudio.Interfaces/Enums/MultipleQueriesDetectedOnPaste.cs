
using System.ComponentModel;

namespace DaxStudio.Interfaces.Enums
{
    public enum MultipleQueriesDetectedOnPaste
    {
        [Description("Always keep only DAX")]
        AlwaysKeepOnlyDax,
        [Description("Always keep both DAX and DirectQuery")]
        AlwaysKeepBoth,
        [Description("Ask which queries to keep")]
        Prompt
    }
}
