using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Caliburn.Micro;
using DaxStudio.Common.Enums;
using DaxStudio.Core.Connections;
using DaxStudio.QueryTrace;
using Microsoft.AnalysisServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace DaxStudio.Tests
{
    [TestClass]
    public class TraceEventFactoryTests
    {
        // The columns supported by an event vary between engine versions. SSAS 2025 (v17) no longer
        // exposes the ApplicationName column on the VertiPaqSEQueryBegin event and adding it results in
        // "The event Id=82 does not contain the column Id=37"
        private static readonly HashSet<TraceColumn> Ssas2025VertiPaqSeQueryBeginColumns = new HashSet<TraceColumn>
        {
            TraceColumn.EventClass,
            TraceColumn.EventSubclass,
            TraceColumn.CurrentTime,
            TraceColumn.StartTime,
            TraceColumn.DatabaseName,
            TraceColumn.NTUserName,
            TraceColumn.SessionID,
            TraceColumn.Spid,
            TraceColumn.TextData,
            TraceColumn.ActivityID,
            TraceColumn.RequestID,
            TraceColumn.ApplicationContext
        };

        [TestMethod]
        public void CreateExcludesColumnsNotSupportedByTheEvent()
        {
            var evt = TraceEventFactory.Create(TraceEventClass.VertiPaqSEQueryBegin, Ssas2025VertiPaqSeQueryBeginColumns);

            var columns = evt.Columns.Cast<TraceColumn>().ToList();

            Assert.AreEqual(TraceEventClass.VertiPaqSEQueryBegin, evt.EventID);
            CollectionAssert.DoesNotContain(columns, TraceColumn.ApplicationName);
            CollectionAssert.DoesNotContain(columns, TraceColumn.Duration);
        }

        [TestMethod]
        public void CreateIncludesColumnsSupportedByTheEvent()
        {
            var evt = TraceEventFactory.Create(TraceEventClass.VertiPaqSEQueryBegin, Ssas2025VertiPaqSeQueryBeginColumns);

            var columns = evt.Columns.Cast<TraceColumn>().ToList();

            CollectionAssert.Contains(columns, TraceColumn.EventClass);
            CollectionAssert.Contains(columns, TraceColumn.TextData);
            CollectionAssert.Contains(columns, TraceColumn.ActivityID);
            CollectionAssert.Contains(columns, TraceColumn.Spid);
        }

        [TestMethod]
        public void CreateWithApplicationNameSupportedIncludesIt()
        {
            var supported = new HashSet<TraceColumn>(Ssas2025VertiPaqSeQueryBeginColumns) { TraceColumn.ApplicationName };

            var evt = TraceEventFactory.Create(TraceEventClass.VertiPaqSEQueryBegin, supported);

            CollectionAssert.Contains(evt.Columns.Cast<TraceColumn>().ToList(), TraceColumn.ApplicationName);
        }

        [TestMethod]
        public void ClearSupportedTraceEventClassesDiscardsTheCachedColumns()
        {
            var connectionManager = new ConnectionManager(Substitute.For<IEventAggregator>());
            var field = typeof(ConnectionManager).GetField("_supportedTraceEventClasses", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "the _supportedTraceEventClasses field could not be found");

            field.SetValue(connectionManager, new Dictionary<DaxStudioTraceEventClass, HashSet<TraceColumn>>
            {
                { DaxStudioTraceEventClass.VertiPaqSEQueryBegin, new HashSet<TraceColumn> { TraceColumn.ApplicationName } }
            });

            connectionManager.ClearSupportedTraceEventClasses();

            Assert.IsNull(field.GetValue(connectionManager), "the cached trace event columns should be cleared so that they get re-discovered for the new connection");
        }

        [TestMethod]
        public void ClearConnectionCachesDiscardsTheServerSpecificMetadata()
        {
            var connectionManager = new ConnectionManager(Substitute.For<IEventAggregator>());
            var clearCaches = typeof(ConnectionManager).GetMethod("ClearConnectionCaches", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(clearCaches, "the ClearConnectionCaches method could not be found");

            var cachedFields = new[] { "_supportedTraceEventClasses", "_dynamicManagementViews", "_functionGroups" };
            foreach (var fieldName in cachedFields)
            {
                var field = typeof(ConnectionManager).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(field, $"the {fieldName} field could not be found");
                // any non-null value will do, we only care that it gets discarded
                field.SetValue(connectionManager, FormatterServices.GetUninitializedObject(field.FieldType));
            }

            clearCaches.Invoke(connectionManager, null);

            foreach (var fieldName in cachedFields)
            {
                var field = typeof(ConnectionManager).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNull(field.GetValue(connectionManager), $"{fieldName} should be cleared when connecting to a different server");
            }
        }
    }
}
