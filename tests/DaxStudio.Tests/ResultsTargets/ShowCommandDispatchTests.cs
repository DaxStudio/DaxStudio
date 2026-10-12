using System.Linq;
using Caliburn.Micro;
using DaxStudio.Core.Interfaces;
using DaxStudio.Core.Model;
using DaxStudio.Interfaces;
using DaxStudio.Parsers.CommentScript;
using DaxStudio.UI.ResultsTargets;
using DaxStudio.UI.Events;
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
            options.UseNewDaxParser.Returns(true);
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

        [TestMethod]
        public void ShowDiagramCommandProducesNoTabDescriptor()
        {
            // SHOW DIAGRAM opens the Model Diagram tool window instead of producing a result tab,
            // so TryHandleShowBatch reports it as handled but adds no descriptor.
            var provider = BuildProvider("--> SHOW DIAGRAM\nEVALUATE { 1 }");
            var runner = Substitute.For<IQueryRunner>();
            var batch = provider.QueryInfo.ScriptBatches.First();

            var handled = ResultsTargetGrid.TryHandleShowBatch(runner, provider, batch, out var descriptors);

            Assert.IsTrue(handled, "A SHOW DIAGRAM command should be treated as handled");
            Assert.AreEqual(0, descriptors.Count, "SHOW DIAGRAM opens a tool window and produces no result tab");
        }

        [TestMethod]
        public void ShowDiagramAndDependenciesParseIntoSameBatch()
        {
            // A batch may contain both SHOW DIAGRAM and SHOW DEPENDENCIES; both consume the same query.
            var provider = BuildProvider("--> SHOW DIAGRAM\n--> SHOW DEPENDENCIES\nEVALUATE { 1 }");
            var batch = provider.QueryInfo.ScriptBatches.First();

            var showTypes = batch.Commands.OfType<ShowCommand>().Select(c => c.ShowType).ToList();

            CollectionAssert.Contains(showTypes, ShowType.Diagram);
            CollectionAssert.Contains(showTypes, ShowType.Dependencies);
        }

        [TestMethod]
        public void BatchParameterCollectionsContainOnlyCurrentBatchValues()
        {
            var provider = BuildProvider(
                "--> PARAMETER @Color = Red\n" +
                "EVALUATE { @Color }\n" +
                "--> GO\n" +
                "--> PARAMETER @Size = Large\n" +
                "EVALUATE { @Size }");

            var first = ResultsTargetGrid.GetBatchParameterCollection(provider.QueryInfo, 0);
            var second = ResultsTargetGrid.GetBatchParameterCollection(provider.QueryInfo, 1);

            Assert.AreEqual(1, first.Count);
            Assert.AreEqual("Color", first[0].ParameterName);
            Assert.AreEqual("Red", first[0].Value);
            Assert.AreEqual(1, second.Count);
            Assert.AreEqual("Size", second[0].ParameterName);
            Assert.AreEqual("Large", second[0].Value);
        }

        [TestMethod]
        public async System.Threading.Tasks.Task CancellingSecondBatchParameterPromptStopsExecution()
        {
            var provider = BuildProvider(
                "EVALUATE { @First }\n" +
                "--> GO\n" +
                "EVALUATE { @Second }");
            provider.QueryInfo.GetBatchParameters(0)["First"].Value = "one";

            var runner = Substitute.For<IQueryRunner>();
            runner.PromptForBatchParametersAsync(provider.QueryInfo, 1)
                .Returns(System.Threading.Tasks.Task.FromResult(false));
            var target = new ResultsTargetGrid(_eventAggregator, NewParserOptions());

            await Assert.ThrowsExactlyAsync<System.OperationCanceledException>(
                () => target.OutputResultsAsync(runner, provider, null));

            runner.Received(1).ExecuteDataReaderQuery(
                Arg.Is<string>(q => q.Contains("@First")),
                Arg.Is<System.Collections.Generic.List<Microsoft.AnalysisServices.AdomdClient.AdomdParameter>>(
                    p => p.Count == 1 && p[0].ParameterName == "First"));
            runner.DidNotReceive().ExecuteDataReaderQuery(
                Arg.Is<string>(q => q.Contains("@Second")),
                Arg.Any<System.Collections.Generic.List<Microsoft.AnalysisServices.AdomdClient.AdomdParameter>>());
        }

        [TestMethod]
        public async System.Threading.Tasks.Task SameNamedParameterPromptsAndExecutesIndependentlyPerBatch()
        {
            var provider = BuildProvider(
                "EVALUATE { @Color }\n" +
                "--> GO\n" +
                "EVALUATE { @Color }");
            var runner = Substitute.For<IQueryRunner>();
            runner.PromptForBatchParametersAsync(provider.QueryInfo, Arg.Any<int>())
                .Returns(call =>
                {
                    var batchIndex = call.ArgAt<int>(1);
                    provider.QueryInfo.GetBatchParameters(batchIndex)["Color"].Value =
                        batchIndex == 0 ? "Red" : "Blue";
                    return System.Threading.Tasks.Task.FromResult(true);
                });
            var target = new ResultsTargetGrid(_eventAggregator, NewParserOptions());

            await target.OutputResultsAsync(runner, provider, null);

            await runner.Received(1).PromptForBatchParametersAsync(provider.QueryInfo, 0);
            await runner.Received(1).PromptForBatchParametersAsync(provider.QueryInfo, 1);
            runner.Received(1).ExecuteDataReaderQuery(
                Arg.Any<string>(),
                Arg.Is<System.Collections.Generic.List<Microsoft.AnalysisServices.AdomdClient.AdomdParameter>>(
                    p => p.Count == 1 && (string)p[0].Value == "Red"));
            runner.Received(1).ExecuteDataReaderQuery(
                Arg.Any<string>(),
                Arg.Is<System.Collections.Generic.List<Microsoft.AnalysisServices.AdomdClient.AdomdParameter>>(
                    p => p.Count == 1 && (string)p[0].Value == "Blue"));
        }

        [TestMethod]
        public async System.Threading.Tasks.Task CommandOnlyFirstBatchUsesLaterBatchParameters()
        {
            var provider = BuildProvider(
                "--> SET Environment = dev\n" +
                "--> GO\n" +
                "--> PARAMETER @Color = Blue\n" +
                "EVALUATE { @Color }");
            var processedQuery = provider.QueryInfo.ProcessedQuery;
            provider.QueryText.Returns(processedQuery);
            Assert.AreEqual(2, provider.QueryInfo.ScriptBatches.Count);
            Assert.AreEqual("Blue", provider.QueryInfo.GetBatchParameters(1)["Color"].Value);
            var runner = Substitute.For<IQueryRunner>();
            var target = new ResultsTargetGrid(_eventAggregator, NewParserOptions());

            await target.OutputResultsAsync(runner, provider, null);

            runner.Received(1).ExecuteDataReaderQuery(
                Arg.Is<string>(q => q.Contains("@Color")),
                Arg.Is<System.Collections.Generic.List<Microsoft.AnalysisServices.AdomdClient.AdomdParameter>>(
                    p => p.Count == 1 && p[0].ParameterName == "Color" && (string)p[0].Value == "Blue"));
        }

        [TestMethod]
        public async System.Threading.Tasks.Task GoDelay_WaitsBetweenExecutableBatchesInOrder()
        {
            var provider = BuildProvider("EVALUATE { 1 }\n--> GO DELAY 25ms\nEVALUATE { 2 }");
            var runner = Substitute.For<IQueryRunner>();
            var target = new ResultsTargetGrid(_eventAggregator, NewParserOptions());

            await target.OutputResultsAsync(runner, provider, null);

            Received.InOrder(() =>
            {
                runner.ProcessBatchAssertionsAsync(0, Arg.Any<System.Collections.Generic.IReadOnlyList<System.Data.DataTable>>());
                runner.WaitForBatchDelayAsync(25);
                runner.ProcessBatchPreQueryCommandsAsync(1);
            });
            await runner.Received(1).WaitForBatchDelayAsync(25);
        }

        [TestMethod]
        public async System.Threading.Tasks.Task GoDelay_WaitsBeforeFollowingShowBatch()
        {
            var provider = BuildProvider("EVALUATE { 1 }\n--> GO DELAY 30ms\n--> SHOW DEPENDENCIES");
            var runner = Substitute.For<IQueryRunner>();
            var target = new ResultsTargetGrid(_eventAggregator, NewParserOptions());

            await target.OutputResultsAsync(runner, provider, null);

            await runner.Received(1).WaitForBatchDelayAsync(30);
        }

        [TestMethod]
        public async System.Threading.Tasks.Task TrailingGoDelay_DoesNotWait()
        {
            var provider = BuildProvider("EVALUATE { 1 }\n--> GO DELAY 30ms");
            var runner = Substitute.For<IQueryRunner>();
            var target = new ResultsTargetGrid(_eventAggregator, NewParserOptions());

            await target.OutputResultsAsync(runner, provider, null);

            await runner.DidNotReceive().WaitForBatchDelayAsync(Arg.Any<int>());
        }

        [TestMethod]
        public async System.Threading.Tasks.Task GoDelay_OnCommandOnlyBatch_WaitsBeforeNextQuery()
        {
            var provider = BuildProvider(
                "EVALUATE { 1 }\n--> GO\n--> SET Env = dev\n--> GO DELAY 40ms\nEVALUATE { 2 }");
            var runner = Substitute.For<IQueryRunner>();
            var target = new ResultsTargetGrid(_eventAggregator, NewParserOptions());

            await target.OutputResultsAsync(runner, provider, null);

            Received.InOrder(() =>
            {
                runner.WaitForBatchDelayAsync(40);
                runner.ProcessBatchPreQueryCommandsAsync(2);
            });
        }

        [TestMethod]
        public async System.Threading.Tasks.Task GoDelay_CancellationStopsBeforeNextBatch()
        {
            var provider = BuildProvider("EVALUATE { 1 }\n--> GO DELAY 5s\nEVALUATE { 2 }");
            var runner = Substitute.For<IQueryRunner>();
            var target = new ResultsTargetGrid(_eventAggregator, NewParserOptions());
            var cancellationToken = new System.Threading.CancellationToken(true);
            runner.WaitForBatchDelayAsync(5000)
                .Returns(System.Threading.Tasks.Task.FromCanceled(cancellationToken));

            var wasCancelled = false;
            try
            {
                await target.OutputResultsAsync(runner, provider, null);
            }
            catch (System.OperationCanceledException)
            {
                wasCancelled = true;
            }

            Assert.IsTrue(wasCancelled);
            await runner.DidNotReceive().ProcessBatchPreQueryCommandsAsync(1);
        }

        [TestMethod]
        public async System.Threading.Tasks.Task GoDelay_IsExcludedFromReportedBatchDuration()
        {
            const int delayMilliseconds = 200;
            var provider = BuildProvider($"EVALUATE {{ 1 }}\n--> GO DELAY {delayMilliseconds}ms\nEVALUATE {{ 2 }}");
            var runner = Substitute.For<IQueryRunner>();
            var target = new ResultsTargetGrid(_eventAggregator, NewParserOptions());
            runner.WaitForBatchDelayAsync(delayMilliseconds)
                .Returns(System.Threading.Tasks.Task.Delay(delayMilliseconds));
            var wallClock = System.Diagnostics.Stopwatch.StartNew();

            await target.OutputResultsAsync(runner, provider, null);

            wallClock.Stop();
            runner.Received().OutputError(
                Arg.Is<string>(message => message.Contains("Query Batch Completed")),
                Arg.Is<double>(duration => duration < wallClock.ElapsedMilliseconds - 100));
        }

        [TestMethod]
        public void ResolveDiagramTablesWithoutConnectionReturnsNullAndWarns()
        {
            // With a query but no live connection we cannot resolve the query's tables, so the full
            // (unfiltered) diagram is requested (null tables) and a warning is emitted.
            var gridAgg = Substitute.For<IEventAggregator>();
            var grid = new ResultsTargetGrid(gridAgg, NewParserOptions());
            var provider = BuildProvider("--> SHOW DIAGRAM\nEVALUATE { 1 }");
            var runner = Substitute.For<IQueryRunner>();
            var batch = provider.QueryInfo.ScriptBatches.First();

            var tables = grid.ResolveDiagramTables(runner, batch);

            Assert.IsNull(tables, "Without a live connection the diagram should be shown unfiltered (null tables)");
            runner.Received().OutputWarning(Arg.Is<string>(s => s.Contains("live connection")));
        }

        [TestMethod]
        public void ResolveDiagramTablesOnItsOwnReturnsNullWithoutWarning()
        {
            // "--> SHOW DIAGRAM" on its own (no query) resolves to the full diagram with no table filter
            // and does not emit a connection warning.
            var gridAgg = Substitute.For<IEventAggregator>();
            var grid = new ResultsTargetGrid(gridAgg, NewParserOptions());
            var provider = BuildProvider("--> SHOW DIAGRAM");
            var runner = Substitute.For<IQueryRunner>();
            var batch = provider.QueryInfo.ScriptBatches.First();

            var tables = grid.ResolveDiagramTables(runner, batch);

            Assert.IsNull(tables, "With no query the diagram should be shown unfiltered (null tables)");
            runner.DidNotReceive().OutputWarning(Arg.Any<string>());
        }

        [TestMethod]
        public void ShowMetricsCommandProducesNoTabDescriptor()
        {
            // SHOW METRICS opens the VertiPaq Analyzer tool window instead of producing a result tab,
            // so TryHandleShowBatch reports it as handled but adds no descriptor.
            var provider = BuildProvider("--> SHOW METRICS\nEVALUATE { 1 }");
            var runner = Substitute.For<IQueryRunner>();
            var batch = provider.QueryInfo.ScriptBatches.First();

            var handled = ResultsTargetGrid.TryHandleShowBatch(runner, provider, batch, out var descriptors);

            Assert.IsTrue(handled, "A SHOW METRICS command should be treated as handled");
            Assert.AreEqual(0, descriptors.Count, "SHOW METRICS opens a tool window and produces no result tab");
        }

        [TestMethod]
        public void ShowDeltaCommandProducesNoTabDescriptor()
        {
            // SHOW DELTA opens the Delta Analyzer tool window instead of producing a result tab.
            var provider = BuildProvider("--> SHOW DELTA\nEVALUATE { 1 }");
            var runner = Substitute.For<IQueryRunner>();
            var batch = provider.QueryInfo.ScriptBatches.First();

            var handled = ResultsTargetGrid.TryHandleShowBatch(runner, provider, batch, out var descriptors);

            Assert.IsTrue(handled, "A SHOW DELTA command should be treated as handled");
            Assert.AreEqual(0, descriptors.Count, "SHOW DELTA opens a tool window and produces no result tab");
        }

        [TestMethod]
        public void ShowMetricsCommandParsesIntoScriptBatch()
        {
            var provider = BuildProvider("--> SHOW METRICS\nEVALUATE { 1 }");
            var showTypes = provider.QueryInfo.ScriptBatches
                .SelectMany(b => b.Commands)
                .OfType<ShowCommand>()
                .Select(c => c.ShowType)
                .ToList();

            CollectionAssert.Contains(showTypes, ShowType.Metrics);
        }

        [TestMethod]
        public void ShowDeltaCommandParsesIntoScriptBatch()
        {
            var provider = BuildProvider("--> SHOW DELTA\nEVALUATE { 1 }");
            var showTypes = provider.QueryInfo.ScriptBatches
                .SelectMany(b => b.Commands)
                .OfType<ShowCommand>()
                .Select(c => c.ShowType)
                .ToList();

            CollectionAssert.Contains(showTypes, ShowType.Delta);
        }

        [TestMethod]
        public void ExportMetricsCommandParsesIntoScriptBatch()
        {
            // "--> EXPORT METRICS <file>" is a side-effect command parsed as an ExportCommand.
            var provider = BuildProvider("--> EXPORT METRICS \"model.vpax\"\nEVALUATE { 1 }");
            var exportCommands = provider.QueryInfo.ScriptBatches
                .SelectMany(b => b.Commands)
                .OfType<ExportCommand>()
                .ToList();

            Assert.AreEqual(1, exportCommands.Count);
            Assert.AreEqual(ExportTarget.Metrics, exportCommands[0].Target);
            Assert.AreEqual("model.vpax", exportCommands[0].FileName);
        }

        [TestMethod]
        public async System.Threading.Tasks.Task ExecuteShowActivations_EmptyList_ActivatesResults()
        {
            var gridAgg = Substitute.For<IEventAggregator>();
            var grid = new ResultsTargetGrid(gridAgg, NewParserOptions());
            var runner = Substitute.For<IQueryRunner>();

            await grid.ExecuteShowActivationsAsync(runner, new System.Collections.Generic.List<ResultsTargetGrid.ShowActivation>());

            runner.Received(1).ActivateResults();
        }

        [TestMethod]
        public async System.Threading.Tasks.Task ExecuteShowActivations_LastPaneWins_DoesNotActivateResults()
        {
            // A tree SHOW (Results) followed by SHOW METRICS: both are executed but the LAST command
            // (METRICS) must be the final activation, so the VertiPaq event is published last and the
            // Results pane is NOT re-activated afterwards.
            var gridAgg = Substitute.For<IEventAggregator>();
            var grid = new ResultsTargetGrid(gridAgg, NewParserOptions());
            var runner = Substitute.For<IQueryRunner>();

            var activations = new System.Collections.Generic.List<ResultsTargetGrid.ShowActivation>
            {
                ResultsTargetGrid.ShowActivation.Simple(ResultsTargetGrid.ShowActivationKind.Results),
                ResultsTargetGrid.ShowActivation.Simple(ResultsTargetGrid.ShowActivationKind.Metrics),
            };

            await grid.ExecuteShowActivationsAsync(runner, activations);

            // The earlier Results activation still fires, but the VertiPaq window is opened after it.
            runner.Received(1).ActivateResults();
            gridAgg.Received().PublishAsync(
                Arg.Is<object>(o => o is OpenVertipaqAnalyzerEvent),
                Arg.Any<System.Func<System.Func<System.Threading.Tasks.Task>, System.Threading.Tasks.Task>>(),
                Arg.Any<System.Threading.CancellationToken>());

            // The metrics window must be activated after the results pane (last SHOW wins focus).
            Received.InOrder(() =>
            {
                runner.ActivateResults();
                gridAgg.PublishAsync(
                    Arg.Is<object>(o => o is OpenVertipaqAnalyzerEvent),
                    Arg.Any<System.Func<System.Func<System.Threading.Tasks.Task>, System.Threading.Tasks.Task>>(),
                    Arg.Any<System.Threading.CancellationToken>());
            });
        }

        [TestMethod]
        public async System.Threading.Tasks.Task ExecuteShowActivations_LastTreeWins_ActivatesResultsAfterPane()
        {
            // SHOW METRICS followed by a tree SHOW (Results): the VertiPaq window opens first, then the
            // Results pane is activated last so it keeps focus.
            var gridAgg = Substitute.For<IEventAggregator>();
            var grid = new ResultsTargetGrid(gridAgg, NewParserOptions());
            var runner = Substitute.For<IQueryRunner>();

            var activations = new System.Collections.Generic.List<ResultsTargetGrid.ShowActivation>
            {
                ResultsTargetGrid.ShowActivation.Simple(ResultsTargetGrid.ShowActivationKind.Metrics),
                ResultsTargetGrid.ShowActivation.Simple(ResultsTargetGrid.ShowActivationKind.Results),
            };

            await grid.ExecuteShowActivationsAsync(runner, activations);

            Received.InOrder(() =>
            {
                gridAgg.PublishAsync(
                    Arg.Is<object>(o => o is OpenVertipaqAnalyzerEvent),
                    Arg.Any<System.Func<System.Func<System.Threading.Tasks.Task>, System.Threading.Tasks.Task>>(),
                    Arg.Any<System.Threading.CancellationToken>());
                runner.ActivateResults();
            });
        }

        [TestMethod]
        public async System.Threading.Tasks.Task ExecuteShowActivations_DiagramPassesTablesThrough()
        {
            var gridAgg = Substitute.For<IEventAggregator>();
            var grid = new ResultsTargetGrid(gridAgg, NewParserOptions());
            var runner = Substitute.For<IQueryRunner>();

            var tables = new System.Collections.Generic.List<string> { "Sales", "Date" };
            var activations = new System.Collections.Generic.List<ResultsTargetGrid.ShowActivation>
            {
                ResultsTargetGrid.ShowActivation.Diagram(tables),
            };

            await grid.ExecuteShowActivationsAsync(runner, activations);

            gridAgg.Received().PublishAsync(
                Arg.Is<object>(o => o is OpenModelDiagramEvent && ((OpenModelDiagramEvent)o).TableNames == tables),
                Arg.Any<System.Func<System.Func<System.Threading.Tasks.Task>, System.Threading.Tasks.Task>>(),
                Arg.Any<System.Threading.CancellationToken>());
            runner.DidNotReceive().ActivateResults();
        }
    }
}
