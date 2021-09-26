using System.Collections.Generic;
using ADOTabular.Interfaces;

namespace ADOTabular
{
    public class ADOTabularDisplayFolder : IADOTabularFolderReference
    {
        public ADOTabularDisplayFolder(string name, string internalReference) {
            Name = name??internalReference;
            InternalReference = internalReference;
            ReferenceType = FolderReferenceType.Folder;
            FolderItems = new List<IADOTabularObjectReference>();
        }

        public string Name { get; }

        public string InternalReference { get; }

        public List<IADOTabularObjectReference> FolderItems { get; }

        public FolderReferenceType ReferenceType { get; }

        public bool IsVisible { get;set; }
    }
}
