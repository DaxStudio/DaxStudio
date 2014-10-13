using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;


namespace DaxStudio.Tests
{
    [TestClass]
    public class HelpAboutTests
    {
        [TestMethod]
        public void GetReferencedAssemblies()
        {
            Caliburn.Micro.IEventAggregator stub = new Caliburn.Micro.EventAggregator();
            var hlp = new DaxStudio.UI.ViewModels.HelpAboutViewModel(stub);
            var ra = hlp.ReferencedAssemblies;
            Assert.AreEqual(30, ra.Count);
        }
        [TestMethod]
        public void GetAssemblyList()
        {
            Caliburn.Micro.IEventAggregator stub = new Caliburn.Micro.EventAggregator();
            var hlp = new DaxStudio.UI.ViewModels.HelpAboutViewModel(stub);
            var ra = hlp.ReferencedAssemblies;
            foreach (var a in ra)
            {
                System.Diagnostics.Debug.WriteLine(a.Key);
            }

            Assert.IsTrue( ra["DaxStudio.Interfaces"].Length > 0);
        }
    }
}
