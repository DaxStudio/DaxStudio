using System;

namespace DaxStudio.Parsers.CommentScript
{
    /// <summary>
    /// A reference to a previously captured baseline, used as the right-hand operand of an
    /// <c>--&gt; ASSERT</c> command (e.g. <c>--&gt; ASSERT DURATION &lt;= BASELINE "v1" * 1.1</c>).
    /// </summary>
    /// <remarks>
    /// The optional <see cref="Factor"/> multiplies the captured baseline value before the comparison
    /// is applied, which is how both a tolerance and an improvement target are expressed:
    /// <list type="bullet">
    /// <item><c>&lt;= BASELINE * 1.1</c> - allow the candidate to be up to 10% worse (timing noise).</item>
    /// <item><c>&lt;= BASELINE * 0.9</c> - require the candidate to be at least 10% better.</item>
    /// </list>
    /// A reference written as <c>PREVIOUS</c> starts out <b>unresolved</b> (<see cref="Name"/> is
    /// <see cref="PreviousName"/>); the post-parse resolver points it at the batch it refers to. See
    /// <see cref="IsPrevious"/>.
    /// </remarks>
    public sealed class BaselineReference
    {
        /// <summary>
        /// The name used by an unnamed <c>--&gt; BASELINE</c> / <c>BASELINE</c> reference, so the named and
        /// unnamed forms share a single lookup path.
        /// </summary>
        public const string DefaultName = "(default)";

        /// <summary>
        /// The placeholder name carried by a <c>PREVIOUS</c> reference until the post-parse resolver
        /// replaces it with the generated name of the batch it refers to.
        /// </summary>
        public const string PreviousName = "(previous)";

        public BaselineReference(string name = null, double factor = 1.0, bool isPrevious = false)
        {
            Name = string.IsNullOrWhiteSpace(name) ? DefaultName : name;
            Factor = factor;
            IsPrevious = isPrevious;
        }

        /// <summary>The baseline being referenced. <see cref="DefaultName"/> for the unnamed form.</summary>
        /// <remarks>
        /// Settable only so the post-parse resolver can rewrite a <c>PREVIOUS</c> reference from
        /// <see cref="PreviousName"/> to the generated name of its target batch.
        /// </remarks>
        public string Name { get; internal set; }

        /// <summary>
        /// The multiplier applied to the captured baseline value before comparing. Defaults to 1.0
        /// (compare against the captured value as-is).
        /// </summary>
        public double Factor { get; }

        /// <summary>
        /// True when the reference was written as <c>PREVIOUS</c> rather than
        /// <c>BASELINE ["name"]</c>. Preserved after resolution so the assertion still displays as
        /// <c>PREVIOUS</c> rather than the resolver's internal name.
        /// </summary>
        public bool IsPrevious { get; }

        /// <summary>True while a <c>PREVIOUS</c> reference has not yet been pointed at a target batch.</summary>
        public bool IsUnresolvedPrevious =>
            IsPrevious && string.Equals(Name, PreviousName, StringComparison.OrdinalIgnoreCase);

        /// <summary>True when this reference targets the unnamed baseline.</summary>
        public bool IsDefault => string.Equals(Name, DefaultName, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// True when <paramref name="name"/> collides with a name DAX Studio generates internally -
        /// <see cref="DefaultName"/> for the unnamed baseline, or the <c>(previous:N)</c> form the
        /// PREVIOUS resolver produces.
        /// </summary>
        /// <remarks>
        /// A baseline name may be a quoted string literal, which the lexer unquotes - so a user
        /// <i>can</i> write <c>--&gt; BASELINE "(default)"</c> or <c>--&gt; BASELINE "(previous:1)"</c>.
        /// Those must be rejected: the baseline store is keyed by name and is last-write-wins, so a
        /// collision would silently make assertions compare against the wrong batch.
        /// </remarks>
        public static bool IsReservedName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return string.Equals(name, DefaultName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, PreviousName, StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("(previous:", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// A human-readable rendering of the reference for test descriptions, e.g.
        /// <c>BASELINE "v1" * 1.1</c>, <c>BASELINE</c> or <c>PREVIOUS * 0.9</c>. A resolved
        /// <c>PREVIOUS</c> still renders as <c>PREVIOUS</c>, never as the resolver's internal name.
        /// </summary>
        public override string ToString()
        {
            var name = IsPrevious
                ? "PREVIOUS"
                : IsDefault ? "BASELINE" : $"BASELINE \"{Name}\"";

            // ReSharper disable once CompareOfFloatsByEqualityOperator - 1.0 is set literally, not computed.
            return Factor == 1.0
                ? name
                : $"{name} * {Factor.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)}";
        }
    }
}
