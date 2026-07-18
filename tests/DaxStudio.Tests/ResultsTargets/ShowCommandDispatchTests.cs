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
            var batch = provider.QueryInfo.ScriptBatches.First();

            var handled = ResultsTargetGrid.TryHandleShowBatch(runner, provider, batch, out var descriptors);

            Assert.IsFalse(handled, "A batch with no SHOW command should not be handled as a SHOW request");
            Assert.AreEqual(0, descriptors.Count, "No SHOW tab descriptor should be produced for a non-SHOW batch");
        }

        [TestMethod]
        public void ShowDependenciesWithoutQueryOutputsError()
        {
            // A SHOW DEPENDENCIES command with no accompanying DAX query cannot be analysed.
            var provider = BuildProvider("--> SHOW DEPENDENCIES");
            var runner = Substitute.For<IQueryRunner>();
            var batch = provider.QueryInfo.ScriptBatches.First();

            var handled = ResultsTargetGrid.TryHandleShowBatch(runner, provider, batch, out var descriptors);

            Assert.IsTrue(handled, "A SHOW command should be treated as handled even when it errors");
            Assert.AreEqual(0, descriptors.Count, "An errored SHOW command should not produce a tab descriptor");
            runner.Received().OutputError(Arg.Is<string>(s => s.Contains("SHOW DEPENDENCIES")));
        }

        [TestMethod]
        public void MultipleShowCommandsInOneBatchAreAllHandled()
        {
            // Two SHOW commands in a single batch (no --> GO) should each be processed, not just the
            // first. Using SHOW DEPENDENCIES with no query lets us verify every command is dispatched
            // (each reports its own error) without needing a live connection.
            var provider = BuildProvider("--> SHOW DEPENDENCIES\n--> SHOW DEPENDENCIES");
            var runner = Substitute.For<IQueryRunner>();
            var batch = provider.QueryInfo.ScriptBatches.First();

            var showCount = batch.Commands.OfType<ShowCommand>().Count();
            Assert.AreEqual(2, showCount, "Both SHOW commands should parse into the same batch");

            var handled = ResultsTargetGrid.TryHandleShowBatch(runner, provider, batch, out var descriptors);

            Assert.IsTrue(handled);
            Assert.AreEqual(0, descriptors.Count, "Both commands error (no query) so no tab is produced");
            // The key assertion: BOTH commands were dispatched, not just the first.
            runner.Received(2).OutputError(Arg.Is<string>(s => s.Contains("SHOW DEPENDENCIES")));
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
