using DaxStudio.UI.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DaxStudio.UI.Events
{
    public class NewDocumentEvent
    {
        private readonly IResultsTarget _target;
        public NewDocumentEvent(IResultsTarget target)
        {
            _target = target;
        }

        public IResultsTarget Target
        {
            get
            {
                return _target;
            }
        }

    }
}
