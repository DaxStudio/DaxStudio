using Caliburn.Micro;
using DaxStudio.UI.Enums;
using DaxStudio.UI.Events;
using System.Collections.Generic;
using System.Linq;

namespace DaxStudio.UI.ViewModels
{

    public class ExportDataWizardExportStatusViewModel : ExportDataWizardBasePageViewModel, IHandle<ExportStatusUpdateEvent>
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

        public void CloseExport()
        {
            Wizard.TryClose(true);
        }

        protected override void OnActivate()
        {
            base.OnActivate();
            Wizard.EventAggregator.Subscribe(this);
        }

        protected override void OnDeactivate(bool close)
        {
            Wizard.EventAggregator.Unsubscribe(this);
            base.OnDeactivate(close);

        }

        public void Handle(ExportStatusUpdateEvent message)
        {
            SelectedTable = message.SelectedTable;
            Completed = message.Completed;
            if (message.Completed)
            {
                NotifyOfPropertyChange(() => CanCloseExport);
                NotifyOfPropertyChange(() => CanRequestCancel);
                NotifyOfPropertyChange(() => Completed);
            }
        }
    }
}
