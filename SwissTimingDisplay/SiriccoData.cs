using System;
using System.Linq;

namespace SwissTimingDisplay
{
    public enum SiriccoMessageModes
    {
        Gill_PolarContinuous,
        Gill_UVContinuous,
        NMEANMEA,
        NMEAGill,
        Gill_Tunnel,
        WindGauge_ReadFrequency
    }

    public enum SiriccoSpeedUnits
    {
        MetresPerSecond,  // M
        Knots,            // N
        MilesPerHour,     // P
        KilometresPerHour,// K
        FeetPerMinute     // F
    }

    public class SiriccoData
    {
        private const char STX = '\u0002';
        private const char ETX = '\u0003';

        public bool IsValid { get; private set; }
        public string? ErrorMessage { get; private set; }

        public double? Value1 { get; private set; }
        public double? Value2 { get; private set; }
        public int? Value3 { get; private set; }
        public int? Value4 { get; private set; }
        public SiriccoSpeedUnits? SpeedUnit { get; private set; }

        public class SiriccoResult
        {
            public bool IsValid { get; set; }
            public double Speed { get; set; } = 0;
            public double Speed1 { get; set; } = 0;
            public double Speed2 { get; set; } = 0;
            public int Direction { get; set; } = 0;
            public int iSpeed { get; set; } = 0;
            public SiriccoSpeedUnits? SpeedUnit { get; set; }
            public SiriccoMessageModes? Mode { get; set; }
            public string ErrorMessage { get; set; } = string.Empty;

        }
        public static SiriccoResult? Siricco_Data { get; set; } = null;
        public SiriccoData(string line)
        {
            Siricco_Data = null;
            SiriccoResult result = new SiriccoResult();
            IsValid = false;
            ErrorMessage = null;

            if (string.IsNullOrWhiteSpace(line))
            {
                ErrorMessage = "Line is empty";
                return;
            }

            // Ignore lines starting with $
            if (line.StartsWith('$'))
            {
                ErrorMessage = "Line starts with $, ignored";
                return;
            }

            // Validate STX at start
            if (line.Length == 0 || line[0] != STX)
            {
                ErrorMessage = "Line does not start with STX";
                return;
            }

            // Validate ETX near end (last 2 characters: ETX + checksum)
            if (line.Length < 2 || (byte)line[line.Length - 2] != ETX)
            {
                ErrorMessage = "Line does not contain ETX before checksum";
                return;
            }

            // Extract checksum character
            char checksumChar = line[line.Length - 1];
            byte expectedChecksum = (byte)checksumChar;

            // Calculate checksum: XOR of all bytes between STX and ETX (not including them)
            byte calculatedChecksum = 0;
            for (int i = 1; i < line.Length - 2; i++)
            {
                calculatedChecksum ^= (byte)line[i];
            }

            if (calculatedChecksum != expectedChecksum)
            {
                ErrorMessage = $"Checksum mismatch. Expected: {expectedChecksum}, Calculated: {calculatedChecksum}";
                return;
            }

            // Validate third byte is Q (permitting optional space after STX)
            if (line.Length < 2)
            {
                ErrorMessage = "Line too short";
                return;
            }

            // Remove optional space between STX and Q
            if (line.Length >= 3 && line[0] == STX && line[1] == ' ' && line[2] == 'Q')
            {
                // Format is <STX><space>Q, remove the space
                line = STX + "Q" + line.Substring(3);
            }
            else if (line.Length >= 2 && line[0] == STX && line[1] == 'Q')
            {
                // Format is <STX>Q, no space, keep as is
            }
            else
            {
                ErrorMessage = "Line does not start with <STX>Q or <STX> Q";
                return;
            }

            // Remove spaces adjoining commas before parsing CSV
            string cleanedLine = line.Replace(" ,", ",").Replace(", ", ",");

            // Parse CSV
            string[] parts = cleanedLine.Split(',');
            if (parts.Length != 6)
            {
                ErrorMessage = $"Expected 6 CSV elements, got {parts.Length}";
                return;
            }

            // Validate element 1: <STX>Q (space already removed if present)
            string element1 = parts[0];
            if (element1.Length < 2 || element1[0] != STX || element1[1] != 'Q')
            {
                ErrorMessage = "Element 1 is not in format <STX>Q";
                return;
            }

            // Parse element 2: integer (0-359) or double
            string element2 = parts[1].Trim();
            bool element2IsInt = int.TryParse(element2, out int intVal2);
            bool element2IsDouble = double.TryParse(element2, out double doubleVal2);


            if (!element2IsInt && !element2IsDouble)
            {
                ErrorMessage = "Element 2 is not a valid integer or double";
                return;
            }

            if (element2IsInt && (intVal2 < 0 || intVal2 > 359))
            {
                ErrorMessage = "Element 2 integer must be between 0 and 359";
                return;
            }

            Value1 = element2IsInt ? intVal2 : doubleVal2;

            // Parse element 3: double or integer
            string element3 = parts[2].Trim();
            bool element3IsInt = int.TryParse(element3, out int intVal3);
            bool element3IsDouble = double.TryParse(element3, out double doubleVal3);

            if (!element3IsInt && !element3IsDouble)
            {
                ErrorMessage = "Element 3 is not a valid integer or double";
                return;
            }

            Value2 = element3IsInt ? intVal3 : doubleVal3;

            // Qualification: items 1 and 2 cannot both be integer
            if (element2IsInt && element3IsInt)
            {
                ErrorMessage = "Elements 1 and 2 cannot both be integers";
                return;
            }

            // Parse element 4: positive integer
            string element4 = parts[3].Trim();
            if (!int.TryParse(element4, out int intVal4) || intVal4 < 0)
            {
                ErrorMessage = "Element 4 is not a valid positive integer";
                return;
            }
            Value3 = intVal4;

            // Parse element 5: positive integer followed by speed unit character
            string element5 = parts[4].Trim();
            if (string.IsNullOrEmpty(element5) || element5.Length < 1)
            {
                ErrorMessage = "Element 5 is empty";
                return;
            }

            // Extract speed unit character (last character before ETX)
            char speedUnitChar = element5[element5.Length - 1];

            // Parse speed unit
            SpeedUnit = ParseSpeedUnit(speedUnitChar);
            if (SpeedUnit == null)
            {
                ErrorMessage = $"Invalid speed unit character: {speedUnitChar}";
                return;
            }

            // Validate element 6: <ETX>D where D is checksum
            string element6 = parts[5].Trim();
            if (element6.Length < 2 || element6[0] != ETX)
            {
                ErrorMessage = "Element 6 is not in format <ETX>D";
                return;
            }
            if (SpeedUnit != null)
            {
                result.SpeedUnit = SpeedUnit;
            }
            if ((element2IsDouble)&& (element3IsInt) && (intVal3 == 1))
            {
                result.IsValid = true;
                IsValid = true;
                result.Speed = doubleVal2;
                result.Mode = SiriccoMessageModes.Gill_Tunnel;
            }
            else if((element2IsInt)&&(element3IsDouble))
            {
                result.IsValid = true;
                IsValid = true;
                result.Direction = intVal2;
                result.Speed = doubleVal3;
                result.Mode = SiriccoMessageModes.Gill_PolarContinuous;
            }
            else if ((element2IsDouble) && (element3IsDouble))
            {
                result.IsValid = true;
                IsValid = true;
                result.Speed1 = doubleVal2;
                result.Speed2 = doubleVal3;
                result.Mode = SiriccoMessageModes.Gill_UVContinuous;
            }
            else
            {
                IsValid = false;
                result.IsValid = false;
                result.ErrorMessage = "Invalid combination of values";
            }
            Siricco_Data = result;
        }

        public static SiriccoSpeedUnits? ParseSpeedUnit(char unitChar)
        {
            return unitChar switch
            {
                'M' => SiriccoSpeedUnits.MetresPerSecond,
                'N' => SiriccoSpeedUnits.Knots,
                'P' => SiriccoSpeedUnits.MilesPerHour,
                'K' => SiriccoSpeedUnits.KilometresPerHour,
                'F' => SiriccoSpeedUnits.FeetPerMinute,
                _ => null
            };
        }

        public static SiriccoData[] ParseLines(string data)
        {
            if (string.IsNullOrWhiteSpace(data))
            {
                return Array.Empty<SiriccoData>();
            }

            string[] lines = data.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            return lines.Select(line => new SiriccoData(line)).ToArray();
        }
    }
}
