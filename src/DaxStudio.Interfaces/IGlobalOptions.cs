using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Interfaces
{
    public interface IGlobalOptions
    {
        bool EditorShowLineNumbers { get; set; }
        double EditorFontSize { get; set; }
        string EditorFontFamily { get; set; }
        bool EditorEnableIntellisense { get; set; }

        int QueryHistoryMaxItems { get; set; }
        bool QueryHistoryShowTraceColumns { get; set; }
        bool ProxyUseSystem { get; set; }
        string ProxyAddress { get; set; }
        string ProxyUser { get; set; }
        SecureString ProxySecurePassword { get; set; }
    }
}
