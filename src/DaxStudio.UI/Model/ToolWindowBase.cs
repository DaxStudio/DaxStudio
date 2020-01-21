using Caliburn.Micro;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Utils;
using System.Windows.Input;
using System;

namespace DaxStudio.UI.Model
{
    public class ToolWindowBase:Screen , IToolWindow
    {
        public event EventHandler VisibilityChanged;
        public virtual string Title { get; set; }
        public virtual string DefaultDockingPane { get; set; }
        public DisabledCommand DockAsDocumentCommand;
        public bool CanCloseWindow { get; set; }
        public ToolWindowBase()
        {
            CanCloseWindow = true;
            CanHide = false;
            AutoHideMinHeight = 100;
            DefaultDockingPane = "DockBottom";
            DockAsDocumentCommand = new DisabledCommand();
            NotifyOfPropertyChange(()=>DockAsDocumentCommand);
            ViewAttached += ToolWindowBase_ViewAttached;
        }

        private void ToolWindowBase_ViewAttached(object sender, ViewAttachedEventArgs e)
        {
            NotifyOfPropertyChange(() => CanCloseWindow);
            DockAsDocumentCommand.RaiseCanExecuteChanged();
        }

        private bool _isVisible = true;
        public bool IsVisible { get => _isVisible;
            set
            {
                _isVisible = value;
                OnVisibilityChanged(EventArgs.Empty);
                NotifyOfPropertyChange(nameof(IsVisible));
            }
        }

        protected virtual void OnVisibilityChanged(EventArgs e)
        {
            EventHandler handler = VisibilityChanged;
            handler?.Invoke(this, e);
        }
        //private bool _canClose;
        //public new bool CanClose { get { return _canClose; } set { if (value != _canClose) { _canClose = value;  NotifyOfPropertyChange(() => CanClose); } } }
        //public bool CanClose { get; set; }
        public bool IsEnabled { get; set; }
        public bool CanHide { get; set; }
        public int AutoHideMinHeight { get; set; }
        public new  bool IsActive { get; set; }
        private bool _isSelected;
        public bool IsSelected {
            get { return _isSelected; }
            set { _isSelected = value;
            NotifyOfPropertyChange(()=>IsSelected);}
        }
        public void Activate()
        {
            //DockAsDocumentCommand.RaiseCanExecuteChanged();
            IsSelected = true;
        }
    }
}
