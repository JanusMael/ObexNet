using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using Win64;

namespace Bennewitz.Ninja.ObexNet;

[SuppressMessage("ReSharper", "UnusedMethodReturnValue.Local")]
public sealed unsafe class Obex
{
    // --- Global State ---
    private readonly List<string> _dllBlockPatterns = [];
    private readonly HashSet<string> _blockedDllsHistory = [];
    private IntPtr _remoteNtdllBase = IntPtr.Zero;
    private IntPtr _remoteLdrAddr = IntPtr.Zero;
    private byte[] _origBytes = []; 
    private IntPtr _hProc = IntPtr.Zero;
    private uint _pendingRearmTid;
    private bool _isFirstDll = true;

    // --- Configuration Flags ---
    private readonly bool _silentMode;
    private readonly bool _jsonOutput;
    private readonly bool _interactiveMode;
    private readonly bool _fakeSuccess;
    private readonly bool _followChildren;

    // --- Architecture State ---
    private bool _isArm64;
    /// <summary>
    /// Byte sequence representing a Windows debug breakpoint
    /// </summary>
    /// <remarks>
    /// <list type="table">
    ///     <listheader>
    ///         <term>Architecture</term>
    ///         <description>Instruction</description>
    ///     </listheader>
    ///     <item>
    ///         <term>X64</term>
    ///         <description>INT 3</description>
    ///     </item>
    ///     <item>
    ///         <term>ARM64</term>
    ///         <description><p>$BRK #0xF000</p><p>Opcode: 0xD43E0000 (Little Endian: 00 00 3E D4)</p></description>
    ///     </item>
    /// </list>
    /// </remarks>
    private byte[] _bpBytes = [];

    public Obex(IEnumerable<string> blockPatterns, bool silentMode, bool jsonOutput, bool interactiveMode, bool fakeSuccess, bool followChildren)
    {
        _dllBlockPatterns.AddRange(blockPatterns);
        _silentMode = silentMode;
        _jsonOutput = jsonOutput;
        _interactiveMode = interactiveMode;
        _fakeSuccess = fakeSuccess;
        _followChildren = followChildren;

        InitializeArchitecture();
    }

    private void InitializeArchitecture()
    {
        var arch = AssemblyUtility.GetSupportedArchitectureOrThrow();
        switch (arch)
        {
            case Architecture.Arm64:
                _isArm64 = true;
                // BRK #0xF000 (Windows debug breakpoint for Arm64)
                // Opcode: 0xD43E0000 (Little Endian: 00 00 3E D4)
                _bpBytes = [0x00, 0x00, 0x3E, 0xD4];
                break;
            case Architecture.X64:
                _isArm64 = false;
                // INT 3
                _bpBytes = [0xCC];
                break;
            default:
                throw new PlatformNotSupportedException($"Unsupported Architecture: {arch}");
        }
    }

    public void Run(string targetCmd)
    {
        if (!_silentMode && !_jsonOutput)
        {
            PrintBanner();
            Console.WriteLine($"[*] Architecture: {RuntimeInformation.ProcessArchitecture}");
            Console.WriteLine($"[*] Command: '{targetCmd}'");
            Console.WriteLine($"[*] Block Patterns: '{string.Join(", ", _dllBlockPatterns)}'");
            Console.WriteLine($"[*] Options: Silent={_silentMode}, JSON={_jsonOutput}, Interactive={_interactiveMode}, FakeSuccess={_fakeSuccess}, FollowChildren={_followChildren}");
        }

        var si = new StartupInfo();
        si.cb = (uint)Marshal.SizeOf(si);

        var creationFlags = Native.DEBUG_ONLY_THIS_PROCESS;
        if (_followChildren)
        {
            creationFlags = Native.DEBUG_PROCESS; // Follow child processes
        }

        var success = Native.CreateProcess(
            null,
            targetCmd,
            IntPtr.Zero,
            IntPtr.Zero,
            false,
            creationFlags,
            IntPtr.Zero,
            null,
            ref si,
            out var pi);

        if (!success)
        {
            LogError($"CreateProcess failed: {Marshal.GetLastWin32Error()}");
            return;
        }

        LogInfo("Started process");
        _hProc = pi.hProcess;

        var done = false;

        while (!done)
        {
            if (!Native.WaitForDebugEvent(out var debugEvent, Native.INFINITE))
            {
                LogError($"WaitForDebugEvent failed: {Marshal.GetLastWin32Error()}");
                break;
            }

            var continueStatus = Native.DBG_CONTINUE;

            switch (debugEvent.dwDebugEventCode)
            {
                case Native.CREATE_PROCESS_DEBUG_EVENT:
                    if (_followChildren && debugEvent.dwProcessId != pi.dwProcessId)
                    {
                        LogInfo($"Attached to child process PID: {debugEvent.dwProcessId}");
                    }
                    Native.CloseHandle(debugEvent.CreateProcessInfo.hFile);
                    break;

                case Native.LOAD_DLL_DEBUG_EVENT:
                    HandleLoadDll(debugEvent);
                    break;

                case Native.EXCEPTION_DEBUG_EVENT:
                    var code = debugEvent.Exception.ExceptionRecord.ExceptionCode;
                    var exceptionAddr = debugEvent.Exception.ExceptionRecord.ExceptionAddress;

                    if (code == unchecked((uint)Native.EXCEPTION_BREAKPOINT))
                    {
                        if (_remoteLdrAddr != IntPtr.Zero && exceptionAddr == _remoteLdrAddr)
                        {
                            var ret = HandleLdrCall(debugEvent.dwThreadId);
                            if (ret == 2 || ret == 1)
                            {
                                continueStatus = Native.DBG_CONTINUE;
                            }
                        }
                        else
                        {
                            continueStatus = Native.DBG_EXCEPTION_NOT_HANDLED;
                        }
                        break;
                    }

                    if (code == unchecked((uint)Native.EXCEPTION_SINGLE_STEP) && _remoteLdrAddr != IntPtr.Zero)
                    {
                        RearmAfterSingleStep(debugEvent);
                        continueStatus = Native.DBG_CONTINUE;
                        break;
                    }

                    continueStatus = Native.DBG_EXCEPTION_NOT_HANDLED;
                    break;

                case Native.EXIT_PROCESS_DEBUG_EVENT:
                    if (debugEvent.dwProcessId == pi.dwProcessId)
                    {
                        LogInfo($"Child exited with code: {debugEvent.ExitProcess.dwExitCode}");
                        continueStatus = Native.DBG_CONTINUE;
                        done = true;
                    }
                    else
                    {
                        LogInfo($"Child process {debugEvent.dwProcessId} exited with code: {debugEvent.ExitProcess.dwExitCode}");
                    }
                    break;
            }

            Native.ContinueDebugEvent(debugEvent.dwProcessId, debugEvent.dwThreadId, continueStatus);
        }

        if (_hProc != IntPtr.Zero)
        {
            Native.CloseHandle(_hProc);
        }

        if (pi.hThread != IntPtr.Zero)
        {
            Native.CloseHandle(pi.hThread);
        }
    }

    private void HandleLoadDll(DebugEvent debugEvent)
    {
        if (_remoteNtdllBase != IntPtr.Zero)
        {
            return;
        }

        var dllName = string.Empty;

        if (debugEvent.LoadDll.hFile != IntPtr.Zero)
        {
            var sb = new StringBuilder(1024);
            var result = Native.GetFinalPathNameByHandle(debugEvent.LoadDll.hFile, sb, 1024, 0);
            if (result > 0)
            {
                dllName = Path.GetFileName(sb.ToString());
            }
            Native.CloseHandle(debugEvent.LoadDll.hFile);
        }

        var isNtdll = string.Equals(dllName, Imports.NtDll, StringComparison.OrdinalIgnoreCase);

        if (!isNtdll)
        {
            var localNtdll = Native.GetModuleHandle(Imports.NtDll);
            if (localNtdll != IntPtr.Zero && debugEvent.LoadDll.lpBaseOfDll == localNtdll)
            {
                isNtdll = true;
            }
        }

        if (!isNtdll && _isFirstDll)
        {
            isNtdll = true;
        }

        _isFirstDll = false;

        if (isNtdll)
        {
            _remoteNtdllBase = debugEvent.LoadDll.lpBaseOfDll;

            var localNtdll = Native.GetModuleHandle(Imports.NtDll);
            var localLdr = Native.GetProcAddress(localNtdll, Native.LdrLoadDll);

            if (localNtdll != IntPtr.Zero && localLdr != IntPtr.Zero)
            {
                // Only check for 0xE9 (JMP) on x64
                if (!_isArm64)
                {
                    var firstByte = *(byte*)localLdr;
                    if (firstByte == 0xE9)
                    {
                        LogWarning($"Local {Native.LdrLoadDll} starts with JMP (0xE9). Offset calculation may be incorrect due to local hooks!");
                    }
                }

                var offset = localLdr - (long) localNtdll;
                _remoteLdrAddr = new IntPtr(_remoteNtdllBase + offset);

                SetBreakpoint(_remoteLdrAddr, out _origBytes);
                LogInfo($"Hooked {Native.LdrLoadDll} at 0x{_remoteLdrAddr:X} (ntdll at 0x{_remoteNtdllBase:X})");
            }
        }
    }

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private int HandleLdrCall(uint threadId)
    {
        var hThread = Native.OpenThread(Native.THREAD_ALL_ACCESS, false, threadId);
        if (hThread == IntPtr.Zero)
        {
            LogDebug($"Failed to OpenThread. Error: {Marshal.GetLastWin32Error()}");
            return 0;
        }

        // Determine Context Size and Alignment based on Arch
        var ctxSize = _isArm64 ? Marshal.SizeOf(typeof(ContextArm64)) : Marshal.SizeOf(typeof(Context64));
        var pRaw = Marshal.AllocHGlobal(ctxSize + 16);
        var pAligned = new IntPtr((pRaw.ToInt64() + 15) & ~15);

        try
        {
            if (_isArm64)
            {
                Marshal.WriteInt32(pAligned, 0, (int)(Native.CONTEXTARM64_CONTROL | Native.CONTEXTARM64_INTEGER));
            }
            else
            {
                Marshal.WriteInt32(pAligned, 48, (int)(Native.CONTEXT64_CONTROL | Native.CONTEXT64_INTEGER));
            }

            if (!Native.GetThreadContext(hThread, pAligned))
            {
                LogDebug($"Failed to GetThreadContext. Error: {Marshal.GetLastWin32Error()}");
                return 0;
            }

            // Restore original bytes to allow execution
            WriteCode(_remoteLdrAddr, _origBytes, _bpBytes.Length);

            // Variables to hold register values generically
            ulong ip;
            ulong arg3_DllNamePtr; 
            ulong arg4_ResultPtr;
            // ulong retAddr = 0; // Only needed for x64 stack fix, or LR on Arm64

            // --- Read Registers ---
            if (_isArm64)
            {
                var ctx = Marshal.PtrToStructure<ContextArm64>(pAligned);
                ctx.Pc = (ulong)_remoteLdrAddr; // Rewind PC
                ip = ctx.Pc;
                arg3_DllNamePtr = ctx.X2;
                arg4_ResultPtr = ctx.X3;
                
                // Write back rewound PC
                Marshal.StructureToPtr(ctx, pAligned, false);
            }
            else
            {
                var ctx = Marshal.PtrToStructure<Context64>(pAligned);
                ctx.Rip = (ulong)_remoteLdrAddr; // Rewind RIP
                ip = ctx.Rip;
                arg3_DllNamePtr = ctx.R8;
                arg4_ResultPtr = ctx.R9;
                
                // Write back rewound RIP
                Marshal.StructureToPtr(ctx, pAligned, false);
            }
            
            LogDebug($"{nameof(ip)}={ip}");

            // --- Analyze Arguments ---
            var shouldBlock = false;
            string? dllName = null;

            if (arg3_DllNamePtr != 0)
            {
                var usSize = Marshal.SizeOf(typeof(UnicodeString));
                var usBuffer = new byte[usSize];

                if (ReadRemote((IntPtr)arg3_DllNamePtr, usBuffer, usSize))
                {
                    var handle = GCHandle.Alloc(usBuffer, GCHandleType.Pinned);
                    var us = (UnicodeString)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(UnicodeString))!;
                    handle.Free();

                    if (us.Buffer != 0 && us.Length > 0)
                    {
                        var strBytes = new byte[us.Length];
                        if (ReadRemote(us.Buffer, strBytes, us.Length))
                        {
                            dllName = Encoding.Unicode.GetString(strBytes);
                            shouldBlock = ShouldBlockDll(dllName);
                        }
                    }
                }
            }

            if (!shouldBlock)
            {
                // Re-enable Single Step to re-apply breakpoint after instruction executes
                SetTfHandling(hThread, pAligned, threadId);
                return 1;
            }

            // --- BLOCKING LOGIC ---
            if (_blockedDllsHistory.Add(dllName!))
            {
                LogEvent("blocked", dllName!);
            }

            if (_interactiveMode)
            {
                Console.Write($"[?] Block loading of '{dllName}'? (Y/n): ");
                var input = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(input) && input.Trim().ToLower().StartsWith('n'))
                {
                    SetTfHandling(hThread, pAligned, threadId);
                    return 1;
                }
            }

            // --- Zero out the BaseAddress Output (Arg4) ---
            const ulong zero = 0;
            if (arg4_ResultPtr != 0)
            {
                WriteRemote((IntPtr)arg4_ResultPtr, BitConverter.GetBytes(zero), 8);
            }

            // --- Modify Return Value and Flow ---
            if (_isArm64)
            {
                var ctx = Marshal.PtrToStructure<ContextArm64>(pAligned);
                // Return Value in X0
                ctx.X0 = _fakeSuccess ? 0 : 0xC0000135; // STATUS_SUCCESS or STATUS_DLL_NOT_FOUND
                
                // Return Address is in LR (X30) for ARM64
                ctx.Pc = ctx.Lr;

                Marshal.StructureToPtr(ctx, pAligned, false);
            }
            else
            {
                var ctx = Marshal.PtrToStructure<Context64>(pAligned);
                // Return Value in Rax
                ctx.Rax = _fakeSuccess ? 0 : 0xC0000135;

                // Return Address is on Stack for x64
                var retAddrBytes = new byte[8];
                if (ReadRemote((IntPtr)ctx.Rsp, retAddrBytes, 8))
                {
                    var retAddr = BitConverter.ToUInt64(retAddrBytes, 0);
                    ctx.Rsp += 8; // Pop stack
                    ctx.Rip = retAddr;
                }

                Marshal.StructureToPtr(ctx, pAligned, false);
            }

            // Re-apply breakpoint immediately since we are simulating return (skipping execution)
            SetBreakpoint(_remoteLdrAddr, out _origBytes);

            if (!Native.SetThreadContext(hThread, pAligned))
            {
                LogDebug($"Failed to SetThreadContext (Block). Error: {Marshal.GetLastWin32Error()}");
            }

            return 2;
        }
        finally
        {
            Marshal.FreeHGlobal(pRaw);
            Native.CloseHandle(hThread);
        }
    }

    private void RearmAfterSingleStep(DebugEvent de)
    {
        if (_remoteLdrAddr == IntPtr.Zero || de.dwThreadId != _pendingRearmTid)
        {
            return;
        }

        var hThread = Native.OpenThread(Native.THREAD_ALL_ACCESS, false, de.dwThreadId);
        if (hThread == IntPtr.Zero)
        {
            return;
        }

        // Determine Context Size
        var ctxSize = _isArm64 ? Marshal.SizeOf(typeof(ContextArm64)) : Marshal.SizeOf(typeof(Context64));
        var pRaw = Marshal.AllocHGlobal(ctxSize + 16);
        var pAligned = new IntPtr((pRaw.ToInt64() + 15) & ~15);

        try
        {
            if (_isArm64)
            {
                Marshal.WriteInt32(pAligned, 0, (int)Native.CONTEXTARM64_CONTROL);
                if (Native.GetThreadContext(hThread, pAligned))
                {
                    var ctx = Marshal.PtrToStructure<ContextArm64>(pAligned);
                    ctx.Cpsr &= ~0x200000u; // Clear SS (Bit 21)
                    Marshal.StructureToPtr(ctx, pAligned, false);
                    Native.SetThreadContext(hThread, pAligned);
                }
            }
            else
            {
                Marshal.WriteInt32(pAligned, 48, (int)Native.CONTEXT64_CONTROL);
                if (Native.GetThreadContext(hThread, pAligned))
                {
                    var ctx = Marshal.PtrToStructure<Context64>(pAligned);
                    ctx.EFlags &= ~0x100u; // Clear TF
                    Marshal.StructureToPtr(ctx, pAligned, false);
                    Native.SetThreadContext(hThread, pAligned);
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(pRaw);
            Native.CloseHandle(hThread);
        }

        SetBreakpoint(_remoteLdrAddr, out _origBytes);
        _pendingRearmTid = 0;
    }

    private void SetTfHandling(IntPtr hThread, IntPtr pAlignedContext, uint threadId)
    {
        if (_isArm64)
        {
            var ctx = Marshal.PtrToStructure<ContextArm64>(pAlignedContext);
            ctx.Cpsr |= 0x200000u; // Set SS (Bit 21)
            Marshal.StructureToPtr(ctx, pAlignedContext, false);
        }
        else
        {
            var ctx = Marshal.PtrToStructure<Context64>(pAlignedContext);
            ctx.EFlags |= 0x100; // Set TF
            Marshal.StructureToPtr(ctx, pAlignedContext, false);
        }

        Native.SetThreadContext(hThread, pAlignedContext);
        _pendingRearmTid = threadId;
    }

    private bool ShouldBlockDll(string? dllPath)
    {
        if (string.IsNullOrEmpty(dllPath))
        {
            return false;
        }

        var fname = Path.GetFileName(dllPath);

        foreach (var pattern in _dllBlockPatterns)
        {
            if (pattern.Contains('*') || pattern.Contains('?'))
            {
                var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
                if (Regex.IsMatch(fname, regexPattern, RegexOptions.IgnoreCase))
                {
                    return true;
                }
            }
            else if (string.Equals(fname, pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private bool SetBreakpoint(IntPtr addr, out byte[] savedBytes)
    {
        savedBytes = new byte[_bpBytes.Length];
        if (!ReadRemote(addr, savedBytes, _bpBytes.Length))
        {
            return false;
        }

        var ok = WriteCode(addr, _bpBytes, _bpBytes.Length);
        return ok;
    }

    private bool ReadRemote(IntPtr addr, byte[] buffer, int size)
    {
        return Native.ReadProcessMemory(_hProc, addr, buffer, size, out var bytesRead) && bytesRead == size;
    }

    private bool WriteRemote(IntPtr addr, byte[] buffer, int size)
    {
        var res = Native.WriteProcessMemory(_hProc, addr, buffer, size, out var bytesWritten) && bytesWritten == size;
        if (res)
        {
            Native.FlushInstructionCache(_hProc, addr, (uint)size);
        }

        return res;
    }

    private bool WriteCode(IntPtr addr, byte[] buffer, int size)
    {
        if (!Native.VirtualProtectEx(_hProc, addr, (uint)size, Native.PAGE_READWRITE, out var oldProt))
        {
            return false;
        }

        var ok = WriteRemote(addr, buffer, size);
        Native.VirtualProtectEx(_hProc, addr, (uint)size, oldProt, out _);
        return ok;
    }

    // --- Logging Helpers ---

    private static void PrintBanner()
    {
        Console.WriteLine("""
                            *
                             ____  _                  _   _      _   
                            / __ \| |                | \ | |    | |  
                           | |  | | |__   ___ __  __ |  \| | ___| |_ 
                           | |  | | '_ \ / _ \|_\/_| | . ` |/ _ \ __|
                           | |__| | |_) |  __/  /\   |  \  |  __/ |_ 
                            \____/|_.__/ \___||_|\_| ,_| \_|\___|\__|
                                                                 
                          """);
    }

    private void LogInfo(string message)
    {
        if (_silentMode || _jsonOutput)
        {
            return;
        }

        Console.WriteLine($"[+] {message}");
    }

    private void LogWarning(string message)
    {
        if (_jsonOutput)
        {
            return;
        }
        Console.WriteLine();
        Console.WriteLine($"[!] {message}");
    }

    private void LogError(string message)
    {
        if (_jsonOutput)
        {
            var json = JsonSerializer.Serialize(
                new JsonError(
                    Type: "error",
                    Message: message),
                ObexJsonContext.Default.JsonError);
            Console.WriteLine(json);
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine($"[-] ERROR: {message}");
        }
    }

    [SuppressMessage("ReSharper", "UnusedParameter.Local")]
    private void LogDebug(string message)
    {
        if (_silentMode || _jsonOutput)
        {
            return;
        }

#if DEBUG
        Console.WriteLine($"[Debug] {message}");
#endif
    }

    private void LogEvent(string type, string target)
    {
        if (_jsonOutput)
        {
            var json = JsonSerializer.Serialize(
                new JsonEvent(
                    Timestamp: DateTime.UtcNow,
                    Type: type,
                    Target: target,
                    Action: _fakeSuccess ? "fake_success" : "block"),
                ObexJsonContext.Default.JsonEvent);
            Console.WriteLine(json);
        }
        else
        {
            Console.WriteLine($"[+] Blocked loading of '{target}'");
        }
    }
}