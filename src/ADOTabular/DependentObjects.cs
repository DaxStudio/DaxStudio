using System.Collections.Generic;

namespace ADOTabular
{
    /// <summary>
    /// Holds the set of model objects that a measure or query depends on
    /// </summary>
    public class DependentObjects
    {
        public List<ADOTabularMeasure> Measures { get; } = new List<ADOTabularMeasure>();

        /// <summary>
        /// The user defined functions that are referenced, ordered so that any function
        /// which is referenced by another function appears first
        /// </summary>
        public List<ADOTabularUserDefinedFunction> Functions { get; } = new List<ADOTabularUserDefinedFunction>();
    }
}
