using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Utils.Intellisense
{
    public interface IInsightProvider
    {
        void ShowInsight(string text);

        /// <summary>
        /// Builds a <c>--&gt; ASSERT TABLE</c> continuation block (the <c>--&gt;&gt; | ... |</c> rows,
        /// prefixed with a newline so it starts on its own line) from the document's current query
        /// results. Returns a small placeholder template when no results are available.
        /// </summary>
        string GetTableAssertionFromResults();
    }
}
