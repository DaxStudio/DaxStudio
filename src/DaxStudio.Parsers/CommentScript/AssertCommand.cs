using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.CommentScript
{
    public class AssertCommand: ScriptCommand
    {
        public AssertCommand(string property, string comparison, int integerValue, double doubleValue ) {
            
            try
            {
                Property = (PerformanceProperty)System.Enum.Parse(typeof(PerformanceProperty), property, true);
            }
            catch
            {
                throw new ArgumentException($"Unable to process ASSERT command '{property}' is not a valid PerformanceProperty");
            }

            Comparison = comparison;
            IntegerValue = integerValue;
            DoubleValue = doubleValue;
        }
        public PerformanceProperty Property {  get; set; }
        public string Comparison { get; set; }
        public int IntegerValue { get; set; }
        public double DoubleValue { get; set; }
    }
}
