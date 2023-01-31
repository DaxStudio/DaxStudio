using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.QueryTrace;
using DaxStudio.UI.Utils;
using DaxStudio.UI.ViewModels;
using Microsoft.AnalysisServices;
using Microsoft.AnalysisServices.Tabular;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Xml;
using amo = Microsoft.AnalysisServices;


namespace DaxStudio.Tests
{
    [TestClass]
    public class ServerTimingsTests
    {
        private IGlobalOptions mockOptions;
        private IEventAggregator mockEventAggregator;
        private IWindowManager mockWindowManager;

        [TestInitialize]
        public void TestSetup()
        {
            mockOptions = new Mock<IGlobalOptions>().Object;
            mockEventAggregator = new Mocks.MockEventAggregator();
            mockWindowManager = new Mock<IWindowManager>().Object;
        }

        [TestMethod]
        public void TestNonOverlappingSEQueries()
        {

            var details = new ServerTimingDetailsViewModel();

            var vm = new ServerTimesViewModel(mockEventAggregator, details, mockOptions, mockWindowManager);

            var e1 = CreateDSVertipaqSEEvent(1, new DateTime(2022, 7, 10, 1, 1, 1, 5), new DateTime(2022, 7, 10, 1, 1, 1, 25)); // 20
            var e2 = CreateDSVertipaqSEEvent(4, new DateTime(2022, 7, 10, 1, 1, 1, 40), new DateTime(2022, 7, 10, 1, 1, 1, 55)); // +15
            var e3 = CreateDSQueryEndEvent(5, new DateTime(2022, 7, 10, 1, 1, 1, 0), new DateTime(2022, 7, 10, 1, 1, 1, 75));
            vm.Events.Enqueue(e1);
            vm.Events.Enqueue(e2);
            vm.Events.Enqueue(e3);
            
            vm.ProcessAllEvents();

            // assert overlaps are detected
            Assert.AreEqual(2, vm.StorageEngineEvents.Count);
            Assert.AreEqual(75, vm.TotalDuration);
            Assert.AreEqual(35, vm.StorageEngineDuration);
            Assert.AreEqual(40, vm.FormulaEngineDuration);
        }

        [TestMethod]
        public void TestOverlappingSEQueries()
        {

            var details = new ServerTimingDetailsViewModel();

            var vm = new ServerTimesViewModel(mockEventAggregator, details, mockOptions, mockWindowManager);

            var e1 = CreateDSVertipaqSEEvent(1,new DateTime(2022, 7, 10, 1, 1, 1, 5), new DateTime(2022, 7, 10, 1, 1, 1, 25));  // 20
            var e2 = CreateDSVertipaqSEEvent(2,new DateTime(2022, 7, 10, 1, 1, 1, 10), new DateTime(2022, 7, 10, 1, 1, 1, 20)); // fully overlapped
            var e3 = CreateDSVertipaqSEEvent(3,new DateTime(2022, 7, 10, 1, 1, 1, 15), new DateTime(2022, 7, 10, 1, 1, 1, 30)); // +5 partial overlap
            var e4 = CreateDSVertipaqSEEvent(4,new DateTime(2022, 7, 10, 1, 1, 1, 40), new DateTime(2022, 7, 10, 1, 1, 1, 55)); // +15
            var e5 = CreateDSQueryEndEvent(5,new DateTime(2022, 7, 10, 1, 1, 1, 0), new DateTime(2022, 7, 10, 1, 1, 1, 75));
            vm.Events.Enqueue(e1);
            vm.Events.Enqueue(e2);
            vm.Events.Enqueue(e3);
            vm.Events.Enqueue(e4);
            vm.Events.Enqueue(e5);

            vm.ProcessAllEvents();

            // assert overlaps are detected
            Assert.AreEqual(4, vm.StorageEngineEvents.Count);
            Assert.AreEqual(75, vm.TotalDuration);
            // The test for StorageEngineNetParallelDuration is now obsolete
            Assert.AreEqual(40, vm.StorageEngineDuration); 
            Assert.AreEqual(40, vm.StorageEngineNetParallelDuration, "If this returns 60 it is double counting overlapped events"); 
            Assert.AreEqual(35, vm.FormulaEngineDuration,"There should be 5ms at the start, 10ms in the middle and 20ms at the end");
        }

        [TestMethod]
        public void TestOverlappingDQ_SEQueries()
        {

            var details = new ServerTimingDetailsViewModel();

            var vm = new ServerTimesViewModel(mockEventAggregator, details, mockOptions,mockWindowManager);

            var e1 = CreateDSDQEvent(1, new DateTime(2022, 7, 10, 1, 1, 1, 5), new DateTime(2022, 7, 10, 1, 1, 1, 25));  // 20
            var e2 = CreateDSDQEvent(2, new DateTime(2022, 7, 10, 1, 1, 1, 10), new DateTime(2022, 7, 10, 1, 1, 1, 20)); // fully overlapped
            var e3 = CreateDSDQEvent(3, new DateTime(2022, 7, 10, 1, 1, 1, 15), new DateTime(2022, 7, 10, 1, 1, 1, 30)); // +5 partial overlap
            var e4 = CreateDSDQEvent(4, new DateTime(2022, 7, 10, 1, 1, 1, 40), new DateTime(2022, 7, 10, 1, 1, 1, 55)); // +15
            var e5 = CreateDSQueryEndEvent(5, new DateTime(2022, 7, 10, 1, 1, 1, 0), new DateTime(2022, 7, 10, 1, 1, 1, 75));
            vm.Events.Enqueue(e1);
            vm.Events.Enqueue(e2);
            vm.Events.Enqueue(e3);
            vm.Events.Enqueue(e4);
            vm.Events.Enqueue(e5);

            vm.ProcessAllEvents();

            // assert overlaps are detected
            Assert.AreEqual(4, vm.StorageEngineEvents.Count);
            Assert.AreEqual(75, vm.TotalDuration);
            Assert.AreEqual(40, vm.StorageEngineDuration);
            // The test for StorageEngineNetParallelDuration is now obsolete
            Assert.AreEqual(40, vm.StorageEngineNetParallelDuration, "If this returns 60 it is double counting overlapped events");
            Assert.AreEqual(35, vm.FormulaEngineDuration, "There should be 5ms at the start, 10ms in the middle and 20ms at the end");
            Assert.AreEqual(true, vm.ParallelStorageEngineEventsDetected);
        }

        private DaxStudioTraceEventArgs CreateDSVertipaqSEEvent(int sequence,DateTime startTime, DateTime endTime)
        {
            return new DaxStudioTraceEventArgs(TraceEventClass.VertiPaqSEQueryEnd.ToString()
                , TraceEventSubclass.VertiPaqScan.ToString()
                , (long)(endTime - startTime).TotalMilliseconds
                , 10
                , $"Test Event {sequence}"
                , ""
                , startTime)
            { EndTime = endTime};

        }
        private DaxStudioTraceEventArgs CreateDSDQEvent(int sequence, DateTime startTime, DateTime endTime)
        {
            return new DaxStudioTraceEventArgs(TraceEventClass.DirectQueryEnd.ToString()
                , TraceEventSubclass.NotAvailable.ToString()
                , (long)(endTime - startTime).TotalMilliseconds
                , 10
                , $"Test Event {sequence}"
                , ""
                , startTime)
            { EndTime = endTime };

        }

        private DaxStudioTraceEventArgs CreateDSQueryEndEvent(int sequence, DateTime startTime, DateTime endTime)
        {
            return new DaxStudioTraceEventArgs(TraceEventClass.QueryEnd.ToString()
                , TraceEventSubclass.DAXQuery.ToString()
                , (long)(endTime - startTime).TotalMilliseconds
                , 10
                , $"Test Query {sequence}"
                , ""
                , startTime)
            { EndTime = endTime };

        }

        [TestMethod]
        public void TestHeatMap()
        {

            var details = new ServerTimingDetailsViewModel();

            var vm = new ServerTimesViewModel(mockEventAggregator, details, mockOptions, mockWindowManager);

            var e1 = CreateDSVertipaqSEEvent(1, new DateTime(2022, 7, 10, 1, 1, 1, 5), new DateTime(2022, 7, 10, 1, 1, 1, 25)); // 20
            var e2 = CreateDSVertipaqSEEvent(4, new DateTime(2022, 7, 10, 1, 1, 1, 40), new DateTime(2022, 7, 10, 1, 1, 1, 55)); // +15
            var e3 = CreateDSQueryEndEvent(5, new DateTime(2022, 7, 10, 1, 1, 1, 0), new DateTime(2022, 7, 10, 1, 1, 1, 75));
            vm.Events.Enqueue(e1);
            vm.Events.Enqueue(e2);
            vm.Events.Enqueue(e3);

            vm.QueryStartDateTime = e3.StartTime;
            vm.QueryEndDateTime = e3.EndTime;
            
            vm.ProcessAllEvents();

            Assert.AreEqual(2, vm.StorageEngineEvents.Count);
            Assert.AreEqual(75, vm.TotalDuration);
            Assert.AreEqual(35, vm.StorageEngineDuration);
            Assert.AreEqual(40, vm.FormulaEngineDuration);

            // generate vector image
            var scanBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 0, 255));
            var batchBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 255, 255));
            var internalBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 255, 0));
            var feBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 0, 0));

            var img = WaterfallHeatmapImageGenerator.GenerateVector(vm.StorageEngineEvents.ToList(), 75, 10, feBrush, scanBrush, batchBrush, internalBrush);

            var rectangles = ((GeometryGroup)((GeometryDrawing)((DrawingGroup)img.Drawing).Children[1]).Geometry).Children;

            Assert.AreEqual(2, rectangles.Count, "Expected 2 scan rectangles");
            
            Assert.AreEqual( (5.0 / 76.0) * 75.0, ((RectangleGeometry)rectangles[0]).Rect.Left,"First rectangle left position");
            Assert.AreEqual((40.0 / 76.0) * 75.0, ((RectangleGeometry)rectangles[1]).Rect.Left,"Second rectangle left position");

            Assert.AreEqual((20.0 / 76.0) * 75.0, ((RectangleGeometry)rectangles[0]).Rect.Width, "First rectangle length");
            Assert.AreEqual((15.0 / 76.0) * 75.0, ((RectangleGeometry)rectangles[1]).Rect.Width,"Second rectangle length");
        }

    }
}
