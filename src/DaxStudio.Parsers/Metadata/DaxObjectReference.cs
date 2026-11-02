namespace DaxStudio.Parsers.Metadata
{
    /// <summary>
    /// The syntactic kind of a reference discovered inside a DAX expression. The parser can only
    /// classify references by syntax; resolving a bare <c>[Name]</c> to a measure or a column (or a
    /// function name to a model / query-scoped function) requires the model metadata and is done by the
    /// caller.
    /// </summary>
    public enum DaxReferenceKind
    {
        /// <summary>A fully-qualified column reference (<c>Table[Column]</c> or <c>'Table'[Column]</c>).</summary>
        Column,

        /// <summary>A bare bracketed reference (<c>[Name]</c>) that could be a measure or a column.</summary>
        ColumnOrMeasure,

        /// <summary>A call to a (non-built-in) function - potentially a model or query-scoped UDF.</summary>
        Function
    }

    /// <summary>
    /// A single object reference (column, measure or function call) found while walking a DAX
    /// expression, e.g. the body of a query-scoped <c>DEFINE FUNCTION</c>. Used to extend the dependency
    /// tree with the objects a query-scoped function depends on.
    /// </summary>
    public class DaxObjectReference
    {
        public DaxObjectReference(DaxReferenceKind kind, string name, string table = null)
        {
            Kind = kind;
            Name = name ?? string.Empty;
            Table = table ?? string.Empty;
        }

        public DaxReferenceKind Kind { get; }

        /// <summary>The column / measure / function name (delimiters already stripped).</summary>
        public string Name { get; }

        /// <summary>The owning table for a <see cref="DaxReferenceKind.Column"/> reference; empty otherwise.</summary>
        public string Table { get; }
    }
}
