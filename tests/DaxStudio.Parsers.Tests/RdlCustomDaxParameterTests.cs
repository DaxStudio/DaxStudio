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
    public  class RdlCustomDaxParameterTests
    {
        [TestMethod]
        public void RdlCustomParameterStringArrayTest()
        {
            var input = @"// this is a leading comment
// this is a function in a comment RDLCustomDaxParameter(@ProductColorName,String)
DEFINE
	VAR __DS0FilterTable = 
		TREATAS({RdlCustomDaxParameter(@Param_Product_Color,String)}, 'Product'[Color])

	VAR __DS0Core = 
		CALCULATETABLE(
			SUMMARIZE(
				'Product',
				'Product'[Color],
				'Product'[Class [0/1]]],
				'Product'[Model (0/1) {}!@#$%^&*]
			),
			KEEPFILTERS(__DS0FilterTable)
		)

EVALUATE
	__DS0Core

ORDER BY
	'Product'[Color], 'Product'[Class [0/1]]], 'Product'[Model (0/1) {}!@#$%^&*]
";

            ICharStream chars = new DAXCharStream(input);  // CharStreams.fromString(input);
            PreProcessorLexer lexer = new PreProcessorLexer(chars);
            ITokenStream stream = new BufferedTokenStream(lexer);
            PreProcessorParser parser = new PreProcessorParser(stream);
            var tokens = lexer.GetAllTokens();
            Assert.HasCount(1, lexer.Parameters);
            Assert.AreEqual(1, lexer.Parameters.Values.Count(p => p));
            lexer.Reset();

            //var tokenList = lexer.GetAllTokens();
            var tree = parser.query();

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();

            arrayParameters.Add("@Param_Product_Color", new List<string> { "Red", "Blue" });
            
            var batches = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batches);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            //Assert.AreEqual(10, tree.ChildCount);

            // var expected = @"EVALUATE SUMMARIZECOLUMNS ( 'Product' [Color Name] , FILTER(ALL('Product'[Color Name]),'Product'[Color Name] = ""Red"" || 'Product'[Color Name] = ""Blue""), FILTER(ALL('Employee'[Department Name]),'Employee'[Department Name] = ""Sales"" || 'Employee'[Department Name] = ""Marketing""), FILTER ( ALL ( 'Product' [Unit Price] ) , 'Product' [Unit Price] > @minUnitPrice ) , ""RSCustomDaxFilter(@ProductColorName,EqualToCondition,[Product].[Color Name],String)"" , [Sales Amount] ) ";
            var expected = @"// this is a leading comment
// this is a function in a comment RDLCustomDaxParameter(@ProductColorName,String)
DEFINE
	VAR __DS0FilterTable = 
		TREATAS({""Red"",""Blue""}, 'Product'[Color])

	VAR __DS0Core = 
		CALCULATETABLE(
			SUMMARIZE(
				'Product',
				'Product'[Color],
				'Product'[Class [0/1]]],
				'Product'[Model (0/1) {}!@#$%^&*]
			),
			KEEPFILTERS(__DS0FilterTable)
		)

EVALUATE
	__DS0Core

ORDER BY
	'Product'[Color], 'Product'[Class [0/1]]], 'Product'[Model (0/1) {}!@#$%^&*]
";
            var result = batches[0].Output.ToString();

            System.Diagnostics.Debug.WriteLine(result);
            Assert.AreEqual(expected, result);

        }

        public void RdlCustomParameterSingleStringTest()
        {
            var input = @"// this is a leading comment
// this is a function in a comment RDLCustomDaxParameter(@ProductColorName,String)
DEFINE
	VAR __DS0FilterTable = 
		TREATAS({RdlCustomDaxParameter(@Param_Product_Color,String)}, 'Product'[Color])

	VAR __DS0Core = 
		CALCULATETABLE(
			SUMMARIZE(
				'Product',
				'Product'[Color],
				'Product'[Class [0/1]]],
				'Product'[Model (0/1) {}!@#$%^&*]
			),
			KEEPFILTERS(__DS0FilterTable)
		)

EVALUATE
	__DS0Core

ORDER BY
	'Product'[Color], 'Product'[Class [0/1]]], 'Product'[Model (0/1) {}!@#$%^&*]
";

            ICharStream chars = new DAXCharStream(input);  // CharStreams.fromString(input);
            PreProcessorLexer lexer = new PreProcessorLexer(chars);
            ITokenStream stream = new BufferedTokenStream(lexer);
            PreProcessorParser parser = new PreProcessorParser(stream);
            var tokens = lexer.GetAllTokens();
            Assert.HasCount(1, lexer.Parameters);
            Assert.AreEqual(1, lexer.Parameters.Values.Count(p => p));
            lexer.Reset();

            //var tokenList = lexer.GetAllTokens();
            var tree = parser.query();

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();

            arrayParameters.Add("@Param_Product_Color", new List<string> { "Silver" });

            var batches = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batches);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            //Assert.AreEqual(10, tree.ChildCount);

            // var expected = @"EVALUATE SUMMARIZECOLUMNS ( 'Product' [Color Name] , FILTER(ALL('Product'[Color Name]),'Product'[Color Name] = ""Red"" || 'Product'[Color Name] = ""Blue""), FILTER(ALL('Employee'[Department Name]),'Employee'[Department Name] = ""Sales"" || 'Employee'[Department Name] = ""Marketing""), FILTER ( ALL ( 'Product' [Unit Price] ) , 'Product' [Unit Price] > @minUnitPrice ) , ""RSCustomDaxFilter(@ProductColorName,EqualToCondition,[Product].[Color Name],String)"" , [Sales Amount] ) ";
            var expected = @"// this is a leading comment
// this is a function in a comment RDLCustomDaxParameter(@ProductColorName,String)
DEFINE
	VAR __DS0FilterTable = 
		TREATAS({""Silver""}, 'Product'[Color])

	VAR __DS0Core = 
		CALCULATETABLE(
			SUMMARIZE(
				'Product',
				'Product'[Color],
				'Product'[Class [0/1]]],
				'Product'[Model (0/1) {}!@#$%^&*]
			),
			KEEPFILTERS(__DS0FilterTable)
		)

EVALUATE
	__DS0Core

ORDER BY
	'Product'[Color], 'Product'[Class [0/1]]], 'Product'[Model (0/1) {}!@#$%^&*]
";
            var result = batches[0].Output.ToString();

            System.Diagnostics.Debug.WriteLine(result);
            Assert.AreEqual(expected, result);

        }

        public void RdlCustomParameterIntegerArrayTest()
        {
            var input = @"// this is a leading comment
// this is a function in a comment RDLCustomDaxParameter(@ProductColorName,String)
--> PARAMETER @Param_Product_Color = {1,2}
DEFINE
	VAR __DS0FilterTable = 
		TREATAS({RdlCustomDaxParameter(@Param_Product_Color,String)}, 'Product'[Color])

	VAR __DS0Core = 
		CALCULATETABLE(
			SUMMARIZE(
				'Product',
				'Product'[Color],
				'Product'[Class [0/1]]],
				'Product'[Model (0/1) {}!@#$%^&*]
			),
			KEEPFILTERS(__DS0FilterTable)
		)

EVALUATE
	__DS0Core

ORDER BY
	'Product'[Color], 'Product'[Class [0/1]]], 'Product'[Model (0/1) {}!@#$%^&*]
";

            ICharStream chars = new DAXCharStream(input);  // CharStreams.fromString(input);
            PreProcessorLexer lexer = new PreProcessorLexer(chars);
            ITokenStream stream = new BufferedTokenStream(lexer);
            PreProcessorParser parser = new PreProcessorParser(stream);
            var tokens = lexer.GetAllTokens();
            Assert.HasCount(1, lexer.Parameters);
            Assert.AreEqual(1, lexer.Parameters.Values.Count(p => p));
            lexer.Reset();

            //var tokenList = lexer.GetAllTokens();
            var tree = parser.query();

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();

            //arrayParameters.Add("@Param_Product_Color", new List<string> { "1", "2" });

            var batches = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batches);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            //Assert.AreEqual(10, tree.ChildCount);

            // var expected = @"EVALUATE SUMMARIZECOLUMNS ( 'Product' [Color Name] , FILTER(ALL('Product'[Color Name]),'Product'[Color Name] = ""Red"" || 'Product'[Color Name] = ""Blue""), FILTER(ALL('Employee'[Department Name]),'Employee'[Department Name] = ""Sales"" || 'Employee'[Department Name] = ""Marketing""), FILTER ( ALL ( 'Product' [Unit Price] ) , 'Product' [Unit Price] > @minUnitPrice ) , ""RSCustomDaxFilter(@ProductColorName,EqualToCondition,[Product].[Color Name],String)"" , [Sales Amount] ) ";
            var expected = @"// this is a leading comment
// this is a function in a comment RDLCustomDaxParameter(@ProductColorName,Int)
--> PARAMETER @Param_Product_Color = {1,2}
DEFINE
	VAR __DS0FilterTable = 
		TREATAS({1,2}, 'Product'[Color])

	VAR __DS0Core = 
		CALCULATETABLE(
			SUMMARIZE(
				'Product',
				'Product'[Color],
				'Product'[Class [0/1]]],
				'Product'[Model (0/1) {}!@#$%^&*]
			),
			KEEPFILTERS(__DS0FilterTable)
		)

EVALUATE
	__DS0Core

ORDER BY
	'Product'[Color], 'Product'[Class [0/1]]], 'Product'[Model (0/1) {}!@#$%^&*]
";
            var result = batches[0].Output.ToString();

            System.Diagnostics.Debug.WriteLine(result);
            Assert.AreEqual(expected, result);

        }

		[TestMethod]
        public void RdlCustomParameterWithScriptParameter()
        {
            var input = @"// leading comment
--> PARAMETER @Param_Product_Color = {""Red"",""Blue""}
DEFINE
	VAR __DS0FilterTable = 
		TREATAS({RdlCustomDaxParameter(@Param_Product_Color,String)}, 'Product'[Color])

	VAR __DS0Core = 
		CALCULATETABLE(
			SUMMARIZE(
				'Product',
				'Product'[Color],
				'Product'[Class [0/1]]],
				'Product'[Model (0/1) {}!@#$%^&*]
			),
			KEEPFILTERS(__DS0FilterTable)
		)
/* middle comment */
EVALUATE
	__DS0Core

ORDER BY
	'Product'[Color], 'Product'[Class [0/1]]], 'Product'[Model (0/1) {}!@#$%^&*]
--trailing comment";

            var errors = new List<Error>();
            var errorListener = new PreProcessorErrorListener(ref errors);
            
            

            ICharStream chars = new DAXCharStream(input);  // CharStreams.fromString(input);
            PreProcessorLexer lexer = new PreProcessorLexer(chars);
            lexer.RemoveErrorListeners();
            lexer.AddErrorListener(errorListener);
            ITokenStream stream = new BufferedTokenStream(lexer);
            PreProcessorParser parser = new PreProcessorParser(stream);
            var tokens = lexer.GetAllTokens();
            lexer.Reset();
            Assert.HasCount(1, lexer.Parameters);
            //Assert.AreEqual(1, lexer.Parameters.Values.Count(p => p));


            //var tokenList = lexer.GetAllTokens();
            var tree = parser.document();

            Assert.IsEmpty(errors);

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();

            //arrayParameters.Add("@Param_Product_Color", new List<string> { "Red", "Blue" });

            var batches = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batches);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            //Assert.AreEqual(10, tree.ChildCount);

            // var expected = @"EVALUATE SUMMARIZECOLUMNS ( 'Product' [Color Name] , FILTER(ALL('Product'[Color Name]),'Product'[Color Name] = ""Red"" || 'Product'[Color Name] = ""Blue""), FILTER(ALL('Employee'[Department Name]),'Employee'[Department Name] = ""Sales"" || 'Employee'[Department Name] = ""Marketing""), FILTER ( ALL ( 'Product' [Unit Price] ) , 'Product' [Unit Price] > @minUnitPrice ) , ""RSCustomDaxFilter(@ProductColorName,EqualToCondition,[Product].[Color Name],String)"" , [Sales Amount] ) ";
            var expected = @"// leading comment
--> PARAMETER @Param_Product_Color = {""Red"",""Blue""}
DEFINE
	VAR __DS0FilterTable = 
		TREATAS({""Red"",""Blue""}, 'Product'[Color])

	VAR __DS0Core = 
		CALCULATETABLE(
			SUMMARIZE(
				'Product',
				'Product'[Color],
				'Product'[Class [0/1]]],
				'Product'[Model (0/1) {}!@#$%^&*]
			),
			KEEPFILTERS(__DS0FilterTable)
		)
/* middle comment */
EVALUATE
	__DS0Core

ORDER BY
	'Product'[Color], 'Product'[Class [0/1]]], 'Product'[Model (0/1) {}!@#$%^&*]
--trailing comment";
            var result = batches[0].Output.ToString();

            System.Diagnostics.Debug.WriteLine(result);
            Assert.AreEqual(expected, result);

        }

        [TestMethod]
        public void RdlCustomParameterWithScriptParameter2()
        {
            var input = @"// leading comment
--> PARAMETER @Param_Product_Color = {""Red"",""Blue""}
DEFINE
	VAR __DS0FilterTable = 
		TREATAS({RdlCustomDaxParameter(@Param_Product_Color,String)}, 'Product'[Color])

	VAR __DS0Core = 
		CALCULATETABLE(
			SUMMARIZE(
				'Product',
				'Product'[Color],
				'Product'[Class [0/1]]],
				'Product'[Model (0/1) {}!@#$%^&*]
			),
			KEEPFILTERS(__DS0FilterTable)
		)

EVALUATE
	__DS0Core

ORDER BY
	'Product'[Color], 'Product'[Class [0/1]]], 'Product'[Model (0/1) {}!@#$%^&*]
--trailing comment";



            List<Error> errors = new List<Error>();
            PreProcessorParser.DocumentContext tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.AreEqual(3, tree.ChildCount, "Tree Child Count");
            Assert.IsNull(tree.exception);

            Assert.IsEmpty(errors, "The errors list should be empty");

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var output = new StringBuilder();
            var daxParameters = new List<string>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            Assert.HasCount(1, batch[0].Commands);

            var paramCmd = batch[0].Commands[0] as ParameterCommand;
            Assert.IsNotNull(paramCmd);
            Assert.AreEqual("@Param_Product_Color", paramCmd.ParameterName);
            Assert.IsTrue(paramCmd.Value is List<string>);
            var list = paramCmd.Value as List<string>;
            Assert.HasCount(2, list);
            Assert.AreEqual("Red", list[0]);
            Assert.AreEqual("Blue", list[1]);

            var expected = @"// leading comment
--> PARAMETER @Param_Product_Color = {""Red"",""Blue""}
DEFINE
	VAR __DS0FilterTable = 
		TREATAS({""Red"",""Blue""}, 'Product'[Color])

	VAR __DS0Core = 
		CALCULATETABLE(
			SUMMARIZE(
				'Product',
				'Product'[Color],
				'Product'[Class [0/1]]],
				'Product'[Model (0/1) {}!@#$%^&*]
			),
			KEEPFILTERS(__DS0FilterTable)
		)

EVALUATE
	__DS0Core

ORDER BY
	'Product'[Color], 'Product'[Class [0/1]]], 'Product'[Model (0/1) {}!@#$%^&*]
--trailing comment";
            Assert.AreEqual(expected, batch[0].Output.ToString());

        }

    }
}
