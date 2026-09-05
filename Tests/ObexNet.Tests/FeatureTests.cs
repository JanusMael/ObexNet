using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Bennewitz.Ninja.ObexNet.Tests;

[TestClass]
public class FeatureTests : TestFixture
{
    [TestMethod]
    public void Test_RegexBlocking()
    {
        // Test that wildcard works: "TestDll*" should match "TestDll.dll" or "ObexNet.TestDll.dll"
        var testDll = GetTestDllPath();
        var victims = "*TestDll*";
        var psi = GetObexProcessStartInfo(testDll, victims);
        
        using (var p = new Process { StartInfo = psi })
        {
            p.Start();
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            Console.WriteLine(output);
            Assert.IsTrue(output.Contains("[+] Blocked loading of"), "Failed to block using Regex wildcard.");
        }
    }

    [TestMethod]
    public void Test_JsonOutput()
    {
        var obexExe = GetObexPath();
        var testDll = GetTestDllPath();
        if (string.IsNullOrEmpty(testDll))
        {
            Assert.Inconclusive("TestDll not found");
        }

        var systemFolder = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var regsvr32 = Path.Combine(systemFolder, "regsvr32.exe");
        var victimCommand = $"{regsvr32} /s \"{testDll}\"";
        var dllName = Path.GetFileName(testDll);

        // Add --json flag
        var arguments = $"\"{victimCommand}\" {dllName} --json";

        var psi = new ProcessStartInfo
        {
            FileName = obexExe,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var p = new Process { StartInfo = psi })
        {
            p.Start();
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            Console.WriteLine(output);
            
            // Check for JSON structure
            // Example: {"timestamp":..., "type":"blocked", "target":"TestDll.dll", "action":"block"}
            Assert.IsTrue(Regex.IsMatch(output, "\\\"type\\\"\\s*:\\s*\\\"blocked\\\""), "JSON output did not contain blocked event.");
            Assert.IsTrue(Regex.IsMatch(output, "\\\"target\\\"\\s*:\\s*\\\".*TestDll.*\\\""), "JSON output did not contain target DLL.");
        }
    }

    [TestMethod]
    public void Test_FakeSuccess()
    {
        var obexExe = GetObexPath();
        var testDll = GetTestDllPath();
		
        if (string.IsNullOrEmpty(testDll))
        {
            Assert.Inconclusive("TestDll not found");
        }

        var systemFolder = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var regsvr32 = Path.Combine(systemFolder, "regsvr32.exe");
        var victimCommand = $"{regsvr32} /s \"{testDll}\"";
        var dllName = Path.GetFileName(testDll);

        // Add --fake-success and --json to easily verify action field
        var arguments = $"\"{victimCommand}\" {dllName} --fake-success --json";

        var psi = new ProcessStartInfo
        {
            FileName = obexExe,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var p = new Process { StartInfo = psi })
        {
            p.Start();
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            Console.WriteLine(output);
            
            // Check for action: fake_success
            Assert.IsTrue(output.Contains("\"action\":\"fake_success\""), "Did not find fake_success action in JSON output.");
        }
    }

    [TestMethod]
    public void Test_ChildProcessFollowing()
    {
        var obexExe = GetObexPath();
        
        // We need a process that spawns another process. cmd.exe /c "cmd.exe /c whoami" is a good candidate.
        var victimCommand = "cmd.exe /c cmd.exe /c whoami";
        
        // Use --follow-children. We don't need to block anything specific to test attaching.
        var arguments = $"\"{victimCommand}\" non_existent_dll.dll --follow-children";

        var psi = new ProcessStartInfo
        {
            FileName = obexExe,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var p = new Process { StartInfo = psi })
        {
            p.Start();
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            Console.WriteLine(output);

            // We expect to see "Attached to child process PID: ..."
            Assert.IsTrue(output.Contains("Attached to child process PID"), "Did not detect child process attachment.");
        }
    }
}