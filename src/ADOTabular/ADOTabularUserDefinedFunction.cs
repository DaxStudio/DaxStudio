using ADOTabular.Interfaces;

namespace ADOTabular
{
    /// <summary>
    /// Represents a DAX user defined function (UDF) that is stored in the model.
    /// These are only available on servers with a compatibility level of 1702 or higher
    /// and the expression is read from the $SYSTEM.TMSCHEMA_FUNCTIONS DMV.
    /// </summary>
    public class ADOTabularUserDefinedFunction : IADOTabularObject
    {
        public ADOTabularUserDefinedFunction(string name, string expression, string description)
        {
            Name = name;
            Expression = expression;
            Description = description;
        }

        public string Name { get; }

        // functions are not translated so there is no difference between the Name and Caption
        public string Caption => Name;

        public string Description { get; }

        /// <summary>
        /// The body of the function, this is everything that appears after the
        /// "FUNCTION &lt;name&gt; = " part of the definition (eg. "(x: INT) => x * 2")
        /// </summary>
        public string Expression { get; }

        public string DaxName => Name;

        public ADOTabularObjectType ObjectType => ADOTabularObjectType.Function;

        public MetadataImages MetadataImage => MetadataImages.Function;

        public bool IsVisible => true;
    }
}
