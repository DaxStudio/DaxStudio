using System.Windows;
using System.Windows.Documents;

namespace DaxStudio.UI.Utils
{
    internal static class FlowDocumentHelper
    {

        public static int CountLines(FlowDocument flowDocument)
        {
            if (flowDocument == null)
                return 0;

            // Create a TextPointer at the start of the document
            TextPointer pointer = flowDocument.Blocks.FirstBlock.ContentStart;
            int lineCount = 0;

            while (pointer != null && pointer.CompareTo(flowDocument.Blocks.FirstBlock.ContentEnd) < 0)
            {
                // Increment the line count
                lineCount++;
                // Move to the next line
                pointer = pointer.GetLineStartPosition(1);
            }

            return lineCount;
        }

    }
}
