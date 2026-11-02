using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Utils.Intellisense;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace DaxStudio.Tests
{
    [TestClass]
    public class IntellisenseProviderFactoryTests
    {
        [TestMethod]
        public void Create_WhenNewDaxParserEnabled_ReturnsAntlrProvider()
        {
            var doc = Substitute.For<IDaxDocument>();
            var eventAggregator = Substitute.For<IEventAggregator>();
            var options = Substitute.For<IGlobalOptions>();
            options.UseNewDaxParser.Returns(true);

            var provider = IntellisenseProviderFactory.Create(doc, eventAggregator, options);

            Assert.IsInstanceOfType(provider, typeof(AntlrIntellisenseProvider));
        }

        [TestMethod]
        public void Create_WhenNewDaxParserDisabled_ReturnsRegexProvider()
        {
            var doc = Substitute.For<IDaxDocument>();
            var eventAggregator = Substitute.For<IEventAggregator>();
            var options = Substitute.For<IGlobalOptions>();
            options.UseNewDaxParser.Returns(false);

            var provider = IntellisenseProviderFactory.Create(doc, eventAggregator, options);

            Assert.IsInstanceOfType(provider, typeof(DaxIntellisenseProvider));
        }
    }
}
