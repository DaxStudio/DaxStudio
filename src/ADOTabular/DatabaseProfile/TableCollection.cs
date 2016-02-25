using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADOTabular.DatabaseProfile
{
    public class TableCollection:List<Table>
    {
        public Table this[string index] {
            get {
                return this.Find(t=> t.Name == index);
            }
        }


    }
}
