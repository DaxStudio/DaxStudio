using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using DaxStudio.Parsers.CommentScript;
using DaxStudio.Parsers.Dax;
using DaxStudio.Parsers.Grammars.Generated;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DaxStudio.Parsers.PreProcessor
{
    /// <summary>
    /// Result of running the ANTLR based pre-processor over a query body.
    /// </summary>
    public class PreProcessResult
    {
        /// <summary>The batches produced by the pre-processor (one per GO separated section).</summary>
        public List<ScriptBatch> Batches { get; } = new List<ScriptBatch>();

        /// <summary>Any syntax errors raised while lexing/parsing.</summary>
        public List<Error> Errors { get; } = new List<Error>();

        /// <summary>
        /// Errors caused by a malformed comment-script (<c>--&gt;</c>) command (e.g. a USE with no
        /// database, a TRACE with an unknown type, or a syntax error on a command line). These are
        /// mistakes the user made in an explicit command and must be surfaced to the user (rather than
        /// silently falling back to the classic pre-processor, which would ignore the command).
        /// </summary>
        public List<Error> CommandErrors { get; } = new List<Error>();

        /// <summary>
        /// The executable query text: the original input with any comment-script (<c>--&gt;</c>)
        /// command lines blanked out (replaced with empty lines so the DAX line numbers stay aligned
        /// with the editor for accurate error markers). Whitespace and formatting of the DAX are
        /// preserved (the listener's batch <c>Output</c> cannot be used for this because whitespace is
        /// lexed on a hidden channel).
        /// </summary>
        public string ProcessedText { get; internal set; } = string.Empty;

        /// <summary>
        /// The <c>@name</c> parameters discovered in the query, keyed by name (without the
        /// leading <c>@</c>). The value indicates whether the parameter was used as an array.
        /// </summary>
        public Dictionary<string, bool> DiscoveredParameters { get; } =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        public bool HasErrors => Errors.Count > 0;

        /// <summary>True when a comment-script (<c>--&gt;</c>) command was malformed or invalid.</summary>
        public bool HasCommandErrors => CommandErrors.Count > 0;
    }

    /// <summary>
    /// Thin driver that wires up the generated PreProcessor lexer/parser with the
    /// <see cref="PreProcessorListener"/> so callers can obtain the batch structures and the
    /// list of discovered parameters with a single call. Mirrors the wiring used by the unit
    /// tests so there is a single, reusable entry point.
    /// </summary>
    public static class AntlrPreProcessor
    {
        public static PreProcessResult Parse(string queryText)
        {
            var result = new PreProcessResult();
            var errors = result.Errors;
            var errorListener = new PreProcessorErrorListener(ref errors);

            ICharStream chars = new DAXCharStream(queryText ?? string.Empty);
            var lexer = new PreProcessorLexer(chars);
            lexer.RemoveErrorListeners();
            lexer.AddErrorListener(errorListener);

            ITokenStream stream = new CommonTokenStream(lexer);
            var parser = new PreProcessorParser(stream);
            parser.RemoveErrorListeners();
            parser.AddErrorListener(errorListener);

            // Parse the whole document (block+ EOF) rather than a single block so that every
            // "--> GO" separated section becomes its own batch and the comment-script commands in
            // later sections are captured. (Using block() only parsed the first section, which
            // dropped trailing batches/commands when an earlier section - e.g. a DMV "$SYSTEM"
            // query - could not be fully parsed by the DAX-oriented grammar.)
            var tree = parser.document();

            var arrayParameters = new Dictionary<string, List<string>>();
            var listener = new PreProcessorListener(arrayParameters, result.Batches);
            var walker = new ParseTreeWalker();
            try
            {
                walker.Walk(listener, tree);
            }
            catch (CommentScriptCommandException ex)
            {
                // A recognised comment-script command was malformed (e.g. USE with no database name).
                // Record it as a command error - a hard, user-facing error - so the caller can surface
                // it rather than silently swallowing it and falling back to the classic pre-processor.
                result.CommandErrors.Add(new Error { Msg = ex.Message, Line = ex.Line, Column = ex.Column });
            }

            // Promote any syntax error reported on a comment-script command ("-->") line to a command
            // error: those are mistakes in an explicit command (e.g. "--> BOGUS", "--> TRACE X") and
            // should be surfaced, not treated as a soft DAX-body parse error that falls back silently.
            PromoteCommandLineErrors(queryText, result);

            // The lexer records every @name token it encounters (keyed with the leading '@').
            // Strip the '@' so the names line up with the values expected by QueryInfo.
            foreach (var kvp in lexer.Parameters)
            {
                var name = kvp.Key.StartsWith("@") ? kvp.Key.Substring(1) : kvp.Key;
                if (!result.DiscoveredParameters.ContainsKey(name))
                    result.DiscoveredParameters.Add(name, kvp.Value);
            }

            result.ProcessedText = StripCommentScriptLines(queryText);

            // Assign the whitespace-preserved executable DAX to each batch by splitting the raw
            // input on the '--> GO' separator lines (the grammar guarantees GO is COMMENT_SCRIPT
            // CS_GO). This aligns one text segment per batch produced by the listener.
            AssignBatchQueryText(queryText, result.Batches);

            // Resolve any "ASSERT ... PREVIOUS" operands now that QueryText is known - PREVIOUS means
            // the previous batch that actually RUNS A QUERY, which cannot be determined during the
            // tree walk above.
            PreviousReferenceResolver.Resolve(result.Batches, result.CommandErrors);

            return result;
        }

        // Splits the raw input into one segment per '--> GO' line and assigns the stripped
        // (whitespace-preserved) DAX of each segment to the matching batch. The listener starts a
        // new batch on every '--> GO', so segment count and batch count line up; a terminal
        // '--> GO' yields a trailing empty batch/segment which is simply left with empty QueryText.
        private static void AssignBatchQueryText(string text, List<ScriptBatch> batches)
        {
            if (batches == null || batches.Count == 0) return;

            var segments = SplitOnGo(text ?? string.Empty);
            for (int i = 0; i < batches.Count; i++)
            {
                var segment = i < segments.Count ? segments[i] : string.Empty;
                batches[i].QueryText = StripCommentScriptLines(segment);
            }
        }

        // Splits text into segments delimited by GO lines, including GO DELAY boundaries.
        // The GO lines themselves are dropped.
        private static List<string> SplitOnGo(string text)
        {
            var segments = new List<string>();
            var current = new List<string>();
            foreach (var line in text.Split('\n'))
            {
                if (IsGoLine(line))
                {
                    segments.Add(string.Join("\n", current));
                    current.Clear();
                }
                else
                {
                    current.Add(line);
                }
            }
            segments.Add(string.Join("\n", current));
            return segments;
        }

        private static bool IsGoLine(string line)
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("-->")) return false;
            var rest = trimmed.Substring(3).Trim();
            return string.Equals(rest, "GO", StringComparison.OrdinalIgnoreCase)
                || (rest.Length > 2
                    && rest.StartsWith("GO", StringComparison.OrdinalIgnoreCase)
                    && char.IsWhiteSpace(rest[2]));
        }

        // Moves any parser/lexer syntax error whose line points at a comment-script command ("-->")
        // line from the (soft) Errors list into the (hard) CommandErrors list. A syntax error on a
        // command line means the user mistyped an explicit command, which must be surfaced; an error
        // on a DAX-body line is a limitation of the partial grammar and correctly falls back to the
        // classic pre-processor.
        private static void PromoteCommandLineErrors(string text, PreProcessResult result)
        {
            if (result.Errors.Count == 0) return;

            var lines = (text ?? string.Empty).Split('\n');
            var remaining = new List<Error>();
            foreach (var err in result.Errors)
            {
                var idx = err.Line - 1; // ANTLR reports 1-based line numbers
                if (idx >= 0 && idx < lines.Length && lines[idx].TrimStart().StartsWith("-->"))
                    result.CommandErrors.Add(err);
                else
                    remaining.Add(err);
            }
            result.Errors.Clear();
            result.Errors.AddRange(remaining);
        }

        // Replaces whole comment-script command lines (those whose trimmed text starts with '-->',
        // which also covers the '-->>' continuation lines) with an EMPTY line, leaving every other
        // line - and its DAX whitespace/formatting - untouched. Blanking (rather than removing) the
        // command lines keeps the line numbers of the executable DAX aligned with the editor, so
        // engine-reported error positions (the red error markers and the "Goto" link) point at the
        // correct editor line. Blank lines are ignored by the DAX engine. Comment script commands are
        // line oriented in the grammar, so this reliably yields executable DAX.
        private static string StripCommentScriptLines(string text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
            var lines = text.Split('\n');
            var kept = lines.Select(l => l.TrimStart().StartsWith("-->") ? string.Empty : l);
            return string.Join("\n", kept);
        }
    }
}
