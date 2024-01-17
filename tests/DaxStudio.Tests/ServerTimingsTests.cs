using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.QueryTrace;
using DaxStudio.Tests.Helpers;
using DaxStudio.UI.Model;
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

            vm.AddTestEvent(TraceEventClass.VertiPaqSEQueryEnd, TraceEventSubclass.VertiPaqScan, new DateTime(2022, 7, 10, 1, 1, 1, 5), 20);  // 20
            vm.AddTestEvent(TraceEventClass.VertiPaqSEQueryEnd, TraceEventSubclass.VertiPaqScan, new DateTime(2022, 7, 10, 1, 1, 1, 40), 15); // +15
            vm.AddTestEvent(TraceEventClass.QueryEnd, TraceEventSubclass.DAXQuery, new DateTime(2022, 7, 10, 1, 1, 1, 0), 75);
           
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

            vm.AddTestEvent(TraceEventClass.VertiPaqSEQueryEnd, TraceEventSubclass.VertiPaqScan, new DateTime(2022, 7, 10, 1, 1, 1, 5), 25);  // 20
            vm.AddTestEvent(TraceEventClass.VertiPaqSEQueryEnd, TraceEventSubclass.VertiPaqScan, new DateTime(2022, 7, 10, 1, 1, 1, 10), 10); // fully overlapped
            vm.AddTestEvent(TraceEventClass.VertiPaqSEQueryEnd, TraceEventSubclass.VertiPaqScan, new DateTime(2022, 7, 10, 1, 1, 1, 15), 15); // +5 partial overlap
            vm.AddTestEvent(TraceEventClass.VertiPaqSEQueryEnd, TraceEventSubclass.VertiPaqScan, new DateTime(2022, 7, 10, 1, 1, 1, 40), 15); // +15
            vm.AddTestEvent(TraceEventClass.QueryEnd, TraceEventSubclass.DAXQuery, new DateTime(2022, 7, 10, 1, 1, 1, 0), 75);

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

            vm.AddTestEvent(TraceEventClass.VertiPaqSEQueryEnd, TraceEventSubclass.VertiPaqScan, new DateTime(2022, 7, 10, 1, 1, 1, 5), 25);  // 25
            vm.AddTestEvent(TraceEventClass.VertiPaqSEQueryEnd, TraceEventSubclass.VertiPaqScan, new DateTime(2022, 7, 10, 1, 1, 1, 10), 10); // +0
            vm.AddTestEvent(TraceEventClass.VertiPaqSEQueryEnd, TraceEventSubclass.VertiPaqScan, new DateTime(2022, 7, 10, 1, 1, 1, 15), 20); // +10
            vm.AddTestEvent(TraceEventClass.VertiPaqSEQueryEnd, TraceEventSubclass.VertiPaqScan, new DateTime(2022, 7, 10, 1, 1, 1, 40), 20); // +20
            vm.AddTestEvent(TraceEventClass.QueryEnd, TraceEventSubclass.DAXQuery, new DateTime(2022, 7, 10, 1, 1, 1, 0), 75);

            vm.ProcessAllEvents();

            // assert overlaps are detected
            Assert.AreEqual(4, vm.StorageEngineEvents.Count);
            Assert.AreEqual(75, vm.TotalDuration);


            /// TODO - the total duration is wrong it should be 70 - the new calc is actually calculating the NetParallelDuration ///
            Assert.AreEqual(50, vm.StorageEngineDuration);
            // The test for StorageEngineNetParallelDuration is now obsolete
            Assert.AreEqual(50, vm.StorageEngineNetParallelDuration, "If this returns 60 it is double counting overlapped events");
            Assert.AreEqual(25, vm.FormulaEngineDuration, "There should be 5ms at the start, 10ms in the middle and 20ms at the end");
            Assert.AreEqual(true, vm.ParallelStorageEngineEventsDetected);
        }

        [TestMethod]
        public void TestHeatMap()
        {

            var details = new ServerTimingDetailsViewModel();

            var vm = new ServerTimesViewModel(mockEventAggregator, details, mockOptions, mockWindowManager);

            vm.AddTestEvent(TraceEventClass.VertiPaqSEQueryEnd, TraceEventSubclass.VertiPaqScan, new DateTime(2022, 7, 10, 1, 1, 1, 5), 20);
            vm.AddTestEvent(TraceEventClass.VertiPaqSEQueryEnd, TraceEventSubclass.VertiPaqScan, new DateTime(2022, 7, 10, 1, 1, 1, 40), 15);
            vm.AddTestEvent(TraceEventClass.QueryEnd, TraceEventSubclass.DAXQuery, new DateTime(2022, 7, 10, 1, 1, 1, 0), 75);
           
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

            var img = TimelineHeatmapImageGenerator.GenerateVector(vm.StorageEngineEvents.ToList(), 75, 10, feBrush, scanBrush, batchBrush, internalBrush);

            var rectangles = ((GeometryGroup)((GeometryDrawing)((DrawingGroup)img.Drawing).Children[1]).Geometry).Children;

            Assert.AreEqual(2, rectangles.Count, "Expected 2 scan rectangles");
            
            Assert.AreEqual( (5.0 / 76.0) * 75.0, ((RectangleGeometry)rectangles[0]).Rect.Left,"First rectangle left position");
            Assert.AreEqual((40.0 / 76.0) * 75.0, ((RectangleGeometry)rectangles[1]).Rect.Left,"Second rectangle left position");

            Assert.AreEqual((20.0 / 76.0) * 75.0, ((RectangleGeometry)rectangles[0]).Rect.Width, "First rectangle length");
            Assert.AreEqual((15.0 / 76.0) * 75.0, ((RectangleGeometry)rectangles[1]).Rect.Width,"Second rectangle length");
        }

    }
}
