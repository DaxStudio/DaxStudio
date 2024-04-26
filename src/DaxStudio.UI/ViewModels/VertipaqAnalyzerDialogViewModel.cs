using Caliburn.Micro;
using Dax.Metadata;
using DaxStudio.Interfaces;
using DaxStudio.Interfaces.Enums;
using System.Windows.Forms;

namespace DaxStudio.UI.ViewModels
{
    public class VertipaqAnalyzerDialogViewModel:Caliburn.Micro.Screen, IVpaOptions
    {
        IVpaOptions _options;
        IEventAggregator _eventAggregator;
        public VertipaqAnalyzerDialogViewModel(IVpaOptions options, IEventAggregator eventAggregator)
        {
            _options = options;
            _eventAggregator = eventAggregator;
            // set defaults from options
            VpaxReadStatisticsFromData = _options.VpaxReadStatisticsFromData;
            VpaxReadStatisticsFromDirectQuery = _options.VpaxReadStatisticsFromDirectQuery;
            VpaxDirectLakeExtractionMode = _options.VpaxDirectLakeExtractionMode;
            VpaTableColumnDisplay = _options.VpaTableColumnDisplay;
            VpaxSampleReferentialIntegrityViolations = _options.VpaxSampleReferentialIntegrityViolations;
            VpaxDontShowOptionsDialog = _options.VpaxDontShowOptionsDialog;
        }

        private bool _vpaxReadStatisticsFromData;
        public bool VpaxReadStatisticsFromData {  get => _vpaxReadStatisticsFromData; 
            set { 
                _vpaxReadStatisticsFromData = value;
                NotifyOfPropertyChange();
            } 
        }
        public bool VpaxReadStatisticsFromDirectQuery { get; set; }

        public DirectLakeExtractionMode VpaxDirectLakeExtractionMode { get; set; }
        public int VpaxSampleReferentialIntegrityViolations { get;set; }
        public VpaTableColumnDisplay VpaTableColumnDisplay { get; set; }
        public DialogResult Result { get; private set; } = DialogResult.Cancel;
        public bool VpaxAdjustSegmentsMetrics { get; set; }
        public bool VpaxDontShowOptionsDialog { get; set; }

        public async void Ok()
        {
            // save the setting back to the global options
            _options.VpaxDontShowOptionsDialog = VpaxDontShowOptionsDialog;
            Result = DialogResult.OK;
            await TryCloseAsync();
        }
    }
}
