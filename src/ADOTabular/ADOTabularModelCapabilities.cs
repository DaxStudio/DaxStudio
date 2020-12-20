using ADOTabular.Interfaces;

namespace ADOTabular
{
    public class ADOTabularModelCapabilities : IModelCapabilities
    {
        /*
         * <bi:ModelCapabilities>
                <bi:EncourageIsEmptyDAXFunctionUsage>true</bi:EncourageIsEmptyDAXFunctionUsage>
                <bi:QueryBatching>1</bi:QueryBatching>
                <bi:Variables>1</bi:Variables>
                <bi:DAXFunctions>
                  <bi:SummarizeColumns>1</bi:SummarizeColumns>
                  <bi:SubstituteWithIndex>1</bi:SubstituteWithIndex>
                  <bi:LeftOuterJoin>1</bi:LeftOuterJoin>
                  <bi:BinaryMinMax>1</bi:BinaryMinMax>
                </bi:DAXFunctions>
              </bi:ModelCapabilities>
         */
        
        public IDAXFunctions DAXFunctions { get;  } = new DaxFunctions();

        public bool Variables { get; set; }

        public bool TableConstructor { get; set; }
      
    }
    public class DaxFunctions : IDAXFunctions
    {
        bool IDAXFunctions.SummarizeColumns { get; set; }
        bool IDAXFunctions.SubstituteWithIndex { get; set; }

        bool IDAXFunctions.TreatAs { get; set; }
    }
}
