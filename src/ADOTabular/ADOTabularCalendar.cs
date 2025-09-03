
namespace ADOTabular
{
    public class ADOTabularCalendar
    {

        public ADOTabularCalendar(int? tableId, string name)
        {
            TableID = tableId;
            Name = name;
        }

        public int? TableID { get; set; }
        public string Name { get; set; }

        public string Description { get => $"{Name} calendar"; }
        public string Caption { get => Name; }
        public string DaxName { get => $"'{Name}'"; }
        public MetadataImages MetadataImage { get => MetadataImages.Calendar; }
    }
}
