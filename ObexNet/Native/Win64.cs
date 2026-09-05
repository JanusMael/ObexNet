using System.Runtime.InteropServices;

namespace Win64;

public static class Imports
{
    public const string NtDll = "ntdll.dll";
    public const string Kernel32 = "kernel32.dll";
}

// ==============================================================================================
// 2. XMM_SAVE_AREA32
// ==============================================================================================

/// <summary>
/// Represents the XMM save area used in the context structure for x64 processors.
/// </summary>
/// <remarks>
/// <para>Original C definition:</para>
/// <code>
/// typedef struct _XMM_SAVE_AREA32 {
///     WORD   ControlWord;
///     WORD   StatusWord;
///     BYTE   TagWord;
///     BYTE   Reserved1;
///     WORD   ErrorOpcode;
///     DWORD  ErrorOffset;
///     WORD   ErrorSelector;
///     WORD   Reserved2;
///     DWORD  DataOffset;
///     WORD   DataSelector;
///     WORD   Reserved3;
///     DWORD  MxCsr;
///     DWORD  MxCsr_Mask;
///     M128A  FloatRegisters[8];
///     M128A  XmmRegisters[16];
///     BYTE   Reserved4[96];
/// } XMM_SAVE_AREA32, *PXMM_SAVE_AREA32;
/// </code>
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct XmmSaveArea32
{
    public ushort ControlWord;
    public ushort StatusWord;
    public byte TagWord;
    public byte Reserved1;
    public ushort ErrorOpcode;
    public uint ErrorOffset;
    public ushort ErrorSelector;
    public ushort Reserved2;
    public uint DataOffset;
    public ushort DataSelector;
    public ushort Reserved3;
    public uint MxCsr;
    public uint MxCsr_Mask;
    public fixed byte FloatRegisters[128];
    public fixed byte XmmRegisters[256];
    public fixed byte Reserved4[96];
}

// ==============================================================================================
// 3. CONTEXT (x64)
// ==============================================================================================

/// <summary>
/// Contains processor-specific register data for an x64 (AMD64) thread.
/// </summary>
/// <remarks>
/// <para>Original C definition (from winnt.h):</para>
/// <code>
/// typedef struct DECLSPEC_ALIGN(16) _CONTEXT {
///     DWORD64 P1Home;
///     DWORD64 P2Home;
///     DWORD64 P3Home;
///     DWORD64 P4Home;
///     DWORD64 P5Home;
///     DWORD64 P6Home;
///     DWORD   ContextFlags;
///     DWORD   MxCsr;
///     WORD    SegCs;
///     WORD    SegDs;
///     WORD    SegEs;
///     WORD    SegFs;
///     WORD    SegGs;
///     WORD    SegSs;
///     DWORD   EFlags;
///     DWORD64 Dr0;
///     DWORD64 Dr1;
///     DWORD64 Dr2;
///     DWORD64 Dr3;
///     DWORD64 Dr6;
///     DWORD64 Dr7;
///     DWORD64 Rax;
///     DWORD64 Rcx;
///     DWORD64 Rdx;
///     DWORD64 Rbx;
///     DWORD64 Rsp;
///     DWORD64 Rbp;
///     DWORD64 Rsi;
///     DWORD64 Rdi;
///     DWORD64 R8;
///     DWORD64 R9;
///     DWORD64 R10;
///     DWORD64 R11;
///     DWORD64 R12;
///     DWORD64 R13;
///     DWORD64 R14;
///     DWORD64 R15;
///     DWORD64 Rip;
///     union {
///         XMM_SAVE_AREA32 FltSave;
///         struct {
///             M128A Header[2];
///             M128A Legacy[8];
///             M128A Xmm0;
///             M128A Xmm1;
///             M128A Xmm2;
///             M128A Xmm3;
///             M128A Xmm4;
///             M128A Xmm5;
///             M128A Xmm6;
///             M128A Xmm7;
///             M128A Xmm8;
///             M128A Xmm9;
///             M128A Xmm10;
///             M128A Xmm11;
///             M128A Xmm12;
///             M128A Xmm13;
///             M128A Xmm14;
///             M128A Xmm15;
///         } DUMMYSTRUCTNAME;
///     } DUMMYUNIONNAME;
///     M128A VectorRegister[26];
///     DWORD64 VectorControl;
///     DWORD64 DebugControl;
///     DWORD64 LastBranchToRip;
///     DWORD64 LastBranchFromRip;
///     DWORD64 LastExceptionToRip;
///     DWORD64 LastExceptionFromRip;
/// } CONTEXT, *PCONTEXT;
/// </code>
/// </remarks>
[StructLayout(LayoutKind.Explicit, Size = 1232)]
public unsafe struct Context64
{
    [FieldOffset(0x000)] public ulong P1Home;
    [FieldOffset(0x008)] public ulong P2Home;
    [FieldOffset(0x010)] public ulong P3Home;
    [FieldOffset(0x018)] public ulong P4Home;
    [FieldOffset(0x020)] public ulong P5Home;
    [FieldOffset(0x028)] public ulong P6Home;

    [FieldOffset(0x030)] public uint ContextFlags;
    [FieldOffset(0x034)] public uint MxCsr;

    [FieldOffset(0x038)] public ushort SegCs;
    [FieldOffset(0x03A)] public ushort SegDs;
    [FieldOffset(0x03C)] public ushort SegEs;
    [FieldOffset(0x03E)] public ushort SegFs;
    [FieldOffset(0x040)] public ushort SegGs;
    [FieldOffset(0x042)] public ushort SegSs;
    [FieldOffset(0x044)] public uint EFlags;

    [FieldOffset(0x048)] public ulong Dr0;
    [FieldOffset(0x050)] public ulong Dr1;
    [FieldOffset(0x058)] public ulong Dr2;
    [FieldOffset(0x060)] public ulong Dr3;
    [FieldOffset(0x068)] public ulong Dr6;
    [FieldOffset(0x070)] public ulong Dr7;

    [FieldOffset(0x078)] public ulong Rax;
    [FieldOffset(0x080)] public ulong Rcx;
    [FieldOffset(0x088)] public ulong Rdx;
    [FieldOffset(0x090)] public ulong Rbx;
    [FieldOffset(0x098)] public ulong Rsp;
    [FieldOffset(0x0A0)] public ulong Rbp;
    [FieldOffset(0x0A8)] public ulong Rsi;
    [FieldOffset(0x0B0)] public ulong Rdi;
    [FieldOffset(0x0B8)] public ulong R8;
    [FieldOffset(0x0C0)] public ulong R9;
    [FieldOffset(0x0C8)] public ulong R10;
    [FieldOffset(0x0D0)] public ulong R11;
    [FieldOffset(0x0D8)] public ulong R12;
    [FieldOffset(0x0E0)] public ulong R13;
    [FieldOffset(0x0E8)] public ulong R14;
    [FieldOffset(0x0F0)] public ulong R15;

    [FieldOffset(0x0F8)] public ulong Rip;

    [FieldOffset(0x100)] public XmmSaveArea32 FltSave;
    [FieldOffset(0x300)] public fixed byte VectorRegister[416];
    [FieldOffset(0x4A0)] public ulong VectorControl;

    [FieldOffset(0x4A8)] public ulong DebugControl;
    [FieldOffset(0x4B0)] public ulong LastBranchToRip;
    [FieldOffset(0x4B8)] public ulong LastBranchFromRip;
    [FieldOffset(0x4C0)] public ulong LastExceptionToRip;
    [FieldOffset(0x4C8)] public ulong LastExceptionFromRip;
}

// ==============================================================================================
// 4. DEBUG_EVENT and Dependencies
// ==============================================================================================

/// <summary>
/// Describes an exception.
/// </summary>
/// <remarks>
/// <para>Original C definition:</para>
/// <code>
/// typedef struct _EXCEPTION_RECORD {
///     DWORD                    ExceptionCode;
///     DWORD                    ExceptionFlags;
///     struct _EXCEPTION_RECORD *ExceptionRecord;
///     PVOID                    ExceptionAddress;
///     DWORD                    NumberParameters;
///     ULONG_PTR                ExceptionInformation[EXCEPTION_MAXIMUM_PARAMETERS];
/// } EXCEPTION_RECORD, *PEXCEPTION_RECORD;
/// </code>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct ExceptionRecord
{
    public uint ExceptionCode;
    public uint ExceptionFlags;
    public IntPtr ExceptionRecordPtr;
    public IntPtr ExceptionAddress;
    public uint NumberParameters;
    public fixed ulong ExceptionInformation[15];
}

/// <summary>
/// Contains exception information that allows a debugger to service an exception.
/// </summary>
/// <remarks>
/// <para>Original C definition:</para>
/// <code>
/// typedef struct _EXCEPTION_DEBUG_INFO {
///     EXCEPTION_RECORD ExceptionRecord;
///     DWORD            dwFirstChance;
/// } EXCEPTION_DEBUG_INFO, *LPEXCEPTION_DEBUG_INFO;
/// </code>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct ExceptionDebugInfo
{
    public ExceptionRecord ExceptionRecord;
    public uint dwFirstChance;
}

/// <summary>
/// Contains thread creation information that can be used by a debugger.
/// </summary>
/// <remarks>
/// <para>Original C definition:</para>
/// <code>
/// typedef struct _CREATE_THREAD_DEBUG_INFO {
///     HANDLE hThread;
///     LPVOID lpThreadLocalBase;
///     LPTHREAD_START_ROUTINE lpStartAddress;
/// } CREATE_THREAD_DEBUG_INFO, *LPCREATE_THREAD_DEBUG_INFO;
/// </code>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct CreateThreadDebugInfo
{
    public IntPtr hThread;
    public IntPtr lpThreadLocalBase;
    public IntPtr lpStartAddress;
}

/// <summary>
/// Contains process creation information that can be used by a debugger.
/// </summary>
/// <remarks>
/// <para>Original C definition:</para>
/// <code>
/// typedef struct _CREATE_PROCESS_DEBUG_INFO {
///     HANDLE hFile;
///     HANDLE hProcess;
///     HANDLE hThread;
///     LPVOID lpBaseOfImage;
///     DWORD  dwDebugInfoFileOffset;
///     DWORD  nDebugInfoSize;
///     LPVOID lpThreadLocalBase;
///     LPTHREAD_START_ROUTINE lpStartAddress;
///     LPVOID lpImageName;
///     WORD   fUnicode;
/// } CREATE_PROCESS_DEBUG_INFO, *LPCREATE_PROCESS_DEBUG_INFO;
/// </code>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct CreateProcessDebugInfo
{
    public IntPtr hFile;
    public IntPtr hProcess;
    public IntPtr hThread;
    public IntPtr lpBaseOfImage;
    public uint dwDebugInfoFileOffset;
    public uint nDebugInfoSize;
    public IntPtr lpThreadLocalBase;
    public IntPtr lpStartAddress;
    public IntPtr lpImageName;
    public ushort fUnicode;
}

/// <summary>
/// Contains the exit code for a terminating thread.
/// </summary>
/// <remarks>
/// <para>Original C definition:</para>
/// <code>
/// typedef struct _EXIT_THREAD_DEBUG_INFO {
///     DWORD dwExitCode;
/// } EXIT_THREAD_DEBUG_INFO, *LPEXIT_THREAD_DEBUG_INFO;
/// </code>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct ExitThreadDebugInfo
{
    public uint dwExitCode;
}

/// <summary>
/// Contains the exit code for a terminating process.
/// </summary>
/// <remarks>
/// <para>Original C definition:</para>
/// <code>
/// typedef struct _EXIT_PROCESS_DEBUG_INFO {
///     DWORD dwExitCode;
/// } EXIT_PROCESS_DEBUG_INFO, *LPEXIT_PROCESS_DEBUG_INFO;
/// </code>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct ExitProcessDebugInfo
{
    public uint dwExitCode;
}

/// <summary>
/// Contains information about a dynamic-link library (DLL) that has just been loaded.
/// </summary>
/// <remarks>
/// <para>Original C definition:</para>
/// <code>
/// typedef struct _LOAD_DLL_DEBUG_INFO {
///     HANDLE hFile;
///     LPVOID lpBaseOfDll;
///     DWORD  dwDebugInfoFileOffset;
///     DWORD  nDebugInfoSize;
///     LPVOID lpImageName;
///     WORD   fUnicode;
/// } LOAD_DLL_DEBUG_INFO, *LPLOAD_DLL_DEBUG_INFO;
/// </code>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct LoadDllDebugInfo
{
    public IntPtr hFile;
    public IntPtr lpBaseOfDll;
    public uint dwDebugInfoFileOffset;
    public uint nDebugInfoSize;
    public IntPtr lpImageName;
    public ushort fUnicode;
}

/// <summary>
/// Contains information about a dynamic-link library (DLL) that has just been unloaded.
/// </summary>
/// <remarks>
/// <para>Original C definition:</para>
/// <code>
/// typedef struct _UNLOAD_DLL_DEBUG_INFO {
///     LPVOID lpBaseOfDll;
/// } UNLOAD_DLL_DEBUG_INFO, *LPUNLOAD_DLL_DEBUG_INFO;
/// </code>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct UnloadDllDebugInfo
{
    public IntPtr lpBaseOfDll;
}

/// <summary>
/// Contains the address, format, and length of a debugging string.
/// </summary>
/// <remarks>
/// <para>Original C definition:</para>
/// <code>
/// typedef struct _OUTPUT_DEBUG_STRING_INFO {
///     LPSTR lpDebugStringData;
///     WORD  fUnicode;
///     WORD  nDebugStringLength;
/// } OUTPUT_DEBUG_STRING_INFO, *LPOUTPUT_DEBUG_STRING_INFO;
/// </code>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct OutputDebugStringInfo
{
    public IntPtr lpDebugStringData;
    public ushort fUnicode;
    public ushort nDebugStringLength;
}

/// <summary>
/// Contains the error associated with a RIP_EVENT debug event.
/// </summary>
/// <remarks>
/// <para>Original C definition:</para>
/// <code>
/// typedef struct _RIP_INFO {
///     DWORD dwError;
///     DWORD dwType;
/// } RIP_INFO, *LPRIP_INFO;
/// </code>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct RipInfo
{
    public uint dwError;
    public uint dwType;
}

/// <summary>
/// Describes a debugging event.
/// </summary>
/// <remarks>
/// <para>Original C definition:</para>
/// <code>
/// typedef struct _DEBUG_EVENT {
///     DWORD dwDebugEventCode;
///     DWORD dwProcessId;
///     DWORD dwThreadId;
///     union {
///         EXCEPTION_DEBUG_INFO      Exception;
///         CREATE_THREAD_DEBUG_INFO  CreateThread;
///         CREATE_PROCESS_DEBUG_INFO CreateProcessInfo;
///         EXIT_THREAD_DEBUG_INFO    ExitThread;
///         EXIT_PROCESS_DEBUG_INFO   ExitProcess;
///         LOAD_DLL_DEBUG_INFO       LoadDll;
///         UNLOAD_DLL_DEBUG_INFO     UnloadDll;
///         OUTPUT_DEBUG_STRING_INFO  DebugString;
///         RIP_INFO                  RipInfo;
///     } u;
/// } DEBUG_EVENT, *LPDEBUG_EVENT;
/// </code>
/// </remarks>
[StructLayout(LayoutKind.Explicit)]
public struct DebugEvent
{
    [FieldOffset(0)] public uint dwDebugEventCode;
    [FieldOffset(4)] public uint dwProcessId;
    [FieldOffset(8)] public uint dwThreadId;

    [FieldOffset(16)] public ExceptionDebugInfo Exception;
    [FieldOffset(16)] public CreateThreadDebugInfo CreateThread;
    [FieldOffset(16)] public CreateProcessDebugInfo CreateProcessInfo;
    [FieldOffset(16)] public ExitThreadDebugInfo ExitThread;
    [FieldOffset(16)] public ExitProcessDebugInfo ExitProcess;
    [FieldOffset(16)] public LoadDllDebugInfo LoadDll;
    [FieldOffset(16)] public UnloadDllDebugInfo UnloadDll;
    [FieldOffset(16)] public OutputDebugStringInfo DebugString;
    [FieldOffset(16)] public RipInfo RipInfo;
}

// ==============================================================================================
// 5. Additional Process Creation Structures
// ==============================================================================================

/// <summary>
/// The UNICODE_STRING structure is used to define Unicode strings.
/// </summary>
/// <remarks>
/// <para>Original C definition (from subauth.h/winternl.h):</para>
/// <code>
/// typedef struct _UNICODE_STRING {
///     USHORT Length;
///     USHORT MaximumLength;
///     PWSTR  Buffer;
/// } UNICODE_STRING, *PUNICODE_STRING;
/// </code>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct UnicodeString
{
    public ushort Length;
    public ushort MaximumLength;
    public IntPtr Buffer;
}

/// <summary>
/// Specifies the window station, desktop, standard handles, and appearance of the main window for a process at creation time.
/// </summary>
/// <remarks>
/// <para>Original C definition:</para>
/// <code>
/// typedef struct _STARTUPINFOA {
///     DWORD  cb;
///     LPSTR  lpReserved;
///     LPSTR  lpDesktop;
///     LPSTR  lpTitle;
///     DWORD  dwX;
///     DWORD  dwY;
///     DWORD  dwXSize;
///     DWORD  dwYSize;
///     DWORD  dwXCountChars;
///     DWORD  dwYCountChars;
///     DWORD  dwFillAttribute;
///     DWORD  dwFlags;
///     WORD   wShowWindow;
///     WORD   cbReserved2;
///     LPBYTE lpReserved2;
///     HANDLE hStdInput;
///     HANDLE hStdOutput;
///     HANDLE hStdError;
/// } STARTUPINFOA, *LPSTARTUPINFOA;
/// </code>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct StartupInfo
{
    public uint cb;
    public IntPtr lpReserved;
    public IntPtr lpDesktop;
    public IntPtr lpTitle;
    public uint dwX;
    public uint dwY;
    public uint dwXSize;
    public uint dwYSize;
    public uint dwXCountChars;
    public uint dwYCountChars;
    public uint dwFillAttribute;
    public uint dwFlags;
    public ushort wShowWindow;
    public ushort cbReserved2;
    public IntPtr lpReserved2;
    public IntPtr hStdInput;
    public IntPtr hStdOutput;
    public IntPtr hStdError;
}

/// <summary>
/// Contains information about a newly created process and its primary thread.
/// </summary>
/// <remarks>
/// <para>Original C definition:</para>
/// <code>
/// typedef struct _PROCESS_INFORMATION {
///     HANDLE hProcess;
///     HANDLE hThread;
///     DWORD  dwProcessId;
///     DWORD  dwThreadId;
/// } PROCESS_INFORMATION, *LPPROCESS_INFORMATION;
/// </code>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct ProcessInformation
{
    public IntPtr hProcess;
    public IntPtr hThread;
    public uint dwProcessId;
    public uint dwThreadId;
}

// ==============================================================================================
// 6. CONTEXT (ARM64)
// ==============================================================================================

/// <summary>
/// Contains processor-specific register data for an ARM64 thread.
/// </summary>
/// <remarks>
/// <para>Original C definition (from winnt.h):</para>
/// <code>
/// typedef struct _CONTEXT {
///     DWORD ContextFlags;
///     DWORD Cpsr;
///     union {
///         struct {
///             DWORD64 X0;
///             DWORD64 X1;
///             DWORD64 X2;
///             DWORD64 X3;
///             DWORD64 X4;
///             DWORD64 X5;
///             DWORD64 X6;
///             DWORD64 X7;
///             DWORD64 X8;
///             DWORD64 X9;
///             DWORD64 X10;
///             DWORD64 X11;
///             DWORD64 X12;
///             DWORD64 X13;
///             DWORD64 X14;
///             DWORD64 X15;
///             DWORD64 X16;
///             DWORD64 X17;
///             DWORD64 X18;
///             DWORD64 X19;
///             DWORD64 X20;
///             DWORD64 X21;
///             DWORD64 X22;
///             DWORD64 X23;
///             DWORD64 X24;
///             DWORD64 X25;
///             DWORD64 X26;
///             DWORD64 X27;
///             DWORD64 X28;
///             DWORD64 Fp;
///             DWORD64 Lr;
///         };
///         DWORD64 X[31];
///     };
///     DWORD64 Sp;
///     DWORD64 Pc;
///     NEON128 V[32];
///     DWORD Fpcr;
///     DWORD Fpsr;
///     DWORD Bcr[ARM64_MAX_BREAKPOINTS];
///     DWORD64 Bvr[ARM64_MAX_BREAKPOINTS];
///     DWORD Wcr[ARM64_MAX_WATCHPOINTS];
///     DWORD64 Wvr[ARM64_MAX_WATCHPOINTS];
/// } CONTEXT, *PCONTEXT;
/// </code>
/// </remarks>
[StructLayout(LayoutKind.Explicit, Size = 912)]
public unsafe struct ContextArm64
{
    // Control Flags
    [FieldOffset(0x000)] public uint ContextFlags;
    [FieldOffset(0x004)] public uint Cpsr;

    // Integer Registers X0-X28
    [FieldOffset(0x008)] public ulong X0;
    [FieldOffset(0x010)] public ulong X1;
    [FieldOffset(0x018)] public ulong X2;
    [FieldOffset(0x020)] public ulong X3;
    [FieldOffset(0x028)] public ulong X4;
    [FieldOffset(0x030)] public ulong X5;
    [FieldOffset(0x038)] public ulong X6;
    [FieldOffset(0x040)] public ulong X7;
    [FieldOffset(0x048)] public ulong X8;
    [FieldOffset(0x050)] public ulong X9;
    [FieldOffset(0x058)] public ulong X10;
    [FieldOffset(0x060)] public ulong X11;
    [FieldOffset(0x068)] public ulong X12;
    [FieldOffset(0x070)] public ulong X13;
    [FieldOffset(0x078)] public ulong X14;
    [FieldOffset(0x080)] public ulong X15;
    [FieldOffset(0x088)] public ulong X16;
    [FieldOffset(0x090)] public ulong X17;
    [FieldOffset(0x098)] public ulong X18;
    [FieldOffset(0x0A0)] public ulong X19;
    [FieldOffset(0x0A8)] public ulong X20;
    [FieldOffset(0x0B0)] public ulong X21;
    [FieldOffset(0x0B8)] public ulong X22;
    [FieldOffset(0x0C0)] public ulong X23;
    [FieldOffset(0x0C8)] public ulong X24;
    [FieldOffset(0x0D0)] public ulong X25;
    [FieldOffset(0x0D8)] public ulong X26;
    [FieldOffset(0x0E0)] public ulong X27;
    [FieldOffset(0x0E8)] public ulong X28;

    // Frame Pointer (X29)
    [FieldOffset(0x0F0)] public ulong Fp;
    
    // Link Register (X30)
    [FieldOffset(0x0F8)] public ulong Lr;

    // Stack Pointer / PC
    [FieldOffset(0x100)] public ulong Sp;
    [FieldOffset(0x108)] public ulong Pc;

    // Floating Point / NEON (V0-V31, 128-bit each)
    [FieldOffset(0x110)] public fixed byte V[512];

    // Control
    [FieldOffset(0x310)] public uint Fpcr;
    [FieldOffset(0x314)] public uint Fpsr;

    // Debug Registers
    [FieldOffset(0x318)] public fixed uint Bcr[8];
    [FieldOffset(0x338)] public fixed ulong Bvr[8];
    [FieldOffset(0x378)] public fixed uint Wcr[2];
    [FieldOffset(0x380)] public fixed ulong Wvr[2];
}