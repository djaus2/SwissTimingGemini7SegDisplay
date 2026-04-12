namespace SwissTimingDisplay.Models
{
    public enum CharCommand : byte
    {
        SOH = 0x01,
        STX = 0x02,
        ETX = 0x03,
        EOT = 0x04,
        HOME = 0x08,
        LF = 0x0A,
        ERP = 0x0C,
        DLE = 0x10,
        DC3 = 0x13,
        DC4 = 0x14,
        SPC = 0x20,
        NNN = 0xF0,
        TTT = 0xF1,
        Rxx = 0xF2,
        Sxx = 0xF3,
        TIME = 0xF4,
        B = (byte)'B',
        I = (byte)'I',
    }
}
