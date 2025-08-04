using HidSharp;
using Microsoft.Extensions.Logging;

namespace WHB04B6Controller;

/// <summary>
/// Direct HID communication with WHB04B-6 pendant
/// Replaces vendor PHB04B.dll with direct USB HID communication
/// Based on LinuxCNC xhc-whb04b-6 implementation
/// </summary>
public class HidCommunication : IDisposable
{
    private const int VendorId = 0x10ce;
    private const int ProductId = 0xeb93;
    private const int InputPacketSize = 8;
    private const int OutputBlockSize = 8; // HidSharp includes report ID in the buffer
    
    private readonly ILogger<HidCommunication> _logger;
    private HidDevice? _inputDevice;
    private HidDevice? _outputDevice;
    private HidStream? _inputStream;
    private HidStream? _outputStream;
    private bool _disposed = false;
    
    public HidCommunication(ILogger<HidCommunication> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Initialize and open the HID device
    /// </summary>
    public bool Initialize()
    {
        try
        {
            _logger.LogDebug("Searching for HID devices...");
            
            // First, list all HID devices to see what's available
            var allDevices = DeviceList.Local.GetHidDevices().ToList();
            _logger.LogDebug("Found {DeviceCount} total HID devices", allDevices.Count);
            
            // Look for our specific device
            var targetDevices = allDevices.Where(d => d.VendorID == VendorId && d.ProductID == ProductId).ToList();
            _logger.LogDebug("Found {MatchingDeviceCount} matching WHB04B-6 devices", targetDevices.Count);
            
            if (targetDevices.Count == 0)
            {
                _logger.LogWarning("WHB04B-6 device not found");
                _logger.LogDebug("Available devices:");
                foreach (var dev in allDevices.Take(10)) // Show first 10 devices
                {
                    try
                    {
                        _logger.LogDebug("  VID: 0x{VendorId:X4}, PID: 0x{ProductId:X4}, Path: {DevicePath}", dev.VendorID, dev.ProductID, dev.DevicePath);
                    }
                    catch
                    {
                        _logger.LogDebug("  VID: 0x{VendorId:X4}, PID: 0x{ProductId:X4}, Path: <error reading path>", dev.VendorID, dev.ProductID);
                    }
                }
                return false;
            }
            
            // Find separate input and output devices
            foreach (var device in targetDevices)
            {
                _logger.LogDebug("Checking device path: {DevicePath}", device.DevicePath);
                try
                {
                    int maxInput = device.GetMaxInputReportLength();
                    int maxOutput = device.GetMaxOutputReportLength();
                    int maxFeature = device.GetMaxFeatureReportLength();
                    
                    _logger.LogDebug("Max input: {MaxInput}, Max output: {MaxOutput}, Max feature: {MaxFeature}", maxInput, maxOutput, maxFeature);
                    
                    // Look for input device (has input capability)
                    if (maxInput > 0 && _inputDevice == null)
                    {
                        _logger.LogDebug("Found input-capable device!");
                        _inputDevice = device;
                    }
                    
                    // Look for output device (has output or feature capability)
                    if ((maxOutput > 0 || maxFeature > 0) && _outputDevice == null)
                    {
                        _logger.LogDebug("Found output-capable device!");
                        _outputDevice = device;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error checking device capabilities");
                }
            }
            
            if (_inputDevice == null)
            {
                _logger.LogError("No input device found!");
                return false;
            }
            
            if (_outputDevice == null)
            {
                _logger.LogError("No output device found!");
                return false;
            }
            
            _logger.LogInformation("Input device path: {InputDevicePath}", _inputDevice.DevicePath);
            _logger.LogInformation("Output device path: {OutputDevicePath}", _outputDevice.DevicePath);
            
            _logger.LogDebug("Attempting to open input device...");
            _inputStream = _inputDevice.Open();
            if (_inputStream == null)
            {
                _logger.LogError("Failed to open input device");
                return false;
            }
            _logger.LogInformation("Input device opened successfully");
            
            _logger.LogDebug("Attempting to open output device...");
            _outputStream = _outputDevice.Open();
            if (_outputStream == null)
            {
                _logger.LogError("Failed to open output device");
                return false;
            }
            _logger.LogInformation("Output device opened successfully");
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initialize failed");
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
            _logger.LogWarning("SendOutput: Invalid parameters - stream={StreamExists}, data.Length={DataLength}", _outputStream != null, data.Length);
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
                
                _logger.LogTrace("Sending block {BlockNumber}: {BlockData}", i/7 + 1, BitConverter.ToString(block));
                
                // Try SetFeature first (HID control transfer)
                try
                {
                    _outputStream.SetFeature(block);
                    _logger.LogTrace("SetFeature succeeded for block {BlockNumber}", i/7 + 1);
                }
                catch (Exception setFeatureEx)
                {
                    _logger.LogDebug(setFeatureEx, "SetFeature failed for block {BlockNumber}, trying Write...", i/7 + 1);
                    
                    // Fallback to Write
                    try
                    {
                        _outputStream.Write(block);
                        _logger.LogTrace("Write succeeded for block {BlockNumber}", i/7 + 1);
                    }
                    catch (Exception writeEx)
                    {
                        _logger.LogError(writeEx, "Write also failed for block {BlockNumber}", i/7 + 1);
                        throw;
                    }
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendOutput failed");
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