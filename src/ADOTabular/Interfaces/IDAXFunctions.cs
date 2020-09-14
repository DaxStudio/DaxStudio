namespace ADOTabular.Interfaces
{
    public interface IDAXFunctions
    {
        public bool SummarizeColumns { get; set; }
        public bool SubstituteWithIndex { get; set; }
        public bool TreatAs { get; set; }
    }
}
