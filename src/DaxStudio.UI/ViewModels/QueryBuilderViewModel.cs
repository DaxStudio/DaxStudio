using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.ViewModels
{
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export]
    public class QueryBuilderViewModel : ToolWindowBase
    {
        [ImportingConstructor]
        public QueryBuilderViewModel(IEventAggregator eventAggregator, DocumentViewModel document, IGlobalOptions globalOptions) : base()
        {
            EventAggregator = eventAggregator;
            Document = document;
            Options = globalOptions;
            Title = "Builder";
        }

        public List<TreeViewColumn> Columns { get; } = new List<TreeViewColumn>();
        public List<TreeViewColumnFilter> Filters { get; } = new List<TreeViewColumnFilter>();

        public IEventAggregator EventAggregator { get; }
        public DocumentViewModel Document { get; }
        public IGlobalOptions Options { get; }
    }

    public class TreeViewColumnFilter { 
        // TODO - implement TreeViewColumnFilter class
    }
}
