using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Converters;
using DaxStudio.UI.Events;
using System.Windows;

namespace DaxStudio.UI.ViewModels
{
    public class HelpWatermarkViewModel:Screen
    ,IHandle<UpdateGlobalOptions>
    ,IHandle<EditorResizeEvent>
    {
        // if the editor get smaller than the values below we
        // need to hide the help text as it will be too big
        private const int MinWidth = 515;
        private const int MinHeight = 290;
        private const double MaxScale = 1.2;
        private Size EditorSize;

        public HelpWatermarkViewModel(IGlobalOptions options)
        {
            Options = options;
        }

        public IGlobalOptions Options { get; set; }

        private bool _showHelpWatermark = true;
        public bool ShowHelpWatermark
        {
            get => _showHelpWatermark && !NeverShowHelpWatermark && !EditorTooSmall;
            set
            {
                if (value == _showHelpWatermark) return;
                _showHelpWatermark = value;
                NotifyOfPropertyChange(nameof(ShowHelpWatermark));
                
            }
        }

        public bool NeverShowHelpWatermark
        {
            get => !Options.ShowHelpWatermark;
            set
            {
                Options.ShowHelpWatermark = !value;
                NotifyOfPropertyChange(nameof(ShowHelpWatermark));
            }
        }

        public void Handle(UpdateGlobalOptions message)
        {
            NotifyOfPropertyChange(nameof(NeverShowHelpWatermark));
            NotifyOfPropertyChange(nameof(ShowHelpWatermark));
        }

        public void Handle(EditorResizeEvent message)
        {
            EditorSize = message.NewSize;
            //if (message.NewSize.Height < MinHeight || message.NewSize.Width < MinWidth) EditorTooSmall = true;
            //else EditorTooSmall = false;
            NotifyOfPropertyChange(nameof(ShowHelpWatermark));
            NotifyOfPropertyChange(nameof(Scale));
        }

        public bool EditorTooSmall { get; set; }

        public double Scale
        {
            get
            {
                //if (EditorSize.Height >= MinHeight && EditorSize.Width >= MinWidth) return 1.0;
                var heightScale = EditorSize.Height / MinHeight;
                var widthScale = EditorSize.Width / MinWidth;
                if (heightScale > widthScale) return widthScale > MaxScale ? MaxScale : widthScale;
                return heightScale > MaxScale ? MaxScale : heightScale;
            }
        }
    }
}
