using HidSharp;

namespace WHB04B6Controller;

/// <summary>
/// Direct HID communication with WHB04B-6 pendant
/// Replaces vendor PHB04B.dll with direct USB HID communication
/// Based on LinuxCNC xhc-whb04b-6 implementation
/// </summary>
internal class HidCommunication : IDisposable
{
    private const int VendorId = 0x10ce;
    private const int ProductId = 0xeb93;
    private const int InputPacketSize = 8;
    private const int OutputBlockSize = 8; // HidSharp includes report ID in the buffer
    
    private HidDevice? _inputDevice;
    private HidDevice? _outputDevice;
    private HidStream? _inputStream;
    private HidStream? _outputStream;
    private bool _disposed = false;
    
    /// <summary>
    /// Initialize and open the HID device
    /// </summary>
    public bool Initialize()
    {
        try
        {
            Console.WriteLine("DEBUG: Searching for HID devices...");
            
            // First, list all HID devices to see what's available
            var allDevices = DeviceList.Local.GetHidDevices().ToList();
            Console.WriteLine($"DEBUG: Found {allDevices.Count} total HID devices");
            
            // Look for our specific device
            var targetDevices = allDevices.Where(d => d.VendorID == VendorId && d.ProductID == ProductId).ToList();
            Console.WriteLine($"DEBUG: Found {targetDevices.Count} matching WHB04B-6 devices");
            
            if (targetDevices.Count == 0)
            {
                Console.WriteLine("DEBUG: WHB04B-6 device not found");
                Console.WriteLine("DEBUG: Available devices:");
                foreach (var dev in allDevices.Take(10)) // Show first 10 devices
                {
                    try
                    {
                        Console.WriteLine($"DEBUG:   VID: 0x{dev.VendorID:X4}, PID: 0x{dev.ProductID:X4}, Path: {dev.DevicePath}");
                    }
                    catch
                    {
                        Console.WriteLine($"DEBUG:   VID: 0x{dev.VendorID:X4}, PID: 0x{dev.ProductID:X4}, Path: <error reading path>");
                    }
                }
                return false;
            }
            
            // Find separate input and output devices
            foreach (var device in targetDevices)
            {
                Console.WriteLine($"DEBUG: Checking device path: {device.DevicePath}");
                try
                {
                    int maxInput = device.GetMaxInputReportLength();
                    int maxOutput = device.GetMaxOutputReportLength();
                    int maxFeature = device.GetMaxFeatureReportLength();
                    
                    Console.WriteLine($"DEBUG: Max input: {maxInput}, Max output: {maxOutput}, Max feature: {maxFeature}");
                    
                    // Look for input device (has input capability)
                    if (maxInput > 0 && _inputDevice == null)
                    {
                        Console.WriteLine($"DEBUG: Found input-capable device!");
                        _inputDevice = device;
                    }
                    
                    // Look for output device (has output or feature capability)
                    if ((maxOutput > 0 || maxFeature > 0) && _outputDevice == null)
                    {
                        Console.WriteLine($"DEBUG: Found output-capable device!");
                        _outputDevice = device;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"DEBUG: Error checking device capabilities: {ex.Message}");
                }
            }
            
            if (_inputDevice == null)
            {
                Console.WriteLine($"DEBUG: No input device found!");
                return false;
            }
            
            if (_outputDevice == null)
            {
                Console.WriteLine($"DEBUG: No output device found!");
                return false;
            }
            
            Console.WriteLine($"DEBUG: Input device path: {_inputDevice.DevicePath}");
            Console.WriteLine($"DEBUG: Output device path: {_outputDevice.DevicePath}");
            
            Console.WriteLine("DEBUG: Attempting to open input device...");
            _inputStream = _inputDevice.Open();
            if (_inputStream == null)
            {
                Console.WriteLine("DEBUG: Failed to open input device");
                return false;
            }
            Console.WriteLine("DEBUG: Input device opened successfully");
            
            Console.WriteLine("DEBUG: Attempting to open output device...");
            _outputStream = _outputDevice.Open();
            if (_outputStream == null)
            {
                Console.WriteLine("DEBUG: Failed to open output device");
                return false;
            }
            Console.WriteLine("DEBUG: Output device opened successfully");
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: Initialize failed: {ex.Message}");
            Console.WriteLine($"DEBUG: Exception type: {ex.GetType().Name}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"DEBUG: Inner exception: {ex.InnerException.Message}");
            }
            return false;
        }
    }
    
    /// <summary>
    /// Close the HID device connections
    /// </summary>
    public void Close()
    {
        _inputStream?.Close();
        _inputStream = null;
        _inputDevice = null;
        
        _outputStream?.Close();
        _outputStream = null;
        _outputDevice = null;
    }
    
    /// <summary>
    /// Read input data from the pendant
    /// Expected packet structure: [0x04, random, button1, button2, feedDial, axisDial, jogDelta, checksum]
    /// </summary>
    /// <param name="buffer">Buffer to store received data (must be at least 8 bytes)</param>
    /// <returns>Number of bytes read, or -1 on error</returns>
    public int ReadInput(byte[] buffer)
    {
        if (_inputStream == null || buffer.Length < InputPacketSize)
        {
            return -1;
        }
        
        try
        {
            _inputStream.ReadTimeout = 100; // 100ms timeout
            var result = _inputStream.Read(buffer, 0, InputPacketSize);
            return result;
        }
        catch
        {
            return -1;
        }
    }
    
    /// <summary>
    /// Send display data to the pendant
    /// Try multiple approaches to match LinuxCNC libusb_control_transfer behavior
    /// </summary>
    /// <param name="data">Data to send (21 bytes total, sent as 3 blocks of 7 bytes each)</param>
    /// <returns>True if successful</returns>
    public bool SendOutput(byte[] data)
    {
        if (_outputStream == null || data.Length != 21) // Must be exactly 21 bytes (3 blocks of 7 bytes)
        {
            Console.WriteLine($"DEBUG: SendOutput: Invalid parameters - stream={_outputStream != null}, data.Length={data.Length}");
            return false;
        }
        
        try
        {
            // Send data in 7-byte chunks - try both SetFeature and Write approaches
            for (int i = 0; i < data.Length; i += 7)
            {
                var block = new byte[OutputBlockSize]; // 8 bytes total
                block[0] = 0x06; // Report ID
                
                int bytesToCopy = Math.Min(7, data.Length - i);
                Array.Copy(data, i, block, 1, bytesToCopy);
                
                Console.WriteLine($"DEBUG: Sending block {i/7 + 1}: {BitConverter.ToString(block)}");
                
                // Try SetFeature first (HID control transfer)
                try
                {
                    _outputStream.SetFeature(block);
                    Console.WriteLine($"DEBUG: SetFeature succeeded for block {i/7 + 1}");
                }
                catch (Exception setFeatureEx)
                {
                    Console.WriteLine($"DEBUG: SetFeature failed: {setFeatureEx.Message}, trying Write...");
                    
                    // Fallback to Write
                    try
                    {
                        _outputStream.Write(block);
                        Console.WriteLine($"DEBUG: Write succeeded for block {i/7 + 1}");
                    }
                    catch (Exception writeEx)
                    {
                        Console.WriteLine($"DEBUG: Write also failed: {writeEx.Message}");
                        throw;
                    }
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: SendOutput failed: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Check if devices are connected and ready
    /// </summary>
    public bool IsConnected => _inputStream != null && _outputStream != null;
    
    public void Dispose()
    {
        if (!_disposed)
        {
            Close();
            _disposed = true;
        }
    }
}

/// <summary>
/// Legacy error codes for compatibility with existing code
/// </summary>
internal static class ErrorCodes
{
    public const int Success = 0;
    public const int UsbDeviceNotOpen = 100;
    public const int UsbDownloadError = 101;
    public const int UsbReadError = 102;
    public const int ParameterError = 103;
}