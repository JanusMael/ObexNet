using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using Win64;

namespace Bennewitz.Ninja.ObexNet.Tests;

[TestClass]
public class IntegrationTests : TestFixture
{
    [TestMethod]
    public void Test_ObexNet_Blocks_TestDll()
    {
        var testDll = GetTestDllPath();
        var victims = Path.GetFileName(testDll);
        var psi = GetObexProcessStartInfo(testDll, victims);

        using (var p = new Process { StartInfo = psi })
        {
            p.Start();
            
            var output = p.StandardOutput.ReadToEnd();
            var error = p.StandardError.ReadToEnd();
            
            p.WaitForExit();

            Console.WriteLine("STDOUT:\n" + output);
            Console.WriteLine("STDERR:\n" + error);

            var applicationName = AssemblyUtility.ApplicationName;
            Assert.IsTrue(output.Contains($"[+] Hooked {Native.LdrLoadDll}"), $"{applicationName} failed to hook {Native.LdrLoadDll}.");
            Assert.IsTrue(output.Contains("Blocked loading of"), $"{applicationName} did not report blocking action.");
            Assert.IsTrue(output.Contains(DllName), $"{applicationName} did not block the specific target DLL.");
        }
    }

    [TestMethod]
    public void Test_ObexNet_Allows_OtherDlls()
    {
        var obexExe = GetObexPath();
        
        var victimCommand = "cmd.exe /C whoami";
        // Block a fake DLL so we don't interfere with cmd.exe's actual dependencies
        var arguments = $"\"{victimCommand}\" ThisDllDoesNotExist.dll";

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
            var error = p.StandardError.ReadToEnd();
            p.WaitForExit();

            Console.WriteLine("STDOUT:\n" + output);
            if (!string.IsNullOrEmpty(error))
            {
                Console.WriteLine("STDERR:\n" + error);
            }

            var applicationName = AssemblyUtility.ApplicationName;
            Assert.IsTrue(output.Contains($"[+] Hooked {Native.LdrLoadDll}"), $"{applicationName} failed to hook {Native.LdrLoadDll}.");
            Assert.IsTrue(output.Contains("[+] Child exited"), "Child process did not exit cleanly.");
            Assert.IsFalse(output.Contains("Blocked loading of"), $"{applicationName} blocked a DLL unexpectedly.");
        }
    }
}