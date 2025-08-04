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
    
    private HidDevice? _device;
    private HidStream? _stream;
    private bool _disposed = false;
    
    /// <summary>
    /// Initialize and open the HID device
    /// </summary>
    public bool Initialize()
    {
        try
        {
            _device = DeviceList.Local.GetHidDevices(VendorId, ProductId).FirstOrDefault();
            if (_device == null)
            {
                Console.WriteLine("DEBUG: WHB04B-6 device not found");
                return false;
            }
            
            Console.WriteLine($"DEBUG: Found device: {_device.GetFriendlyName()}");
            Console.WriteLine($"DEBUG: Max input report length: {_device.GetMaxInputReportLength()}");
            Console.WriteLine($"DEBUG: Max output report length: {_device.GetMaxOutputReportLength()}");
            Console.WriteLine($"DEBUG: Max feature report length: {_device.GetMaxFeatureReportLength()}");
            
            _stream = _device.Open();
            return _stream != null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: Initialize failed: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Close the HID device connection
    /// </summary>
    public void Close()
    {
        _stream?.Close();
        _stream = null;
        _device = null;
    }
    
    /// <summary>
    /// Read input data from the pendant
    /// Expected packet structure: [0x04, random, button1, button2, feedDial, axisDial, jogDelta, checksum]
    /// </summary>
    /// <param name="buffer">Buffer to store received data (must be at least 8 bytes)</param>
    /// <returns>Number of bytes read, or -1 on error</returns>
    public int ReadInput(byte[] buffer)
    {
        if (_stream == null || buffer.Length < InputPacketSize)
        {
            return -1;
        }
        
        try
        {
            _stream.ReadTimeout = 100; // 100ms timeout
            var result = _stream.Read(buffer, 0, InputPacketSize);
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
        if (_stream == null || data.Length != 21) // Must be exactly 21 bytes (3 blocks of 7 bytes)
        {
            Console.WriteLine($"DEBUG: SendOutput: Invalid parameters - stream={_stream != null}, data.Length={data.Length}");
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
                    _stream.SetFeature(block);
                    Console.WriteLine($"DEBUG: SetFeature succeeded for block {i/7 + 1}");
                }
                catch (Exception setFeatureEx)
                {
                    Console.WriteLine($"DEBUG: SetFeature failed: {setFeatureEx.Message}, trying Write...");
                    
                    // Fallback to Write
                    try
                    {
                        _stream.Write(block);
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
    /// Check if device is connected and ready
    /// </summary>
    public bool IsConnected => _stream != null;
    
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