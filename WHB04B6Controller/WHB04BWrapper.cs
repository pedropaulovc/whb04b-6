using System.Runtime.InteropServices;
using System.Timers;

namespace WHB04B6Controller;

/// <summary>
/// Windows API imports for getting console window handle
/// </summary>
internal static partial class WindowsApi
{
    [LibraryImport("kernel32.dll")]
    internal static partial IntPtr GetConsoleWindow();
}

/// <summary>
/// High-level wrapper library for the WHB04B-6 CNC pendant controller
/// Provides simplified APIs that translate low-level controller operations
/// </summary>
public class WHB04BWrapper : IDisposable
{
    private const int InputBufferSizeBytes = 5;
    private bool _disposed = false;
    private bool _initialized = false;
    private System.Timers.Timer? _pollingTimer;
    private byte[] _previousData = [];
    private readonly Lock _lockObject = new();
    private IntPtr _dataInputBuffer;
    private IntPtr _inputLengthPtr;

    /// <summary>
    /// Event raised when pendant data changes
    /// </summary>
    public event EventHandler<PendantInputData>? DataChanged;

    /// <summary>
    /// Initializes a new instance of the WHB04BWrapper class
    /// Automatically calls XInit, opens the device, and starts polling
    /// </summary>
    public WHB04BWrapper()
    {
        // Allocate buffers once
        _dataInputBuffer = Marshal.AllocHGlobal(InputBufferSizeBytes);
        _inputLengthPtr = Marshal.AllocHGlobal(Marshal.SizeOf<int>());
            
        PHB04BLibrary.Xinit();
        _initialized = true;
            
        // Get console window handle and open the device
        IntPtr consoleHandle = WindowsApi.GetConsoleWindow();
        if (consoleHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Could not get console window handle");
        }

        int result = PHB04BLibrary.XOpen((int)consoleHandle);
        PHB04BException.ThrowIfNotSuccess(result);
            
        StartPolling();
    }

    /// <summary>
    /// Sends data to the pendant device
    /// </summary>
    /// <param name="data">Data buffer to send</param>
    public void SendData(byte[] data)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (data == null || data.Length == 0)
        {
            throw new ArgumentException("Data cannot be null or empty", nameof(data));
        }

        IntPtr lengthPtr = Marshal.AllocHGlobal(Marshal.SizeOf<int>());
        try
        {
            Marshal.WriteInt32(lengthPtr, data.Length);
            int result = PHB04BLibrary.XSendOutput(data, lengthPtr);
            PHB04BException.ThrowIfNotSuccess(result);
        }
        finally
        {
            Marshal.FreeHGlobal(lengthPtr);
        }
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
    /// Disposes of the wrapper and closes the controller connection
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Starts the background polling timer
    /// </summary>
    private void StartPolling()
    {
        _pollingTimer = new System.Timers.Timer(100); // 100ms interval
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
            }
            catch
            {
                // Silently ignore polling errors to prevent timer crashes
            }
        }
    }

    /// <summary>
    /// Checks if the new data is different from the previous data
    /// </summary>
    private bool HasDataChanged(byte[] newData)
    {
        return !newData.SequenceEqual(_previousData);
    }

    /// <summary>
    /// Internal method for reading data without disposal checks (used by polling)
    /// </summary>
    private byte[] ReadDataInternal()
    {
        Marshal.WriteInt32(_inputLengthPtr, InputBufferSizeBytes);
        int result = PHB04BLibrary.XGetInput(_dataInputBuffer, _inputLengthPtr);
        PHB04BException.ThrowIfNotSuccess(result);
            
        byte[] data = new byte[InputBufferSizeBytes];
        Marshal.Copy(_dataInputBuffer, data, 0, InputBufferSizeBytes);
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
                    int result = PHB04BLibrary.XClose();
                    PHB04BException.ThrowIfNotSuccess(result);
                }
                catch (PHB04BException)
                {
                    // Suppress exceptions during disposal
                }
                finally
                {
                    _initialized = false;
                }
            }
                
            if (_dataInputBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_dataInputBuffer);
                _dataInputBuffer = IntPtr.Zero;
            }
                
            if (_inputLengthPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_inputLengthPtr);
                _inputLengthPtr = IntPtr.Zero;
            }
                
            _disposed = true;
        }
    }

    /// <summary>
    /// Finalizer to ensure resources are cleaned up
    /// </summary>
    ~WHB04BWrapper()
    {
        Dispose(false);
    }
}
