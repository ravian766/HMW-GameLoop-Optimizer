using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GameLoopOptimizer.Core;

public static class StandbyListCleanerService
{
    private const int SE_PRIVILEGE_ENABLED = 0x00000002;
    private const int TOKEN_QUERY = 0x00000008;
    private const int TOKEN_ADJUST_PRIVILEGES = 0x00000020;

    private const int SystemMemoryListInformation = 80;
    private const int MemoryPurgeStandbyList = 4;
    private const int MemoryPurgeLowPriorityStandbyList = 5;
    private const int MemoryEmptyWorkingSets = 2;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct TOKEN_PRIVILEGES
    {
        public int Count;
        public long Luid;
        public int Attributes;
    }

    [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr ProcessHandle, int DesiredAccess, out IntPtr TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool LookupPrivilegeValue(string? lpSystemName, string lpName, out long lpLuid);

    [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, bool DisableAllPrivileges, ref TOKEN_PRIVILEGES NewState, int BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

    [DllImport("ntdll.dll")]
    private static extern int NtSetSystemInformation(int SystemInformationClass, IntPtr SystemInformation, int SystemInformationLength);

    [DllImport("psapi.dll")]
    private static extern int EmptyWorkingSet(IntPtr hwProc);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    private static bool SetPrivilege(string privilegeName, bool enable)
    {
        try
        {
            if (!OpenProcessToken(Process.GetCurrentProcess().Handle, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out IntPtr tokenHandle))
            {
                return false;
            }

            try
            {
                if (!LookupPrivilegeValue(null, privilegeName, out long luid))
                {
                    return false;
                }

                var tp = new TOKEN_PRIVILEGES
                {
                    Count = 1,
                    Luid = luid,
                    Attributes = enable ? SE_PRIVILEGE_ENABLED : 0
                };

                return AdjustTokenPrivileges(tokenHandle, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
            }
            finally
            {
                CloseHandle(tokenHandle);
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Purges the Windows Standby Memory List, eliminating micro-stutters from memory cache contention.
    /// </summary>
    public static (bool Success, string Message) PurgeStandbyList()
    {
        try
        {
            SetPrivilege("SeProfileSingleProcessPrivilege", true);
            SetPrivilege("SeIncreaseQuotaPrivilege", true);

            GCHandle handle = GCHandle.Alloc(MemoryPurgeStandbyList, GCHandleType.Pinned);
            try
            {
                int result = NtSetSystemInformation(SystemMemoryListInformation, handle.AddrOfPinnedObject(), Marshal.SizeOf(MemoryPurgeStandbyList));
                if (result == 0)
                {
                    Logger.Success("StandbyCleaner", "Purged Windows Standby Memory List successfully.");
                    return (true, "Standby List Cache purged successfully.");
                }
                
                // Try low priority standby list as fallback
                GCHandle lowHandle = GCHandle.Alloc(MemoryPurgeLowPriorityStandbyList, GCHandleType.Pinned);
                try
                {
                    int lowResult = NtSetSystemInformation(SystemMemoryListInformation, lowHandle.AddrOfPinnedObject(), Marshal.SizeOf(MemoryPurgeLowPriorityStandbyList));
                    if (lowResult == 0)
                    {
                        Logger.Success("StandbyCleaner", "Purged Low-Priority Standby List.");
                        return (true, "Low-Priority Standby List purged.");
                    }
                }
                finally
                {
                    lowHandle.Free();
                }

                // Fallback to trimming working sets across processes
                TrimBackgroundWorkingSets();
                return (true, "Trimmed active working sets (Administrator privilege recommended for full standby purge).");
            }
            finally
            {
                handle.Free();
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("StandbyCleaner", $"Standby list purge failed: {ex.Message}");
            TrimBackgroundWorkingSets();
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Flushes working sets for non-game background processes.
    /// </summary>
    public static int TrimBackgroundWorkingSets()
    {
        int trimmedCount = 0;
        try
        {
            var procs = Process.GetProcesses();
            foreach (var proc in procs)
            {
                try
                {
                    string name = proc.ProcessName;
                    if (name.Contains("Android", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("aow", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("GameLoop", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    EmptyWorkingSet(proc.Handle);
                    trimmedCount++;
                }
                catch
                {
                    // Ignore access denied on secure system processes
                }
                finally
                {
                    proc.Dispose();
                }
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        catch (Exception ex)
        {
            Logger.Warn("StandbyCleaner", $"Working set trim encountered error: {ex.Message}");
        }

        return trimmedCount;
    }
}
