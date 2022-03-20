using Caliburn.Micro;
using DaxStudio.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.ViewModels
{
    public  class GettingStartedViewModel:Screen, IDisposable
    {
        public GettingStartedViewModel(IGlobalOptions options)
        {
            Options = options;
            
        }

        public void Close()
        {
            this.TryCloseAsync();
        }

        public void OpenQueryBuilder()
        {

        }

        private bool _showHelpWatermark = true;
        public bool ShowHelpWatermark
        {
            get => _showHelpWatermark && !NeverShowHelpWatermark;
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

        public IGlobalOptions Options { get; }

        public void Dispose()
        {
            // do nothing
        }
    }
}
