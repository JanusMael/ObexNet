using System.Diagnostics;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Bennewitz.Ninja.ObexNet.Tests;

public abstract class TestFixture
{
    protected const string DllName = "ObexNet.TestDll.dll";
    
    protected TestFixture()
    {
        // Ensure we are running in a 64-bit process (x64 or Arm64)
        AssemblyUtility.GetSupportedArchitectureOrThrow();
    }

    protected static string GetTargetFrameworkMoniker()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var attr = assembly.GetCustomAttribute<TargetFrameworkAttribute>();

        if (attr != null)
        {
            var frameworkName = new FrameworkName(attr.FrameworkName);
            if (frameworkName.Identifier == ".NETCoreApp")
            {
                // Converts ".NETCoreApp,Version=v10.0" to "net10.0"
                return $"net{frameworkName.Version.Major}.{frameworkName.Version.Minor}";
            }
        }
        
        return "net10.0"; // Fallback default
    }
    
    protected static string GetBuildConfig(DirectoryInfo currentDir)
    {
        return currentDir.FullName.Contains("Release") ? "Release" : "Debug";
    }

    protected static DirectoryInfo GetSolutionRoot()
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

    /// <summary>
    /// Environment variable that overrides which <c>ObexNet.exe</c> the suite exercises.
    /// </summary>
    /// <remarks>
    /// Unset, the suite runs against the ordinary build output, which is framework-dependent and
    /// untrimmed. That build cannot vouch for the binary users actually download: publish/publish.ps1
    /// produces a self-contained, single-file, <em>trimmed</em> executable, and trimming removes code
    /// an untrimmed build keeps. Point this variable at the published executable to run the same
    /// assertions against the shipping artefact.
    /// </remarks>
    protected const string ObexPathOverrideVariable = "OBEXNET_EXE";

    protected static string GetObexPath()
    {
        var overridePath = Environment.GetEnvironmentVariable(ObexPathOverrideVariable);
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            if (!File.Exists(overridePath))
            {
                Assert.Fail($"{ObexPathOverrideVariable} is set to '{overridePath}', but no file exists at that path.");
            }
            return overridePath;
        }

        var solutionDir = GetSolutionRoot();
        var buildConfig = GetBuildConfig(new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory));
        var tfm = GetTargetFrameworkMoniker();

        // Dynamic TFM usage
        var path = Path.Combine(solutionDir.FullName, AssemblyUtility.ApplicationName, "bin", buildConfig, tfm, $"{AssemblyUtility.ApplicationName}.exe");
        
        if (!File.Exists(path))
        {
            Assert.Fail($"{AssemblyUtility.ApplicationName}.exe not found at calculated path: {path}. Please build the {AssemblyUtility.ApplicationName} project.");
        }
        return path;
    }

    protected static string GetTestDllPath()
    {
        // 1. Check local output (standard for ProjectReference)
        var localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DllName);
        if (File.Exists(localPath))
        {
            return localPath;
        }

        // 2. Search known locations relative to solution root
        var solutionDir = GetSolutionRoot();
        var buildConfig = GetBuildConfig(new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory));
        var tfm = GetTargetFrameworkMoniker();

        var path = Path.Combine(
            solutionDir.FullName, "Tests", $"{AssemblyUtility.ApplicationName}.Tests", "bin", buildConfig, tfm, DllName);

        if (File.Exists(path))
        {
            return path;
        }

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