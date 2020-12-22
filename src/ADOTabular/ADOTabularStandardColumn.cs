namespace ADOTabular
{
    public class ADOTabularStandardColumn: ADOTabularColumn
    {
                public ADOTabularStandardColumn( ADOTabularTable table, string internalName, string name, string caption,  string description,
                                bool isVisible, ADOTabularObjectType columnType, string contents)
        :base(table,internalName,name, caption,description,isVisible,columnType,contents)

    {}
    }
}
