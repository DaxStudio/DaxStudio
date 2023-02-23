using DaxStudio.Common.Enums;
using System.Collections.Generic;

namespace DaxStudio.UI.Model
{
    public class CustomTraceTemplate
    { 
        public string Name { get; set; }
        public bool FilterForCurrentSession { get; set; }
        public List<DaxStudioTraceEventClass> Events { get; set; } = new List<DaxStudioTraceEventClass>();
    }
}
