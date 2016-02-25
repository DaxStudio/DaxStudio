using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADOTabular.DatabaseProfile
{
    public class ColumnCollection:List<Column>
    {
        public Column this[string index] {
            get {
                return this.Find(c => c.Name == index);
            }
        }

        public Column GetById(string id)
        {
            return this.Find(c => c.Id == id);
        }
    }
}
