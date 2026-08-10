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
        public AssertCommand(string property, string comparison, int integerValue, double doubleValue )
            : this(property, comparison, integerValue, doubleValue, null)
        { }

        /// <summary>
        /// Creates a performance assertion whose expected value comes from a previously captured
        /// baseline (<c>--&gt; ASSERT DURATION &lt;= BASELINE "v1"</c>) rather than a literal.
        /// </summary>
        public AssertCommand(string property, string comparison, BaselineReference baseline)
            : this(property, comparison, 0, 0.0, baseline)
        { }

        private AssertCommand(string property, string comparison, int integerValue, double doubleValue, BaselineReference baseline) {
            
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
            Baseline = baseline;
        }
        public PerformanceProperty Property {  get; set; }
        public string Comparison { get; set; }
        public int IntegerValue { get; set; }
        public double DoubleValue { get; set; }

        /// <summary>
        /// The baseline supplying the expected value, or <c>null</c> when the assertion compares
        /// against the literal in <see cref="IntegerValue"/> / <see cref="DoubleValue"/>.
        /// </summary>
        public BaselineReference Baseline { get; set; }
    }
}
