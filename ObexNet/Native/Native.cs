using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace Win64;

/// <summary>
/// Provides Platform Invoke (P/Invoke) definitions for Win32 API calls required for debugging and process manipulation.
/// </summary>
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

    public const string LdrLoadDll = nameof(LdrLoadDll);
    /// <summary>
    /// Creates a new process and its primary thread. The new process runs in the security context of the calling process.
    /// </summary>
    /// <param name="lpApplicationName">The name of the module to be executed.</param>
    /// <param name="lpCommandLine">The command line to be executed.</param>
    /// <param name="lpProcessAttributes">A pointer to a SECURITY_ATTRIBUTES structure that determines whether the returned handle to the new process object can be inherited by child processes.</param>
    /// <param name="lpThreadAttributes">A pointer to a SECURITY_ATTRIBUTES structure that determines whether the returned handle to the new thread object can be inherited by child processes.</param>
    /// <param name="bInheritHandles">If this parameter is TRUE, each inheritable handle in the calling process is inherited by the new process.</param>
    /// <param name="dwCreationFlags">The flags that control the priority class and the creation of the process.</param>
    /// <param name="lpEnvironment">A pointer to the environment block for the new process.</param>
    /// <param name="lpCurrentDirectory">The full path to the current directory for the process.</param>
    /// <param name="lpStartupInfo">A pointer to a STARTUPINFO or STARTUPINFOEX structure.</param>
    /// <param name="lpProcessInformation">A pointer to a PROCESS_INFORMATION structure that receives identification information about the new process.</param>
    /// <returns>If the function succeeds, the return value is nonzero. If the function fails, the return value is zero.</returns>
    [DllImport(Imports.Kernel32, SetLastError = true, CharSet = CharSet.Unicode)]
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

    /// <summary>
    /// Waits for a debugging event to occur in a process being debugged.
    /// </summary>
    /// <param name="lpDebugEvent">A pointer to a DEBUG_EVENT structure that receives information about the debugging event.</param>
    /// <param name="dwMilliseconds">The number of milliseconds to wait for a debugging event.</param>
    /// <returns>If the function succeeds, the return value is nonzero. If the function fails, the return value is zero.</returns>
    [DllImport(Imports.Kernel32, SetLastError = true)]
    public static extern bool WaitForDebugEvent(out DebugEvent lpDebugEvent, int dwMilliseconds);

    /// <summary>
    /// Enables a debugger to continue a thread that previously reported a debugging event.
    /// </summary>
    /// <param name="dwProcessId">The process identifier of the process to continue.</param>
    /// <param name="dwThreadId">The thread identifier of the thread to continue.</param>
    /// <param name="dwContinueStatus">The options to continue the thread that reported the debugging event.</param>
    /// <returns>If the function succeeds, the return value is nonzero. If the function fails, the return value is zero.</returns>
    [DllImport(Imports.Kernel32, SetLastError = true)]
    public static extern bool ContinueDebugEvent(uint dwProcessId, uint dwThreadId, int dwContinueStatus);

    /// <summary>
    /// Reads data from an area of memory in a specified process. The entire area to be read must be accessible or the operation fails.
    /// </summary>
    /// <param name="hProcess">A handle to the process with memory that is being read.</param>
    /// <param name="lpBaseAddress">A pointer to the base address in the specified process from which to read.</param>
    /// <param name="lpBuffer">A pointer to a buffer that receives the contents from the address space of the specified process.</param>
    /// <param name="dwSize">The number of bytes to be read from the specified process.</param>
    /// <param name="lpNumberOfBytesRead">A pointer to a variable that receives the number of bytes transferred into the specified buffer.</param>
    /// <returns>If the function succeeds, the return value is nonzero.</returns>
    [DllImport(Imports.Kernel32, SetLastError = true)]
    public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

    /// <summary>
    /// Writes data to an area of memory in a specified process. The entire area to be written to must be accessible or the operation fails.
    /// </summary>
    /// <param name="hProcess">A handle to the process memory to be modified.</param>
    /// <param name="lpBaseAddress">A pointer to the base address in the specified process to which data is written.</param>
    /// <param name="lpBuffer">A pointer to the buffer that contains data to be written in the address space of the specified process.</param>
    /// <param name="dwSize">The number of bytes to be written to the specified process.</param>
    /// <param name="lpNumberOfBytesWritten">A pointer to a variable that receives the number of bytes transferred into the specified process.</param>
    /// <returns>If the function succeeds, the return value is nonzero.</returns>
    [DllImport(Imports.Kernel32, SetLastError = true)]
    public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesWritten);

    /// <summary>
    /// Changes the protection of a region of committed pages in the virtual address space of a specified process.
    /// </summary>
    /// <param name="hProcess">A handle to the process whose memory protection is to be changed.</param>
    /// <param name="lpAddress">A pointer to the base address of the region of pages whose access protection attributes are to be changed.</param>
    /// <param name="dwSize">The size of the region whose access protection attributes are changed, in bytes.</param>
    /// <param name="flNewProtect">The memory protection option.</param>
    /// <param name="lpflOldProtect">A pointer to a variable that receives the previous access protection of the first page in the specified region of pages.</param>
    /// <returns>If the function succeeds, the return value is nonzero.</returns>
    [DllImport(Imports.Kernel32, SetLastError = true)]
    public static extern bool VirtualProtectEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flNewProtect, out uint lpflOldProtect);

    /// <summary>
    /// Flushes the instruction cache for the specified process.
    /// </summary>
    /// <param name="hProcess">A handle to the process whose instruction cache is to be flushed.</param>
    /// <param name="lpBaseAddress">A pointer to the base of the region to be flushed.</param>
    /// <param name="dwSize">The size of the region to be flushed, in bytes.</param>
    /// <returns>If the function succeeds, the return value is nonzero.</returns>
    [DllImport(Imports.Kernel32, SetLastError = true)]
    public static extern bool FlushInstructionCache(IntPtr hProcess, IntPtr lpBaseAddress, uint dwSize);

    /// <summary>
    /// Opens an existing thread object.
    /// </summary>
    /// <param name="dwDesiredAccess">The access to the thread object.</param>
    /// <param name="bInheritHandle">If this value is TRUE, processes created by this process will inherit the handle.</param>
    /// <param name="dwThreadId">The identifier of the thread to be opened.</param>
    /// <returns>If the function succeeds, the return value is an open handle to the specified thread.</returns>
    [DllImport(Imports.Kernel32, SetLastError = true)]
    public static extern IntPtr OpenThread(uint dwDesiredAccess, bool bInheritHandle, uint dwThreadId);

    /// <summary>
    /// Retrieves the context of the specified thread.
    /// </summary>
    /// <param name="hThread">A handle to the thread whose context is to be retrieved.</param>
    /// <param name="lpContext">A pointer to a CONTEXT structure (or raw memory) that receives the appropriate context of the specified thread.</param>
    /// <returns>If the function succeeds, the return value is nonzero.</returns>
    [DllImport(Imports.Kernel32, SetLastError = true)]
    public static extern bool GetThreadContext(IntPtr hThread, IntPtr lpContext);

    /// <summary>
    /// Sets the context for the specified thread.
    /// </summary>
    /// <param name="hThread">A handle to the thread whose context is to be set.</param>
    /// <param name="lpContext">A pointer to a CONTEXT structure that contains the context to be set in the specified thread.</param>
    /// <returns>If the function succeeds, the return value is nonzero.</returns>
    [DllImport(Imports.Kernel32, SetLastError = true)]
    public static extern bool SetThreadContext(IntPtr hThread, IntPtr lpContext);

    /// <summary>
    /// Closes an open object handle.
    /// </summary>
    /// <param name="hObject">A valid handle to an open object.</param>
    /// <returns>If the function succeeds, the return value is nonzero.</returns>
    [DllImport(Imports.Kernel32, SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);

    /// <summary>
    /// Retrieves a module handle for the specified module. The module must have been loaded by the calling process.
    /// </summary>
    /// <param name="lpModuleName">The name of the loaded module (either a .dll or .exe file).</param>
    /// <returns>If the function succeeds, the return value is a handle to the specified module.</returns>
    [DllImport(Imports.Kernel32, CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string lpModuleName);

    /// <summary>
    /// Retrieves the address of an exported function or variable from the specified dynamic-link library (DLL).
    /// </summary>
    /// <remarks>
    /// <para>Original C definition:</para>
    /// <code>
    /// FARPROC GetProcAddress(
    ///   HMODULE hModule,
    ///   LPCSTR  lpProcName
    /// );
    /// </code>
    /// </remarks>
    /// <param name="hModule">A handle to the DLL module that contains the function or variable.</param>
    /// <param name="lpProcName">The function or variable name, or the function's ordinal value.</param>
    /// <returns>If the function succeeds, the return value is the address of the exported function or variable. If the function fails, the return value is NULL.</returns>
    [DllImport(Imports.Kernel32, CharSet = CharSet.Ansi, SetLastError = true)]
    public static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    /// <summary>
    /// Retrieves the final path for the specified file.
    /// </summary>
    /// <remarks>
    /// <para>Original C definition:</para>
    /// <code>
    /// DWORD GetFinalPathNameByHandleW(
    ///   HANDLE hFile,
    ///   LPWSTR lpszFilePath,
    ///   DWORD  cchFilePath,
    ///   DWORD  dwFlags
    /// );
    /// </code>
    /// </remarks>
    /// <param name="hFile">A handle to the file or directory.</param>
    /// <param name="lpszFilePath">A pointer to a buffer that receives the path.</param>
    /// <param name="cchFilePath">The size of lpszFilePath, in TCHARs.</param>
    /// <param name="dwFlags">The type of result to return.</param>
    /// <returns>If the function succeeds, the return value is the length of the string received by lpszFilePath, in TCHARs. If the function fails, the return value is zero.</returns>
    [DllImport(Imports.Kernel32, SetLastError = true, CharSet = CharSet.Auto)]
    public static extern int GetFinalPathNameByHandle(IntPtr hFile, [Out] StringBuilder lpszFilePath, int cchFilePath, int dwFlags);
}