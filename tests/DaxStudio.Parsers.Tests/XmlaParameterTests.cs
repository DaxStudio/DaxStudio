using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using DaxStudio.Parsers.Dax;
using DaxStudio.Parsers.Dax;
using DaxStudio.Parsers.CommentScript;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.Tests
{
    [TestClass]
    public class XmlaParameterTests
    {


        [TestMethod]
        public void ParameterNameTest()
        {
            var input = @"<Name>param1</Name>";
            List<Error> errors = new List<Error>();
            ICharStream chars = new DAXCharStream(input);  // CharStreams.fromString(input);
            var errorListener = new PreProcessorErrorListener(ref errors);
            PreProcessorLexer lexer = new PreProcessorLexer(chars);
            lexer.Mode(PreProcessorLexer.XMLA_PARAMETER_MODE);
            lexer.RemoveErrorListeners();
            lexer.AddErrorListener(errorListener);
            ITokenStream stream = new BufferedTokenStream(lexer);
            var tokens = lexer.GetAllTokens();
            lexer.Reset();
            lexer.Mode(PreProcessorLexer.XMLA_PARAMETER_MODE);
            var parser = new PreProcessorParser(stream);
            var tree = parser.xmla_name();

            Assert.IsNull(tree.exception);

        }

        [TestMethod]
        public void ParameterValueTest()
        {
            var input = @"<Value xsi:type=""xsd:string"">red</Value>";
            List<Error> errors = new List<Error>();
            ICharStream chars = new DAXCharStream(input);  // CharStreams.fromString(input);
            var errorListener = new PreProcessorErrorListener(ref errors);
            PreProcessorLexer lexer = new PreProcessorLexer(chars);
            lexer.Mode(PreProcessorLexer.XMLA_PARAMETER_MODE);
            lexer.RemoveErrorListeners();
            lexer.AddErrorListener(errorListener);
            ITokenStream stream = new BufferedTokenStream(lexer);
            var tokens = lexer.GetAllTokens();
            lexer.Reset();
            lexer.Mode(PreProcessorLexer.XMLA_PARAMETER_MODE);
            var parser = new PreProcessorParser(stream);
            var tree = parser.xmla_value();

            Assert.IsNull(tree.exception);

        }

        [TestMethod]
        public void ParameterTest()
        {
            var input = @"<Parameter>
    <Name>param1</Name>
    <Value xsi:type=""xsd:string"">red</Value>
  </Parameter>";
            List<Error> errors = new List<Error>();
            ICharStream chars = new DAXCharStream(input);  // CharStreams.fromString(input);
            var errorListener = new PreProcessorErrorListener(ref errors);
            PreProcessorLexer lexer = new PreProcessorLexer(chars);
            lexer.Mode(PreProcessorLexer.XMLA_PARAMETER_MODE);
            lexer.RemoveErrorListeners();
            lexer.AddErrorListener(errorListener);
            ITokenStream stream = new BufferedTokenStream(lexer);
            var tokens = lexer.GetAllTokens();
            lexer.Reset();
            lexer.Mode(PreProcessorLexer.XMLA_PARAMETER_MODE);
            var parser = new PreProcessorParser(stream);
            var tree = parser.xmla_parameter();

            Assert.IsNull(tree.exception);

        }

        [TestMethod]
        public void ParametersOpenTest()
        {
            var input = @"<Parameters xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns=""urn:schemas-microsoft-com:xml-analysis"">
  <Parameter>
    <Name>param1</Name>
    <Value xsi:type=""xsd:string"">red</Value>
  </Parameter>
  <Parameter>
    <Name>param2</Name>
    <Value xsi:type=""xsd:string"">blue</Value>
  </Parameter>
</Parameters>
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
            var parser = new PreProcessorParser(stream);
            var tree = parser.xmla_parameters();
            Assert.IsNull(tree.exception);
        }


        [TestMethod]
        public void stripXsd()
        {
            var input = @"xsi:type=""xsd:string""";

            var output = input.Substring(14, input.Length - 15);

            Assert.AreEqual("string", output);
        }

        [TestMethod]
        public void StringParameterTest()
        {
            // 
            var input = @"--> USE ""Adventure Works""
evaluate { @param1, @param2, blah <> blah2 }
<Parameters xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns=""urn:schemas-microsoft-com:xml-analysis"">  <Parameter>
    <Name>param1</Name>
    <Value xsi:type=""xsd:string"">red</Value>
  </Parameter>
  <Parameter>
    <Name>param2</Name>
    <Value xsi:type=""xsd:string"">blue</Value>
  </Parameter>
</Parameters>
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

            Assert.AreEqual(3, tree.ChildCount,"tree.ChildCount");
            Assert.IsNull(tree.exception);

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batches = new List<ScriptBatch>();
            var listener = new PreProcessorListener( arrayParameters,batches);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            Assert.HasCount(1, batches);
            Assert.HasCount(3, batches[0].Commands);

            var param1 = batches[0].Commands[0] as UseCommand;
            Assert.AreEqual("Adventure Works", param1.DatabaseName);

            var param2 = batches[0].Commands[1] as ParameterCommand;
            Assert.AreEqual("param1", param2.ParameterName);
            Assert.AreEqual("red", param2.Value);

            var param3 = batches[0].Commands[2] as ParameterCommand;
            Assert.AreEqual("param2", param3.ParameterName);
            Assert.AreEqual("blue", param3.Value);
        }

        [TestMethod]
        public void TestAllParameterTypes()
        {
            var input = @"
evaluate { @param1, @param2, @param3, @param4, @param5 }

<Parameters xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns=""urn:schemas-microsoft-com:xml-analysis"">  <Parameter>
    <Name>param1</Name>
    <Value xsi:type=""xsd:string"">abc</Value>
  </Parameter>
  <Parameter>
    <Name>param2</Name>
    <Value xsi:type=""xsd:int"">34</Value>
  </Parameter>
  <Parameter>
    <Name>param3</Name>
    <Value xsi:type=""xsd:dateTime"">19/07/2020 12:00:00 AM</Value>
  </Parameter>
  <Parameter>
    <Name>param4</Name>
    <Value xsi:type=""xsd:boolean"">False</Value>
  </Parameter>
  <Parameter>
    <Name>param5</Name>
    <Value xsi:type=""xsd:double"">1.2</Value>
  </Parameter>
</Parameters>
";

            List<Error> errors = new List<Error>();

            ICharStream chars = new DAXCharStream(input);  // CharStreams.fromString(input);
            var errorListener = new PreProcessorErrorListener(ref errors);
            PreProcessorLexer lexer = new PreProcessorLexer(chars);
            lexer.RemoveErrorListeners();
            lexer.AddErrorListener(errorListener);
            ITokenStream stream = new BufferedTokenStream(lexer);
            //var tokens = lexer.GetAllTokens();
            //lexer.Reset();
            PreProcessorParser parser = new PreProcessorParser(stream);
            parser.RemoveErrorListeners();
            parser.AddErrorListener(errorListener);
            var tree = parser.document();

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batches = new List<ScriptBatch>();
            var listener = new PreProcessorListener( arrayParameters, batches);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            //PreProcessorParser.DocumentContext tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.IsEmpty(errors, "errors collection should be empty");

            Assert.HasCount(5, batches[0].Commands);

            var paramList = batches[0].Commands.OfType<ParameterCommand>().ToList();

            Assert.AreEqual("param1", paramList[0].ParameterName);
            Assert.AreEqual("param2", paramList[1].ParameterName);
            Assert.AreEqual("param3", paramList[2].ParameterName);
            Assert.AreEqual("param4", paramList[3].ParameterName);
            Assert.AreEqual("param5", paramList[4].ParameterName);

            Assert.AreEqual("abc", paramList[0].Value);
            Assert.AreEqual("34", paramList[1].Value);
            Assert.AreEqual("19/07/2020 12:00:00 AM", paramList[2].Value);
            Assert.AreEqual("False", paramList[3].Value);
            Assert.AreEqual("1.2", paramList[4].Value);

            Assert.AreEqual("string", paramList[0].TypeName);
            Assert.AreEqual("int", paramList[1].TypeName);
            Assert.AreEqual("dateTime", paramList[2].TypeName);
            Assert.AreEqual("boolean", paramList[3].TypeName);
            Assert.AreEqual("double", paramList[4].TypeName);
        }

        
    }
}
