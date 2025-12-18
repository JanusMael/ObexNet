using System.Diagnostics.CodeAnalysis;

namespace ObexNet;

[SuppressMessage("ReSharper", "UnusedMethodReturnValue.Local")]
[SuppressMessage("ReSharper", "RedundantUnsafeContext")]
public static unsafe class Program
{
    private static void Main(string[] args)
    {
        if (IntPtr.Size == 4)
        {
            throw new PlatformNotSupportedException("ObexNet only supports 64-bit architectures (x64/Arm64). 32-bit is not supported.");
        }

        if (args.Length < 1)
        {
            PrintUsage();
            return;
        }

        // --- Configuration Flags ---
        var silentMode = false;
        var jsonOutput = false;
        var interactiveMode = false;
        var fakeSuccess = false;
        var followChildren = false;
        var dllBlockPatterns = new List<string>();

        // Parse arguments
        var cmdLineArgs = new List<string>();
        foreach (var arg in args)
        {
            if (arg.StartsWith("--"))
            {
                switch (arg.ToLower())
                {
                    case "--silent": silentMode = true; break;
                    case "--json": jsonOutput = true; break;
                    case "--interactive": interactiveMode = true; break;
                    case "--fake-success": fakeSuccess = true; break;
                    case "--follow-children": followChildren = true; break;
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
            dllBlockPatterns.AddRange(patterns);
        }
        else
        {
            dllBlockPatterns.Add("amsi.dll"); // Default
        }

        // Instantiate and run Obex
        try 
        {
            var obex = new Obex(dllBlockPatterns, silentMode, jsonOutput, interactiveMode, fakeSuccess, followChildren);
            obex.Run(targetCmd);
        }
        catch (Exception ex)
        {
            if (jsonOutput)
            {
                 // Minimal JSON error handling if crash occurs before Obex logging takes over
                 Console.WriteLine($"{{\"type\":\"error\",\"message\":\"{ex.Message}\"}}");
            }
            else
            {
                Console.WriteLine($"[-] Fatal Error: {ex.Message}");
            }
        }
    }

    private static void PrintUsage()
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
}