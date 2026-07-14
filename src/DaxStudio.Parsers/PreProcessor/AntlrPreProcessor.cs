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
        /// The executable query text: the original input with any comment-script (<c>--&gt;</c>)
        /// command lines removed. Whitespace and formatting of the DAX are preserved (the listener's
        /// batch <c>Output</c> cannot be used for this because whitespace is lexed on a hidden channel).
        /// </summary>
        public string ProcessedText { get; internal set; } = string.Empty;

        /// <summary>
        /// The <c>@name</c> parameters discovered in the query, keyed by name (without the
        /// leading <c>@</c>). The value indicates whether the parameter was used as an array.
        /// </summary>
        public Dictionary<string, bool> DiscoveredParameters { get; } =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        public bool HasErrors => Errors.Count > 0;
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

            var tree = parser.block();

            var arrayParameters = new Dictionary<string, List<string>>();
            var listener = new PreProcessorListener(arrayParameters, result.Batches);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

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

        // Splits text into segments delimited by lines whose trimmed content is '--> GO'
        // (case-insensitive). The GO lines themselves are dropped.
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
            return string.Equals(rest, "GO", StringComparison.OrdinalIgnoreCase);
        }

        // Removes whole comment-script command lines (those whose trimmed text starts with '-->')
        // while leaving all other lines - and their DAX whitespace/formatting - untouched. Comment
        // script commands are line oriented in the grammar, so this reliably yields executable DAX.
        private static string StripCommentScriptLines(string text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
            var lines = text.Split('\n');
            var kept = lines.Where(l => !l.TrimStart().StartsWith("-->"));
            return string.Join("\n", kept);
        }
    }
}
