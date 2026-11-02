using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;

namespace DaxStudio.Core.Extensions
{
    public static class ProcessExtensions
    {
        /// <summary>
        /// Returns the parent <see cref="Process"/> of the supplied process, or null if it cannot
        /// be determined (or the parent has already exited).
        /// </summary>
        /// <remarks>
        /// The caller owns the returned <see cref="Process"/> and is responsible for disposing it.
        /// The parent id is resolved via a native NtQueryInformationProcess call which is orders of
        /// magnitude cheaper than the WMI query used as a fallback - scanning for Power BI Desktop
        /// instances does this once per running msmdsrv process, and a cold WMI query can take
        /// seconds while the WMI service starts up.
        /// </remarks>
        public static Process GetParent(this Process process)
        {
            if (process == null) return null;

            if (!process.TryGetParentProcessId(out var parentId)) return null;

            try
            {
                return Process.GetProcessById(parentId);
            }
            catch (ArgumentException)
            {
                // the parent has exited since we read its id
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>
        /// Resolves the id of the parent of <paramref name="process"/>. Uses the native
        /// NtQueryInformationProcess API and falls back to WMI if that fails.
        /// </summary>
        /// <returns>true when a live, plausible parent process id was found.</returns>
        public static bool TryGetParentProcessId(this Process process, out int parentProcessId)
        {
            parentProcessId = 0;
            if (process == null) return false;

            if (TryGetParentProcessIdNative(process, out var nativeParentId)
                && IsPlausibleParent(process, nativeParentId))
            {
                parentProcessId = nativeParentId;
                return true;
            }

            if (TryGetParentProcessIdSnapshot(process, out var snapshotParentId)
                && IsPlausibleParent(process, snapshotParentId))
            {
                parentProcessId = snapshotParentId;
                return true;
            }

            if (TryGetParentProcessIdWmi(process, out var wmiParentId)
                && IsPlausibleParent(process, wmiParentId))
            {
                parentProcessId = wmiParentId;
                return true;
            }

            return false;
        }

        private static bool TryGetParentProcessIdNative(Process process, out int parentProcessId)
        {
            parentProcessId = 0;
            var handle = NativeMethods.TryOpenForQuery(process.Id);
            if (handle == IntPtr.Zero)
            {
                Log.Verbose("{class} {method} {message}", nameof(ProcessExtensions), nameof(TryGetParentProcessIdNative), $"Could not open process {process.Id} for query (error {Marshal.GetLastWin32Error()}), falling back to WMI");
                return false;
            }

            try
            {
                var info = new NativeMethods.ProcessBasicInformation();
                var status = NativeMethods.NtQueryInformationProcess(
                    handle,
                    NativeMethods.ProcessBasicInformationClass,
                    ref info,
                    Marshal.SizeOf(info),
                    out _);

                if (status != 0)
                {
                    Log.Verbose("{class} {method} {message}", nameof(ProcessExtensions), nameof(TryGetParentProcessIdNative), $"NtQueryInformationProcess returned 0x{status:X8} for process {process.Id}");
                    return false;
                }

                parentProcessId = info.InheritedFromUniqueProcessId.ToInt32();
                return parentProcessId != 0;
            }
            catch (Exception ex)
            {
                // the process may have exited between opening the handle and querying it
                Log.Verbose(ex, "{class} {method} {message}", nameof(ProcessExtensions), nameof(TryGetParentProcessIdNative), $"Falling back to WMI for parent of process {process.Id}: {ex.Message}");
                return false;
            }
            finally
            {
                NativeMethods.CloseHandle(handle);
            }
        }

        /// <summary>
        /// Reads the parent process id from a system-wide Toolhelp snapshot. Unlike
        /// NtQueryInformationProcess this needs no rights on the target process, so it works for
        /// service-owned processes (services.exe / RSHostingService hosted msmdsrv) that a
        /// non-elevated DAX Studio cannot open. It is also several orders of magnitude faster than
        /// the WMI fallback, which has been measured at 4-12 seconds per process on a cold WMI
        /// service.
        /// </summary>
        private static bool TryGetParentProcessIdSnapshot(Process process, out int parentProcessId)
        {
            parentProcessId = 0;
            var snapshot = GetProcessTreeSnapshot();
            return snapshot != null
                && snapshot.TryGetValue(process.Id, out parentProcessId)
                && parentProcessId != 0;
        }

        private static readonly object _snapshotLock = new object();
        private static Dictionary<int, int> _snapshotCache;
        private static DateTime _snapshotCacheExpiry = DateTime.MinValue;

        /// <summary>
        /// Builds (and briefly caches) a pid -> parent pid map for every running process. Taking the
        /// snapshot costs ~100ms on a busy machine, and the Power BI scan resolves a parent for each
        /// running msmdsrv in parallel, so the map is built once per scan rather than once per
        /// process.
        /// </summary>
        private static Dictionary<int, int> GetProcessTreeSnapshot()
        {
            lock (_snapshotLock)
            {
                if (_snapshotCache != null && DateTime.UtcNow < _snapshotCacheExpiry) return _snapshotCache;

                var map = BuildProcessTreeSnapshot();
                if (map == null) return _snapshotCache;

                _snapshotCache = map;
                _snapshotCacheExpiry = DateTime.UtcNow.AddSeconds(SnapshotCacheSeconds);
                return _snapshotCache;
            }
        }

        private const int SnapshotCacheSeconds = 2;

        private static Dictionary<int, int> BuildProcessTreeSnapshot()
        {
            var snapshot = NativeMethods.CreateToolhelp32Snapshot(NativeMethods.TH32CS_SNAPPROCESS, 0);
            if (snapshot == NativeMethods.InvalidHandleValue)
            {
                Log.Verbose("{class} {method} {message}", nameof(ProcessExtensions), nameof(BuildProcessTreeSnapshot), $"CreateToolhelp32Snapshot failed with error {Marshal.GetLastWin32Error()}");
                return null;
            }

            try
            {
                var map = new Dictionary<int, int>();
                var entry = new NativeMethods.ProcessEntry32 { dwSize = Marshal.SizeOf(typeof(NativeMethods.ProcessEntry32)) };
                if (!NativeMethods.Process32First(snapshot, ref entry)) return null;

                do
                {
                    map[entry.th32ProcessID] = entry.th32ParentProcessID;
                } while (NativeMethods.Process32Next(snapshot, ref entry));

                return map;
            }
            catch (Exception ex)
            {
                Log.Verbose(ex, "{class} {method} {message}", nameof(ProcessExtensions), nameof(BuildProcessTreeSnapshot), $"Error reading the process snapshot: {ex.Message}");
                return null;
            }
            finally
            {
                NativeMethods.CloseHandle(snapshot);
            }
        }

        private static bool TryGetParentProcessIdWmi(Process process, out int parentProcessId)
        {
            parentProcessId = 0;
            try
            {
                using (var query = new ManagementObjectSearcher(
                  "SELECT ParentProcessId " +
                  "FROM Win32_Process " +
                  "WHERE ProcessId=" + process.Id))
                {
                    parentProcessId = query
                      .Get()
                      .OfType<ManagementObject>()
                      .Select(p => (int)(uint)p["ParentProcessId"])
                      .FirstOrDefault();

                    return parentProcessId != 0;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{class} {method} {message}", nameof(ProcessExtensions), nameof(TryGetParentProcessIdWmi), $"Error getting parent processid via WMI: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Guards against process id reuse. Windows recycles process ids, so a recorded parent id
        /// may now belong to an unrelated process that started *after* the child - which cannot be
        /// the real parent.
        /// </summary>
        private static bool IsPlausibleParent(Process process, int parentProcessId)
        {
            if (parentProcessId == 0 || parentProcessId == process.Id) return false;

            Process parent;
            try
            {
                parent = Process.GetProcessById(parentProcessId);
            }
            catch (ArgumentException)
            {
                // no process with that id is running, so it cannot be the parent
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }

            using (parent)
            {
                return IsPlausibleParentStartTime(TryGetStartTime(parent), TryGetStartTime(process));
            }
        }

        private static DateTime? TryGetStartTime(Process process)
        {
            // Native first: Process.StartTime opens the target with PROCESS_QUERY_INFORMATION on
            // .NET Framework, which is denied for service-owned processes when we are not elevated.
            // PROCESS_QUERY_LIMITED_INFORMATION is granted in that case, so the PID reuse guard
            // keeps working for exactly the services.exe / RSHostingService / msmdsrv parents the
            // Power BI scan has to identify.
            var nativeStartTime = NativeMethods.TryGetStartTime(process.Id);
            if (nativeStartTime != null) return nativeStartTime;

            try
            {
                return process.StartTime;
            }
            catch (Exception ex)
            {
                Log.Verbose(ex, "{class} {method} {message}", nameof(ProcessExtensions), nameof(TryGetStartTime), $"Could not read the start time of process {process.Id}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// PID reuse guard: a parent must have started no later than its child. When either start
        /// time is unreadable the parent demonstrably exists, so it is treated as plausible -
        /// rejecting it here would return a null parent and silently skip the SSAS / Power BI
        /// Report Server filtering the scanner depends on.
        /// </summary>
        internal static bool IsPlausibleParentStartTime(DateTime? parentStart, DateTime? childStart)
        {
            if (parentStart == null || childStart == null) return true;
            return parentStart.Value <= childStart.Value;
        }

        private static class NativeMethods
        {
            public const int ProcessBasicInformationClass = 0;

            private const int PROCESS_QUERY_INFORMATION = 0x0400;
            private const int PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

            public const int TH32CS_SNAPPROCESS = 0x00000002;
            public static readonly IntPtr InvalidHandleValue = new IntPtr(-1);

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            public struct ProcessEntry32
            {
                public int dwSize;
                public int cntUsage;
                public int th32ProcessID;
                public IntPtr th32DefaultHeapID;
                public int th32ModuleID;
                public int cntThreads;
                public int th32ParentProcessID;
                public int pcPriClassBase;
                public int dwFlags;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
                public string szExeFile;
            }

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern IntPtr CreateToolhelp32Snapshot(int flags, int processId);

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "Process32FirstW")]
            public static extern bool Process32First(IntPtr snapshot, ref ProcessEntry32 entry);

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "Process32NextW")]
            public static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry32 entry);

            [StructLayout(LayoutKind.Sequential)]
            public struct ProcessBasicInformation
            {
                public IntPtr ExitStatus;
                public IntPtr PebBaseAddress;
                public IntPtr AffinityMask;
                public IntPtr BasePriority;
                public IntPtr UniqueProcessId;
                public IntPtr InheritedFromUniqueProcessId;
            }

            /// <summary>
            /// Opens a process with the least privilege that still allows us to read its basic
            /// information. PROCESS_QUERY_LIMITED_INFORMATION is granted to a normal user for
            /// processes owned by other accounts (including services), where the wider
            /// PROCESS_QUERY_INFORMATION - and therefore <see cref="Process.Handle"/> and
            /// <see cref="Process.StartTime"/> - is denied.
            /// Returns IntPtr.Zero when the process cannot be opened; the caller must close a
            /// non-zero handle with <see cref="CloseHandle"/>.
            /// </summary>
            public static IntPtr TryOpenForQuery(int processId)
            {
                var handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
                if (handle == IntPtr.Zero) handle = OpenProcess(PROCESS_QUERY_INFORMATION, false, processId);
                return handle;
            }

            public static DateTime? TryGetStartTime(int processId)
            {
                var handle = TryOpenForQuery(processId);
                if (handle == IntPtr.Zero) return null;

                try
                {
                    if (!GetProcessTimes(handle, out var creation, out _, out _, out _)) return null;
                    return DateTime.FromFileTime(creation);
                }
                catch (Exception)
                {
                    return null;
                }
                finally
                {
                    CloseHandle(handle);
                }
            }

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern IntPtr OpenProcess(int desiredAccess, bool inheritHandle, int processId);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool GetProcessTimes(IntPtr processHandle, out long creationTime, out long exitTime, out long kernelTime, out long userTime);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool CloseHandle(IntPtr handle);

            [DllImport("ntdll.dll")]
            public static extern int NtQueryInformationProcess(
                IntPtr processHandle,
                int processInformationClass,
                ref ProcessBasicInformation processInformation,
                int processInformationLength,
                out int returnLength);
        }
    }
}
