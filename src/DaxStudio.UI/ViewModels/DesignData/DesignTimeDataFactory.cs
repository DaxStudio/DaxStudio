using ICSharpCode.AvalonEdit.Document;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.ViewModels.DesignData
{
    public static class DesignTimeDataFactory
    {
        public static DesignDocumentViewModel DocumentViewModel
        {
            get
            {

                //ServerTimingDetailsViewModel timingDetails = new ServerTimingDetailsViewModel();
                //return new DocumentViewModel(null,null,null, null, timingDetails,null);
                var doc = new TextDocument();
                doc.Text = "//Test Comment\n EVALUATE FILTER(table, table[Column] = \"Hello Designer\"";
                OutputPaneViewModel output = new OutputPaneViewModel(null);
                output.DisplayName = "Output";
                output.Messages.Add(new Events.OutputMessage(Events.MessageType.Information, "Hello Designer"));
                return new DesignDocumentViewModel() { Document = doc , OutputPane = output };
            }
        }



    }
}
