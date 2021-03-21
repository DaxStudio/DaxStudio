using System.Windows.Data;
using System;
using System.Globalization;

namespace DaxStudio.UI.ViewModels
{
    public class TableGroupDescription : PropertyGroupDescription, IComparable
    {
        public TableGroupDescription(string propertyName) : base(propertyName) { }

        public override bool NamesMatch(object groupName, object itemName)
        {
            var groupTable = (VpaTableViewModel)groupName;
            var itemTable = (VpaTableViewModel)itemName;
            return base.NamesMatch(groupTable.TableName, itemTable.TableName);
        }
        public override object GroupNameFromItem(object item, int level, CultureInfo culture)
        {
            var col = item as VpaColumnViewModel;
            var rel = item as VpaRelationshipViewModel;
            return col != null ? col.Table : rel.Table;
        }

        public int CompareTo(object obj)
        {
            throw new NotImplementedException();
        }
    }
}
