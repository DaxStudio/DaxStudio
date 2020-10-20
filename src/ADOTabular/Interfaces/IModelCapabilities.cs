namespace ADOTabular.Interfaces
{
    public interface IModelCapabilities
    {
        public bool Variables { get; set; }
        public bool TableConstructor { get; set; }

        public IDAXFunctions DAXFunctions { get;  }
    }
}
