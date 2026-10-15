using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace OpenCodeJesk.Services
{
    /// <summary>
    /// Binds child processes to the VS process lifecycle via Windows Job Objects.
    /// When VS exits (including crash), the child process is auto-terminated.
    /// </summary>
    internal static class ProcessBinding
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string lpName);

        [DllImport("kernel32.dll")]
        private static extern bool SetInformationJobObject(
            IntPtr hJob, int infoType, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll")]
        private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        private const int JobObjectExtendedLimitInformation = 9;
        private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;

        public static void BindToCurrentProcess(System.Diagnostics.Process childProcess)
        {
            try
            {
                var jobName = $"OpenCodeJesk_{System.Diagnostics.Process.GetCurrentProcess().Id}";
                var hJob = CreateJobObject(IntPtr.Zero, jobName);
                if (hJob == IntPtr.Zero) return;

                var infoSize = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
                var infoPtr = Marshal.AllocHGlobal(infoSize);

                var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
                {
                    BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
                    {
                        LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
                    }
                };
                Marshal.StructureToPtr(info, infoPtr, false);

                SetInformationJobObject(hJob, JobObjectExtendedLimitInformation, infoPtr, (uint)infoSize);
                Marshal.FreeHGlobal(infoPtr);

                AssignProcessToJobObject(hJob, childProcess.Handle);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ProcessBinding failed: {ex}");
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }
    }
}
