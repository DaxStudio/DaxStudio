using ADOTabular;

namespace DaxStudio.UI.Events
{
    public class DefineFunctionOnEditor
    {
        public DefineFunctionOnEditor(string functionName, string functionExpression)
        {
            this.FunctionName = functionName;
            this.FunctionExpression = functionExpression;
        }

        public DefineFunctionOnEditor(ADOTabularUserDefinedFunction function)
        {
            this.FunctionName = function.Name;
            this.FunctionExpression = function.Expression;
        }

        public string FunctionName { get; set; }
        public string FunctionExpression { get; set; }
    }
}
