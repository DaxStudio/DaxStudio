using DaxStudio.Common;
using DaxStudio.Common.Enums;
using DaxStudio.Core.Model;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DaxStudio.Core.Trace
{
    public static class CustomTraceTemplateLoader
    {
        public static SortedList<string, CustomTraceTemplate> LoadTemplates()
        {
            var templates = new SortedList<string, CustomTraceTemplate>();
            AddDefaultRefreshTemplate(templates);

            var templatefolder = ApplicationPaths.CustomTraceTemplatePath;
            if (!Directory.Exists(templatefolder)) return templates;

            var ser = JsonSerializer.Create();
            foreach (var file in Directory.GetFiles(templatefolder, "*.json"))
            {
                try
                {
                    using (var strmReader = new StreamReader(file))
                    using (var jsonReader = new JsonTextReader(strmReader))
                    {
                        var template = ser.Deserialize<CustomTraceTemplate>(jsonReader);
                        templates.Add(template.Name, template);
                    }
                }
                catch
                {
                    // ignore malformed template files
                }
            }
            return templates;
        }

        private static void AddDefaultRefreshTemplate(SortedList<string, CustomTraceTemplate> templates)
        {
            var template = new CustomTraceTemplate()
            {
                Name = "Refresh Trace",
                Events = { DaxStudioTraceEventClass.CommandBegin,
                    DaxStudioTraceEventClass.CommandEnd,
                    DaxStudioTraceEventClass.JobGraph,
                    DaxStudioTraceEventClass.ProgressReportBegin,
                    DaxStudioTraceEventClass.ProgressReportEnd,
                    DaxStudioTraceEventClass.ProgressReportError,
                    DaxStudioTraceEventClass.Error,
                }
            };
            templates.Add(template.Name, template);
        }
    }
}
