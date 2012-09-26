using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ADOTabular;

namespace DaxStudio.ViewModel
{
    class ColumnViewModel: TreeViewItemViewModel
    {
        readonly ADOTabularColumn _column;
        private readonly TableViewModel _parent;

        public ColumnViewModel(ADOTabularColumn column, TableViewModel parent)
            : base(null)
        {
            _column = column;
            _parent = parent;
        }

        public string ColumnName
        {
            get { return _column.Description; }
        }

        public TableViewModel Table
        {
            get { return _parent; }
        }

    }
}
