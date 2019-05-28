namespace DaxStudio.UI.Model
{
    public enum RunStyleIcons
    {
        RunOnly,
        ClearThenRun,
        RunFunction,
        RunScalar
    }
    public class RunStyle
    {
        public RunStyle(string name, RunStyleIcons icon, bool clearCache, bool injectEvaluate, bool injectRowFunction, string tooltip)
        {
            Name = name;
            Icon = icon;
            ClearCache = clearCache;
            Tooltip = tooltip;
            InjectEvaluate = injectEvaluate;
            InjectRowFunction = injectRowFunction;
        }
        public string Name { get; }
        public RunStyleIcons Icon { get;  }
        public string Tooltip { get;  }
        public bool ClearCache { get;  }
        public bool InjectEvaluate { get; }
        public bool InjectRowFunction { get; }
    }
}
