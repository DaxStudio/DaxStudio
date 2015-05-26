using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Utils
{
    public static class DatabaseCollectionExtensions
    {
        public static BindableCollection<string> ToBindableCollection(this ADOTabular.ADOTabularDatabaseCollection databases)
        {
            var ss = new BindableCollection<string>();
            foreach (var dbname in databases)
            { ss.Add(dbname); }
            return ss;
        }
    }
}
