using DaxStudio.UI.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Interfaces
{
    public interface IQueryTextProvider
    {
        string EditorText { get; }
        string QueryText {get; }
        List<Microsoft.AnalysisServices.AdomdClient.AdomdParameter> ParameterCollection { get; }
        QueryInfo QueryInfo { get; set; }
    }
}
