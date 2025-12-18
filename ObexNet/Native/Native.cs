using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace Win64;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public static class Native
{
    public const int DEBUG_PROCESS = 0x00000001;
    public const int DEBUG_ONLY_THIS_PROCESS = 0x00000002;
    public const int INFINITE = -1;
    public const int DBG_CONTINUE = 0x00010002;
    public const int DBG_EXCEPTION_NOT_HANDLED = unchecked((int)0x80010001);

    public const int EXCEPTION_DEBUG_EVENT = 1;
    public const int CREATE_PROCESS_DEBUG_EVENT = 3;
    public const int EXIT_PROCESS_DEBUG_EVENT = 5;
    public const int LOAD_DLL_DEBUG_EVENT = 6;

    public const int EXCEPTION_BREAKPOINT = -2147483645; // 0x80000003
    public const int EXCEPTION_SINGLE_STEP = -2147483644; // 0x80000004

    public const uint THREAD_ALL_ACCESS = 0x1FFFFF;
    public const uint PAGE_READWRITE = 0x04;
    
    // --- Context Flags (x64) ---
    public const uint CONTEXT_AMD64 = 0x00100000;
    public const uint CONTEXT64_CONTROL = CONTEXT_AMD64 | 0x00000001;
    public const uint CONTEXT64_INTEGER = CONTEXT_AMD64 | 0x00000002;
    public const uint CONTEXT64_SEGMENTS = CONTEXT_AMD64 | 0x00000004;
    public const uint CONTEXT64_FLOATING_POINT = CONTEXT_AMD64 | 0x00000008;
    public const uint CONTEXT64_DEBUG_REGISTERS = CONTEXT_AMD64 | 0x00000010;
    [SuppressMessage("ReSharper", "UnusedMember.Global")] 
    public const uint CONTEXT64_ALL = CONTEXT64_CONTROL | CONTEXT64_INTEGER | CONTEXT64_SEGMENTS | CONTEXT64_FLOATING_POINT | CONTEXT64_DEBUG_REGISTERS;

    // --- Context Flags (ARM64) ---
    public const uint CONTEXT_ARM64 = 0x00400000;
    public const uint CONTEXTARM64_CONTROL = CONTEXT_ARM64 | 0x00000001;
    public const uint CONTEXTARM64_INTEGER = CONTEXT_ARM64 | 0x00000002;
    public const uint CONTEXTARM64_FLOATING_POINT = CONTEXT_ARM64 | 0x00000004;
    public const uint CONTEXTARM64_DEBUG_REGISTERS = CONTEXT_ARM64 | 0x00000008;
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public const uint CONTEXTARM64_ALL = CONTEXTARM64_CONTROL | CONTEXTARM64_INTEGER | CONTEXTARM64_FLOATING_POINT | CONTEXTARM64_DEBUG_REGISTERS;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool CreateProcess(
        string? lpApplicationName,
        string lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        int dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref StartupInfo lpStartupInfo,
        out ProcessInformation lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool WaitForDebugEvent(out DebugEvent lpDebugEvent, int dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ContinueDebugEvent(uint dwProcessId, uint dwThreadId, int dwContinueStatus);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool VirtualProtectEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flNewProtect, out uint lpflOldProtect);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool FlushInstructionCache(IntPtr hProcess, IntPtr lpBaseAddress, uint dwSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenThread(uint dwDesiredAccess, bool bInheritHandle, uint dwThreadId);

    // Raw Pointer (For manual alignment and architecture agnostic calls)
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetThreadContext(IntPtr hThread, IntPtr lpContext);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetThreadContext(IntPtr hThread, IntPtr lpContext);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    public static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern int GetFinalPathNameByHandle(IntPtr hFile, [Out] StringBuilder lpszFilePath, int cchFilePath, int dwFlags);
}