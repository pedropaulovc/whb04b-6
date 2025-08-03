namespace WHB04B6Controller;

/// <summary>
/// Represents the key pressed on the pendant
/// Mapping matches LinuxCNC implementation key codes exactly
/// </summary>
public enum KeyPressed
{
    None = 0,
    Key1 = 1,   // Reset (LinuxCNC: reset)
    Key2 = 2,   // Stop (LinuxCNC: stop)
    Key3 = 3,   // Start/Pause (LinuxCNC: start)
    Key4 = 4,   // Macro-1/Feed+ (LinuxCNC: feed_plus)
    Key5 = 5,   // Macro-2/Feed- (LinuxCNC: feed_minus)
    Key6 = 6,   // Macro-3/Spindle+ (LinuxCNC: spindle_plus)
    Key7 = 7,   // Macro-4/Spindle- (LinuxCNC: spindle_minus)
    Key8 = 8,   // Macro-5/M-Home (LinuxCNC: machine_home)
    Key9 = 9,   // Macro-6/Safe-Z (LinuxCNC: safe_z)
    Key10 = 10, // Macro-7/W Home (LinuxCNC: workpiece_home)
    Key11 = 11, // Macro-8/S on/off (LinuxCNC: spindle_on_off)
    Key12 = 12, // Fn (LinuxCNC: function)
    Key13 = 13, // Macro-9/Probe Z (LinuxCNC: probe_z)
    Key14 = 16, // Macro-10 (LinuxCNC: macro10, key code 0x10)
    Key15 = 14, // Continuous (LinuxCNC: manual_pulse_generator, key code 0x0e)
    Key16 = 15, // Step (LinuxCNC: step_continuous, key code 0x0f)
    Unknown = 255
}

/// <summary>
/// Represents the dial position on the pendant
/// Left dial: axis selection (Off/X/Y/Z/A/B/C)
/// Right dial: feed rate/spindle speed (0.001-Lead or 2%-100%)
/// Mapping matches LinuxCNC implementation exactly
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
/// HID packet structure (8 bytes): [header=0x04, random, button1, button2, feedDial, axisDial, jogDelta, checksum]
/// </summary>
public class PendantInputData : EventArgs
{
    public byte[] RawData { get; }
    public DateTime Timestamp { get; }

    public KeyPressed FirstKeyPressed => ParseKeyPressed(RawData.Length > 2 ? RawData[2] : (byte)0);
    public KeyPressed SecondKeyPressed => ParseKeyPressed(RawData.Length > 3 ? RawData[3] : (byte)0);
    public DialPosition RightDial => ParseRightDial(RawData.Length > 4 ? RawData[4] : (byte)0);
    public DialPosition LeftDial => ParseLeftDial(RawData.Length > 5 ? RawData[5] : (byte)0);
    public int JogCountOffset => RawData.Length > 6 ? (sbyte)RawData[6] : 0;

    public PendantInputData(byte[] data)
    {
        RawData = data ?? throw new ArgumentNullException(nameof(data));
            
        if (data.Length < 8)
        {
            throw new ArgumentException("Data must be at least 8 bytes", nameof(data));
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
            0x10 => KeyPressed.Key14,  // Macro-10
            0x0E => KeyPressed.Key15,  // Continuous (MPG)
            0x0F => KeyPressed.Key16,  // Step
            _ => KeyPressed.Unknown
        };
    }

    private static DialPosition ParseRightDial(byte value)
    {
        return value switch
        {
            0x0D => DialPosition.Position1, // 0.001/2%
            0x0E => DialPosition.Position2, // 0.01/5%
            0x0F => DialPosition.Position3, // 0.1/10%
            0x10 => DialPosition.Position4, // 1/30%
            0x1A => DialPosition.Position5, // 60%
            0x1B => DialPosition.Position6, // 100%
            0x1C => DialPosition.Position7, // Lead (corrected from 0x9B to 0x1C)
            _ => DialPosition.Unknown
        };
    }

    private static DialPosition ParseLeftDial(byte value)
    {
        return value switch
        {
            0x06 => DialPosition.Position1, // Off
            0x11 => DialPosition.Position2, // X
            0x12 => DialPosition.Position3, // Y
            0x13 => DialPosition.Position4, // Z
            0x14 => DialPosition.Position5, // A
            0x15 => DialPosition.Position6, // B
            0x16 => DialPosition.Position7, // C
            _ => DialPosition.Unknown
        };
    }
}
