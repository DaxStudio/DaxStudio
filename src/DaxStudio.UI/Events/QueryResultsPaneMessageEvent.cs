using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Events
{
    public class QueryResultsPaneMessageEvent
    {
        private IResultsTarget _target;
        public QueryResultsPaneMessageEvent(IResultsTarget target)
        {
            _target = target;
        }
        public IResultsTarget Target
        {
            get { return _target; }
        }
    }
}
