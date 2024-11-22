using DaxStudio.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Rebar;

namespace DaxStudio.UI.Utils
{
    public static class TimelineHeatmapImageGenerator
    {
        const double MinWidth = 0.01;
        public static DrawingImage GenerateVector(List<TraceStorageEngineEvent> events, double totalWidth, double height, Brush feBrush, Brush scanBrush, Brush batchBrush, Brush internalBrush)
        {

            var drawingGroup = new DrawingGroup();
            drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0, 0, totalWidth, height));
            var scanGeomGroup = new GeometryGroup();
            var batchGeomGroup = new GeometryGroup();
            var internalGeomGroup = new GeometryGroup();
            var feGeomGroup = new GeometryGroup();

            var background = new Rect(0, 0, totalWidth, height);
            var rectBackground = new RectangleGeometry(background);
            feGeomGroup.Children.Add(rectBackground);
            drawingGroup.Children.Add(new GeometryDrawing(feBrush, null, feGeomGroup));

            foreach (var evt in events)
            {
                var x = CalculateStart(evt, totalWidth);
                var width = CalculateWidth(evt, totalWidth);
                var rect = new Rect(x, 0, width, height);
                var rectGeom = new RectangleGeometry(rect);
                if (evt.IsBatchEvent)
                {
                    batchGeomGroup.Children.Add(rectGeom);
                }
                else if (evt.IsInternalEvent)
                {
                    internalGeomGroup.Children.Add(rectGeom);
                }
                else
                {
                    scanGeomGroup.Children.Add(rectGeom);
                }
            }
            drawingGroup.Children.Add(new GeometryDrawing(scanBrush, null, scanGeomGroup));
            drawingGroup.Children.Add(new GeometryDrawing(batchBrush, null, batchGeomGroup));
            drawingGroup.Children.Add(new GeometryDrawing(internalBrush, null, internalGeomGroup));
            var img = new DrawingImage(drawingGroup);
            img.Freeze();
            return img;
        }


        private static double CalculateStart(TraceStorageEngineEvent e, double totalWidth)
        {
            if (e.TimelineDuration == null) return 0;
            return (((double)(e.StartOffsetMs ?? 0)) / ((double)(e.TimelineDuration ?? 1))) * totalWidth;
        }

        private static double CalculateWidth(TraceStorageEngineEvent e, double totalWidth)
        {
            if (e.DisplayDuration == null) return 0;
            var dur = (((double)(e.DisplayDuration ?? 0)) / ((double)(e.TimelineDuration ?? 1))) * totalWidth;
            return dur >= MinWidth?dur:MinWidth;
        }

        private static List<TraceStorageEngineEvent> AdjustEvents(List<TraceStorageEngineEvent> events)
        {
            DateTime? ScanStartTime = null;
            DateTime? ScanEndTime = null;
            DateTime? BatchStartTime = null;
            DateTime? BatchEndTime = null;
            // TODO: should we copy the events in order to preserve the original data?
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                if (e.IsInternalEvent)
                {
                    // Set boundaries of Scan
                    ScanStartTime = ScanStartTime.HasValue
                        ? (e.StartTime < ScanStartTime ? e.StartTime : ScanStartTime)
                        : e.StartTime;
                    ScanEndTime = ScanEndTime.HasValue
                        ? (e.EndTime > ScanEndTime ? e.EndTime : ScanEndTime)
                        : e.EndTime;
                }
                else if (e.IsBatchEvent)
                {
                    // Apply boundaries of Batch
                    e.StartTime = BatchStartTime.HasValue && (BatchStartTime.Value < e.StartTime)
                        ? BatchStartTime.Value
                        : e.StartTime;
                    e.EndTime = BatchEndTime.HasValue && (BatchEndTime.Value > e.EndTime)
                        ? BatchEndTime.Value
                        : e.EndTime;

                    BatchStartTime = null;
                    BatchEndTime = null;
                    ScanStartTime = null;
                    ScanEndTime = null;
                }
                else // e.InternalBatchEvent and regular scan
                {
                    // Apply boundaries of Scan
                    e.StartTime = ScanStartTime.HasValue && (ScanStartTime.Value < e.StartTime)
                        ? ScanStartTime.Value
                        : e.StartTime;
                    e.EndTime = ScanEndTime.HasValue && (ScanEndTime.Value > e.EndTime)
                           ? ScanEndTime.Value
                           : e.EndTime;

                    ScanStartTime = null;
                    ScanEndTime = null;

                    // Set boundaries of Batch
                    BatchStartTime = BatchStartTime.HasValue
                        ? (e.StartTime < BatchStartTime ? e.StartTime : BatchStartTime)
                        : e.StartTime;
                    BatchEndTime = BatchEndTime.HasValue
                        ? (e.EndTime > BatchEndTime ? e.EndTime : BatchEndTime)
                        : e.EndTime;
                }
            }
            return events;
        }

        public static ImageSource GenerateBitmap(List<TraceStorageEngineEvent> events, double totalWidth, double height, Brush feBrush, Brush scanBrush, Brush batchBrush, Brush internalBrush)
        {
            // Adjust events
            //events = AdjustEvents(events);

            // create a visual and a drawing context
            DrawingVisual drawingVisual = new DrawingVisual();
            using (DrawingContext drawingContext = drawingVisual.RenderOpen())
            {
                // draw the FE background
                var background = new Rect(0, 0, totalWidth, height);
                drawingContext.DrawRectangle(feBrush, null, background);

                // draw the SE events
                DrawEvents(events.Where(e => e.IsScanEvent && e.IsBatchEvent), batchBrush);
                // Note: the internalBrush is probably useless as it should be 
                //       completely overwritten by the following scanBrush
                DrawEvents(events.Where(e => e.IsScanEvent && !e.IsBatchEvent && e.IsInternalEvent), internalBrush);
                DrawEvents(events.Where(e => e.IsScanEvent && !e.IsBatchEvent && !e.IsInternalEvent), scanBrush);

                void DrawEvents (IEnumerable<TraceStorageEngineEvent> drawEvents, Brush brush)
                {
                    // draw the SE events
                    foreach (var evt in drawEvents)
                    {
                        var x = CalculateStart(evt, totalWidth);
                        var width = CalculateWidth(evt, totalWidth);
                        var rect = new Rect(x, 0, width, height);
                        var rectGeom = new RectangleGeometry(rect);
                        drawingContext.DrawRectangle(brush, null, rect);
                    }

                }
            }

            // render the visual on a bitmap
            var bmp = new RenderTargetBitmap(
                pixelWidth: (int)totalWidth,
                pixelHeight: (int)height,
                dpiX: 0, dpiY: 0, pixelFormat: PixelFormats.Pbgra32);
            bmp.Render(drawingVisual);

            return bmp;
        }
            
    }
}
