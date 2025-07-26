using WHB04B6Controller;

Console.WriteLine("WHB04B-6 USB CNC Remote Control Pendant Controller");
Console.WriteLine("==================================================");

try
{
    Console.WriteLine("Initializing PHB04B controller...");
    PHB04BController.Xinit();
    Console.WriteLine("Controller initialized successfully.");
    
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
finally
{
    Console.WriteLine("\nClosing connection...");
    PHB04BController.XClose();
    Console.WriteLine("Connection closed.");
}
