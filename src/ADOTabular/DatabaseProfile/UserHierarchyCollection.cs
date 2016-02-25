using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADOTabular.DatabaseProfile
{
    public class UserHierarchyCollection : List<UserHierarchy>
    {
        public UserHierarchy this[string name] {
            get {
                var hier = this.Find(h => h.Name == name);
                if (hier == null) {
                    // if a hierarchy with this name does not exist create it
                    hier = new UserHierarchy(){Name = name};
                    this.Add(hier);
                }
                return hier;
            }
        }
    }
}
