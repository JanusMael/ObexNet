using System.Runtime.InteropServices;

namespace Win64;

// ==============================================================================================
// 1. M128A
// ==============================================================================================

[StructLayout(LayoutKind.Sequential, Pack = 16)]
public struct M128A
{
    public ulong Low;
    public long High;
}

// ==============================================================================================
// 2. XMM_SAVE_AREA32
// ==============================================================================================

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

[StructLayout(LayoutKind.Sequential)]
public struct ExceptionDebugInfo
{
    public ExceptionRecord ExceptionRecord;
    public uint dwFirstChance;
}

[StructLayout(LayoutKind.Sequential)]
public struct CreateThreadDebugInfo
{
    public IntPtr hThread;
    public IntPtr lpThreadLocalBase;
    public IntPtr lpStartAddress;
}

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

[StructLayout(LayoutKind.Sequential)]
public struct ExitThreadDebugInfo
{
    public uint dwExitCode;
}

[StructLayout(LayoutKind.Sequential)]
public struct ExitProcessDebugInfo
{
    public uint dwExitCode;
}

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

[StructLayout(LayoutKind.Sequential)]
public struct UnloadDllDebugInfo
{
    public IntPtr lpBaseOfDll;
}

[StructLayout(LayoutKind.Sequential)]
public struct OutputDebugStringInfo
{
    public IntPtr lpDebugStringData;
    public ushort fUnicode;
    public ushort nDebugStringLength;
}

[StructLayout(LayoutKind.Sequential)]
public struct RipInfo
{
    public uint dwError;
    public uint dwType;
}

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

[StructLayout(LayoutKind.Sequential)]
public struct UnicodeString
{
    public ushort Length;
    public ushort MaximumLength;
    public IntPtr Buffer;
}

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