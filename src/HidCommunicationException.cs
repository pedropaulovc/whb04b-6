namespace WHB04B6Controller;

/// <summary>
/// Exception thrown when HID communication operations fail
/// </summary>
public class HidCommunicationException : Exception
{
    public int ErrorCode { get; }

    internal HidCommunicationException(int errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }

    internal HidCommunicationException(int errorCode, string message, Exception innerException) : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Throws HidCommunicationException if the return code indicates an error
    /// </summary>
    /// <param name="returnCode">Return code from HID communication operation</param>
    /// <exception cref="HidCommunicationException">Thrown when returnCode is not 0</exception>
    public static void ThrowIfNotSuccess(int returnCode)
    {
        if (returnCode == 0)
        {
            return;
        }

        string message = returnCode switch
        {
            100 => "USB device not open",
            101 => "USB download error", 
            102 => "USB read error",
            103 => "Parameter error",
            _ => $"Unknown error code: {returnCode}"
        };

        throw new HidCommunicationException(returnCode, message);
    }
}
