using System.Timers;
using Microsoft.Extensions.Logging;

namespace WHB04B6Controller;

/// <summary>
/// High-level wrapper library for the WHB04B-6 CNC pendant controller
/// Provides simplified APIs that translate low-level controller operations
/// </summary>
public class WHB04BClient : IDisposable
{
    private const int InputBufferSizeBytes = 8;
    private bool _disposed = false;
    private bool _initialized = false;
    private System.Timers.Timer? _pollingTimer;
    private byte[] _previousData = [];
    private readonly object _lockObject = new();
    private HidCommunication? _hidDevice;
    private readonly ILogger<WHB04BClient> _logger;

    /// <summary>
    /// Event raised when pendant data changes
    /// </summary>
    public event EventHandler<PendantInputData>? DataChanged;

    /// <summary>
    /// Initializes a new instance of the WHB04BClient class
    /// Automatically opens the HID device and starts polling
    /// </summary>
    public WHB04BClient(ILogger<WHB04BClient> logger, ILogger<HidCommunication> hidLogger)
    {
        _logger = logger;
        _hidDevice = new HidCommunication(hidLogger);
        
        if (!_hidDevice.Initialize())
        {
            throw new InvalidOperationException("Could not initialize HID device. Ensure WHB04B-6 pendant is connected.");
        }
        
        _initialized = true;
        StartPolling();
    }

    /// <summary>
    /// Sends display data to the pendant device
    /// </summary>
    /// <param name="displayData">Display data to send to pendant</param>
    public void SendDisplayData(PendantDisplayData displayData)
    {
        ArgumentNullException.ThrowIfNull(displayData);

        SendData(displayData.RawData);
    }

    /// <summary>
    /// Clears the pendant display by sending clear data twice
    /// </summary>
    public void ClearDisplay()
    {
        var clearData = new PendantDisplayData(JogMode.None, CoordinateSystem.XYZ, 0m, 0m, 0m);
        SendDisplayData(clearData);
        SendDisplayData(clearData);
    }

    /// <summary>
    /// Disposes of the client and closes the controller connection
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Sends data to the pendant device
    /// </summary>
    /// <param name="data">Data buffer to send</param>
    private void SendData(byte[] data)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (data == null || data.Length == 0)
        {
            throw new ArgumentException("Data cannot be null or empty", nameof(data));
        }

        if (_hidDevice == null || !_hidDevice.IsConnected)
        {
            throw new InvalidOperationException("HID device is not connected");
        }

        // Pad data to 21 bytes if necessary (3 blocks of 7 bytes each)
        byte[] paddedData = new byte[21];
        Array.Copy(data, 0, paddedData, 0, Math.Min(data.Length, 21));
        
        if (!_hidDevice.SendOutput(paddedData))
        {
            throw new InvalidOperationException("Failed to send data to HID device");
        }
    }

    /// <summary>
    /// Starts the background polling timer
    /// </summary>
    private void StartPolling()
    {
        _pollingTimer = new System.Timers.Timer(50); // 50ms interval for responsive input
        _pollingTimer.Elapsed += OnPollingTimerElapsed;
        _pollingTimer.AutoReset = true;
        _pollingTimer.Start();
    }

    /// <summary>
    /// Handles the polling timer elapsed event
    /// </summary>
    private void OnPollingTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        lock (_lockObject)
        {
            try
            {
                byte[] newData = ReadDataInternal();
                if (HasDataChanged(newData))
                {
                    _previousData = newData;
                    DataChanged?.Invoke(this, new PendantInputData(newData));
                }
                else if (newData.Length >= 8 && newData[0] == 0x04)
                {
                    // Debug: log filtered packets occasionally
                    if (DateTime.Now.Millisecond % 500 < 50) // Log ~10% of filtered packets
                    {
                        _logger.LogTrace("Filtered packet: {PacketData}", BitConverter.ToString(newData));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Polling error");
            }
        }
    }

    /// <summary>
    /// Checks if the new data is different from the previous data and is valid
    /// </summary>
    private bool HasDataChanged(byte[] newData)
    {
        // Check if packet is valid (header should be 0x04)
        if (newData.Length >= 8 && newData[0] != 0x04)
        {
            return false; // Invalid packet header
        }
        
        // Don't trigger events for packets with all Unknown dial positions
        // This filters out transitional/garbage packets
        if (newData.Length >= 8)
        {
            var rightDialValue = newData[4];
            var leftDialValue = newData[5];
            
            // Check if both dials are unknown (not in valid ranges)
            bool rightDialValid = rightDialValue switch
            {
                0x0D or 0x0E or 0x0F or 0x10 or 0x1A or 0x1B or 0x1C => true,
                _ => false
            };
            
            bool leftDialValid = leftDialValue switch
            {
                0x06 or 0x11 or 0x12 or 0x13 or 0x14 or 0x15 or 0x16 => true,
                _ => false
            };
            
            // If both dials are invalid, and no buttons are pressed, skip this packet
            if (!rightDialValid && !leftDialValid && newData[2] == 0 && newData[3] == 0 && newData[6] == 0)
            {
                return false;
            }
        }
        
        return !newData.SequenceEqual(_previousData);
    }

    /// <summary>
    /// Internal method for reading data without disposal checks (used by polling)
    /// </summary>
    private byte[] ReadDataInternal()
    {
        if (_hidDevice == null || !_hidDevice.IsConnected)
        {
            return new byte[InputBufferSizeBytes];
        }

        byte[] data = new byte[InputBufferSizeBytes];
        int bytesRead = _hidDevice.ReadInput(data);
        
        if (bytesRead <= 0)
        {
            return new byte[InputBufferSizeBytes]; // Return empty array on read failure
        }
            
        return data;
    }

    /// <summary>
    /// Protected dispose method
    /// </summary>
    /// <param name="disposing">True if disposing managed resources</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _pollingTimer?.Stop();
                _pollingTimer?.Dispose();
            }

            if (_initialized)
            {
                try
                {
                    _hidDevice?.Close();
                }
                catch
                {
                    // Suppress exceptions during disposal
                }
                finally
                {
                    _initialized = false;
                }
            }
            
            _hidDevice?.Dispose();
            _hidDevice = null;
                
            _disposed = true;
        }
    }

    /// <summary>
    /// Finalizer to ensure resources are cleaned up
    /// </summary>
    ~WHB04BClient()
    {
        Dispose(false);
    }
}
