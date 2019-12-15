using ADOTabular;
using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Enums;
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

namespace DaxStudio.UI.Model
{
    public class TreeViewColumnFilter {

        public TreeViewColumnFilter(IADOTabularColumn obj)
        {
            TabularObject = obj;
        }

        public IADOTabularObject TabularObject {get;}

        public string Caption => TabularObject.Caption;

        public FilterType FilterType { get; set; }

        public IEnumerable<FilterType> FilterTypes
        {
            get
            {
                var items = Enum.GetValues(typeof(FilterType)).Cast<FilterType>();
                return items;
            }
        }

        public string FilterValue { get; set; }
        public object FilterExpression { get {
                return $"FILTER(VALUES( {TabularObject.DaxName} ), {TabularObject.DaxName} = \"{FilterValue}\")";   
            } 
        }
    }
}
