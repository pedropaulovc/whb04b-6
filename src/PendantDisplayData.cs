namespace WHB04B6Controller;

/// <summary>
/// Jog mode for the pendant
/// </summary>
public enum JogMode
{
    Continuous = 0x00, // xxxx xx00
    Step = 0x01,       // xxxx xx01
    None = 0x02,       // xxxx xx1x
    Reset = 0x40       // x1xx xxxx
}

/// <summary>
/// Coordinate system for the pendant
/// </summary>
public enum CoordinateSystem
{
    XYZ = 0x00,    // 0xxx xxxx
    X1Y1Z1 = 0x80  // 1xxx xxxx
}

/// <summary>
/// Represents data to be displayed on the pendant
/// </summary>
public class PendantDisplayData(JogMode jogMode, CoordinateSystem coordinateSystem, decimal number1, decimal number2, decimal number3)
{
    public decimal Number1 { get; } = ValidateNumber(number1, nameof(number1));
    public decimal Number2 { get; } = ValidateNumber(number2, nameof(number2));
    public decimal Number3 { get; } = ValidateNumber(number3, nameof(number3));
    public JogMode JogMode { get; } = jogMode;
    public CoordinateSystem CoordinateSystem { get; } = coordinateSystem;

    public byte[] RawData => GenerateRawData();

    private static decimal ValidateNumber(decimal value, string parameterName)
    {
        if (Math.Abs(value) > 65535.9999m)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Absolute value must not exceed 65535.9999");
        }

        return value;
    }

    /// <summary>
    /// Encode a decimal number in the proprietary 32-bit fixed-point format.
    /// Range: -65535.9999 … +65535.9999
    /// Step: 0.0001 (four decimal digits)
    /// Layout: [byte0, byte1, byte2, byte3] little-endian
    ///   byte0-1 -> 16-bit unsigned integer part
    ///   byte2-3 -> 15-bit fraction (0-9999)
    ///   MSB of byte3 -> sign (1 = negative)
    /// </summary>
    private static byte[] EncodeNumber(decimal value)
    {
        // 1. Sign
        byte signBit = (byte)(value < 0 ? 0x80 : 0);
        decimal absVal = Math.Abs(value);

        // 2. Split integer/fraction
        int intPart = (int)absVal;
        int fracRaw = (int)Math.Round((absVal - intPart) * 10000m);

        // Handle rounding carry (e.g. 1.99995 → 2.0000)
        if (fracRaw == 10000)
        {
            intPart += 1;
            fracRaw = 0;
        }

        // 3. Range check
        if (intPart > 0xFFFF)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Magnitude exceeds 65535.9999");
        }

        // 4. Pack bytes
        byte byte0 = (byte)(intPart & 0xFF);           // least-significant integer byte
        byte byte1 = (byte)((intPart >> 8) & 0xFF);    // most-significant integer byte  
        byte byte2 = (byte)(fracRaw & 0xFF);           // low-order fraction bits
        byte byte3 = (byte)(((fracRaw >> 8) & 0x7F) | signBit); // high-order fraction + sign

        return [byte0, byte1, byte2, byte3];
    }

    private byte GenerateControlByte()
    {
        return (byte)((byte)CoordinateSystem | (byte)JogMode);
    }

    private byte[] GenerateRawData()
    {
        // Based on LinuxCNC HID protocol: [header(2), seed(1), displayFlags(1), row1(4), row2(4), row3(4), feedRate(2), spindleRate(2)]
        // Total: 20 bytes, padded to 21 bytes for HID transmission (3 blocks of 7 bytes each)
        
        byte controlByte = GenerateControlByte();
        byte[] number1Bytes = EncodeNumber(Number1);
        byte[] number2Bytes = EncodeNumber(Number2);
        byte[] number3Bytes = EncodeNumber(Number3);

        byte[] result = new byte[21]; // HID requires 21 bytes (3 blocks of 7 bytes)
        
        // Header (constant 0xfdfe)
        result[0] = 0xfe;
        result[1] = 0xfd;
        
        // Seed (constant 0xfe for compatibility)
        result[2] = 0xfe;
        
        // Display mode flags
        result[3] = controlByte;
        
        // Three coordinate values (4 bytes each)
        Array.Copy(number1Bytes, 0, result, 4, 4);
        Array.Copy(number2Bytes, 0, result, 8, 4);
        Array.Copy(number3Bytes, 0, result, 12, 4);
        
        // Feed rate (2 bytes) - set to 0 for now
        result[16] = 0;
        result[17] = 0;
        
        // Spindle rate (2 bytes) - set to 0 for now  
        result[18] = 0;
        result[19] = 0;
        
        // Padding byte for 21-byte alignment
        result[20] = 0;

        return result;
    }
}
