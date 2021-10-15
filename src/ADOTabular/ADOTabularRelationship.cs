namespace ADOTabular
{
    public class ADOTabularRelationship
    {
        public string InternalName { get; set; }
        public ADOTabularTable FromTable { get; set; }
        public ADOTabularTable ToTable { get; set; }
        public string FromColumn { get; set; }
        public string FromColumnMultiplicity { get; set; }
        public string ToColumnMultiplicity { get; set; }
        public string ToColumn { get; set; }
        public string CrossFilterDirection { get; set; }
        public bool IsActive { get; internal set; }
    }
}
