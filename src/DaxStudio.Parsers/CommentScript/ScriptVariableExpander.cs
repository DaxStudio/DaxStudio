using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DaxStudio.Parsers.CommentScript
{
    /// <summary>
    /// Expands <c>$(...)</c> references in comment-script command string arguments against a set of
    /// script variables (defined by <c>--&gt; SET</c>) and built-in namespaces (<c>now</c>,
    /// <c>utcnow</c>, <c>env</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Variables are stored case-insensitively. <see cref="SetVariable"/> expands the supplied value
    /// <b>eagerly</b> and stores the resolved literal, so a captured built-in such as
    /// <c>$(now:yyyy-MM-dd)</c> is frozen at the point the SET executes and stays constant for the
    /// rest of the run.
    /// </para>
    /// <para>
    /// An undefined variable, an unknown built-in namespace, a bad date format, or a reference cycle
    /// all raise <see cref="CommentScriptCommandException"/> so the run fails (the CI-safe default).
    /// A literal <c>$(</c> is written as <c>$$(</c>.
    /// </para>
    /// </remarks>
    public sealed class ScriptVariableExpander
    {
        private const int MaxDepth = 16;
        private static readonly Regex RefRegex = new Regex(@"\$\((?<ref>[^)]*)\)", RegexOptions.Compiled);
        private const string EscapePlaceholder = "\u0001DXS_ESC_DOLLAR_PAREN\u0001";

        private readonly Dictionary<string, string> _vars =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Func<DateTime> _nowLocal;
        private readonly Func<DateTime> _nowUtc;

        public ScriptVariableExpander(Func<DateTime> nowLocal = null, Func<DateTime> nowUtc = null)
        {
            _nowLocal = nowLocal ?? (() => DateTime.Now);
            _nowUtc = nowUtc ?? (() => DateTime.UtcNow);
        }

        /// <summary>
        /// Eagerly expands <paramref name="rawValue"/> and stores the resolved literal under
        /// <paramref name="name"/> (last write wins).
        /// </summary>
        public void SetVariable(string name, string rawValue)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            _vars[name] = Expand(rawValue);
        }

        /// <summary>Removes all defined variables (used to reset state between CLI files).</summary>
        public void Reset() => _vars.Clear();

        /// <summary>True when a variable with the given name has been defined.</summary>
        public bool ContainsVariable(string name) => name != null && _vars.ContainsKey(name);

        /// <summary>
        /// Replaces every <c>$(...)</c> reference in <paramref name="input"/> with its resolved value.
        /// <c>$$(</c> yields a literal <c>$(</c>. Returns <paramref name="input"/> unchanged when it is
        /// null or contains no references.
        /// </summary>
        public string Expand(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            if (input.IndexOf("$(", StringComparison.Ordinal) < 0
                && input.IndexOf("$$(", StringComparison.Ordinal) < 0)
            {
                return input;
            }

            // Protect the escape sequence "$$(" so its "$(" is not treated as a reference start.
            var protectedInput = input.Replace("$$(", EscapePlaceholder);
            var expanded = ExpandCore(protectedInput, 0);
            return expanded.Replace(EscapePlaceholder, "$(");
        }

        private string ExpandCore(string input, int depth)
        {
            if (depth > MaxDepth)
                throw new CommentScriptCommandException($"Script variable expansion exceeded the maximum nesting depth of {MaxDepth}; check for a reference cycle.");

            return RefRegex.Replace(input, match =>
            {
                var reference = match.Groups["ref"].Value;
                var resolved = ResolveRef(reference);
                // Recursively expand so a variable value may reference other variables/built-ins.
                return ExpandCore(resolved, depth + 1);
            });
        }

        private string ResolveRef(string reference)
        {
            var colonIndex = reference.IndexOf(':');
            if (colonIndex >= 0)
            {
                var ns = reference.Substring(0, colonIndex);
                var arg = reference.Substring(colonIndex + 1);
                switch (ns.ToLowerInvariant())
                {
                    case "now":
                        return FormatDate(_nowLocal(), arg, "now");
                    case "utcnow":
                        return FormatDate(_nowUtc(), arg, "utcnow");
                    case "env":
                        var envValue = Environment.GetEnvironmentVariable(arg);
                        if (envValue == null)
                            throw new CommentScriptCommandException($"Environment variable '{arg}' referenced by $(env:{arg}) is not defined.");
                        return envValue;
                    default:
                        throw new CommentScriptCommandException($"Unknown built-in namespace '{ns}' in reference $({reference}). Valid namespaces are: now, utcnow, env.");
                }
            }

            if (_vars.TryGetValue(reference, out var value))
                return value;

            throw new CommentScriptCommandException($"Undefined script variable '{reference}'. Define it with '--> SET {reference} = <value>' before it is used.");
        }

        private static string FormatDate(DateTime value, string format, string ns)
        {
            if (string.IsNullOrEmpty(format))
                throw new CommentScriptCommandException($"$({ns}:...) requires a .NET date format string, e.g. $({ns}:yyyy-MM-dd).");
            try
            {
                return value.ToString(format, CultureInfo.InvariantCulture);
            }
            catch (FormatException ex)
            {
                throw new CommentScriptCommandException($"Invalid date format '{format}' in $({ns}:{format}): {ex.Message}");
            }
        }

        /// <summary>
        /// Applies script-variable expansion across every command in <paramref name="batches"/>, in
        /// order. A <see cref="VariableCommand"/> defines/updates a variable (captured eagerly); every
        /// path/target-bearing command has its string argument expanded in place so downstream
        /// consumers see the resolved value. Because the walk is in command order, a <c>SET</c> is
        /// visible only to commands that follow it (including across <c>--&gt; GO</c> batch boundaries).
        /// </summary>
        /// <remarks>Mutates command properties in place; safe to call once per run.</remarks>
        public static void ExpandBatches(IEnumerable<ScriptBatch> batches, Func<DateTime> nowLocal = null, Func<DateTime> nowUtc = null)
        {
            if (batches == null) return;
            var expander = new ScriptVariableExpander(nowLocal, nowUtc);
            foreach (var batch in batches)
            {
                if (batch?.Commands == null) continue;
                foreach (var cmd in batch.Commands)
                {
                    switch (cmd)
                    {
                        case VariableCommand v:
                            expander.SetVariable(v.Name, v.RawValue);
                            break;
                        case ConnectCommand c when c.ConnectionName != null:
                            c.ConnectionName = expander.Expand(c.ConnectionName);
                            break;
                        case UseCommand u when u.DatabaseName != null:
                            u.DatabaseName = expander.Expand(u.DatabaseName);
                            break;
                        case ExportCommand m when m.FileName != null:
                            m.FileName = expander.Expand(m.FileName);
                            break;
                        case AssertTableCommand a when a.FilePath != null:
                            a.FilePath = expander.Expand(a.FilePath);
                            break;
                        case SaveAsCommand s when s.FileName != null:
                            s.FileName = expander.Expand(s.FileName);
                            break;
                    }
                }
            }
        }
    }
}
