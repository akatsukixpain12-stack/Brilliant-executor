using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace RblxExecutorUI
{
    public static class RblxCore
    {
        private const string DllName = "Syntax.dll";

        // Check if the native DLL is available
        public static bool IsDllAvailable()
        {
            string dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DllName);
            return File.Exists(dllPath);
        }

        public static string DllPath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DllName); }
        }

        // Helper to show a friendly error if DLL is missing
        private static void EnsureDll()
        {
            if (!IsDllAvailable())
            {
                throw new DllNotFoundException(
                    $"Native library '{DllName}' was not found. " +
                    "Please ensure the C++ core is built (run build_all.bat) and {DllName} is placed next to the application.");
            }
        }

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        private static extern bool NativeInitialize();

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        private static extern uint NativeFindRobloxProcess();

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        private static extern bool NativeConnect(uint pid);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        private static extern void NativeDisconnect();

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        private static extern uint NativeGetRobloxPid();

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        private static extern void NativeRedirConsole();

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        private static extern UIntPtr NativeGetDataModel();

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        private static extern int NativeGetJobCount();

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private static extern int NativeExecuteScript([MarshalAs(UnmanagedType.LPStr)] string source, int sourceLen);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        private static extern int NativeGetLastExecError(StringBuilder buffer, int bufLen);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        private static extern bool NativeReadMemory(UIntPtr address, IntPtr buffer, UIntPtr size);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        private static extern bool NativeWriteMemory(UIntPtr address, IntPtr buffer, UIntPtr size);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private static extern bool NativeGetClientInfo(StringBuilder buffer, int maxSize);

        // Safe wrappers that check for DLL availability
        public static bool Initialize()
        {
            if (!IsDllAvailable()) return false;
            try { return NativeInitialize(); }
            catch (DllNotFoundException) { return false; }
            catch (EntryPointNotFoundException) { return false; }
        }

        public static uint FindRobloxProcess()
        {
            if (!IsDllAvailable()) return 0;
            try { return NativeFindRobloxProcess(); }
            catch (DllNotFoundException) { return 0; }
            catch (EntryPointNotFoundException) { return 0; }
        }

        public static bool Connect(uint pid)
        {
            if (!IsDllAvailable()) return false;
            try { return NativeConnect(pid); }
            catch (DllNotFoundException) { return false; }
            catch (EntryPointNotFoundException) { return false; }
        }

        public static void Disconnect()
        {
            if (!IsDllAvailable()) return;
            try { NativeDisconnect(); }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }

        public static uint GetRobloxPid()
        {
            if (!IsDllAvailable()) return 0;
            try { return NativeGetRobloxPid(); }
            catch (DllNotFoundException) { return 0; }
            catch (EntryPointNotFoundException) { return 0; }
        }

        public static void RedirConsole()
        {
            if (!IsDllAvailable()) return;
            try { NativeRedirConsole(); }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }

        public static UIntPtr GetDataModel()
        {
            if (!IsDllAvailable()) return UIntPtr.Zero;
            try { return NativeGetDataModel(); }
            catch (DllNotFoundException) { return UIntPtr.Zero; }
            catch (EntryPointNotFoundException) { return UIntPtr.Zero; }
        }

        public static int GetJobCount()
        {
            if (!IsDllAvailable()) return -1;
            try { return NativeGetJobCount(); }
            catch (DllNotFoundException) { return -1; }
            catch (EntryPointNotFoundException) { return -1; }
        }

        public static int ExecuteScript(string source, int sourceLen)
        {
            if (!IsDllAvailable()) return -1;
            try { return NativeExecuteScript(source, sourceLen); }
            catch (DllNotFoundException) { return -1; }
            catch (EntryPointNotFoundException) { return -1; }
        }

        private static int GetLastExecError(StringBuilder buffer, int bufLen)
        {
            if (!IsDllAvailable()) return 0;
            try { return NativeGetLastExecError(buffer, bufLen); }
            catch (DllNotFoundException) { return 0; }
            catch (EntryPointNotFoundException) { return 0; }
        }

        public static bool ReadMemory(UIntPtr address, IntPtr buffer, UIntPtr size)
        {
            if (!IsDllAvailable()) return false;
            try { return NativeReadMemory(address, buffer, size); }
            catch (DllNotFoundException) { return false; }
            catch (EntryPointNotFoundException) { return false; }
        }

        public static bool WriteMemory(UIntPtr address, IntPtr buffer, UIntPtr size)
        {
            if (!IsDllAvailable()) return false;
            try { return NativeWriteMemory(address, buffer, size); }
            catch (DllNotFoundException) { return false; }
            catch (EntryPointNotFoundException) { return false; }
        }

        public static bool GetClientInfo(StringBuilder buffer, int maxSize)
        {
            if (!IsDllAvailable()) return false;
            try { return NativeGetClientInfo(buffer, maxSize); }
            catch (DllNotFoundException) { return false; }
            catch (EntryPointNotFoundException) { return false; }
        }

        // Helper: get last error message
        public static string GetLastError()
        {
            if (!IsDllAvailable()) return "Native DLL not found. Build the C++ core first.";
            var sb = new StringBuilder(1024);
            GetLastExecError(sb, sb.Capacity);
            return sb.ToString();
        }

        // Helper for easy memory reading in C#
        public static T Read<T>(UIntPtr address) where T : struct
        {
            int size = Marshal.SizeOf(typeof(T));
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                if (ReadMemory(address, ptr, (UIntPtr)size))
                {
                    return Marshal.PtrToStructure<T>(ptr);
                }
                return default;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
    }
}