using Caliburn.Micro;
using DaxStudio.Common;
using DaxStudio.UI.Enums;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Model;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Screen = Caliburn.Micro.Screen;

namespace DaxStudio.UI.ViewModels
{
    public class RequestInformationViewModel : BaseDialogViewModel, ITraceDiagnostics
    {
        private DialogResult _dialogResult;

        public RequestInformationViewModel(ITraceDiagnostics diagnosticsProvider)
        {
            ActivityID = diagnosticsProvider.ActivityID;
            StartDatetime = diagnosticsProvider.StartDatetime;
            CommandText = diagnosticsProvider.CommandText;
            Parameters = diagnosticsProvider.Parameters;
        }

        public string ActivityID { get ; set ; }
        public DateTime StartDatetime { get;set; }

        public string StartDatetimeFormatted => StartDatetime.ToString("O");
        public string CommandText { get; set; }
        public string Parameters { get; set; }

        public bool ShowParameters => !string.IsNullOrEmpty(Parameters);

        public async void Ok()
        {
            _dialogResult = DialogResult.OK;
            await TryCloseAsync(true);
        }
        public void Copy(object context)
        {

            ClipboardManager.SetText(context.ToString());
        }

        public DialogResult Result => _dialogResult;

        public override void Close()
        {
            _dialogResult = DialogResult.Cancel;
            TryCloseAsync();
        }
    }
}
