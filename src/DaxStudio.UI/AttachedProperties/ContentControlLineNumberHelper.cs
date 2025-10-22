using DaxStudio.UI.Controls;
using DaxStudio.UI.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;

namespace DaxStudio.UI.AttachedProperties
{
    public class ContentControlLineNumberHelper
    {
        // Dictionary to store debounce timers for each control
        private static readonly Dictionary<object, DispatcherTimer> _debounceTimers = new Dictionary<object, DispatcherTimer>();
        private static readonly Dictionary<object, LineNumberCache> _lineNumberCaches = new Dictionary<object, LineNumberCache>();
        private static readonly TimeSpan _debounceDelay = TimeSpan.FromMilliseconds(50);

        // Cache to avoid repeated string allocations
        private class LineNumberCache
        {
            public int LastLineCount { get; set; } = -1;
            public string CachedLineNumbers { get; set; } = string.Empty;
            public StringBuilder StringBuilder { get; } = new StringBuilder(1000); // Pre-allocated, reusable
        }

        #region BindableLineCount AttachedProperty
        public static string GetBindableLineCount(DependencyObject obj)
        {
            return (string)obj.GetValue(BindableLineCountProperty);
        }

        public static void SetBindableLineCount(DependencyObject obj, string value)
        {
            obj.SetValue(BindableLineCountProperty, value);
        }

        // Using a DependencyProperty as the backing store for BindableLineCount.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty BindableLineCountProperty =
            DependencyProperty.RegisterAttached(
            "BindableLineCount",
            typeof(string),
            typeof(ContentControlLineNumberHelper),
            new UIPropertyMetadata("1"));

        #endregion // BindableLineCount AttachedProperty

        #region HasBindableLineCount AttachedProperty
        public static bool GetHasBindableLineCount(DependencyObject obj)
        {
            return (bool)obj.GetValue(HasBindableLineCountProperty);
        }

        public static void SetHasBindableLineCount(DependencyObject obj, bool value)
        {
            obj.SetValue(HasBindableLineCountProperty, value);
        }

        // Using a DependencyProperty as the backing store for HasBindableLineCount.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty HasBindableLineCountProperty =
            DependencyProperty.RegisterAttached(
            "HasBindableLineCount",
            typeof(bool),
            typeof(ContentControlLineNumberHelper),
            new UIPropertyMetadata(
                false,
                new PropertyChangedCallback(OnHasBindableLineCountChanged)));

        private static void OnHasBindableLineCountChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            var textBox = (DaxStudio.UI.Controls.BindableRichTextBox)o;
            if ((e.NewValue as bool?) == true)
            {
                // Initialize cache for this control
                if (!_lineNumberCaches.ContainsKey(textBox))
                {
                    _lineNumberCaches[textBox] = new LineNumberCache();
                }

                textBox.SizeChanged += new SizeChangedEventHandler(box_SizeChanged);
                textBox.TextChanged += box_TextChanged;
                
                // Initial line count calculation
                var lineCount = EstimateLineCount(textBox);
                UpdateLineNumbersOptimized(textBox, lineCount);
            }
            else
            {
                textBox.SizeChanged -= box_SizeChanged;
                textBox.TextChanged -= box_TextChanged;
                
                // Clean up resources when removing the behavior
                CleanupDebounceTimer(textBox);
                CleanupCache(textBox);
            }
        }

        private static void box_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateLineNumbersDebounced(sender);
        }

        static void box_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateLineNumbersDebounced(sender);
        }

        private static void UpdateLineNumbersDebounced(object sender)
        {
            // Get or create a debounce timer for this control
            if (!_debounceTimers.TryGetValue(sender, out DispatcherTimer timer))
            {
                timer = new DispatcherTimer
                {
                    Interval = _debounceDelay
                };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    UpdateLineNumbers(sender);
                };
                _debounceTimers[sender] = timer;
            }

            // Reset the timer - this will cancel any pending execution and start fresh
            timer.Stop();
            timer.Start();
        }

        private const double _approxLineHeight = 16.3; // Approximate line height in pixels
        private static int EstimateLineCount(BindableRichTextBox textBox)
        {
            return Convert.ToInt32(Math.Round(textBox.DesiredSize.Height / _approxLineHeight));
        }

        private static void UpdateLineNumbers(object sender)
        {
            var textBox = (Controls.BindableRichTextBox)sender;
            //var lineCount = FlowDocumentHelper.CountLines(textBox.Document);
            var lineCount = EstimateLineCount(textBox);
            UpdateLineNumbersOptimized(textBox, lineCount);
        }

        private static void UpdateLineNumbersOptimized(Controls.BindableRichTextBox textBox, int lineCount)
        {
            if (!_lineNumberCaches.TryGetValue(textBox, out var cache))
            {
                cache = new LineNumberCache();
                _lineNumberCaches[textBox] = cache;
            }

            // Only regenerate the string if the line count has changed
            if (cache.LastLineCount != lineCount)
            {
                cache.LastLineCount = lineCount;
                cache.CachedLineNumbers = GenerateLineNumbersOptimized(cache.StringBuilder, lineCount);
            }

            // Set the cached value (avoids string allocation if unchanged)
            textBox.SetValue(BindableLineCountProperty, cache.CachedLineNumbers);
        }

        private static string GenerateLineNumbersOptimized(StringBuilder sb, int lineCount)
        {
            // Clear and reuse the existing StringBuilder to avoid allocations
            sb.Clear();
            
            // Pre-calculate capacity to avoid internal resizing
            var estimatedCapacity = EstimateCapacity(lineCount);
            if (sb.Capacity < estimatedCapacity)
            {
                sb.Capacity = estimatedCapacity;
            }

            // Generate line numbers more efficiently
            for (int i = 1; i <= lineCount; i++)
            {
                AppendNumberOptimized(sb, i);
                if (i < lineCount) // Don't add newline after the last line number
                    sb.Append('\n');
            }

            return sb.ToString();
        }

        private static int EstimateCapacity(int lineCount)
        {
            // Estimate string capacity based on line count to avoid StringBuilder resizing
            if (lineCount <= 9) return lineCount * 2; // Single digit + newline
            if (lineCount <= 99) return lineCount * 3; // Two digits + newline
            if (lineCount <= 999) return lineCount * 4; // Three digits + newline
            if (lineCount <= 9999) return lineCount * 5; // Four digits + newline
            return lineCount * 6; // Five+ digits + newline
        }

        private static void AppendNumberOptimized(StringBuilder sb, int number)
        {
            // Optimized number-to-string conversion to avoid boxing/ToString() allocations
            if (number == 0)
            {
                sb.Append('0');
                return;
            }

            // Handle negative numbers (shouldn't happen for line numbers, but just in case)
            if (number < 0)
            {
                sb.Append('-');
                number = -number;
            }

            // Convert number to digits without allocating a string
            var startPos = sb.Length;
            while (number > 0)
            {
                sb.Append((char)('0' + (number % 10)));
                number /= 10;
            }

            // Reverse the digits in place (they were added backwards)
            var endPos = sb.Length - 1;
            while (startPos < endPos)
            {
                var temp = sb[startPos];
                sb[startPos] = sb[endPos];
                sb[endPos] = temp;
                startPos++;
                endPos--;
            }
        }

        private static void CleanupDebounceTimer(object sender)
        {
            if (_debounceTimers.TryGetValue(sender, out DispatcherTimer timer))
            {
                timer.Stop();
                _debounceTimers.Remove(sender);
            }
        }

        private static void CleanupCache(object sender)
        {
            if (_lineNumberCaches.TryGetValue(sender, out var cache))
            {
                cache.StringBuilder.Clear(); // Help GC by clearing the internal buffer
                _lineNumberCaches.Remove(sender);
            }
        }

        #endregion // HasBindableLineCount AttachedProperty
    }
}