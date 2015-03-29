using Caliburn.Micro;

namespace DaxStudio.UI.Model
{
    public class ToolWindowBase:Screen , IToolWindow
    {
        public virtual string Title { get; set; }
        public virtual string DefaultDockingPane { get; set; }

        public ToolWindowBase()
        {
            CanClose = false;
            CanHide = false;
            AutoHideMinHeight = 100;
            DefaultDockingPane = "DockBottom";
        }

        public  bool CanClose { get; set; }
        public  bool CanHide { get; set; }
        public virtual int AutoHideMinHeight { get; set; }
        public bool IsActive { get; set; }
        private bool _isSelected;
        public bool IsSelected {
            get { return _isSelected; }
            set { _isSelected = value;
            NotifyOfPropertyChange(()=>IsSelected);}
        }
        public void Activate()
        {
            IsSelected = true;
        }
    }
}
