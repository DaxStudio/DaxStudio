
using System.Collections.Generic;

namespace ADOTabular
{
    public interface IADOTabularObject
    {
        string Caption { get;  }
        string DaxName { get; }
        string Name { get; }
        bool IsVisible { get; }
        ADOTabularObjectType ObjectType { get; }   
    }
}
