using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using AvalonDock.Controls;
using DaxStudio.UI.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.Tests
{
    [TestClass]
    public class DocumentTabStripTests
    {
        [DataTestMethod]
        [DataRow(400.0, 2, 200.0)]
        [DataRow(228.0, 2, 200.0)]
        [DataRow(220.0, 2, 100.0)]
        [DataRow(100.0, 2, 0.0)]
        [DataRow(400.0, 0, 0.0)]
        public void ButtonFollowsLastVisibleTab(double width, int tabCount, double expectedLeft)
        {
            RunOnStaThread(() =>
            {
                var strip = CreateStrip(tabCount, out var tabs, out var button);
                Layout(strip, width);

                var buttonSlot = LayoutInformation.GetLayoutSlot(button);
                Assert.AreEqual(expectedLeft, buttonSlot.Left, 0.01);
                Assert.IsTrue(buttonSlot.Right <= width);
            });
        }

        [TestMethod]
        public void ResizingRestoresOverflowTabs()
        {
            RunOnStaThread(() =>
            {
                var strip = CreateStrip(3, out var tabs, out var button);
                Layout(strip, 220);
                Assert.AreEqual(Visibility.Hidden, tabs.Children[1].Visibility);
                Assert.AreEqual(100.0, LayoutInformation.GetLayoutSlot(button).Left, 0.01);

                Layout(strip, 400);
                Assert.AreEqual(Visibility.Visible, tabs.Children[2].Visibility);
                Assert.AreEqual(300.0, LayoutInformation.GetLayoutSlot(button).Left, 0.01);
            });
        }

        [TestMethod]
        public void ClosingTabMovesButtonLeft()
        {
            RunOnStaThread(() =>
            {
                var strip = CreateStrip(3, out var tabs, out var button);
                Layout(strip, 400);
                tabs.Children.RemoveAt(1);
                Layout(strip, 400);

                Assert.AreEqual(200.0, LayoutInformation.GetLayoutSlot(button).Left, 0.01);
            });
        }

        [TestMethod]
        public void TabMarginsAreIncludedInButtonPosition()
        {
            RunOnStaThread(() =>
            {
                var strip = CreateStrip(2, out var tabs, out var button);
                ((FrameworkElement)tabs.Children[0]).Margin = new Thickness(3, 0, 7, 0);
                Layout(strip, 400);

                Assert.AreEqual(210.0, LayoutInformation.GetLayoutSlot(button).Left, 0.01);
            });
        }

        [TestMethod]
        public void RenamingTabMovesButton()
        {
            RunOnStaThread(() =>
            {
                var strip = CreateStrip(2, out var tabs, out var button);
                Layout(strip, 400);
                ((FrameworkElement)tabs.Children[0]).Width = 150;
                Layout(strip, 400);

                Assert.AreEqual(250.0, LayoutInformation.GetLayoutSlot(button).Left, 0.01);
            });
        }

        [TestMethod]
        public void RenamingOverflowTabMovesButton()
        {
            RunOnStaThread(() =>
            {
                var strip = CreateStrip(3, out var tabs, out var button);
                Layout(strip, 250);
                Assert.AreEqual(200.0, LayoutInformation.GetLayoutSlot(button).Left, 0.01);

                ((FrameworkElement)tabs.Children[0]).Width = 150;
                Layout(strip, 250);

                Assert.AreEqual(150.0, LayoutInformation.GetLayoutSlot(button).Left, 0.01);
            });
        }

        private static DocumentTabStrip CreateStrip(int tabCount, out DocumentPaneTabPanel tabs, out Button button)
        {
            tabs = new DocumentTabPanel();
            for (var i = 0; i < tabCount; i++)
                tabs.Children.Add(new TabItem { Width = 100, Height = 24 });

            button = new Button { Width = 24, Height = 24, Margin = new Thickness(4, 0, 0, 0) };
            var strip = new DocumentTabStrip();
            strip.Children.Add(tabs);
            strip.Children.Add(button);
            return strip;
        }

        private static void Layout(DocumentTabStrip strip, double width)
        {
            strip.Measure(new Size(width, 30));
            strip.Arrange(new Rect(0, 0, width, 30));
            strip.UpdateLayout();
        }

        private static void RunOnStaThread(Action action)
        {
            ExceptionDispatchInfo failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    failure = ExceptionDispatchInfo.Capture(ex);
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            failure?.Throw();
        }
    }
}
