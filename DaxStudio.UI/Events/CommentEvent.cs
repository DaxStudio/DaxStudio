using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Events
{
    public class CommentEvent
    {
        public CommentEvent(bool commentSelection)
        {
            CommentSelection = commentSelection;
        }
        public bool CommentSelection { get; set; }
    }
}
