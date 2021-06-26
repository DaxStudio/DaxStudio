using System.Collections.Generic;

namespace ADOTabular.Interfaces
{

	public enum FolderReferenceType
    {
        None,
		Folder,
		Column
    }
    public interface IADOTabularFolderReference : IADOTabularObjectReference
    {
        List<IADOTabularObjectReference> FolderItems { get; }
     
		FolderReferenceType ReferenceType { get; }
        bool IsVisible { get; }
    }
}
