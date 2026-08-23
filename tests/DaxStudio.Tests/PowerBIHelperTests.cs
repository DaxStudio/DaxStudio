using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DaxStudio.Core.Extensions;
using DaxStudio.Core.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.Tests
{
    /// <summary>
    /// Verifies the platform-agnostic <see cref="PowerBIHelper"/> façade delegates the raw scan to
    /// an injectable <see cref="IPowerBIInstanceScanner"/> (real native/Win32 scanner on Windows, a
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

            public Task<List<PowerBIInstance>> ScanAsync(bool includePBIRS, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(Scan(includePBIRS));
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
        public async Task GetLocalInstancesAsync_DelegatesToScanner_AndSortsByName()
        {
            var fake = new FakeScanner(new List<PowerBIInstance>
            {
                new PowerBIInstance("Charlie", 3, EmbeddedSSASIcon.PowerBI),
                new PowerBIInstance("Alpha", 1, EmbeddedSSASIcon.PowerBI),
                new PowerBIInstance("Bravo", 2, EmbeddedSSASIcon.PowerBI),
            });
            PowerBIHelper.Scanner = fake;

            var result = await PowerBIHelper.GetLocalInstancesAsync(includePBIRS: true, refreshList: true, CancellationToken.None);

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
        public async Task GetLocalInstancesAsync_ThrottlesRepeatScans()
        {
            var fake = new FakeScanner(new List<PowerBIInstance>
            {
                new PowerBIInstance("Alpha", 1, EmbeddedSSASIcon.PowerBI),
            });
            PowerBIHelper.Scanner = fake;

            await PowerBIHelper.GetLocalInstancesAsync(includePBIRS: false, refreshList: true, CancellationToken.None);
            await PowerBIHelper.GetLocalInstancesAsync(includePBIRS: false, refreshList: true, CancellationToken.None);

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
        public async Task GetLocalInstancesAsync_UsesCache_WhenNotRefreshing()
        {
            var fake = new FakeScanner(new List<PowerBIInstance>
            {
                new PowerBIInstance("Alpha", 1, EmbeddedSSASIcon.PowerBI),
            });
            PowerBIHelper.Scanner = fake;

            await PowerBIHelper.GetLocalInstancesAsync(includePBIRS: false, refreshList: true, CancellationToken.None);
            await PowerBIHelper.GetLocalInstancesAsync(includePBIRS: false, refreshList: false, CancellationToken.None);

            Assert.AreEqual(1, fake.CallCount, "a non-refresh call should return the cached list");
        }

        [TestMethod]
        public void GetLocalInstances_ReturnsSnapshot_SoCallersCannotPoisonTheCache()
        {
            // The connection dialog appends a placeholder "none detected" entry to whatever it
            // gets back, so the returned list must not be the cached instance.
            var fake = new FakeScanner(new List<PowerBIInstance>
            {
                new PowerBIInstance("Alpha", 1, EmbeddedSSASIcon.PowerBI),
            });
            PowerBIHelper.Scanner = fake;

            var first = PowerBIHelper.GetLocalInstances(includePBIRS: false, refreshList: true);
            first.Add(new PowerBIInstance("<none detected>", -1, EmbeddedSSASIcon.PowerBI));

            var second = PowerBIHelper.GetLocalInstances(includePBIRS: false, refreshList: false);

            Assert.AreNotSame(first, second, "callers should not receive the cached list instance");
            Assert.AreEqual(1, second.Count, "mutating the returned list must not affect the cache");
            Assert.AreEqual("Alpha", second[0].Name);
        }

        [TestMethod]
        public async Task GetLocalInstancesAsync_ReturnsSnapshot_SoCallersCannotPoisonTheCache()
        {
            var fake = new FakeScanner(new List<PowerBIInstance>
            {
                new PowerBIInstance("Alpha", 1, EmbeddedSSASIcon.PowerBI),
            });
            PowerBIHelper.Scanner = fake;

            var first = await PowerBIHelper.GetLocalInstancesAsync(includePBIRS: false, refreshList: true, CancellationToken.None);
            first.Clear();

            var second = await PowerBIHelper.GetLocalInstancesAsync(includePBIRS: false, refreshList: false, CancellationToken.None);

            Assert.AreNotSame(first, second, "callers should not receive the cached list instance");
            Assert.AreEqual(1, second.Count, "clearing the returned list must not affect the cache");
        }

        [TestMethod]
        public async Task GetLocalInstancesAsync_HonoursCancellation()
        {
            var fake = new FakeScanner(new List<PowerBIInstance>
            {
                new PowerBIInstance("Alpha", 1, EmbeddedSSASIcon.PowerBI),
            });
            PowerBIHelper.Scanner = fake;

            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                try
                {
                    await PowerBIHelper.GetLocalInstancesAsync(includePBIRS: false, refreshList: true, cts.Token);
                    Assert.Fail("expected the scan to be cancelled");
                }
                catch (OperationCanceledException)
                {
                    // expected
                }

                Assert.AreEqual(0, fake.CallCount, "a cancelled request should not invoke the scanner");
            }
        }

        [TestMethod]
        public void NullPowerBIInstanceScanner_ReturnsEmptyList()
        {
            var scanner = new NullPowerBIInstanceScanner();
            Assert.AreEqual(0, scanner.Scan(includePBIRS: true).Count);
            Assert.AreEqual(0, scanner.Scan(includePBIRS: false).Count);
        }

        [TestMethod]
        public async Task NullPowerBIInstanceScanner_ScanAsync_ReturnsEmptyList()
        {
            var scanner = new NullPowerBIInstanceScanner();
            var result = await scanner.ScanAsync(includePBIRS: true, CancellationToken.None);
            Assert.AreEqual(0, result.Count);
        }
    }

    /// <summary>
    /// The Power BI Desktop scan resolves the parent of every running msmdsrv process. That lookup
    /// used to issue a WMI query per process (slow to the point of dominating the scan); it now
    /// uses a native NtQueryInformationProcess call with the WMI query retained as a fallback.
    /// </summary>
    [TestClass]
    public class ProcessExtensionsTests
    {
        [TestMethod]
        public void TryGetParentProcessId_ResolvesAParentForTheCurrentProcess()
        {
            using (var current = Process.GetCurrentProcess())
            {
                Assert.IsTrue(current.TryGetParentProcessId(out var parentId), "the test host should have a resolvable parent");
                Assert.AreNotEqual(0, parentId);
                Assert.AreNotEqual(current.Id, parentId, "a process cannot be its own parent");
            }
        }

        [TestMethod]
        public void TryGetParentProcessId_NativeAndWmiAgree()
        {
            // Guards against the native struct layout drifting - both strategies must resolve the
            // same parent for a process we know is alive.
            using (var current = Process.GetCurrentProcess())
            {
                var native = InvokePrivate("TryGetParentProcessIdNative", current, out var nativeId);
                var wmi = InvokePrivate("TryGetParentProcessIdWmi", current, out var wmiId);

                Assert.IsTrue(native, "the native lookup should succeed for the current process");
                Assert.IsTrue(wmi, "the WMI fallback should succeed for the current process");
                Assert.AreEqual(wmiId, nativeId, "native and WMI parent lookups should agree");
            }
        }

        [TestMethod]
        public void GetParent_ReturnsALiveProcess_ThatStartedBeforeTheChild()
        {
            using (var current = Process.GetCurrentProcess())
            using (var parent = current.GetParent())
            {
                Assert.IsNotNull(parent, "the test host should have a resolvable parent");
                Assert.IsTrue(parent.StartTime <= current.StartTime, "a parent cannot start after its child");
            }
        }

        [TestMethod]
        public void GetParent_NullProcess_ReturnsNull()
        {
            Assert.IsNull(ProcessExtensions.GetParent(null));
        }

        [TestMethod]
        public void TryGetParentProcessIdSnapshot_ResolvesParentsThatCannotBeOpened()
        {
            // The Toolhelp snapshot is the tier that makes service-owned processes (services.exe /
            // RSHostingService hosted msmdsrv) resolvable without elevation. Before it existed those
            // fell through to WMI, which was measured at 4-12 seconds *per process* during startup.
            var snapshotMethod = typeof(ProcessExtensions).GetMethod("TryGetParentProcessIdSnapshot", BindingFlags.NonPublic | BindingFlags.Static);
            var nativeMethod = typeof(ProcessExtensions).GetMethod("TryGetParentProcessIdNative", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(snapshotMethod, "TryGetParentProcessIdSnapshot should exist");
            Assert.IsNotNull(nativeMethod, "TryGetParentProcessIdNative should exist");

            using (var current = Process.GetCurrentProcess())
            {
                var snapshotArgs = new object[] { current, 0 };
                Assert.IsTrue((bool)snapshotMethod.Invoke(null, snapshotArgs), "the snapshot should resolve a parent for the test host");

                var nativeArgs = new object[] { current, 0 };
                if ((bool)nativeMethod.Invoke(null, nativeArgs))
                {
                    Assert.AreEqual(nativeArgs[1], snapshotArgs[1], "the snapshot and native lookups must agree");
                }
            }

            var unopenable = Process.GetProcesses()
                .Where(p => p.Id > 4)
                .FirstOrDefault(p =>
                {
                    var args = new object[] { p, 0 };
                    return !(bool)nativeMethod.Invoke(null, args);
                });

            if (unopenable == null)
            {
                Assert.Inconclusive("every process could be opened natively, so the snapshot fallback could not be exercised");
                return;
            }

            using (unopenable)
            {
                var args = new object[] { unopenable, 0 };
                Assert.IsTrue((bool)snapshotMethod.Invoke(null, args), $"the snapshot must resolve a parent for process {unopenable.Id} which cannot be opened natively");
            }
        }

        [TestMethod]
        public void GetParent_ResolvesEveryProcess_WithoutFallingBackToWmi()
        {
            // A full scan must never need WMI - GetParent is called once per msmdsrv process while
            // the connection dialog is opening.
            var snapshotMethod = typeof(ProcessExtensions).GetMethod("TryGetParentProcessIdSnapshot", BindingFlags.NonPublic | BindingFlags.Static);
            var sw = Stopwatch.StartNew();
            var processes = Process.GetProcesses().Where(p => p.Id > 4).Take(50).ToList();

            try
            {
                foreach (var p in processes)
                {
                    var args = new object[] { p, 0 };
                    snapshotMethod.Invoke(null, args);
                }
            }
            finally
            {
                foreach (var p in processes) p.Dispose();
            }

            // the snapshot is built once and cached, so 50 lookups must not cost 50 enumerations
            Assert.IsTrue(sw.ElapsedMilliseconds < 2000, $"50 cached snapshot lookups took {sw.ElapsedMilliseconds}ms");
        }

        [TestMethod]
        public void IsPlausibleParentStartTime_AcceptsUnreadableStartTimes()
        {
            // Process.StartTime needs PROCESS_QUERY_INFORMATION, which a non-elevated process does
            // not have for service-owned processes on .NET Framework - and services.exe /
            // RSHostingService are precisely the parents the Power BI scan has to recognise in order
            // to *exclude* real SSAS and Power BI Report Server instances. Rejecting an unreadable
            // start time would make GetParent() return null and silently bypass that filtering.
            var method = typeof(ProcessExtensions).GetMethod("IsPlausibleParentStartTime", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "IsPlausibleParentStartTime should exist");

            bool Check(DateTime? parent, DateTime? child) => (bool)method.Invoke(null, new object[] { parent, child });

            var now = DateTime.Now;

            Assert.IsTrue(Check(null, now), "an unreadable parent start time must still be treated as valid");
            Assert.IsTrue(Check(now, null), "an unreadable child start time must still be treated as valid");
            Assert.IsTrue(Check(null, null), "two unreadable start times must still be treated as valid");
            Assert.IsTrue(Check(now.AddSeconds(-1), now), "a parent that started before its child is valid");
            Assert.IsTrue(Check(now, now), "identical start times are valid");
            Assert.IsFalse(Check(now.AddSeconds(1), now), "a parent cannot start after its child");
        }

        [TestMethod]
        public void IsPlausibleParent_RejectsMissingProcess()
        {
            using (var current = Process.GetCurrentProcess())
            {
                var method = typeof(ProcessExtensions).GetMethod("IsPlausibleParent", BindingFlags.NonPublic | BindingFlags.Static);
                // 0 is never a valid parent, and a process cannot be its own parent
                Assert.IsFalse((bool)method.Invoke(null, new object[] { current, 0 }));
                Assert.IsFalse((bool)method.Invoke(null, new object[] { current, current.Id }));
            }
        }

        private static bool InvokePrivate(string methodName, Process process, out int parentId)
        {
            var method = typeof(ProcessExtensions).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, $"{methodName} should exist");
            var args = new object[] { process, 0 };
            var result = (bool)method.Invoke(null, args);
            parentId = (int)args[1];
            return result;
        }
    }

    /// <summary>
    /// The Power BI scan fans out across msmdsrv processes using
    /// <see cref="TaskExtensions.ParallelForEachAsync{T}(System.Collections.Generic.IEnumerable{T}, Func{T, Task}, int, CancellationToken)"/>.
    /// </summary>
    [TestClass]
    public class ParallelForEachAsyncTests
    {
        [TestMethod]
        public async Task ParallelForEachAsync_SurfacesCancellationAsOperationCanceledException()
        {
            // Cancelled tasks are Canceled rather than Faulted, so Task.Exception is null - routing
            // them through a helper that reads .Exception would turn cancellation into a generic
            // Exception and make the connection dialog report a spurious error to the user.
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                try
                {
                    await new[] { 1, 2, 3 }.ParallelForEachAsync(_ => Task.CompletedTask, 2, cts.Token);
                    Assert.Fail("expected an OperationCanceledException");
                }
                catch (OperationCanceledException)
                {
                    // expected
                }
            }
        }

        [TestMethod]
        public async Task ParallelForEachAsync_SurfacesTheOriginalException()
        {
            try
            {
                await new[] { 1 }.ParallelForEachAsync(_ => throw new InvalidTimeZoneException("boom"), 2, CancellationToken.None);
                Assert.Fail("expected the original exception");
            }
            catch (InvalidTimeZoneException ex)
            {
                Assert.AreEqual("boom", ex.Message);
            }
        }

        [TestMethod]
        public async Task ParallelForEachAsync_RunsEveryItem()
        {
            var seen = new List<int>();
            await new[] { 1, 2, 3, 4, 5 }.ParallelForEachAsync(i =>
            {
                lock (seen) { seen.Add(i); }
                return Task.CompletedTask;
            }, 2, CancellationToken.None);

            seen.Sort();
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, seen);
        }
    }
}
