using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DaxStudio.Core.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.Tests
{
    [TestClass]
    public class PowerBIInstanceCacheTests
    {
        private static PowerBIInstance Instance(string name, int port)
        {
            return new PowerBIInstance(name + " - Power BI Desktop", port, EmbeddedSSASIcon.PowerBI);
        }

        [TestMethod]
        public void GetInstancesReturnsDefensiveCopies()
        {
            var cache = new PowerBIInstanceCache(_ => new List<PowerBIInstance> { Instance("Report", 1234) });
            var first = cache.GetInstances(false, false);
            first.Clear();
            first.Add(Instance("Loading", -1));

            var second = cache.GetInstances(false, false);

            Assert.AreEqual(1, second.Count);
            Assert.AreEqual(1234, second[0].Port);
            Assert.AreNotSame(first, second);
        }

        [TestMethod]
        public void GetInstancesCopiesAndSortsScannerResults()
        {
            var scanned = new List<PowerBIInstance> { Instance("Z Report", 1234), Instance("A Report", 2345) };
            var cache = new PowerBIInstanceCache(_ => scanned);

            var snapshot = cache.GetInstances(false, false);
            Assert.AreEqual("A Report", snapshot[0].Name);
            Assert.AreEqual("Z Report", scanned[0].Name);
            scanned.Clear();

            Assert.AreEqual(2, cache.GetInstances(false, false).Count);
        }

        [TestMethod]
        public void GetInstancesReturnsOnlyCompleteSnapshotsDuringRefresh()
        {
            using (var scanStarted = new ManualResetEventSlim())
            using (var releaseScan = new ManualResetEventSlim())
            using (var readerStarted = new ManualResetEventSlim())
            {
                var scans = 0;
                var cache = new PowerBIInstanceCache(_ =>
                {
                    var results = new List<PowerBIInstance> { Instance("Report A", 1234) };
                    if (Interlocked.Increment(ref scans) > 1)
                    {
                        scanStarted.Set();
                        Assert.IsTrue(releaseScan.Wait(TimeSpan.FromSeconds(10)));
                        results.Add(Instance("Report B", 2345));
                    }
                    return results;
                });
                var previous = cache.GetInstances(false, false);
                var refresh = Task.Run(() => cache.GetInstances(false, true, true));
                Task<List<PowerBIInstance>> reader = null;
                try
                {
                    Assert.IsTrue(scanStarted.Wait(TimeSpan.FromSeconds(5)));
                    reader = Task.Run(() =>
                    {
                        readerStarted.Set();
                        return cache.GetInstances(false, false);
                    });
                    Assert.IsTrue(readerStarted.Wait(TimeSpan.FromSeconds(5)));
                    Assert.AreEqual(1, previous.Count);
                    Assert.AreEqual(1234, previous[0].Port);
                }
                finally
                {
                    releaseScan.Set();
                    Assert.IsTrue(refresh.Wait(TimeSpan.FromSeconds(5)));
                    if (reader != null) Assert.IsTrue(reader.Wait(TimeSpan.FromSeconds(5)));
                }

                Assert.AreEqual(2, refresh.Result.Count);
                Assert.IsNotNull(reader);
                Assert.AreEqual(2, reader.Result.Count);
                Assert.AreEqual(1, previous.Count);
            }
        }

        [TestMethod]
        public void ConcurrentCacheRequestsShareOneCompleteScan()
        {
            var scans = 0;
            var cache = new PowerBIInstanceCache(_ =>
            {
                Interlocked.Increment(ref scans);
                return Enumerable.Range(1, 20).Select(port => Instance("Report " + port, port)).ToList();
            });

            Parallel.For(0, 20, _ =>
            {
                var snapshot = cache.GetInstances(false, false);
                Assert.AreEqual(20, snapshot.Count);
                Assert.AreEqual(20, snapshot.Select(i => i.Port).Distinct().Count());
                snapshot.Clear();
            });

            Assert.AreEqual(1, scans);
            Assert.AreEqual(20, cache.GetInstances(false, false).Count);
        }

        [TestMethod]
        public void NormalRefreshIsThrottled()
        {
            var scans = 0;
            var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var cache = new PowerBIInstanceCache(_ =>
            {
                scans++;
                return new List<PowerBIInstance> { Instance("Report", 1234) };
            }, () => now);
            cache.GetInstances(false, false);

            cache.GetInstances(false, true);

            Assert.AreEqual(1, scans);
        }

        [TestMethod]
        public void NormalRefreshRescansAfterThrottleWindow()
        {
            var scans = 0;
            var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var cache = new PowerBIInstanceCache(_ =>
            {
                scans++;
                return new List<PowerBIInstance>();
            }, () => now);
            cache.GetInstances(false, false);
            now = now.AddSeconds(2);

            cache.GetInstances(false, true);

            Assert.AreEqual(2, scans);
        }

        [TestMethod]
        public void ChangingReportServerScopeDoesNotReuseCachedResults()
        {
            var scopes = new List<bool>();
            var cache = new PowerBIInstanceCache(includePBIRS =>
            {
                scopes.Add(includePBIRS);
                return new List<PowerBIInstance>();
            });
            cache.GetInstances(true, false);

            cache.GetInstances(false, false);

            CollectionAssert.AreEqual(new[] { true, false }, scopes);
        }

        [TestMethod]
        public void FindByPortUsesCachedMatchWithoutRescanning()
        {
            var scans = 0;
            var report = Instance("Report", 1234);
            var cache = new PowerBIInstanceCache(_ =>
            {
                scans++;
                return new List<PowerBIInstance> { report };
            });
            cache.GetInstances(false, false);

            var result = cache.FindByPort(1234);

            Assert.AreSame(report, result);
            Assert.AreEqual(1, scans);
        }

        [TestMethod]
        public void FindByPortForcesFreshDiscoveryWithinThrottleWindow()
        {
            var scans = 0;
            var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var cache = new PowerBIInstanceCache(_ =>
            {
                scans++;
                return scans == 1
                    ? new List<PowerBIInstance> { Instance("Old report", 1234) }
                    : new List<PowerBIInstance> { Instance("New report", 2345) };
            }, () => now);
            cache.GetInstances(false, false);

            var result = cache.FindByPort(2345);

            Assert.IsNotNull(result);
            Assert.AreEqual("New report", result.Name);
            Assert.AreEqual(2, scans);
            Assert.AreEqual(2345, cache.GetInstances(false, false)[0].Port);
        }

        [TestMethod]
        public void FindByPortReturnsNullAfterOneUnsuccessfulFreshScan()
        {
            var scans = 0;
            var cache = new PowerBIInstanceCache(_ =>
            {
                scans++;
                return new List<PowerBIInstance>();
            });

            var result = cache.FindByPort(1234);

            Assert.IsNull(result);
            Assert.AreEqual(2, scans);
        }

        [TestMethod]
        [Timeout(10000)]
        public async Task ConcurrentAsyncRefreshesShareOneCompleteScan()
        {
            var completion = new TaskCompletionSource<List<PowerBIInstance>>(TaskCreationOptions.RunContinuationsAsynchronously);
            var scans = 0;
            var cache = new PowerBIInstanceCache((includePBIRS, cancellationToken) =>
            {
                scans++;
                return completion.Task;
            });

            var first = cache.GetInstancesAsync(false, true, CancellationToken.None);
            var second = cache.GetInstancesAsync(false, true, CancellationToken.None);
            Assert.IsFalse(first.IsCompleted);
            Assert.IsFalse(second.IsCompleted);
            completion.SetResult(new List<PowerBIInstance> { Instance("Z Report", 1234), Instance("A Report", 2345) });

            var snapshots = await Task.WhenAll(first, second);

            Assert.AreEqual(1, scans);
            Assert.AreEqual(2, snapshots[0].Count);
            Assert.AreEqual("A Report", snapshots[0][0].Name);
            Assert.AreNotSame(snapshots[0], snapshots[1]);
            snapshots[0].Clear();
            Assert.AreEqual(2, snapshots[1].Count);
        }

        [TestMethod]
        [Timeout(10000)]
        public async Task CancellationWhileWaitingDoesNotCancelActiveScan()
        {
            var completion = new TaskCompletionSource<List<PowerBIInstance>>(TaskCreationOptions.RunContinuationsAsynchronously);
            var scans = 0;
            var cache = new PowerBIInstanceCache((includePBIRS, cancellationToken) =>
            {
                scans++;
                return completion.Task;
            });
            var active = cache.GetInstancesAsync(false, true, CancellationToken.None);

            using (var cts = new CancellationTokenSource())
            {
                try
                {
                    var waiting = cache.GetInstancesAsync(false, true, cts.Token);
                    cts.Cancel();
                    await Assert.ThrowsAsync<OperationCanceledException>(() => waiting);
                    Assert.IsFalse(active.IsCompleted);
                    Assert.AreEqual(1, scans);
                }
                finally
                {
                    completion.SetResult(new List<PowerBIInstance> { Instance("Report", 1234) });
                    await active;
                }
            }

            Assert.AreEqual(1234, (await cache.GetInstancesAsync(false, false, CancellationToken.None))[0].Port);
            Assert.AreEqual(1, scans);
        }

        [TestMethod]
        public async Task CancelledRefreshPreservesPreviousSnapshotAndReleasesLock()
        {
            using (var cts = new CancellationTokenSource())
            {
                var scans = 0;
                var cache = new PowerBIInstanceCache((includePBIRS, cancellationToken) =>
                {
                    scans++;
                    if (scans > 1)
                    {
                        Assert.AreEqual(cts.Token, cancellationToken);
                        cts.Cancel();
                    }
                    return Task.FromResult(new List<PowerBIInstance> { Instance("Report", scans) });
                });
                await cache.GetInstancesAsync(false, false, CancellationToken.None);

                await Assert.ThrowsAsync<OperationCanceledException>(() => cache.GetInstancesAsync(false, true, cts.Token, true));

                Assert.AreEqual(1, (await cache.GetInstancesAsync(false, false, CancellationToken.None))[0].Port);
                Assert.AreEqual(2, scans);
            }
        }

        [TestMethod]
        public async Task FailedAsyncRefreshPreservesPreviousSnapshotAndReleasesLock()
        {
            var scans = 0;
            var cache = new PowerBIInstanceCache(async (includePBIRS, cancellationToken) =>
            {
                await Task.Yield();
                if (++scans > 1) throw new InvalidOperationException("Discovery failed");
                return new List<PowerBIInstance> { Instance("Report", 1234) };
            });
            await cache.GetInstancesAsync(false, false, CancellationToken.None);

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => cache.GetInstancesAsync(false, true, CancellationToken.None, true));

            Assert.AreEqual(1234, (await cache.GetInstancesAsync(false, false, CancellationToken.None))[0].Port);
        }

        [TestMethod]
        public void FailedRefreshPreservesPreviousSnapshot()
        {
            var scans = 0;
            var cache = new PowerBIInstanceCache(_ =>
            {
                if (++scans > 1) throw new InvalidOperationException("Discovery failed");
                return new List<PowerBIInstance> { Instance("Report", 1234) };
            });
            cache.GetInstances(false, false);

            Assert.ThrowsExactly<InvalidOperationException>(() => cache.GetInstances(false, true, true));

            Assert.AreEqual(1234, cache.GetInstances(false, false)[0].Port);
        }
    }
}
