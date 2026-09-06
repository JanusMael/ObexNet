using System.Reflection;
using System.Runtime.InteropServices;

namespace Bennewitz.Ninja.ObexNet;

public static class AssemblyUtility
{
    public static readonly string ApplicationName = GetApplicationName(typeof(AssemblyUtility));
    
    /// <summary>
    /// Retrieves the bare application name — <c>ObexNet</c> — from the declaring assembly's
    /// simple name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="AssemblyProductAttribute"/> is deliberately not used. The
    /// <c>Bennewitz.Ninja.AutoVersioning</c> generator wired up in <c>Directory.Build.props</c>
    /// always stamps the product as <c>"&lt;AssemblyProduct&gt; v&lt;version&gt;"</c> — for this
    /// repository, <c>"ObexNet v2026.3.905.1826"</c> — and exposes no switch to drop the suffix,
    /// so that attribute can never carry a bare name.
    /// </para>
    /// <para>
    /// <see cref="AssemblyName.Name"/> carries no version, requires no attribute to survive
    /// trimming in the published single-file build, and equals the executable's base name, which
    /// is what <c>TestFixture.GetObexPath</c> composes the path to <c>ObexNet.exe</c> from.
    /// </para>
    /// </remarks>
    /// <returns>The application name, falling back to <c>ObexNet</c> if the assembly is unnamed.</returns>
    private static string GetApplicationName(this Type type)
    {
        // The assembly that declares this type — i.e. the executable.
        var assembly = type.Assembly;

        return assembly.GetName().Name ?? nameof(ObexNet);
    }
    
    public static Architecture GetSupportedArchitectureOrThrow()
    {
        var applicationName = ApplicationName;
        
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException(
                $"{applicationName} only supports the {OSPlatform.Windows} OS"
            );
        }
        var architecture = RuntimeInformation.ProcessArchitecture;
        
        switch (architecture)
        {
            case Architecture.X86:
            case Architecture.X64:
            case Architecture.Arm64:
                return architecture;
            default:
            {
                if (IntPtr.Size == 4)
                {
                    throw new PlatformNotSupportedException(
                        $"{applicationName} only supports 64-bit architectures (x64/Arm64). Neither 32-bit architectures nor 32-bit processes on 64-bit architectures are supported"
                    );
                }
                throw new PlatformNotSupportedException(
                    $"{applicationName} only supports x64 and ARM64 architectures. {architecture} is not supported"
                );
            }
        }
    }
}