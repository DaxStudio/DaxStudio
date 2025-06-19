using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DaxStudio.Interfaces;
using DaxStudio.UI.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace DaxStudio.Tests
{
    [TestClass]
    public class HelpAboutTests
    {
        [TestMethod]
        public void GetReferencedAssemblies()
        {
            Caliburn.Micro.IEventAggregator stubEventAgg = new Caliburn.Micro.EventAggregator();
            var stubVerChk = Substitute.For<IVersionCheck>();
            using (var stubHost = Substitute.For<IDaxStudioHost>())
            {
                var mockOptions = Substitute.For<IGlobalOptions>();
                var hlp = new DaxStudio.UI.ViewModels.HelpAboutViewModel(stubEventAgg, stubVerChk, stubHost, mockOptions);
                var ra = hlp.ReferencedAssemblies;
                Assert.IsTrue(ra.Count >= 32);
            }
        }
        [TestMethod]
        public void GetAssemblyList()
        {
            Caliburn.Micro.IEventAggregator stubEventAgg = new Caliburn.Micro.EventAggregator();
            var stubVerChk = Substitute.For<IVersionCheck>();
            using (var stubHost = Substitute.For<IDaxStudioHost>())
            {
                var mockOptions = Substitute.For<IGlobalOptions>();
                var hlp = new DaxStudio.UI.ViewModels.HelpAboutViewModel(stubEventAgg, stubVerChk, stubHost, mockOptions);
                var ra = hlp.ReferencedAssemblies;
                foreach (var a in ra)
                {
                    System.Diagnostics.Debug.WriteLine(a.Key);
                }

                Assert.IsTrue(ra["DaxStudio.Interfaces"].Length > 0);
            }
        }
    }

}
