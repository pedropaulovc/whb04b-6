using System.Runtime.InteropServices;

namespace WHB04B6Controller
{
    /// <summary>
    /// PHB04B DLL wrapper class
    /// Contains methods to communicate with XHC wireless pendant through USB controller
    /// </summary>
    public class PHB04BController
    {
        /// <summary>
        /// Initialize the PHB04B USB controller
        /// Must be called before any other functions
        /// </summary>
        [DllImport("PHB04B.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern void Xinit();

        /// <summary>
        /// Close the connection to the USB controller
        /// </summary>
        /// <returns>0 for success, error codes: 100 (USB device not open), 101 (USB download error), 102 (USB read error), 103 (Parameter error)</returns>
        [DllImport("PHB04B.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int XClose();

        /// <summary>
        /// Open the USB controller device to establish communication
        /// </summary>
        /// <param name="handle">The handle of the parent window for receiving messages</param>
        /// <returns>0 for success, error codes: 100 (USB device not open), 101 (USB download error), 102 (USB read error), 103 (Parameter error)</returns>
        [DllImport("PHB04B.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int XOpen(int handle);

        /// <summary>
        /// Download information to the device display
        /// Processes opaque byte streams for device communication
        /// </summary>
        /// <param name="sendBuffer">Buffer containing data to send</param>
        /// <param name="length">Pointer to the length of data being sent</param>
        /// <returns>0 for success, error codes: 100 (USB device not open), 101 (USB download error), 102 (USB read error), 103 (Parameter error)</returns>
        [DllImport("PHB04B.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int XSendOutput(byte[] sendBuffer, IntPtr length);

        /// <summary>
        /// Read data from the pendant device
        /// Processes opaque byte streams for device communication
        /// </summary>
        /// <param name="getBuffer">Buffer pointer to store received data</param>
        /// <param name="length">Pointer to the length of data to read</param>
        /// <returns>0 for success, error codes: 100 (USB device not open), 101 (USB download error), 102 (USB read error), 103 (Parameter error)</returns>
        [DllImport("PHB04B.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern int XGetInput(IntPtr getBuffer, IntPtr length);
    }
}