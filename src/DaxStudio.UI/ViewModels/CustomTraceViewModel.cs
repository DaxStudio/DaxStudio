using System;
using System.ComponentModel.Composition;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Text;
using System.Windows.Data;
using Caliburn.Micro;
using DaxStudio.Controls.DataGridFilter;
using DaxStudio.Core;
using DaxStudio.Core.Enums;
using DaxStudio.Core.Model;
using DaxStudio.Core.Trace;
using DaxStudio.Interfaces;
using DaxStudio.UI.Enums;
using DaxStudio.UI.Events;
using DaxStudio.UI.Extensions;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Model;
using System.ComponentModel;
using DataGridExtensions = DaxStudio.Controls.DataGridFilter.DataGridExtensions;

namespace DaxStudio.UI.ViewModels
{
    public class CustomTraceViewModel
        : CustomTraceModel,
        ISaveState,
        IViewAware
    {
        [ImportingConstructor]
        public CustomTraceViewModel(IEventAggregator eventAggregator, IGlobalOptions globalOptions, IWindowManager windowManager)
            : base(eventAggregator, globalOptions, windowManager)
        {
        }

        public override bool ShouldStartTrace()
        {
            var dialog = new CustomTraceDialogViewModel(_globalOptions);
            WindowManager.ShowDialogBoxAsync(dialog).Wait();

            // if the dialog result is not OK then exit here
            if (dialog.Result != DialogResult.OK) return false;

            // set the template
            Template = dialog.SelectedTraceTemplate;

            // set the trace output
            SetTraceOutput(dialog.SelectedTraceOutput);

            OutputFile = dialog.OutputFile;
            EventCount = 0;
            return true;
        }

        public TraceEvent SelectedQuery { get; set; }

        public override void CopyAll()
        {
            // We need to get the default view as that is where any filtering is done
            ICollectionView view = CollectionViewSource.GetDefaultView(TraceEvents);

            var sb = new StringBuilder();
            foreach (var itm in view)
            {
                if (itm is QueryEvent q)
                {
                    sb.AppendLine();
                    sb.AppendLine($"// {q.QueryType} query against Database: {q.DatabaseName} ");
                    sb.AppendLine($"{q.Query}");
                }
            }
            sb.AppendLine();
            _eventAggregator.PublishAsync(new SendTextToEditor(sb.ToString()));
        }

        public override void ClearFilters()
        {
            var vw = GetView() as Views.CustomTraceView;
            if (vw == null) return;
            var controller = DataGridExtensions.GetDataGridFilterQueryController(vw.TraceEvents);
            controller.ClearFilter();
        }

        public void SetDefaultFilter(string column, string value)
        {
            var vw = this.GetView() as Views.RefreshTraceView;
            if (vw == null) return;
            var controller = DataGridExtensions.GetDataGridFilterQueryController(vw.RefreshEvents);
            var filters = controller.GetFiltersForColumns();

            var columnFilter = filters.FirstOrDefault(w => w.Key == column);
            if (columnFilter.Key != null)
            {
                columnFilter.Value.QueryString = value;
                controller.SetFiltersForColumns(filters);
            }
        }

        public void TextDoubleClick()
        {
            TextDoubleClick(SelectedQuery);
        }

        public void TextDoubleClick(TraceEvent refreshEvent)
        {
            if (refreshEvent == null) return; // if the user clicked on an empty query exit here
            _eventAggregator.PublishAsync(new SendTextToEditor($"// {refreshEvent.EventClass} - {refreshEvent.EventSubClass}\n{refreshEvent.Text}"));
        }

        #region ISaveState methods
        void ISaveState.Save(string filename)
        {
            string json = GetJson();
            File.WriteAllText(filename + ".customTrace", json);
        }

        void ISaveState.Load(string filename)
        {
            filename = filename + ".customTrace";
            if (!File.Exists(filename)) return;

            _eventAggregator.PublishAsync(new ShowTraceWindowEvent(this));
            string data = File.ReadAllText(filename);
            LoadJson(data);
        }

        public void LoadPackage(Package package)
        {
            var uri = PackUriHelper.CreatePartUri(new Uri(DaxxFormat.CustomTrace, UriKind.Relative));
            if (!package.PartExists(uri)) return;

            _eventAggregator.PublishAsync(new ShowTraceWindowEvent(this));
            var part = package.GetPart(uri);
            using (TextReader tr = new StreamReader(part.GetStream()))
            {
                string data = tr.ReadToEnd();
                LoadJson(data);
            }
        }
        #endregion
    }
}
