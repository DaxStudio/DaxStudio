using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Model
{
    public class DaxFile
    {
        public DaxFile(string initialValue)
        {
            var parts = initialValue.Split('|');
            switch (parts.Length )
            {
                case 1:
                    Pinned = false;
                    FullPath = parts[0];
                    break;
                default:  //ignore more than 2 parts
                    Pinned = bool.Parse(parts[0]);
                    FullPath = parts[1];
                    break;
            }
        }

        public bool Pinned { get; private set; }
        private string _fullPath;
        public string FullPath {
            get { return _fullPath; }
            private set { 
                _fullPath = value;
                FileName = Path.GetFileNameWithoutExtension(_fullPath);
                FileAndExtension = Path.GetFileName(_fullPath);
                Folder = Path.GetDirectoryName(_fullPath);
            }
        }

        public string FileName { get; private set; }
        public string Folder { get; private set; }
        public string FileAndExtension { get; private set; }

        public override string ToString()
        {
            return string.Format("{0}|{1}", Pinned.ToString(), FullPath);
        }
        
    }
}
