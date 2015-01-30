using Caliburn.Micro;
using DaxStudio.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Model
{
    public class QueryPlanModel
    {
        public BindableCollection<PhysicalQueryPlanRow> PhysicalQueryPlanRows {get;set;}
        public BindableCollection<LogicalQueryPlanRow> LogicalQueryPlanRows { get; set; }
    }
}
