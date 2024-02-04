using Caliburn.Micro;
using DaxStudio.UI.Interfaces;
using Microsoft.AnalysisServices.AdomdClient;
using System;
using System.Collections.Generic;
using System.Windows;

namespace DaxStudio.UI.Model
{
    internal class ClipboardTextProvider : IQueryTextProvider
    {
        public string EditorText { 
            get {
                string text = string.Empty;
                bool containsEval = false;
                Execute.OnUIThread(() =>
                {
                    text = Clipboard.GetText();
                    containsEval = text.IndexOf("EVALUATE", StringComparison.OrdinalIgnoreCase) >= 0;
                    
                });
                return containsEval ? text : string.Empty;
            } 
        }

        public string QueryText => EditorText;

        public List<AdomdParameter> ParameterCollection => new List<AdomdParameter>();

        public QueryInfo QueryInfo { get ; set ; }
    }
}
