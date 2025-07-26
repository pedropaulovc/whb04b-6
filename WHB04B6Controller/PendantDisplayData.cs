namespace WHB04B6Controller
{
    /// <summary>
    /// Represents data to be displayed on the pendant
    /// </summary>
    public class PendantDisplayData
    {
        public byte[] RawData { get; }

        public PendantDisplayData(byte[] data)
        {
            RawData = data ?? throw new ArgumentNullException(nameof(data));
        }
    }
}