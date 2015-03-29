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
            Caliburn.Micro.IEventAggregator stubEventAgg = new Caliburn.Micro.EventAggregator();
            var stubVerChk = new VersionCheckMock();
            var hlp = new DaxStudio.UI.ViewModels.HelpAboutViewModel(stubEventAgg,stubVerChk );
            var ra = hlp.ReferencedAssemblies;
            Assert.IsTrue(ra.Count >= 32);
        }
        [TestMethod]
        public void GetAssemblyList()
        {
            Caliburn.Micro.IEventAggregator stubEventAgg = new Caliburn.Micro.EventAggregator();
            var stubVerChk = new VersionCheckMock();
            var hlp = new DaxStudio.UI.ViewModels.HelpAboutViewModel(stubEventAgg,stubVerChk);
            var ra = hlp.ReferencedAssemblies;
            foreach (var a in ra)
            {
                System.Diagnostics.Debug.WriteLine(a.Key);
            }

            Assert.IsTrue( ra["DaxStudio.Interfaces"].Length > 0);
        }
    }

    public class VersionCheckMock: DaxStudio.Interfaces.IVersionCheck
    {
        public void CheckVersion()
        {
            throw new NotImplementedException();
        }

        public Version DismissedVersion
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public DateTime LastVersionCheck
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public Version ServerVersion
        {
            get { throw new NotImplementedException(); }
        }

        public Version LocalVersion
        {
            get { throw new NotImplementedException(); }
        }

        public bool VersionIsLatest
        {
            get { throw new NotImplementedException(); }
        }

        public string VersionStatus
        {
            get { throw new NotImplementedException(); }
        }

        public void Update()
        {
            throw new NotImplementedException();
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    }
}
