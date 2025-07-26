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
        // Take only the first 16 bytes for display
        byte[] dataToShow = e.Data.Length > 16 ? e.Data[..16] : e.Data;
        string hexData = Convert.ToHexString(dataToShow);
        Console.WriteLine($"[{e.Timestamp:HH:mm:ss.fff}] Data changed: {hexData}");
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
