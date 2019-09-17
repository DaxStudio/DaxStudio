using DaxStudio.Interfaces;
using DaxStudio.UI.Enums;
using Newtonsoft.Json;
using System.IO;
using System.Runtime.Serialization;

namespace DaxStudio.UI.Model
{
    [DataContract]
    public class DaxFile: IDaxFile
    {

        [JsonConstructor]
        public DaxFile(string fullPath, bool pinned)
        {
            FullPath = fullPath;
            Pinned = pinned;
        }
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
        [DataMember]
        public bool Pinned { get; set; }
        private string _fullPath;
        [DataMember]
        public string FullPath {
            get { return _fullPath; }
            set { 
                _fullPath = value;
                FileName = Path.GetFileNameWithoutExtension(_fullPath);
                FileAndExtension = Path.GetFileName(_fullPath);
                Folder = Path.GetDirectoryName(_fullPath);
                Extension = Path.GetExtension(_fullPath).TrimStart('.').ToUpper();
                ExtensionLabel = Extension == "DAX"?"":Extension;
            }
        }

        public string Extension {get;private set;}
        public string ExtensionLabel {get; private set;}

        public string FileName { get; private set; }
        public string Folder { get; private set; }
        public string FileAndExtension { get; private set; }

        public override string ToString()
        {
            return string.Format("{0}|{1}", Pinned.ToString(), FullPath);
        }
        
        public  FileIcons Icon
        {
            get{
                return Extension == "DAX" ?  FileIcons.Dax : FileIcons.Other;
            }
        }
    }
}
