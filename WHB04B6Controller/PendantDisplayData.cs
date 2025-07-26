namespace WHB04B6Controller
{
    /// <summary>
    /// Represents data to be displayed on the pendant
    /// </summary>
    public class PendantDisplayData(byte[] data)
    {
        public byte[] RawData { get; } = data ?? throw new ArgumentNullException(nameof(data));
    }
}