using System;
using System.ComponentModel.Composition;
using Caliburn.Micro;
using DaxStudio.Core.Extensions;
using DaxStudio.Core.Interfaces;
using DaxStudio.Core.Trace;
using DaxStudio.Interfaces;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Utils;
using Fluent;

namespace DaxStudio.UI.ViewModels
{
    public abstract class TraceWatcherBaseViewModel
        : TraceWatcherBaseModel
        , IZoomable
    {
        [ImportingConstructor]
        protected TraceWatcherBaseViewModel(IEventAggregator eventAggregator, IGlobalOptions globalOptions, IWindowManager windowManager)
            : base(eventAggregator, globalOptions, windowManager)
        {
            HideCommand = new DelegateCommand(HideTrace, CanHideTrace);
        }

        public DelegateCommand HideCommand { get; set; }

        public virtual RibbonControlSizeDefinition SizeDefinition { get; } =
            new RibbonControlSizeDefinition() { Large = RibbonControlSize.Large, Middle = RibbonControlSize.Large, Small = RibbonControlSize.Middle };

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

        // Override the Core hook to show a Windows SaveFileDialog
        protected override string PromptExportFilePath()
        {
            var dialog = new System.Windows.Forms.SaveFileDialog
            {
                Filter = "JSON file (*.json)|*.json",
                Title = "Export Trace Details"
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                return dialog.FileName;
            }
            return null;
        }
    }
}
