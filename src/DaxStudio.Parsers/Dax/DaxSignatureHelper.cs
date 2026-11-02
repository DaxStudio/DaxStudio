using Antlr4.Runtime;
using DaxStudio.Parsers.Dax;
using DaxStudio.Parsers.Metadata;
using System.Collections.Generic;
using System.Linq;

using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.Dax
{
    /// <summary>
    /// Provides function signature help by finding the enclosing function call
    /// at a cursor position and determining which argument is active.
    /// </summary>
    public class DaxSignatureHelper
    {
        /// <summary>
        /// Gets signature help information at the given cursor offset.
        /// Returns null if the cursor is not inside a function call.
        /// </summary>
        public static SignatureHelpResult GetSignatureHelp(string input, int cursorOffset, IModelMetadataProvider metadata = null)
        {
            ICharStream chars = new DAXCharStream(input);
            var lexer = new DAXLexer(chars);
            lexer.RemoveErrorListeners();
            lexer.AddErrorListener(new DaxIntellisenseErrorListener());

            var tokenStream = new CommonTokenStream(lexer);
            tokenStream.Fill();

            return GetSignatureHelpFromTokens(tokenStream, cursorOffset, metadata);
        }

        private static SignatureHelpResult GetSignatureHelpFromTokens(CommonTokenStream tokenStream, int cursorOffset, IModelMetadataProvider metadata)
        {
            var tokens = tokenStream.GetTokens()
                .Where(t => t.Channel == 0 && t.Type != DAXLexer.Eof)
                .ToList();

            // Walk backward from cursor to find enclosing function call
            int parenDepth = 0;
            int argumentIndex = 0;
            string functionName = null;

            for (int i = tokens.Count - 1; i >= 0; i--)
            {
                var token = tokens[i];
                if (token.StartIndex > cursorOffset) continue;

                switch (token.Type)
                {
                    case DAXLexer.CLOSE_PARENS:
                        parenDepth++;
                        break;

                    case DAXLexer.OPEN_PARENS:
                        if (parenDepth > 0)
                        {
                            parenDepth--;
                        }
                        else
                        {
                            // Found the opening paren of the enclosing function
                            if (i > 0)
                            {
                                functionName = GetFunctionNameBefore(tokens, i);
                            }
                            if (functionName != null)
                            {
                                return BuildResult(functionName, argumentIndex, metadata);
                            }
                            return null;
                        }
                        break;

                    case DAXLexer.COMMA:
                        if (parenDepth == 0)
                        {
                            argumentIndex++;
                        }
                        break;
                }
            }

            return null;
        }

        private static string GetFunctionNameBefore(List<IToken> tokens, int parenIndex)
        {
            if (parenIndex <= 0) return null;

            // Collect function name tokens going backward (handling dotted names like BETA.DIST)
            var nameParts = new List<string>();
            int i = parenIndex - 1;

            while (i >= 0)
            {
                var token = tokens[i];
                if (token.Channel != 0)
                {
                    i--;
                    continue;
                }

                if (IsFunctionNameToken(token.Type))
                {
                    nameParts.Insert(0, token.Text);

                    // Check for dot-separated components
                    if (i > 1 && tokens[i - 1].Type == DAXLexer.DOT)
                    {
                        nameParts.Insert(0, ".");
                        i -= 2;
                        continue;
                    }
                    break;
                }
                break;
            }

            return nameParts.Count > 0 ? string.Join("", nameParts) : null;
        }

        private static bool IsFunctionNameToken(int tokenType)
        {
            // Any built-in function token, identifier, or keyword that could be a function name
            return tokenType == DAXLexer.IDENTIFIER
                || tokenType == DAXLexer.TABLE_KW
                || tokenType == DAXLexer.COLUMN_KW
                || (tokenType >= DAXLexer.ABS && tokenType <= DAXLexer.YIELDMAT)
                || tokenType == DAXLexer.INFO_FUNCTIONS
                || tokenType == DAXLexer.NOT;
        }

        private static SignatureHelpResult BuildResult(string functionName, int argumentIndex, IModelMetadataProvider metadata)
        {
            FunctionSignature signature = null;

            if (metadata != null)
            {
                // Look up in built-in functions
                var builtIns = metadata.GetBuiltInFunctions();
                signature = builtIns?.FirstOrDefault(f =>
                    string.Equals(f.Name, functionName, System.StringComparison.OrdinalIgnoreCase));

                // Look up in UDFs
                if (signature == null)
                {
                    var udfs = metadata.GetUserDefinedFunctions();
                    var udf = udfs?.FirstOrDefault(f =>
                        string.Equals(f.Name, functionName, System.StringComparison.OrdinalIgnoreCase));
                    if (udf != null)
                    {
                        signature = new FunctionSignature(
                            udf.Name,
                            udf.Description,
                            "Variant",
                            udf.Parameters.Select(p => new FunctionParameter(
                                p.Name,
                                $"{p.TypeCategory} {p.TypeSubtype}",
                                "",
                                false,
                                false)).ToList());
                    }
                }
            }

            return new SignatureHelpResult
            {
                FunctionName = functionName,
                ActiveArgumentIndex = argumentIndex,
                Signature = signature
            };
        }
    }

    /// <summary>
    /// Result of a signature help query.
    /// </summary>
    public class SignatureHelpResult
    {
        /// <summary>The name of the function the cursor is inside.</summary>
        public string FunctionName { get; set; }

        /// <summary>Zero-based index of the active argument (determined by comma counting).</summary>
        public int ActiveArgumentIndex { get; set; }

        /// <summary>The function signature from metadata, or null if not found.</summary>
        public FunctionSignature Signature { get; set; }
    }
}
