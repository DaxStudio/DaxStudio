using Caliburn.Micro;

namespace DaxStudio.UI.ViewModels
{
    public class HelpWatermarkViewModel:Screen
    {

        public HelpWatermarkViewModel()
        {
        }

        private bool _showHelpWatermark = true;
        public bool ShowHelpWatermark
        {
            get => _showHelpWatermark; 
            set
            {
                if (value == _showHelpWatermark) return;
                _showHelpWatermark = value;
                NotifyOfPropertyChange(nameof(ShowHelpWatermark));
                
            }
        }

    }
}
