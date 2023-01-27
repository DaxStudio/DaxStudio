using ADOTabular;
using ADOTabular.Interfaces;

namespace DaxStudio.UI.Model
{
    public class ADOTabularTableStub : IADOTabularObject
    {
        public string Caption { get; set; }

        public string DaxName { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public bool IsVisible {get; set; }

        public ADOTabularObjectType ObjectType {get;set; }
    }
}
