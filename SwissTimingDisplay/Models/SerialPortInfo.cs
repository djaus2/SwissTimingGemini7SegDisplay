namespace SwissTimingDisplay.Models
{
    public sealed record SerialPortInfo(string PortName, string DisplayName, bool IsProlific)
    {
        public override string ToString() => DisplayName;
    }
}
