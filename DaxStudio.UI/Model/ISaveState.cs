using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Model
{
    public interface ISaveState
    {
        void Save(string filename);
        void Load(string filename);
    }
}
