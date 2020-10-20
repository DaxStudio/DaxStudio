using ADOTabular.Interfaces;

namespace ADOTabular
{
    public class ADOTabularObjectReference : IADOTabularObjectReference
    {
        public ADOTabularObjectReference(string name, string internalReference)
        {
            InternalReference = internalReference;
            Name = name;
        }
        public string InternalReference { get; }

        public string Name { get; }
    }
}
