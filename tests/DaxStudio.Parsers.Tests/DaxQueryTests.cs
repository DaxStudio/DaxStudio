using Antlr4.Runtime.Tree;
using Antlr4.Runtime;
using DaxStudio.Parsers.Dax;
using DaxStudio.Parsers.CommentScript;
using DaxStudio.Parsers.Dax;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.Tests
{
    [TestClass]
    public class DaxQueryTests
    {
        [TestMethod]
        public void QueryWithParameters()
        {
            // 
            var input = @"/* Query Builder */
EVALUATE
SUMMARIZECOLLNNS(
Transactions [Index] ,
Transactions [document_date] ,
Transactions [document—no] ,
Transactions [Descri pti on] ,
KEEPFILTERS( TREATAS( {@cust}, Customers [customer_lookup] ) )
KEEPFILTERS( FILTER( ALL( Dates[Date] ) , Dates[Date] >= VALUE(@Start_Date) && Dates[Date] <= (@End_Date) )),
""Amount"" , [Amount] 
  ";

            List<Error> errors = new List<Error>();

            ICharStream chars = new DAXCharStream(input);  // CharStreams.fromString(input);
            var errorListener = new PreProcessorErrorListener(ref errors);
            PreProcessorLexer lexer = new PreProcessorLexer(chars);
            lexer.RemoveErrorListeners();
            lexer.AddErrorListener(errorListener);
            ITokenStream stream = new BufferedTokenStream(lexer);
            var tokens = lexer.GetAllTokens();
            lexer.Reset();
            PreProcessorParser parser = new PreProcessorParser(stream);
            parser.RemoveErrorListeners();
            parser.AddErrorListener(errorListener);
            var tree = parser.document();

            //PreProcessorParser.DocumentContext tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.IsEmpty(errors, "errors collection should be empty");

            Assert.AreEqual(2, tree.ChildCount);
            Assert.IsNull(tree.exception);

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            Assert.IsEmpty(batch[0].Commands);

            Assert.HasCount(3, arrayParameters, "Incorrect Parameter Count");
            Assert.IsTrue(arrayParameters.Keys.Contains("@cust"));
            Assert.IsTrue(arrayParameters.Keys.Contains("@Start_Date"));
            Assert.IsTrue(arrayParameters.Keys.Contains("@End_Date"));
        }
    }
}
