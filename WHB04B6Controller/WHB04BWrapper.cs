using System.Runtime.InteropServices;
using System.Timers;

namespace WHB04B6Controller
{
    /// <summary>
    /// Windows API imports for getting console window handle
    /// </summary>
    internal static class WindowsApi
    {
        [DllImport("kernel32.dll")]
        internal static extern IntPtr GetConsoleWindow();
    }


    /// <summary>
    /// High-level wrapper library for the WHB04B-6 CNC pendant controller
    /// Provides simplified APIs that translate low-level controller operations
    /// </summary>
    public class WHB04BWrapper : IDisposable
    {
        private const int BufferSize = 5;
        private bool _disposed = false;
        private bool _initialized = false;
        private System.Timers.Timer? _pollingTimer;
        private byte[]? _previousData;
        private readonly object _lockObject = new object();
        private IntPtr _dataBuffer;
        private IntPtr _lengthPtr;

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
            _dataBuffer = Marshal.AllocHGlobal(BufferSize);
            _lengthPtr = Marshal.AllocHGlobal(Marshal.SizeOf<int>());
            
            PHB04BController.Xinit();
            _initialized = true;
            
            // Get console window handle and open the device
            IntPtr consoleHandle = WindowsApi.GetConsoleWindow();
            if (consoleHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Could not get console window handle");
            }

            int result = PHB04BController.XOpen((int)consoleHandle);
            if (result != 0)
            {
                throw new InvalidOperationException($"Failed to open USB device. Error code: {result}");
            }
            
            StartPolling();
        }

        /// <summary>
        /// Opens the USB controller device for communication
        /// </summary>
        /// <param name="parentWindowHandle">Handle of the parent window for receiving messages</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool OpenDevice(int parentWindowHandle)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            int result = PHB04BController.XOpen(parentWindowHandle);
            if (result == 0)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Sends data to the pendant device
        /// </summary>
        /// <param name="data">Data buffer to send</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool SendData(byte[] data)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (data == null || data.Length == 0)
            {
                return false;
            }

            IntPtr lengthPtr = Marshal.AllocHGlobal(Marshal.SizeOf<int>());
            try
            {
                Marshal.WriteInt32(lengthPtr, data.Length);
                int result = PHB04BController.XSendOutput(data, lengthPtr);
                return result == 0;
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
        /// <returns>True if successful, false otherwise</returns>
        public bool SendDisplayData(PendantDisplayData displayData)
        {
            if (displayData == null)
            {
                throw new ArgumentNullException(nameof(displayData));
            }

            return SendData(displayData.RawData);
        }

        /// <summary>
        /// Reads data from the pendant device
        /// </summary>
        /// <returns>Data read from device, or null if error occurred</returns>
        public byte[]? ReadData()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            lock (_lockObject)
            {
                Marshal.WriteInt32(_lengthPtr, BufferSize);
                int result = PHB04BController.XGetInput(_dataBuffer, _lengthPtr);
                
                if (result == 0)
                {
                    byte[] data = new byte[BufferSize];
                    Marshal.Copy(_dataBuffer, data, 0, BufferSize);
                    return data;
                }
                return null;
            }
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
                    byte[]? newData = ReadDataInternal();
                    if (newData != null && HasDataChanged(newData))
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
            if (_previousData == null)
            {
                return true;
            }

            if (_previousData.Length != newData.Length)
            {
                return true;
            }

            for (int i = 0; i < newData.Length; i++)
            {
                if (_previousData[i] != newData[i])
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Internal method for reading data without disposal checks (used by polling)
        /// </summary>
        private byte[]? ReadDataInternal()
        {
            Marshal.WriteInt32(_lengthPtr, BufferSize);
            int result = PHB04BController.XGetInput(_dataBuffer, _lengthPtr);
            
            if (result == 0)
            {
                byte[] data = new byte[BufferSize];
                Marshal.Copy(_dataBuffer, data, 0, BufferSize);
                return data;
            }
            return null;
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
                    int result = PHB04BController.XClose();
                    _initialized = false;
                }
                
                if (_dataBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_dataBuffer);
                    _dataBuffer = IntPtr.Zero;
                }
                
                if (_lengthPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_lengthPtr);
                    _lengthPtr = IntPtr.Zero;
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
}