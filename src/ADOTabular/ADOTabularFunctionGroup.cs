namespace ADOTabular
{
    public class ADOTabularFunctionGroup
    {
        private readonly ADOTabularConnection _connection;
        public ADOTabularFunctionGroup(string caption, ADOTabularConnection connection)
        {
            Caption = caption;
            _connection = connection;
            Functions = new ADOTabularFunctionCollection(_connection);
        }
        public string Caption { get; private set; }
        public ADOTabularFunctionCollection Functions { get; private set; }
        public MetadataImages MetadataImage =>  MetadataImages.Folder;
        
    }
}
