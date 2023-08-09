using DaxStudio.Tests.Mocks;
using DaxStudio.UI.Model;
using ICSharpCode.AvalonEdit.Folding;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Tests
{
    [TestClass]
    public class FoldingTests
    {

        [TestMethod]
        public void BasicFoldingTest() {
            var qry =     "11111111" + Environment.NewLine
                        + " 2222222" + Environment.NewLine
                        + "  333333" + Environment.NewLine
                        + " 4444444" + Environment.NewLine
                        + "55555555" + Environment.NewLine;

            var doc = new ICSharpCode.AvalonEdit.Document.TextDocument(qry);  
            var foldingStrategy = new IndentFoldingStrategy();
            var foldings = foldingStrategy.CreateNewFoldings(doc);

            Assert.AreEqual(2, foldings.Count());

        }

        [TestMethod]
        public void DoubleUnindentFoldingTest()
        {
            var qry = "11111111" + Environment.NewLine
                    + " 2222222" + Environment.NewLine
                    + "  333333" + Environment.NewLine
                    + "  444444" + Environment.NewLine
                    + "55555555" + Environment.NewLine;

            var doc = new ICSharpCode.AvalonEdit.Document.TextDocument(qry);
            var foldingStrategy = new IndentFoldingStrategy();
            var foldings = foldingStrategy.CreateNewFoldings(doc);

            Assert.AreEqual(2, foldings.Count());

        }

        [TestMethod]
        public void TripleFoldingTest()
        {
            var qry = "11111111" + Environment.NewLine
                    + " 2222222" + Environment.NewLine
                    + "  333333" + Environment.NewLine
                    + "  444444" + Environment.NewLine
                    + " 5555555" + Environment.NewLine
                    + "  666666" + Environment.NewLine
                    +"777777777" + Environment.NewLine;

            var doc = new ICSharpCode.AvalonEdit.Document.TextDocument(qry);
            var foldingStrategy = new IndentFoldingStrategy();
            var foldings = foldingStrategy.CreateNewFoldings(doc);

            Assert.AreEqual(3, foldings.Count());
            var foldingArray = foldings.ToArray();
            Assert.AreEqual(8, foldingArray[0].StartOffset);
            Assert.AreEqual(58, foldingArray[0].EndOffset);
            Assert.AreEqual(18, foldingArray[1].StartOffset);
            Assert.AreEqual(38, foldingArray[1].EndOffset);
            Assert.AreEqual(48, foldingArray[2].StartOffset);
            Assert.AreEqual(58, foldingArray[2].EndOffset);
        }

    }
}
