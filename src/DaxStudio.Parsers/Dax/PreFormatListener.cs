using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using DaxStudio.Parsers.CommentScript;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static DaxStudio.Parsers.Grammars.Generated.PreProcessorParser;

using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.Dax
{

    public class PreFormatListener : PreProcessorParserBaseListener
    {
        private StringBuilder output;
        private bool _commentRsCustomDaxFilter = false;
        private bool exitFormatComment;
        public PreFormatListener(StringBuilder sb, bool commentRsCustomDaxFilter)
        {
            output = sb;
            _commentRsCustomDaxFilter = commentRsCustomDaxFilter;
        }

        public override void ExitDaxParameter([NotNull] DaxParameterContext context)
        {
            var currentToken = context.GetText();
            var startType = context.Start.Type;
            ProcessToken(currentToken, startType);
        }

        public override void EnterOther([NotNull] PreProcessorParser.OtherContext context)
        {
            var currentToken = context.GetText();
            var startType = context.Start.Type;
            ProcessToken(currentToken, startType);

            base.EnterOther(context);
        }

        private void ProcessToken(string currentToken, int startType)
        {
            

            if (startType != PreProcessorLexer.COLUMN_OR_MEASURE
                && output.Length > 0) output.Append(" ");

            if (exitFormatComment)
            {
                if (currentToken == ",")
                {
                    output.Append(currentToken);
                    output.Append("~*/");
                    output.Append('\n');
                }
                else
                {
                    output.Append("~*/");
                    output.Append('\n');
                    output.Append(currentToken);
                }
                exitFormatComment = false;
            }
            else
            {
                output.Append(currentToken);
            }
        }

        #region RSCustomDaxFilter Methods
        public override void EnterRSCustomDaxFilter([NotNull] RSCustomDaxFilterContext context)
        {
            if (_commentRsCustomDaxFilter) output.Append("/*~");

            var ctxt = (RscustomdaxfilterContext)context.children[0];

            foreach( var c in ctxt.children)
            {
                var token = c.Payload as CommonToken;
                if (token != null && token.Type == PreProcessorLexer.RS_QUOTEDNAME) output.Append('[');
                //if (c.Payload)
                output.Append(c.GetText());
                if (token != null && token.Type == PreProcessorLexer.RS_QUOTEDNAME) output.Append(']');
            }
            base.EnterRSCustomDaxFilter(context);
        }

        public override void ExitRSCustomDaxFilter([NotNull] PreProcessorParser.RSCustomDaxFilterContext context)
        {
            if(_commentRsCustomDaxFilter) exitFormatComment = true;
            base.ExitRSCustomDaxFilter(context);
        }



#endregion

       


    }

}
