using System;
using System.Collections.Generic;
using System.Linq;
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

    }
}
