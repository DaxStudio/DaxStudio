using ADOTabular;

namespace DaxStudio.ViewModel
{
    public class TableViewModel: TreeViewItemViewModel
    {
        
        readonly ADOTabularTable _table;
        private readonly TreeViewItemViewModel _parent;

        public TableViewModel(ADOTabularTable table, TreeViewItemViewModel parent)
            : base(null)
        {
            _table = table;
            _parent = parent;
        }

        public string TableName
        {
            get { return _table.Description; }
        }

        protected override void LoadChildren()
        {
            foreach (ADOTabularColumn column in _table.Columns)
                base.Children.Add(new ColumnViewModel(column, this));
        }

    }
}
