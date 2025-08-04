namespace WHB04B6Controller;

/// <summary>
/// Exception thrown when PHB04B operations fail
/// </summary>
public class PHB04BException : Exception
{
    public int ErrorCode { get; }

    internal PHB04BException(int errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }

    internal PHB04BException(int errorCode, string message, Exception innerException) : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Throws PHB04BException if the return code indicates an error
    /// </summary>
    /// <param name="returnCode">Return code from PHB04B API call</param>
    /// <exception cref="PHB04BException">Thrown when returnCode is not 0</exception>
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

        throw new PHB04BException(returnCode, message);
    }
}
