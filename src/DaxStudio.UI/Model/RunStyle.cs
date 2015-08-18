using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Model
{
    public enum RunStyleIcons
    {
        RunOnly,
        ClearThenRun
    }
    public class RunStyle
    {
        public RunStyle(string name, RunStyleIcons icon, bool clearCache, string tooltip)
        {
            Name = name;
            Icon = icon;
            ClearCache = clearCache;
            Tooltip = tooltip;
        }
        public string Name { get; private set; }
        public RunStyleIcons Icon { get; private set; }
        public string Tooltip { get; private set; }
        public bool ClearCache { get; private set; }

    }
}
