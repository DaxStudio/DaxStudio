using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADOTabular.DatabaseProfile
{
    public class DatabaseProfile
    {
        public DatabaseProfile()
        {
            Tables = new TableCollection();
        }

        public string Name { get; set; }
        public string Server { get; set; }
        public DateTime ModifiedDate { get; set; }
        public DateTime ProfileDate { get; set; }
        public string Id { get; set; }
        public TableCollection Tables { get; internal set; }
    }
}
