using System.ComponentModel.Composition;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using DaxStudio.UI.Model;
using DaxStudio.UI.Interfaces;
using System.Windows.Data;
using System;
using System.ComponentModel;
using Serilog;
using System.Windows.Input;
using DaxStudio.Interfaces;
using Dax.ViewModel;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Windows.Navigation;

namespace DaxStudio.UI.ViewModels
{
    public class PartitionGroupDescription : PropertyGroupDescription
    {
        public PartitionGroupDescription(string propertyName) : base(propertyName) { }

        public override bool NamesMatch(object groupName, object itemName)
        {
            var groupTable = (VpaTableViewModel)groupName;
            var itemTable = (VpaTableViewModel)itemName;
            return base.NamesMatch(groupTable.TableName, itemTable.TableName);
        }
        public override object GroupNameFromItem(object item, int level, CultureInfo culture)
        {
            var partition = item as VpaPartitionViewModel;
            return partition.Table;
        }

    }
}
