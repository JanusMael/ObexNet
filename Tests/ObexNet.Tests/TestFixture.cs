using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ObexNet.Tests;

public abstract class TestFixture
{
    protected const string DllName = "ObexNet.TestDll.dll";
    
    protected string GetBuildConfig(DirectoryInfo currentDir)
    {
        return currentDir.FullName.Contains("Release") ? "Release" : "Debug";
    }

    protected DirectoryInfo GetSolutionRoot()
    {
        var currentDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        var searchDir = currentDir;

        // Walk up until we find a folder containing a .sln file
        while (searchDir != null)
        {
            if (searchDir.GetFiles("*.sln").Length > 0)
            {
                return searchDir;
            }
            searchDir = searchDir.Parent;
        }

        Assert.Fail($"Could not locate Solution Root (.sln file). Searched upwards from {currentDir.FullName}");
        return null; // Unreachable
    }

    protected string GetObexPath()
    {
        var solutionDir = GetSolutionRoot();
        var buildConfig = GetBuildConfig(new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory));

        // Look for ObexNet/bin/Debug/net10.0/ObexNet.exe relative to solution
        var path = Path.Combine(solutionDir.FullName, "ObexNet", "bin", buildConfig, "net10.0", "ObexNet.exe");
        
        if (!File.Exists(path))
        {
            Assert.Fail($"ObexNet.exe not found at calculated path: {path}. Please build the ObexNet project.");
        }
        return path;
    }

    protected string GetTestDllPath()
    {
        // 1. Check local output (standard for ProjectReference)
        var localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DllName);
        if (File.Exists(localPath)) return localPath;

        // 2. Search known locations relative to solution root
        var solutionDir = GetSolutionRoot();
        var buildConfig = GetBuildConfig(new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory));

        var path = Path.Combine(
            solutionDir.FullName, "Tests", "ObexNet.Tests", "bin", buildConfig, "net10.0", DllName);

        if (File.Exists(path)) return path;

        Assert.Fail($"{DllName} not found. Checked: {path}");
        return "";
    }

    protected ProcessStartInfo GetObexProcessStartInfo(string testDll, string victims)
    {
        var obexExe = GetObexPath();

        // rundll32 will attempt to load the DLL to run 'Entry'
        //string victimCommand = $"rundll32.exe \"{testDll}\",Entry";
        //string arguments = $"\"{victimCommand}\" {DllName}";
        // USE REGSVR32 /S (Silent)
        // This loads the DLL to register it. If blocked by ObexNet, the load fails.
        // The /s flag suppresses the "Module not found" message box.
        var systemFolder = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var regsvr32 = Path.Combine(systemFolder, "regsvr32.exe");
        
        var victimCommand = $"{regsvr32} /s \"{testDll}\"";
        var arguments = $"\"{victimCommand}\" {victims}";

        var psi = new ProcessStartInfo
        {
            FileName = obexExe,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        return psi;
    }
}