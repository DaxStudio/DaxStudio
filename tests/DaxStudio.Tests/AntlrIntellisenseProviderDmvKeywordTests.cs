using System.Linq;
using DaxStudio.Parsers.Metadata;
using DaxStudio.UI.Utils.Intellisense;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.Tests
{
    // Regression tests for the DMV/SQL keyword completions (SELECT/FROM/WHERE/$SYSTEM). These
    // keywords must appear both at the very start of a statement and while the user is typing the
    // first word - typing a character moves the parser from TopLevel to Identifier, which used to
    // hide the keywords.
    [TestClass]
    public class AntlrIntellisenseProviderDmvKeywordTests
    {
        [TestMethod]
        public void DmvKeywords_OfferedAtTopLevel()
        {
            Assert.IsTrue(AntlrIntellisenseProvider.ShouldOfferDmvKeywords(EditState.TopLevel));
        }

        [TestMethod]
        public void DmvKeywords_OfferedWhileTypingIdentifier()
        {
            // Typing the first character of "SELECT" puts the parser in the Identifier state; the
            // DMV keywords must still be offered here (this was the regression).
            Assert.IsTrue(AntlrIntellisenseProvider.ShouldOfferDmvKeywords(EditState.Identifier));
        }

        [TestMethod]
        public void DmvKeywords_NotOfferedInOtherContexts()
        {
            Assert.IsFalse(AntlrIntellisenseProvider.ShouldOfferDmvKeywords(EditState.DefineContext));
            Assert.IsFalse(AntlrIntellisenseProvider.ShouldOfferDmvKeywords(EditState.EvaluateContext));
            Assert.IsFalse(AntlrIntellisenseProvider.ShouldOfferDmvKeywords(EditState.FunctionArgument));
        }

        [TestMethod]
        public void DmvKeywords_ContainExpectedKeywords()
        {
            var labels = AntlrIntellisenseProvider._dmvKeywords.Select(k => k.Label).ToList();
            CollectionAssert.Contains(labels, "$SYSTEM");
            CollectionAssert.Contains(labels, "SELECT");
            CollectionAssert.Contains(labels, "FROM");
            CollectionAssert.Contains(labels, "WHERE");
        }
    }
}
