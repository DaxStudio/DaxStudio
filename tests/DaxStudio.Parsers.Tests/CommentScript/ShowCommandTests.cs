using Antlr4.Runtime.Tree;
using DaxStudio.Parsers.Dax;
using DaxStudio.Parsers.CommentScript;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace DaxStudio.Parsers.Tests.CommentScript
{
    [TestClass]
    public class ShowCommandTests
    {
        private static ShowCommand ParseSingleShowCommand(string input)
        {
            List<Error> errors = new List<Error>();
            var tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.IsNull(tree.exception);
            Assert.IsEmpty(errors, "Should have no errors");

            var arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            Assert.HasCount(1, batch[0].Commands);
            var cmd = batch[0].Commands[0] as ShowCommand;
            Assert.IsNotNull(cmd);
            return cmd;
        }

        [TestMethod]
        public void ShowDependencies()
        {
            var cmd = ParseSingleShowCommand("--> SHOW DEPENDENCIES\nEVALUATE { 1 }\n");
            Assert.AreEqual(ShowType.Dependencies, cmd.ShowType);
        }

        [TestMethod]
        public void ShowLastUpdated()
        {
            var cmd = ParseSingleShowCommand("--> SHOW LAST_UPDATED\nEVALUATE { 1 }\n");
            Assert.AreEqual(ShowType.LastUpdated, cmd.ShowType);
        }

        [TestMethod]
        public void ShowMaxUpdated()
        {
            var cmd = ParseSingleShowCommand("--> SHOW MAX_UPDATED\nEVALUATE { 1 }\n");
            Assert.AreEqual(ShowType.MaxUpdated, cmd.ShowType);
        }

        [TestMethod]
        public void ShowDiagram()
        {
            var cmd = ParseSingleShowCommand("--> SHOW DIAGRAM\nEVALUATE { 1 }\n");
            Assert.AreEqual(ShowType.Diagram, cmd.ShowType);
        }

        [TestMethod]
        public void ShowDiagramWithoutQuery()
        {
            var cmd = ParseSingleShowCommand("--> SHOW DIAGRAM\n");
            Assert.AreEqual(ShowType.Diagram, cmd.ShowType);
        }

        [TestMethod]
        public void ShowMetrics()
        {
            var cmd = ParseSingleShowCommand("--> SHOW METRICS\nEVALUATE { 1 }\n");
            Assert.AreEqual(ShowType.Metrics, cmd.ShowType);
        }

        [TestMethod]
        public void ShowMetricsWithoutQuery()
        {
            var cmd = ParseSingleShowCommand("--> SHOW METRICS\n");
            Assert.AreEqual(ShowType.Metrics, cmd.ShowType);
        }

        [TestMethod]
        public void ShowDelta()
        {
            var cmd = ParseSingleShowCommand("--> SHOW DELTA\nEVALUATE { 1 }\n");
            Assert.AreEqual(ShowType.Delta, cmd.ShowType);
        }

        [TestMethod]
        public void ShowDeltaWithoutQuery()
        {
            var cmd = ParseSingleShowCommand("--> SHOW DELTA\n");
            Assert.AreEqual(ShowType.Delta, cmd.ShowType);
        }
    }
}
