namespace DaxStudio.UI.Model
{
    public enum RunStyleIcons
    {
        RunOnly,
        RunBuilder
    }
    public class RunStyle : Caliburn.Micro.PropertyChangedBase
    {
        public RunStyle(string name, RunStyleIcons icon, string tooltip)
        {
            Name = name;
            Icon = icon;
            Tooltip = tooltip;

            if (icon == RunStyleIcons.RunBuilder) ImageResource = "run_query_builderDrawingImage";
            else ImageResource = "runDrawingImage";

        }
        public string Name { get; }
        public RunStyleIcons Icon { get;  }
        public string ImageResource { get; }
        public string Tooltip { get;  }
        public bool ClearCache { get; set; }
        private bool _isEnabled = false;
        public bool IsEnabled { get => _isEnabled; 
            set { 
                _isEnabled = value;
                NotifyOfPropertyChange();    
            } 
        } 
    }
}
