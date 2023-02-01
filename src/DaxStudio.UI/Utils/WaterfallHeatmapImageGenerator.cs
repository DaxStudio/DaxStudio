using DaxStudio.UI.ViewModels;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Rebar;

namespace DaxStudio.UI.Utils
{
    public static class WaterfallHeatmapImageGenerator
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
            if (e.WaterfallDuration == null) return 0;
            return (((double)(e.StartOffsetMs ?? 0)) / ((double)(e.WaterfallDuration ?? 1))) * totalWidth;
        }

        private static double CalculateWidth(TraceStorageEngineEvent e, double totalWidth)
        {
            if (e.DisplayDuration == null) return 0;
            var dur = (((double)(e.DisplayDuration ?? 0)) / ((double)(e.WaterfallDuration ?? 1))) * totalWidth;
            return dur >= MinWidth?dur:MinWidth;
        }

        
        public static ImageSource GenerateBitmap(List<TraceStorageEngineEvent> events, double totalWidth, double height, Brush feBrush, Brush scanBrush, Brush batchBrush, Brush internalBrush)
        {

            
            // create a visual and a drawing context
            DrawingVisual drawingVisual = new DrawingVisual();
            using (DrawingContext drawingContext = drawingVisual.RenderOpen())
            {
                // draw the FE background
                var background = new Rect(0, 0, totalWidth, height);
                drawingContext.DrawRectangle(feBrush, null, background);

                // draw the SE events
                foreach (var evt in events)
                {
                    var x = CalculateStart(evt, totalWidth);
                    var width = CalculateWidth(evt, totalWidth);
                    var rect = new Rect(x, 0, width, height);
                    var rectGeom = new RectangleGeometry(rect);

                    if (evt.IsBatchEvent)
                    {
                        drawingContext.DrawRectangle(batchBrush, null, rect);
                    }
                    else if (evt.IsInternalEvent)
                    {
                        drawingContext.DrawRectangle(internalBrush, null, rect);
                    }
                    else
                    {
                        drawingContext.DrawRectangle(scanBrush, null, rect);
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
