namespace ADOTabular.Interfaces
{
    public interface IADOTabularObject
    {
        string Caption { get;  }
        string DaxName { get; }
        string Name { get; }
        string Description { get; }
        bool IsVisible { get; }
        ADOTabularObjectType ObjectType { get; }   
    }
}
