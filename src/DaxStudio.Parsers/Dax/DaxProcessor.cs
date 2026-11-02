using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using DaxStudio.Parsers.Dax;
using DaxStudio.Parsers.CommentScript;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static DaxStudio.Parsers.Grammars.Generated.PreProcessorParser;

using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.Dax
{

    public enum EditState
    {
        PartialMeasure,
        PartialTable,
        Table,
        Identifier,
        Unknown,
        PartialColumn
    }

    public class DaxState
    {
        public DaxState(string function, int argumentIndex, EditState state, List<string> variables)
        {
            CurrentFunction = function;
            ArgumentIndex = argumentIndex;
            State = state;
            Variables = variables;
        }
        public string CurrentFunction { get; }
        public int ArgumentIndex { get; }
        public EditState State { get; }
        public List<string> Variables { get; }
    }

    public static class DaxProcessor 
    {
        private enum Mode { Unknown, Adding, Removing};
        
        public static string ToggleComments(string input)
        {
            var output = new StringBuilder();
            var mode = Mode.Unknown;
            var commentNextToken = false;

            ICharStream chars = new DAXCharStream(input);  // CharStreams.fromString(input);
            PreProcessorLexer lexer = new PreProcessorLexer(chars);
            ITokenStream stream = new BufferedTokenStream(lexer);
            PreProcessorParser parser = new PreProcessorParser(stream);
            var tokens = lexer.GetAllTokens();

            var addComments =  tokens.Any(t => t.Type != SINGLE_LINE_COMMENT && t.Type != WHITESPACES && t.Type != DELIMITED_COMMENT );

            foreach (var token in tokens)
            {
                // skip leading whitespace
                if (token.Type == PreProcessorLexer.WHITESPACES && mode == Mode.Unknown)
                {
                    output.Append(token.Text);
                    continue;
                }

                if (mode == Mode.Unknown && token.Type == PreProcessorLexer.SINGLE_LINE_COMMENT) mode = Mode.Removing;
                else mode = Mode.Adding;

                if (mode == Mode.Adding && token.Type == WHITESPACES && token.Text.Contains('\n'))
                {
                    commentNextToken = true;
                    output.Append(token.Text);
                    continue;
                }

                if (mode == Mode.Removing && token.Type == SINGLE_LINE_COMMENT)
                {
                    output.Append(token.Text.Substring(2));
                    continue;
                }

                if (commentNextToken)
                {
                    if (token.Type != PreProcessorLexer.DELIMITED_COMMENT)
                    {
                        output.Append("--");
                        commentNextToken = false;
                    }
                    output.Append(token.Text);
                }
                else
                {
                    output.Append(token.Text);
                }

            }

            return output.ToString();
        }


        public static string ToggleComments2(string input)
        {
            var output = new StringBuilder();
            var commentNextToken = false;

            ICharStream chars = new DAXCharStream(input);  // CharStreams.fromString(input);
            PreProcessorLexer lexer = new PreProcessorLexer(chars);
            ITokenStream stream = new BufferedTokenStream(lexer);
            PreProcessorParser parser = new PreProcessorParser(stream);
            var tokens = lexer.GetAllTokens();

            var addComments = tokens.Any(t => t.Type != SINGLE_LINE_COMMENT && t.Type != WHITESPACES && t.Type != DELIMITED_COMMENT);

            foreach (var token in tokens)
            {


                if (addComments && token.Type == WHITESPACES && token.Text.Contains('\n'))
                {
                    commentNextToken = true;
                    output.Append(token.Text);
                    continue;
                }

                if (!addComments && token.Type == SINGLE_LINE_COMMENT)
                {
                    output.Append(token.Text.Substring(2).Trim());
                    continue;
                }

                if (commentNextToken)
                {
                    if (token.Type != PreProcessorLexer.DELIMITED_COMMENT)
                    {
                        output.Append("-- ");
                        commentNextToken = false;
                    }
                    output.Append(token.Text);
                }
                else
                {
                    output.Append(token.Text);
                }

            }

            return output.ToString();
        }

        // calltip - one open_parens or comma grab top item off call stack
        // intellisense - PARTIAL_MEASURE - show measures
        //                table or identifier followed by measure - show columns
        //                PARTIAL_Table - show tables
        //                identifier or key word - show functions or tables
        public static Stack<string> BuildCallStack(string input)
        {
            ICharStream chars = new DAXCharStream(input);  // CharStreams.fromString(input);
            PreProcessorLexer lexer = new PreProcessorLexer(chars);
            var tokens = lexer.GetAllTokens();
            Stack<string> output = new Stack<string>();

            string currentIdentifier = string.Empty;

            foreach (var token in tokens)
            {
                switch (token.Type)
                {
                    case TABLE_OR_VARIABLE: 
                        currentIdentifier = token.Text;
                        break;
                    case OPEN_PARENS:
                        output.Push(currentIdentifier);
                        break;
                    case CLOSE_PARENS:
                        output.Pop();
                        break;
                }
            }
            return output;
        }


        public static DaxState BuildCallStack2(string input)
        {
            ICharStream chars = new DAXCharStream(input);  // CharStreams.fromString(input);
            PreProcessorLexer lexer = new PreProcessorLexer(chars);
            // Ignore channels other than the default one
            ITokenStream stream = new CommonTokenStream(lexer);

            Stack<string> output = new Stack<string>();
            Stack<List<string>> variables = new Stack<List<string>>();
            variables.Push(new List<string>()); // push the variable list for the outer context
            int argumentIndex = 0;

            string currentIdentifier = string.Empty;
            int currentTokenType;

            stream.Consume();
            while (stream.LA(1) != PreProcessorLexer.Eof)
            {
                currentTokenType = stream.LA(1);
                switch (currentTokenType)
                {
                    
                    case TABLE_OR_VARIABLE:
                        currentIdentifier = stream.LT(1).Text;
                        if (stream.LA(-1) == K_VAR) variables.Peek().Add(stream.LT(1).Text);
                        break;
                    case OPEN_PARENS:
                        output.Push(currentIdentifier);
                        variables.Push(new List<string>());
                        break;
                    case CLOSE_PARENS:
                        output.Pop();
                        variables.Pop();
                        argumentIndex = 0; // reset the argument count
                        break;
                    case COMMA:
                        argumentIndex++; // increment the argument count
                        break;
                }
                stream.Consume();
            }

            var finalToken = stream.LT(-1);
            var editState = EditState.Unknown;
            // intellisense - PARTIAL_MEASURE - show measures
            //                table or identifier followed by measure - show columns
            //                PARTIAL_Table - show tables
            //                identifier or key word - show functions or tables
            
            switch (finalToken.Type)
            {
                case PARTIAL_COLUMN_OR_MEASURE:
                    if (stream.LA(-2) == TABLE || stream.LA(-2) == TABLE_OR_VARIABLE) editState = EditState.PartialColumn;
                    else editState = EditState.PartialMeasure;
                    break;
                case PARTIAL_TABLE:
                    editState = EditState.PartialTable;
                    break;
                case TABLE_OR_VARIABLE:
                    editState = EditState.Table;
                    break;            
            }

            List<string> variableList=
                variables.Aggregate(new List<string>(), (current,next) => { current.AddRange(next); return current; });

            var state = new DaxState(output.Peek(), argumentIndex, editState , variableList);

            return state;
        }
    }

}
