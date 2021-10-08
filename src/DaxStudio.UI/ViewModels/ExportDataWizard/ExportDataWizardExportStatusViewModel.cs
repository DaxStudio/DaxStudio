using Caliburn.Micro;
using DaxStudio.UI.Enums;
using DaxStudio.UI.Events;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DaxStudio.UI.ViewModels
{

    public class ExportDataWizardExportStatusViewModel : ExportDataWizardBasePageViewModel
        , IHandle<ExportStatusUpdateEvent>
    {
        public ExportDataWizardExportStatusViewModel( ExportDataWizardViewModel wizard) : base(wizard) {
            // start running export
            Wizard.Export();
        }

        public IEnumerable<SelectedTable> Tables { get { return Wizard.Tables.Where(t => t.IsSelected); } }
        private SelectedTable _selectedTable;
        public SelectedTable SelectedTable {
            get { return _selectedTable; }
            set { _selectedTable = value; NotifyOfPropertyChange(() => SelectedTable); }
        }

        public void RequestCancel()
        {
            Wizard.CancelRequested = true;
            NotifyOfPropertyChange(() => CanRequestCancel);
            NotifyOfPropertyChange(() => CanCloseExport);
        }

        public bool Completed { get; private set; }

        public bool CanRequestCancel
        {
            get { return !Wizard.CancelRequested && !Completed; }
        }

        public bool CanCloseExport
        {
            get { return Completed 
                    || Tables.Where(t => t.Status == ExportStatus.Ready || t.Status == ExportStatus.Exporting).Count() == 0
                    || Wizard.CancelRequested;
            }
        }

        public async void CloseExport()
        {
            await Wizard.TryCloseAsync(true);
        }

        protected override async Task OnActivateAsync(CancellationToken cancellationToken)
        {
            await base.OnActivateAsync(cancellationToken);
            Wizard.EventAggregator.SubscribeOnPublishedThread(this);
        }
        
        protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
        {
            Wizard.EventAggregator.Unsubscribe(this);
            await base.OnDeactivateAsync(close, cancellationToken);

        }

        public Task HandleAsync(ExportStatusUpdateEvent message, CancellationToken cancellationToken)
        {
            SelectedTable = message.SelectedTable;
            Completed = message.Completed;
            if (message.Completed)
            {
                NotifyOfPropertyChange(() => CanCloseExport);
                NotifyOfPropertyChange(() => CanRequestCancel);
                NotifyOfPropertyChange(() => Completed);
            }
            return Task.CompletedTask;
        }
    }
}
