namespace WHB04B6Controller;

/// <summary>
/// Represents the key pressed on the pendant
/// </summary>
public enum KeyPressed
{
    None = 0,
    Key1 = 1,   // Reset
    Key2 = 2,   // Stop
    Key3 = 3,   // Start/Pause
    Key4 = 4,   // Macro-1/Feed+
    Key5 = 5,   // Macro-2/Feed-
    Key6 = 6,   // Macro-3/Spindle+
    Key7 = 7,   // Macro-4/Spindle-
    Key8 = 8,   // Macro-5/M-Home
    Key9 = 9,   // Macro-6/Safe-Z
    Key10 = 10, // Macro-7/W Home
    Key11 = 11, // Macro-8/S on/off
    Key12 = 12, // Fn
    Key13 = 13, // Macro-9/Probe Z
    Key14 = 14, // Macro-10
    Key15 = 15, // Continuous
    Key16 = 16, // Step
    Unknown = 255
}

/// <summary>
/// Represents the dial position on the pendant
/// Left dial: axis selection (Off/X/Y/Z/A/B/C)
/// Right dial: feed rate/spindle speed (0.001-Lead or 2%-100%)
/// </summary>
public enum DialPosition
{
    Position1 = 1, // Left: Off,    Right: 0.001/2%
    Position2 = 2, // Left: X,      Right: 0.01/5%
    Position3 = 3, // Left: Y,      Right: 0.1/10%
    Position4 = 4, // Left: Z,      Right: 1/30%
    Position5 = 5, // Left: A,      Right: 60%
    Position6 = 6, // Left: B,      Right: 100%
    Position7 = 7, // Left: C,      Right: Lead
    Unknown = 255
}

/// <summary>
/// Event arguments for pendant data changes
/// </summary>
public class PendantInputData : EventArgs
{
    public byte[] RawData { get; }
    public DateTime Timestamp { get; }

    public KeyPressed FirstKeyPressed => ParseKeyPressed(RawData.Length > 0 ? RawData[0] : (byte)0);
    public KeyPressed SecondKeyPressed => ParseKeyPressed(RawData.Length > 1 ? RawData[1] : (byte)0);
    public DialPosition RightDial => ParseRightDial(RawData.Length > 2 ? RawData[2] : (byte)0);
    public DialPosition LeftDial => ParseLeftDial(RawData.Length > 3 ? RawData[3] : (byte)0);
    public int JogCountOffset => RawData.Length > 4 ? (sbyte)RawData[4] : 0;

    public PendantInputData(byte[] data)
    {
        RawData = data ?? throw new ArgumentNullException(nameof(data));
            
        if (data.Length < 5)
        {
            throw new ArgumentException("Data must be at least 5 bytes", nameof(data));
        }
            
        Timestamp = DateTime.Now;
    }

    private static KeyPressed ParseKeyPressed(byte value)
    {
        return value switch
        {
            0x00 => KeyPressed.None,
            0x01 => KeyPressed.Key1,
            0x02 => KeyPressed.Key2,
            0x03 => KeyPressed.Key3,
            0x04 => KeyPressed.Key4,
            0x05 => KeyPressed.Key5,
            0x06 => KeyPressed.Key6,
            0x07 => KeyPressed.Key7,
            0x08 => KeyPressed.Key8,
            0x09 => KeyPressed.Key9,
            0x0A => KeyPressed.Key10,
            0x0B => KeyPressed.Key11,
            0x0C => KeyPressed.Key12,
            0x0D => KeyPressed.Key13,
            0x10 => KeyPressed.Key14,
            0x0E => KeyPressed.Key15,
            0x0F => KeyPressed.Key16,
            _ => KeyPressed.Unknown
        };
    }

    private static DialPosition ParseRightDial(byte value)
    {
        return value switch
        {
            0x0D => DialPosition.Position1,
            0x0E => DialPosition.Position2,
            0x0F => DialPosition.Position3,
            0x10 => DialPosition.Position4,
            0x1A => DialPosition.Position5,
            0x1B => DialPosition.Position6,
            0x9B => DialPosition.Position7,
            _ => DialPosition.Unknown
        };
    }

    private static DialPosition ParseLeftDial(byte value)
    {
        return value switch
        {
            0x06 => DialPosition.Position1,
            0x11 => DialPosition.Position2,
            0x12 => DialPosition.Position3,
            0x13 => DialPosition.Position4,
            0x14 => DialPosition.Position5,
            0x15 => DialPosition.Position6,
            0x16 => DialPosition.Position7,
            _ => DialPosition.Unknown
        };
    }
}
