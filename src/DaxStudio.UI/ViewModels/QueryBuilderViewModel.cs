using ADOTabular;
using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Events;
using DaxStudio.UI.Model;
using GongSolutions.Wpf.DragDrop;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.ViewModels
{
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export]
    public class QueryBuilderViewModel : ToolWindowBase
        ,IQueryTextProvider
    { 
        [ImportingConstructor]
        public QueryBuilderViewModel(IEventAggregator eventAggregator, DocumentViewModel document, IGlobalOptions globalOptions) : base()
        {
            EventAggregator = eventAggregator;
            Document = document;
            Options = globalOptions;
            Title = "Builder";

        }

        public QueryBuilderFieldList Columns { get; } = new QueryBuilderFieldList();
        public QueryBuilderFilterList Filters { get; } = new QueryBuilderFilterList();

        public IEventAggregator EventAggregator { get; }
        public DocumentViewModel Document { get; }
        public IGlobalOptions Options { get; }

        public string QueryText => BuildQuery();


        public void RunQuery() {
            var qry = BuildQuery();
            EventAggregator.PublishOnUIThread(new RunQueryEvent(Document.SelectedTarget) { QueryProvider = this });
        }

        public void SendTextToEditor()
        {
            var qry = BuildQuery();
            EventAggregator.PublishOnUIThread(new SendTextToEditor(qry));
        }

        private string BuildQuery()
        {
            
            var cols = Columns.Items.Select(c => c.DaxName).Aggregate((i, j) => i + "\n    ," + j );

            return $"// START QUERY BUILDER\nEVALUATE\nSUMMARIZECOLUMNS(\n    {cols}\n)\n// END QUERY BUILDER";
        }
    }

    public class TreeViewColumnFilter { 
        // TODO - implement TreeViewColumnFilter class
    }
}
