using Caliburn.Micro;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Utils;
using System;
using System.Windows.Media;

namespace DaxStudio.UI.Model
{
    public abstract class ToolWindowBase:Screen , IToolWindow, IZoomable
    {
        public event EventHandler VisibilityChanged;
        public abstract string Title { get; }
        public abstract string DefaultDockingPane { get;  }
        public DisabledCommand DockAsDocumentCommand;
        public bool CanCloseWindow { get; set; }

        protected ToolWindowBase()
        {
            CanCloseWindow = false;
            CanHide = false;
            AutoHideMinHeight = 100;
            DockAsDocumentCommand = new DisabledCommand();
            NotifyOfPropertyChange(()=>DockAsDocumentCommand);
            ViewAttached += ToolWindowBase_ViewAttached;
            HideCommand = new DelegateCommand(HideToolWindow, CanHideToolWindow);
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

        public DelegateCommand HideCommand { get; set; }
        public bool CanHideToolWindow(object obj) { return CanHide; }
        public void HideToolWindow(object obj) { this.IsVisible = false; this.TryCloseAsync(); }
        public virtual bool CanHide { get;  }
        public int AutoHideMinHeight { get; set; }
        public new  bool IsActive { get; set; }
        private bool _isSelected;
        public bool IsSelected {
            get { return _isSelected; }
            set { _isSelected = value;
            NotifyOfPropertyChange(()=>IsSelected);}
        }

        public abstract string ContentId { get; }

        public void Activate()
        {
            //DockAsDocumentCommand.RaiseCanExecuteChanged();
            IsSelected = true;
        }

        #region IZoomable

        public event EventHandler OnScaleChanged;

        private double _scale = 1;
        public double Scale
        {
            get => _scale;
            set
            {
                _scale = value;
                NotifyOfPropertyChange();
                OnScaleChanged?.Invoke(this, null);
            }
        }
        #endregion
    }
}
