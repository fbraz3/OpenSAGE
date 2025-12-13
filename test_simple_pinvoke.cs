using System;
using System.Runtime.InteropServices;

class TestSimplePInvoke
{
    [DllImport("libc", CallingConvention = CallingConvention.Cdecl)]
    private static extern int printf(string format);

    static void Main()
    {
        try
        {
            Console.WriteLine("Testing simple P/Invoke...");
            printf("Hello from P/Invoke!\n");
            Console.WriteLine("Success!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex}");
        }
    }
}
