using Caliburn.Micro;
using DaxStudio.Core.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using ADOTabular;
using ADOTabular.Enums;
using ADOTabular.Interfaces;
using System;
using System.Data;
using System.Reflection;
using System.Threading.Tasks;

namespace DaxStudio.Tests
{
    [TestClass]
    public class ConnectionManagerDependencyTests
    {
        private static ConnectionManager NewConnectionManager()
        {
            return new ConnectionManager(Substitute.For<IEventAggregator>());
        }

        private static ADOTabularDatabaseCollection CreateDatabaseList(params string[] names)
        {
            var databases = new ADOTabularDatabaseCollection(null);
            var table = new DataTable();
            table.Columns.Add("CATALOG_NAME", typeof(string));
            foreach (var name in names) table.Rows.Add(name);
            var dataSet = new DataSet();
            dataSet.Tables.Add(table);
            var field = typeof(ADOTabularDatabaseCollection).GetField("_dsDatabases", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            field.SetValue(databases, dataSet);
            return databases;
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task DatabaseSwitchesRunConcurrentlyAndWaitForBoth(bool queryCompletesFirst)
        {
            using var bothStarted = new System.Threading.CountdownEvent(2);
            using var releaseQuery = new System.Threading.ManualResetEventSlim();
            using var releaseDmv = new System.Threading.ManualResetEventSlim();
            using var firstCompleted = new System.Threading.ManualResetEventSlim();
            string queryDatabase = null;
            string dmvDatabase = null;

            var switching = ConnectionManager.ChangeDatabasesAsync("Selected", name =>
            {
                queryDatabase = name;
                bothStarted.Signal();
                if (!releaseQuery.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Query switch was not released.");
                if (queryCompletesFirst) firstCompleted.Set();
            }, name =>
            {
                dmvDatabase = name;
                bothStarted.Signal();
                if (!releaseDmv.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("DMV switch was not released.");
                if (!queryCompletesFirst) firstCompleted.Set();
            });

            try
            {
                Assert.IsTrue(bothStarted.Wait(TimeSpan.FromSeconds(5)), "Both switches must start before either is released.");
                Assert.IsFalse(switching.IsCompleted);
                (queryCompletesFirst ? releaseQuery : releaseDmv).Set();
                Assert.IsTrue(firstCompleted.Wait(TimeSpan.FromSeconds(5)));
                Assert.IsFalse(switching.IsCompleted, "The second switch is still running.");
            }
            finally
            {
                releaseQuery.Set();
                releaseDmv.Set();
                await switching;
            }

            Assert.AreEqual("Selected", queryDatabase);
            Assert.AreEqual("Selected", dmvDatabase);
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task FailedDatabaseSwitchWaitsForOtherConnection(bool queryFails)
        {
            using var failureStarted = new System.Threading.ManualResetEventSlim();
            using var otherStarted = new System.Threading.ManualResetEventSlim();
            using var releaseOther = new System.Threading.ManualResetEventSlim();
            var expected = new InvalidOperationException("Switch failed.");
            Action<string> failingSwitch = name =>
            {
                failureStarted.Set();
                throw expected;
            };
            Action<string> pendingSwitch = name =>
            {
                otherStarted.Set();
                if (!releaseOther.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Other switch was not released.");
            };

            var switching = ConnectionManager.ChangeDatabasesAsync("Selected",
                queryFails ? failingSwitch : pendingSwitch,
                queryFails ? pendingSwitch : failingSwitch);
            try
            {
                Assert.IsTrue(failureStarted.Wait(TimeSpan.FromSeconds(5)));
                Assert.IsTrue(otherStarted.Wait(TimeSpan.FromSeconds(5)));
                Assert.IsFalse(switching.IsCompleted, "Failure must not abandon the other switch.");
            }
            finally
            {
                releaseOther.Set();
                var actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => switching);
                Assert.AreSame(expected, actual);
            }
        }

        [TestMethod]
        public async Task RefreshTablesWaitsForMetadataPaneSubscriber()
        {
            var events = Substitute.For<IEventAggregator>();
            var subscriberStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var subscriberFinished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            events.PublishAsync(Arg.Any<object>(), Arg.Any<Func<Func<Task>, Task>>(),
                Arg.Any<System.Threading.CancellationToken>()).Returns(call =>
                {
                    if (call[0] is DaxStudio.Core.Events.TablesRefreshedEvent)
                    {
                        subscriberStarted.TrySetResult(true);
                        return subscriberFinished.Task;
                    }
                    return Task.CompletedTask;
                });
            var manager = new ConnectionManager(events);
            using var connection = new ADOTabularConnection(string.Empty, AdomdType.AnalysisServices);
            var visitor = Substitute.For<IMetaDataVisitor>();
            connection.Visitor = visitor;
            typeof(ADOTabularConnection).GetField("_adomdConn", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(connection, null);
            var database = new ADOTabularDatabase(connection, "Test", "Test", DateTime.Today, "1200", "*", "Test");
            var model = new ADOTabularModel(connection, database, "Model", "Model", string.Empty, string.Empty);
            database.Models.Add(model);
            visitor.Visit(connection).Returns(database);
            typeof(ConnectionManager).GetField("_dmvConnection", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(manager, connection);
            manager.SelectedModelName = model.Name;

            var refreshing = manager.RefreshTablesAsync();
            try
            {
                var started = await Task.WhenAny(subscriberStarted.Task, Task.Delay(TimeSpan.FromSeconds(5)));
                Assert.AreSame(subscriberStarted.Task, started);
                Assert.IsFalse(refreshing.IsCompleted);
            }
            finally
            {
                subscriberFinished.TrySetResult(true);
                await refreshing;
            }
        }

        [TestMethod]
        public async Task ExplicitDatabaseSkipsCatalogDiscoveryAndPicker()
        {
            var selected = await ConnectionManager.SelectInitialDatabaseAsync("Requested", null,
                databases => throw new AssertFailedException("The picker should not be invoked."));

            Assert.AreEqual("Requested", selected);
        }

        [TestMethod]
        [DataRow(0, false)]
        [DataRow(0, true)]
        [DataRow(1, false)]
        [DataRow(1, true)]
        public async Task AtMostOneDatabaseDoesNotSwitchOrShowPicker(int count, bool pickerEnabled)
        {
            var databases = count == 0 ? CreateDatabaseList() : CreateDatabaseList("Only database");
            Func<ADOTabularDatabaseCollection, Task<string>> picker = available =>
                throw new AssertFailedException("The picker should not be invoked.");

            var selected = await ConnectionManager.SelectInitialDatabaseAsync(null, databases, pickerEnabled ? picker : null);

            Assert.IsNull(selected);
        }

        [TestMethod]
        public async Task MultipleDatabasesWithoutPickerSelectFirstDatabase()
        {
            var databases = CreateDatabaseList("First", "Second");

            var selected = await ConnectionManager.SelectInitialDatabaseAsync(null, databases, null);

            Assert.AreEqual("First", selected);
        }

        [TestMethod]
        public async Task MultipleDatabasesAwaitPickerWithoutSelectingFirstDatabase()
        {
            var databases = CreateDatabaseList("First", "Second");
            var selection = new TaskCompletionSource<string>();
            var pickerCalls = 0;
            var pending = ConnectionManager.SelectInitialDatabaseAsync(null, databases, available =>
            {
                Assert.AreSame(databases, available);
                pickerCalls++;
                return selection.Task;
            });

            Assert.AreEqual(1, pickerCalls);
            Assert.IsFalse(pending.IsCompleted);
            selection.SetResult("Second");

            Assert.AreEqual("Second", await pending);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        public async Task CancelledPickerDoesNotFallBackToFirstDatabase(string selection)
        {
            var databases = CreateDatabaseList("First", "Second");

            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
                ConnectionManager.SelectInitialDatabaseAsync(null, databases, available => Task.FromResult(selection)));
        }

        [TestMethod]
        public void DmvListReusesCurrentConnectionCache()
        {
            var manager = NewConnectionManager();
            Assert.IsNull(manager.DynamicManagementViews);
            using var firstConnection = new ADOTabularConnection(string.Empty, AdomdType.AnalysisServices);
            var connectionField = typeof(ConnectionManager).GetField("_dmvConnection", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(connectionField);
            connectionField.SetValue(manager, firstConnection);

            var dmvs = manager.DynamicManagementViews;
            Assert.AreSame(firstConnection.DynamicManagementViews, dmvs);
            Assert.AreSame(dmvs, manager.DynamicManagementViews);

            using var replacementConnection = new ADOTabularConnection(string.Empty, AdomdType.AnalysisServices);
            connectionField.SetValue(manager, replacementConnection);
            Assert.AreSame(replacementConnection.DynamicManagementViews, manager.DynamicManagementViews);
            Assert.AreNotSame(dmvs, manager.DynamicManagementViews);

            connectionField.SetValue(manager, null);
            Assert.IsNull(manager.DynamicManagementViews);
        }

        [TestMethod]
        public void ManagerSharesServerPropertiesDuringConcurrentInitialization()
        {
            var manager = NewConnectionManager();
            using var query = new ADOTabularConnection(string.Empty, AdomdType.AnalysisServices);
            using var dmv = new ADOTabularConnection(string.Empty, AdomdType.AnalysisServices);
            query.ServerType = ServerType.PowerBIService;
            query.Visitor = Substitute.For<IMetaDataVisitor>();
            dmv.Visitor = Substitute.For<IMetaDataVisitor>();
            var requests = 0;
            query.ServerProperties = new ADOTabularServerProperties(() =>
            {
                System.Threading.Interlocked.Increment(ref requests);
                return ADOTabularTests.ServerPropertiesXml;
            });
            dmv.ServerProperties = new ADOTabularServerProperties(() =>
                throw new AssertFailedException("DMV connection must use the shared server response."));
            var queryField = typeof(ConnectionManager).GetField("_connection", BindingFlags.Instance | BindingFlags.NonPublic);
            var dmvField = typeof(ConnectionManager).GetField("_dmvConnection", BindingFlags.Instance | BindingFlags.NonPublic);
            queryField.SetValue(manager, query);
            dmvField.SetValue(manager, dmv);

            Parallel.For(0, 16, index => manager.EnsureServerProperties());
            Parallel.Invoke(query.InitializeMetadata, dmv.InitializeMetadata);

            Assert.AreSame(query.ServerProperties, dmv.ServerProperties);
            Assert.AreEqual("Enterprise", query.ServerEdition);
            Assert.AreEqual("Enterprise", dmv.ServerEdition);
            Assert.AreEqual("Cloud", query.ServerLocation);
            Assert.AreEqual("Cloud", dmv.ServerLocation);
            Assert.AreEqual("Tabular", query.ServerMode);
            Assert.AreEqual("Tabular", dmv.ServerMode);
            Assert.AreEqual("TestServer", query.ServerId);
            Assert.AreEqual("TestServer", dmv.ServerId);
            Assert.AreEqual(1, requests);

            using var replacementDmv = new ADOTabularConnection(string.Empty, AdomdType.AnalysisServices);
            dmvField.SetValue(manager, replacementDmv);
            manager.EnsureServerProperties();
            Assert.AreSame(query.ServerProperties, replacementDmv.ServerProperties);
            Assert.AreEqual("Tabular", replacementDmv.ServerMode);
            Assert.AreEqual("TestServer", replacementDmv.ServerId);
            Assert.AreEqual(1, requests);

            using var replacementQuery = new ADOTabularConnection(string.Empty, AdomdType.AnalysisServices);
            replacementQuery.ServerProperties = new ADOTabularServerProperties(() =>
            {
                System.Threading.Interlocked.Increment(ref requests);
                return "<Server><ID>OtherServer</ID><ServerMode>Multidimensional</ServerMode></Server>";
            });
            queryField.SetValue(manager, replacementQuery);
            manager.EnsureServerProperties();
            Assert.AreNotSame(query.ServerProperties, replacementQuery.ServerProperties);
            Assert.AreSame(replacementQuery.ServerProperties, replacementDmv.ServerProperties);
            Assert.AreEqual("OtherServer", replacementDmv.ServerId);
            Assert.AreEqual("Multidimensional", replacementDmv.ServerMode);
            Assert.AreEqual(2, requests);
        }

        [TestMethod]
        public void ManagerSharesLanguageMetadataAcrossConnectionRoles()
        {
            var manager = NewConnectionManager();
            using var query = new ADOTabularConnection(string.Empty, AdomdType.AnalysisServices);
            using var dmv = new ADOTabularConnection(string.Empty, AdomdType.AnalysisServices);
            query.ServerType = ServerType.PowerBIService;
            var queryVisitor = Substitute.For<IMetaDataVisitor>();
            var dmvVisitor = Substitute.For<IMetaDataVisitor>();
            query.Visitor = queryVisitor;
            dmv.Visitor = dmvVisitor;
            var queryField = typeof(ConnectionManager).GetField("_connection", BindingFlags.Instance | BindingFlags.NonPublic);
            var dmvField = typeof(ConnectionManager).GetField("_dmvConnection", BindingFlags.Instance | BindingFlags.NonPublic);
            queryField.SetValue(manager, query);
            dmvField.SetValue(manager, dmv);

            Parallel.For(0, 16, index => manager.EnsureLanguageMetadata());
            Parallel.Invoke(query.InitializeMetadata, dmv.InitializeMetadata);

            Assert.AreSame(query.Keywords, dmv.Keywords);
            Assert.AreSame(query.FunctionGroups, manager.FunctionGroups);
            Assert.AreSame(dmv.FunctionGroups, manager.FunctionGroups);
            CollectionAssert.AreEqual(new string[0], System.Linq.Enumerable.ToArray(manager.AllFunctions));
            queryVisitor.DidNotReceive().Visit(Arg.Any<ADOTabularKeywordCollection>());
            queryVisitor.DidNotReceive().Visit(Arg.Any<ADOTabularFunctionGroupCollection>());
            dmvVisitor.Received(1).Visit(Arg.Any<ADOTabularKeywordCollection>());
            dmvVisitor.Received(1).Visit(Arg.Any<ADOTabularFunctionGroupCollection>());

            using var replacementQuery = new ADOTabularConnection(string.Empty, AdomdType.AnalysisServices);
            var replacementVisitor = Substitute.For<IMetaDataVisitor>();
            replacementQuery.Visitor = replacementVisitor;
            queryField.SetValue(manager, replacementQuery);
            manager.EnsureLanguageMetadata();

            Assert.AreSame(dmv.Keywords, replacementQuery.Keywords);
            Assert.AreSame(manager.FunctionGroups, replacementQuery.FunctionGroups);
            replacementVisitor.DidNotReceive().Visit(Arg.Any<ADOTabularKeywordCollection>());
            replacementVisitor.DidNotReceive().Visit(Arg.Any<ADOTabularFunctionGroupCollection>());
        }

        [TestMethod]
        public void ManagerRetriesFailedLanguageMetadataDiscovery()
        {
            var manager = NewConnectionManager();
            using var query = new ADOTabularConnection(string.Empty, AdomdType.AnalysisServices);
            using var dmv = new ADOTabularConnection(string.Empty, AdomdType.AnalysisServices);
            var visitor = Substitute.For<IMetaDataVisitor>();
            dmv.Visitor = visitor;
            typeof(ConnectionManager).GetField("_connection", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, query);
            typeof(ConnectionManager).GetField("_dmvConnection", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, dmv);
            var attempts = 0;
            visitor.When(metadata => metadata.Visit(Arg.Any<ADOTabularFunctionGroupCollection>()))
                .Do(call =>
                {
                    if (++attempts == 1) throw new InvalidOperationException("Discovery failed.");
                });

            Assert.ThrowsExactly<InvalidOperationException>(() => manager.EnsureLanguageMetadata());
            manager.EnsureLanguageMetadata();

            Assert.AreSame(dmv.Keywords, query.Keywords);
            Assert.AreSame(dmv.FunctionGroups, query.FunctionGroups);
            visitor.Received(1).Visit(Arg.Any<ADOTabularKeywordCollection>());
            visitor.Received(2).Visit(Arg.Any<ADOTabularFunctionGroupCollection>());
        }

        [TestMethod]
        public void FunctionGroupsReuseCurrentDmvConnectionCache()
        {
            var manager = NewConnectionManager();
            Assert.IsNull(manager.FunctionGroups);
            var visitor = Substitute.For<IMetaDataVisitor>();
            using var firstConnection = new ADOTabularConnection(string.Empty, AdomdType.AnalysisServices);
            firstConnection.Visitor = visitor;
            var firstGroups = firstConnection.FunctionGroups;
            var connectionField = typeof(ConnectionManager).GetField("_dmvConnection", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(connectionField);
            connectionField.SetValue(manager, firstConnection);

            Assert.AreSame(firstGroups, manager.FunctionGroups);
            Assert.AreSame(firstGroups, manager.FunctionGroups);
            visitor.Received(1).Visit(Arg.Any<ADOTabularFunctionGroupCollection>());

            var replacementVisitor = Substitute.For<IMetaDataVisitor>();
            using var replacementConnection = new ADOTabularConnection(string.Empty, AdomdType.AnalysisServices);
            replacementConnection.Visitor = replacementVisitor;
            connectionField.SetValue(manager, replacementConnection);

            var replacementGroups = manager.FunctionGroups;
            Assert.AreSame(replacementConnection.FunctionGroups, replacementGroups);
            Assert.AreNotSame(firstGroups, replacementGroups);
            replacementVisitor.Received(1).Visit(Arg.Any<ADOTabularFunctionGroupCollection>());

            connectionField.SetValue(manager, null);
            Assert.IsNull(manager.FunctionGroups);
        }

        [TestMethod]
        public void GetQueryDependencyTablesReturnsEmptyForNullQuery()
        {
            var cnn = NewConnectionManager();
            var tables = cnn.GetQueryDependencyTables(null);
            Assert.IsNotNull(tables);
            Assert.AreEqual(0, tables.Count);
        }

        [TestMethod]
        public void GetQueryDependencyTablesReturnsEmptyForBlankQuery()
        {
            var cnn = NewConnectionManager();
            var tables = cnn.GetQueryDependencyTables("   ");
            Assert.IsNotNull(tables);
            Assert.AreEqual(0, tables.Count);
        }
    }
}
