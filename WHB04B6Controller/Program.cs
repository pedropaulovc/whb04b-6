using WHB04B6Controller;

Console.WriteLine("WHB04B-6 USB CNC Remote Control Pendant Controller");
Console.WriteLine("==================================================");

try
{
    using var controller = new WHB04BWrapper();
    Console.WriteLine("Controller initialized successfully.");
    
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
