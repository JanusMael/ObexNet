namespace TestDll;

public class Library
{
    // A simple class. We don't need complex logic here.
    // The mere act of loading this DLL by the victim process 
    // is what we are testing.
    public static void SayHello()
    {
        Console.WriteLine("TestDll Loaded!");
    }
}