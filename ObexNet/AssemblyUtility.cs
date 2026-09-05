using System.Reflection;
using System.Runtime.InteropServices;

namespace Bennewitz.Ninja.ObexNet;

public static class AssemblyUtility
{
    public static readonly string ApplicationName = GetApplicationName(typeof(AssemblyUtility));
    
    /// <summary>
    /// Retrieves the product name from the <see cref="AssemblyProductAttribute"/> of the entry assembly.
    /// </summary>
    /// <returns>
    /// The product name specified in the assembly attributes, or <c>null</c> if the attribute is not found.
    /// </returns>
    private static string GetApplicationName(this Type type)
    {
        // Get the assembly that started the application (usually the executable)
        var assembly = type.Assembly;

        // Retrieve the AssemblyProductAttribute
        var attribute = assembly.GetCustomAttribute<AssemblyProductAttribute>();

        // Return the Product property value
        return attribute?.Product ?? nameof(ObexNet);
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