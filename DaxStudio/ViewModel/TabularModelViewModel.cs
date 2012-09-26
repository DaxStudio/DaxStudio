using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ADOTabular;

namespace DaxStudio.ViewModel
{
    public class TabularModelViewModel : TreeViewItemViewModel
    {
        readonly ADOTabular.ADOTabularModel _model;

        public TabularModelViewModel(ADOTabular.ADOTabularModel model) 
            : base(null)
        {
            _model = model;
        }

        public string ModelName
        {
            get { return _model.Description; }
        }

        protected override void LoadChildren()
        {
            foreach (ADOTabularTable table in _model.Tables)
                base.Children.Add(new TableViewModel(table, this));
        }

    }

}
