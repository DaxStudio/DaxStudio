using ICSharpCode.AvalonEdit.Document;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.ViewModels.DesignData
{
    public class DesignDocumentViewModel
    {
        public TextDocument Document { get; set; }
        public OutputPaneViewModel  OutputPane { get; set; }
    }
}
