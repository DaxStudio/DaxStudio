using ADOTabular;
using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Extensions
{
    public static class DatabaseCollectionExtensions
    {
        public static BindableCollection<DatabaseDetails> ToBindableCollection(this ADOTabular.ADOTabularDatabaseCollection databases)
        {
            var ss = new BindableCollection<DatabaseDetails>();
            foreach (var dbname in databases)
            { ss.Add(dbname); }
            return ss;
        }
    }
}
