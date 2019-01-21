namespace DaxStudio.UI.Model
{
    public class QueryParameter
    {
        public QueryParameter(string name)
        {
            Name = name;
        }
        public QueryParameter(string name, string value)
        {
            Name = name;
            Value = value;
        }
        public string Name { get; set; }
        public string Value { get; set; }
    }
}
