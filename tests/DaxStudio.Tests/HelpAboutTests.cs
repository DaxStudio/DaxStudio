using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DaxStudio.Interfaces;
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
            var stubVerChk = new VersionCheckMock();
            using (var stubHost = new Mocks.MockDaxStudioHost())
            {
                var mockOptions = Substitute.For<IGlobalOptions>();
                var hlp = new DaxStudio.UI.ViewModels.HelpAboutViewModel(stubEventAgg, stubVerChk, stubHost, mockOptions);
                var ra = hlp.ReferencedAssemblies;
                Assert.IsGreaterThanOrEqualTo(32, ra.Count);
            }
        }
        [TestMethod]
        public void GetAssemblyList()
        {
            Caliburn.Micro.IEventAggregator stubEventAgg = new Caliburn.Micro.EventAggregator();
            var stubVerChk = new VersionCheckMock();
            using (var stubHost = new Mocks.MockDaxStudioHost())
            {
                var mockOptions = Substitute.For<IGlobalOptions>();
                var hlp = new DaxStudio.UI.ViewModels.HelpAboutViewModel(stubEventAgg, stubVerChk, stubHost, mockOptions);
                var ra = hlp.ReferencedAssemblies;
                foreach (var a in ra)
                {
                    System.Diagnostics.Debug.WriteLine(a.Key);
                }

                Assert.IsGreaterThan(0, ra["DaxStudio.Interfaces"].Length);
            }
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

        public Uri DownloadUrl
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public void Update()
        {
            throw new NotImplementedException();
        }
#pragma warning disable 0067
        // required for implementing the interface, but not used for these tests
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        public event EventHandler UpdateCompleteCallback;
        public event EventHandler UpdateStartingCallback;
#pragma warning restore 0067
    }
}
