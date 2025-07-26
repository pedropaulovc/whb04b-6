using WHB04B6Controller;

Console.WriteLine("WHB04B-6 USB CNC Remote Control Pendant Controller");
Console.WriteLine("==================================================");

// Set up Ctrl+C handler
var cancellationTokenSource = new CancellationTokenSource();
Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    cancellationTokenSource.Cancel();
    Console.WriteLine("\nShutting down...");
};

try
{
    using var controller = new WHB04BWrapper();
    Console.WriteLine("Controller initialized successfully.");
    Console.WriteLine("Listening for pendant data changes... Press Ctrl+C to exit.");
    Console.WriteLine();

    // Subscribe to data change events
    controller.DataChanged += (sender, e) =>
    {
        string hexData = Convert.ToHexString(e.RawData);
        Console.WriteLine($"[{e.Timestamp:HH:mm:ss.fff}] Raw: {hexData} | Key1: {e.FirstKeyPressed} | Key2: {e.SecondKeyPressed} | RightDial: {e.RightDial} | LeftDial: {e.LeftDial} | Jog: {e.JogCountOffset}");
    };

    // Keep the application running until Ctrl+C is pressed
    try
    {
        await Task.Delay(Timeout.Infinite, cancellationTokenSource.Token);
    }
    catch (TaskCanceledException)
    {
        // Expected when cancellation is requested
    }

    Console.WriteLine("Closing controller...");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
