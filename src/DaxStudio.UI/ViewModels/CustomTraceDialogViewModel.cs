using Caliburn.Micro;
using DaxStudio.Common;
using DaxStudio.Common.Enums;
using DaxStudio.Interfaces;
using DaxStudio.UI.Enums;
using DaxStudio.UI.Model;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DaxStudio.UI.ViewModels
{
    public class CustomTraceDialogViewModel:Screen
    {

        public CustomTraceDialogViewModel(IGlobalOptions options)
        {
            Options = options;
            //TODO load trace templates
            TraceTemplates = new List<string>
            {
                "RefreshTrace"
            };
            SelectedTraceTemplateName = "RefreshTrace";
            LoadTemplates();

        }
        public async void Ok()
        {
            if (IsFileOutput && !IsValidFilePath(OutputFile, out var message))
            {
                // TODO - set invalid file path warning
                FileError = message;
                return;
            }

            Result = DialogResult.OK;
            await TryCloseAsync();
        }

        private bool IsValidFilePath(string filePath, out string message)
        {
            try
            {
                message = string.Empty;
                new FileInfo(filePath);
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }
        public void Cancel()
        {
            Result = DialogResult.Cancel;
        }

        public IGlobalOptions Options { get; }
        public List<string> TraceTemplates { get; set; }
        public string SelectedTraceTemplateName { get; set; }

        public CustomTraceTemplate SelectedTraceTemplate { get { return Templates[SelectedTraceTemplateName]; } }
        private CustomTraceOutput _selectedTraceOutput = CustomTraceOutput.File;
        public CustomTraceOutput SelectedTraceOutput { get => _selectedTraceOutput;
            set {
                _selectedTraceOutput = value;
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(IsFileOutput));
                if (!IsFileOutput) FileError = string.Empty;
            } 
        }
        public IEnumerable<CustomTraceOutput> TraceOutputs
        {
            get
            {
                var items = Enum.GetValues(typeof(CustomTraceOutput)).Cast<CustomTraceOutput>();
                return items;
            }
        }

        private string _outputFile = string.Empty;
        public string OutputFile
        {
            get => _outputFile;
            set
            {
                _outputFile = value;
                NotifyOfPropertyChange();
                FileError = string.Empty;
            }
        }

        private string _fileError = string.Empty;
        public string FileError { get => _fileError;
            set { _fileError = value;
                NotifyOfPropertyChange();    
            } 
        }
        public void Browse()
        {
            // show file save as dialog
            var dlg = new SaveFileDialog() {
                Filter = "json file|*.json"
            };
            var result = dlg.ShowDialog();
            if(result == true)
            {
                OutputFile= dlg.FileName;
                NotifyOfPropertyChange(nameof(OutputFile));
            }

        }

        public bool IsFileOutput => SelectedTraceOutput == CustomTraceOutput.File
                                || SelectedTraceOutput == CustomTraceOutput.FileAndGrid;
        public DialogResult Result { get; set; }

        public SortedList<string, CustomTraceTemplate> Templates { get; set; } = new SortedList<string, CustomTraceTemplate>();

        public void LoadTemplates()
        {
            var templatefolder = ApplicationPaths.CustomTraceTemplatePath;
            AddDefaultRefreshTemplate();
            var ser = JsonSerializer.Create();
            foreach (var file in Directory.GetFiles(templatefolder, "*.json"))
            {
                try
                {
                    using (var strmReader = new StreamReader(file))
                    using (var jsonReader = new JsonTextReader(strmReader)) 
                    {
                        var template = ser.Deserialize<CustomTraceTemplate>(jsonReader);
                        Templates.Add(template.Name, template);
                    }
                }
                catch
                {
                    // todo raise error event
                }
            }
            SelectedTraceTemplateName = Templates.Keys.FirstOrDefault();

            NotifyOfPropertyChange(nameof(Templates));
        }

        private void AddDefaultRefreshTemplate()
        {
            var template = new CustomTraceTemplate()
            { Name = "Refresh Trace",
              Events = { DaxStudioTraceEventClass.CommandBegin,
                DaxStudioTraceEventClass.CommandEnd,
                DaxStudioTraceEventClass.JobGraph,
                DaxStudioTraceEventClass.ProgressReportBegin,
                DaxStudioTraceEventClass.ProgressReportEnd,
                DaxStudioTraceEventClass.ProgressReportError,
                DaxStudioTraceEventClass.Error,
                }
            };
            Templates.Add(template.Name, template);
        }
    }
}
