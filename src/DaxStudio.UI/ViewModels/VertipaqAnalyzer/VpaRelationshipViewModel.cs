using Dax.ViewModel;

namespace DaxStudio.UI.ViewModels
{
    public class VpaRelationshipViewModel
    {
        VpaRelationship _rel;
        public VpaRelationshipViewModel(VpaRelationship rel, VpaTableViewModel table)
        {
            Table = table;
            _rel = rel;
        }
        public VpaTableViewModel Table { get; }
        public string RelationshipFromToName => _rel.RelationshipFromToName;
        public long UsedSize => _rel.UsedSize;
        public long FromColumnCardinality => _rel.FromColumnCardinality;
        public long ToColumnCardinality => _rel.ToColumnCardinality;
        public long MissingKeys => _rel.MissingKeys;
        public long InvalidRows => _rel.InvalidRows;
        public string SampleReferentialIntegrityViolations => _rel.SampleReferentialIntegrityViolations;
        public double OneToManyRatio => _rel.OneToManyRatio;

        public string RiViolationQuery => $@"// Values in: {_rel.FromColumnName} missing in: {_rel.ToColumnName} 
EVALUATE 
CALCULATETABLE ( 
    DISTINCT ( {_rel.FromColumnName} ), 
    ISBLANK( {_rel.ToColumnName} ),
    USERELATIONSHIP( {_rel.FromColumnName}, {_rel.ToColumnName} )
)";


    }
}
