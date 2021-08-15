using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Events
{
    public class ViewAsEvent
    {
        public ViewAsEvent(string userName, string roles)
        {
            UserName = userName;
            Roles = roles;
        }

        public string UserName { get; }
        public string Roles { get; }
    }
}
