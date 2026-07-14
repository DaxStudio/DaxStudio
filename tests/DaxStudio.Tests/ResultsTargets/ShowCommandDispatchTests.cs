using System.Linq;
using Caliburn.Micro;
using DaxStudio.Core.Interfaces;
using DaxStudio.Core.Model;
using DaxStudio.Interfaces;
using DaxStudio.Parsers.CommentScript;
using DaxStudio.UI.ResultsTargets;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace DaxStudio.Tests.ResultsTargets
{
    [TestClass]
    public class ShowCommandDispatchTests
    {
        private IEventAggregator _eventAggregator;

        [TestInitialize]
        public void Init()
        {
            _eventAggregator = Substitute.For<IEventAggregator>();
        }

        private static IGlobalOptions NewParserOptions()
        {
            var options = Substitute.For<IGlobalOptions>();
            options.UseNewPreprocessor.Returns(true);
            return options;
        }

        private IQueryTextProvider BuildProvider(string script)
        {
            var queryInfo = new QueryInfo(script, _eventAggregator, NewParserOptions());
            var provider = Substitute.For<IQueryTextProvider>();
            provider.QueryInfo.Returns(queryInfo);
            provider.QueryText.Returns(queryInfo.ScriptBatches.FirstOrDefault()?.QueryText ?? string.Empty);
            return provider;
        }

        [TestMethod]
        public void NoShowCommandReturnsFalse()
        {
            var provider = BuildProvider("EVALUATE\nROW(\"x\", 1)");
            var runner = Substitute.For<IQueryRunner>();

            var handled = ResultsTargetGrid.TryHandleShowCommand(runner, provider);

            Assert.IsFalse(handled, "A script with no SHOW command should not be handled as a SHOW request");
            runner.DidNotReceive().DisplayShowTree(Arg.Any<System.Collections.Generic.IList<ShowTreeNode>>(), Arg.Any<ShowType>());
        }

        [TestMethod]
        public void ShowDependenciesWithoutQueryOutputsError()
        {
            // A SHOW DEPENDENCIES command with no accompanying DAX query cannot be analysed.
            var provider = BuildProvider("--> SHOW DEPENDENCIES");
            var runner = Substitute.For<IQueryRunner>();

            var handled = ResultsTargetGrid.TryHandleShowCommand(runner, provider);

            Assert.IsTrue(handled, "A SHOW command should be treated as handled even when it errors");
            runner.Received().OutputError(Arg.Is<string>(s => s.Contains("SHOW DEPENDENCIES")));
            runner.DidNotReceive().DisplayShowTree(Arg.Any<System.Collections.Generic.IList<ShowTreeNode>>(), Arg.Any<ShowType>());
        }

        [TestMethod]
        public void ShowCommandIsParsedIntoScriptBatch()
        {
            var provider = BuildProvider("--> SHOW MAX_UPDATED");

            var showCommands = provider.QueryInfo.ScriptBatches
                .SelectMany(b => b.Commands)
                .OfType<ShowCommand>()
                .ToList();

            Assert.AreEqual(1, showCommands.Count);
            Assert.AreEqual(ShowType.MaxUpdated, showCommands[0].ShowType);
        }
    }
}
