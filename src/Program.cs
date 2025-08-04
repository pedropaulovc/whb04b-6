using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WHB04B6Controller;

Console.WriteLine("WHB04B-6 USB CNC Remote Control Pendant Controller");
Console.WriteLine("==================================================");

// Set up dependency injection and logging
var services = new ServiceCollection()
    .AddLogging(builder =>
    {
        builder.AddConsole();
        builder.SetMinimumLevel(LogLevel.Information); // Change to LogLevel.Debug for more verbose output
    })
    .AddSingleton<WHB04BClient>()
    .BuildServiceProvider();

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
    using var controller = services.GetRequiredService<WHB04BClient>();
    Console.WriteLine("Controller initialized successfully.");
    Console.WriteLine("Listening for pendant data changes and sending display data...");
    Console.WriteLine("Press Ctrl+C to exit.");
    Console.WriteLine();

    // Subscribe to data change events
    controller.DataChanged += (sender, e) =>
    {
        var rightDialDisplay = e.RightDial == DialPosition.Unknown ? $"Unknown(0x{e.RightDialRawValue:X2})" : e.RightDial.ToString();
        var leftDialDisplay = e.LeftDial == DialPosition.Unknown ? $"Unknown(0x{e.LeftDialRawValue:X2})" : e.LeftDial.ToString();
        
        Console.WriteLine($"[RECEIVED] [{e.Timestamp:HH:mm:ss.fff}] Key1: {e.FirstKeyPressed,-12} | Key2: {e.SecondKeyPressed,-13} | RightDial: {rightDialDisplay,-15} | LeftDial: {leftDialDisplay,-15} | Jog: {e.JogCountOffset,3}");
    };

    // Send initial zero state
    controller.ClearDisplay();
    Console.WriteLine($"[SENT    ] [{DateTime.Now:HH:mm:ss.fff}] ClearDisplay       | Success: true");

    // Send sample display data periodically
    var timer = new System.Timers.Timer(10000); // Every 10 seconds
    decimal x = 0, y = 0, z = 0;
    int cycleCount = 0;
    timer.Elapsed += (sender, e) =>
    {
        try
        {
            // Increment coordinates for demonstration (including negative values)
            x += (cycleCount % 2 == 0) ? 1.2345m : -0.9876m;
            y += (cycleCount % 3 == 0) ? -2.3456m : 1.5432m;
            z += (cycleCount % 4 == 0) ? -0.1m : 0.2m;

            // Cycle through different jog modes and coordinate systems
            var jogModes = new[] { JogMode.Continuous, JogMode.Step, JogMode.None, JogMode.Reset };
            var coordinateSystems = new[] { CoordinateSystem.XYZ, CoordinateSystem.X1Y1Z1 };
            
            var currentJogMode = jogModes[cycleCount % jogModes.Length];
            var currentCoordinateSystem = coordinateSystems[(cycleCount / 2) % coordinateSystems.Length];

            // Simulate feed and spindle rates
            ushort feedRate = (ushort)((cycleCount * 10) % 200); // 0-200% feed rate
            ushort spindleRate = (ushort)((cycleCount * 100) % 2000); // 0-2000 RPM spindle speed
            
            // Create display data
            var displayData = new PendantDisplayData(
                currentJogMode,
                currentCoordinateSystem,
                x,
                y,
                z,
                feedRate,
                spindleRate
            );

            // Send to pendant
            controller.SendDisplayData(displayData);
            Console.WriteLine($"[SENT    ] [{DateTime.Now:HH:mm:ss.fff}] X={x,+10:F4}, Y={y,+10:F4}, Z={z,+10:F4} | JogMode: {currentJogMode,-11} | CoordSys: {currentCoordinateSystem,-9} | F:{feedRate,3}% | S:{spindleRate,4} | Success: true");
            
            cycleCount++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending display data: {ex.Message}");
        }
    };
    timer.Start();

    // Keep the application running until Ctrl+C is pressed
    try
    {
        await Task.Delay(Timeout.Infinite, cancellationTokenSource.Token);
    }
    catch (TaskCanceledException)
    {
        // Expected when cancellation is requested
    }

    timer.Stop();
    timer.Dispose();
    Console.WriteLine("Closing controller...");
}
catch (HidCommunicationException ex)
{
    Console.WriteLine($"Device Error (Code {ex.ErrorCode}): {ex.Message}");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}