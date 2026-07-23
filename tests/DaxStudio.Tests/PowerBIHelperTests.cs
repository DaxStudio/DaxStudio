using System;
using System.Collections.Generic;
using System.Reflection;
using DaxStudio.Core.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.Tests
{
    /// <summary>
    /// Verifies the platform-agnostic <see cref="PowerBIHelper"/> façade delegates the raw scan to
    /// an injectable <see cref="IPowerBIInstanceScanner"/> (real WMI/Win32 scanner on Windows, a
    /// no-op stub on cross-platform builds) while retaining its caching/throttling behaviour.
    /// </summary>
    [TestClass]
    public class PowerBIHelperTests
    {
        private IPowerBIInstanceScanner _originalScanner;

        private sealed class FakeScanner : IPowerBIInstanceScanner
        {
            private readonly List<PowerBIInstance> _result;
            public int CallCount { get; private set; }
            public bool? LastIncludePBIRS { get; private set; }

            public FakeScanner(List<PowerBIInstance> result) { _result = result; }

            public List<PowerBIInstance> Scan(bool includePBIRS)
            {
                CallCount++;
                LastIncludePBIRS = includePBIRS;
                return new List<PowerBIInstance>(_result);
            }
        }

        [TestInitialize]
        public void Init()
        {
            _originalScanner = PowerBIHelper.Scanner;
            ResetCache();
        }

        [TestCleanup]
        public void Cleanup()
        {
            PowerBIHelper.Scanner = _originalScanner;
            ResetCache();
        }

        // PowerBIHelper keeps a static cache; reset it via reflection so each test starts clean.
        private static void ResetCache()
        {
            var t = typeof(PowerBIHelper);
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
            t.GetField("instancesLoaded", flags).SetValue(null, false);
            t.GetField("_lastScanUtc", flags).SetValue(null, DateTime.MinValue);
            t.GetField("_lastScanIncludedPBIRS", flags).SetValue(null, false);
            var instances = (System.Collections.IList)t.GetField("_instances", flags).GetValue(null);
            instances.Clear();
        }

        [TestMethod]
        public void GetLocalInstances_DelegatesToScanner_AndSortsByName()
        {
            var fake = new FakeScanner(new List<PowerBIInstance>
            {
                new PowerBIInstance("Charlie", 3, EmbeddedSSASIcon.PowerBI),
                new PowerBIInstance("Alpha", 1, EmbeddedSSASIcon.PowerBI),
                new PowerBIInstance("Bravo", 2, EmbeddedSSASIcon.PowerBI),
            });
            PowerBIHelper.Scanner = fake;

            var result = PowerBIHelper.GetLocalInstances(includePBIRS: true, refreshList: true);

            Assert.AreEqual(1, fake.CallCount, "scanner should be invoked once");
            Assert.AreEqual(true, fake.LastIncludePBIRS, "includePBIRS should be passed through");
            CollectionAssert.AreEqual(
                new[] { "Alpha", "Bravo", "Charlie" },
                result.ConvertAll(i => i.Name),
                "results should be sorted by name");
        }

        [TestMethod]
        public void GetLocalInstances_ThrottlesRepeatScans()
        {
            var fake = new FakeScanner(new List<PowerBIInstance>
            {
                new PowerBIInstance("Alpha", 1, EmbeddedSSASIcon.PowerBI),
            });
            PowerBIHelper.Scanner = fake;

            PowerBIHelper.GetLocalInstances(includePBIRS: false, refreshList: true);
            // A second force-refresh with the same scope inside the throttle window should reuse
            // the cached result rather than re-scanning.
            PowerBIHelper.GetLocalInstances(includePBIRS: false, refreshList: true);

            Assert.AreEqual(1, fake.CallCount, "near-simultaneous force-refreshes should be coalesced");
        }

        [TestMethod]
        public void GetLocalInstances_UsesCache_WhenNotRefreshing()
        {
            var fake = new FakeScanner(new List<PowerBIInstance>
            {
                new PowerBIInstance("Alpha", 1, EmbeddedSSASIcon.PowerBI),
            });
            PowerBIHelper.Scanner = fake;

            PowerBIHelper.GetLocalInstances(includePBIRS: false, refreshList: true);
            PowerBIHelper.GetLocalInstances(includePBIRS: false, refreshList: false);

            Assert.AreEqual(1, fake.CallCount, "a non-refresh call should return the cached list");
        }

        [TestMethod]
        public void NullPowerBIInstanceScanner_ReturnsEmptyList()
        {
            var scanner = new NullPowerBIInstanceScanner();
            Assert.AreEqual(0, scanner.Scan(includePBIRS: true).Count);
            Assert.AreEqual(0, scanner.Scan(includePBIRS: false).Count);
        }
    }
}
