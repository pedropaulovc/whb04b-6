using System.Runtime.InteropServices;

namespace WHB04B6Controller
{
    /// <summary>
    /// High-level wrapper library for the WHB04B-6 CNC pendant controller
    /// Provides simplified APIs that translate low-level controller operations
    /// </summary>
    public class WHB04BWrapper : IDisposable
    {
        private bool _disposed = false;
        private bool _initialized = false;

        /// <summary>
        /// Initializes a new instance of the WHB04BWrapper class
        /// Automatically calls XInit to initialize the controller
        /// </summary>
        public WHB04BWrapper()
        {
            PHB04BController.Xinit();
            _initialized = true;
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
        /// Reads data from the pendant device
        /// </summary>
        /// <param name="bufferSize">Size of buffer to allocate for reading</param>
        /// <returns>Data read from device, or null if error occurred</returns>
        public byte[]? ReadData(int bufferSize = 64)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (bufferSize <= 0)
            {
                return null;
            }

            IntPtr dataBuffer = Marshal.AllocHGlobal(bufferSize);
            IntPtr lengthPtr = Marshal.AllocHGlobal(Marshal.SizeOf<int>());
            
            try
            {
                Marshal.WriteInt32(lengthPtr, bufferSize);
                int result = PHB04BController.XGetInput(dataBuffer, lengthPtr);
                
                if (result == 0)
                {
                    int actualLength = Marshal.ReadInt32(lengthPtr);
                    byte[] data = new byte[actualLength];
                    Marshal.Copy(dataBuffer, data, 0, actualLength);
                    return data;
                }
                return null;
            }
            finally
            {
                Marshal.FreeHGlobal(dataBuffer);
                Marshal.FreeHGlobal(lengthPtr);
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
        /// Protected dispose method
        /// </summary>
        /// <param name="disposing">True if disposing managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (_initialized)
                {
                    int result = PHB04BController.XClose();
                    _initialized = false;
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