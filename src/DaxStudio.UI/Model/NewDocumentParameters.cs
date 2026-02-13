using DaxStudio.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Model
{
    internal class NewDocumentParameters
    {
        public DocumentViewModel SourceDocument { get; set; }
        public bool CopyContent { get; set; }
        }
}
