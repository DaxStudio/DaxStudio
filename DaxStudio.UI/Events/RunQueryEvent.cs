using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DaxStudio.UI.Model;

namespace DaxStudio.UI.Events
{
    public class RunQueryEvent
    {
        public RunQueryEvent(IResultsTarget target, string selectedWorksheet)
        {
            ResultsTarget = target;
            SelectedWorksheet = selectedWorksheet;
        }
        public IResultsTarget ResultsTarget { get; set; }
        public string SelectedWorksheet { get; set; }
    }
}
