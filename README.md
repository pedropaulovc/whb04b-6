# WHB04B-6 CNC Pendant Controller

A .NET 8 library and console application for interfacing with the WHB04B-6 wireless CNC pendant controller via USB.

## Features

- **USB Communication**: Direct communication with WHB04B-6 pendant through USB controller
- **Real-time Input Processing**: Continuously polls pendant for button presses, dial positions, and jog wheel movements
- **Display Output**: Send coordinate data and control information to the pendant's LCD display
- **Modern C# Implementation**: Built with .NET 8, using latest C# features and best practices
- **Exception Handling**: Comprehensive error handling with specific exception types for pendant operations

## Hardware Requirements

- WHB04B-6 wireless CNC pendant
- USB receiver/controller for the pendant
- Windows operating system (required for USB driver)

## Project Structure

- `PHB04BLibrary.cs` - Low-level P/Invoke wrapper for the PHB04B.dll
- `WHB04BWrapper.cs` - High-level managed wrapper with automatic polling and event handling
- `PendantInputData.cs` - Classes for parsing pendant input (buttons, dials, jog wheel)
- `PendantDisplayData.cs` - Classes for formatting display output data
- `PHB04BException.cs` - Custom exception types for pendant operations
- `Program.cs` - Console application demonstrating library usage

## Usage

### Basic Example

```csharp
using var controller = new WHB04BWrapper();

// Subscribe to input events
controller.DataChanged += (sender, e) =>
{
    Console.WriteLine($"Key: {e.FirstKeyPressed}, Jog: {e.JogCountOffset}");
};

// Send display data
var displayData = new PendantDisplayData(
    JogMode.Continuous,
    CoordinateSystem.XYZ,
    x: 123.4567m,
    y: -98.7654m,
    z: 0.1234m
);

controller.SendDisplayData(displayData);
```

### Input Data

The pendant provides:
- **Button Presses**: 16 numbered keys (Key1-Key16)
- **Dial Positions**: Left and right rotary dials (7 positions each)
- **Jog Wheel**: Signed offset values for incremental movement

### Display Data

Send coordinate information with:
- **Coordinate Values**: X, Y, Z positions (range: ±65535.9999)
- **Jog Mode**: Continuous, Step, None, or Reset
- **Coordinate System**: XYZ or X1Y1Z1

## Building

Requires .NET 8 SDK:

```bash
dotnet build
```

## Running

```bash
dotnet run
```

The console application will:
1. Initialize the pendant controller
2. Display real-time input data from the pendant
3. Send sample coordinate data to the pendant display every 10 seconds
4. Run until Ctrl+C is pressed

## Dependencies

- .NET 8.0
- PHB04B.dll (native USB driver - must be in application directory)
- Windows x86 platform (required for USB driver compatibility)

## License

See LICENSE file for details.