using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Utils
{
    public static class UserHelper
    {
        public static bool IsAdministrator()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        public static string GetUser()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                string name = identity.Name;
                var principal = new WindowsPrincipal(identity);
                bool isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
                return name + (isAdmin ? $" [Admin]": string.Empty);
            }

        }
    }
}
