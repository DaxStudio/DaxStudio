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
        ClearThenRun,
        RunFunction
    }
    public class RunStyle
    {
        public RunStyle(string name, RunStyleIcons icon, bool clearCache, bool injectEvaluate, string tooltip)
        {
            Name = name;
            Icon = icon;
            ClearCache = clearCache;
            Tooltip = tooltip;
            InjectEvaluate = injectEvaluate;
        }
        public string Name { get; }
        public RunStyleIcons Icon { get;  }
        public string Tooltip { get;  }
        public bool ClearCache { get;  }
        public bool InjectEvaluate { get; }

    }
}
