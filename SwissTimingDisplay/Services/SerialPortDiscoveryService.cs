using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using SwissTimingDisplay.Models;

namespace SwissTimingDisplay.Services
{
    public sealed class SerialPortDiscoveryService
    {
        private static readonly Regex ComRegex = new Regex(@"\((COM\d+)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public IReadOnlyList<SerialPortInfo> GetSerialPorts(bool onlyProlific)
        {
            var ports = SerialPort.GetPortNames();
            Array.Sort(ports, StringComparer.OrdinalIgnoreCase);

            var wmiByCom = TryGetWmiComPortInfo();

            var result = new List<SerialPortInfo>(ports.Length);

            foreach (var port in ports)
            {
                if (wmiByCom.TryGetValue(port, out var wmi))
                {
                    if (onlyProlific && !wmi.IsProlific)
                    {
                        continue;
                    }

                    result.Add(new SerialPortInfo(port, wmi.DisplayName, wmi.IsProlific));
                }
                else
                {
                    // Fallback: we still expose the port, but cannot vendor-filter without WMI.
                    if (onlyProlific)
                    {
                        continue;
                    }

                    result.Add(new SerialPortInfo(port, port, false));
                }
            }

            return result;
        }

        private static Dictionary<string, (string DisplayName, bool IsProlific)> TryGetWmiComPortInfo()
        {
            try
            {
                // Win32_PnPEntity contains the friendly Name like "USB-SERIAL CH340 (COM3)".
                // We also grab HardwareID to vendor-detect Prolific (VID_067B).
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Name, HardwareID FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'");

                var dict = new Dictionary<string, (string DisplayName, bool IsProlific)>(StringComparer.OrdinalIgnoreCase);

                foreach (var obj in searcher.Get().OfType<ManagementObject>())
                {
                    var name = obj["Name"]?.ToString();
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    var match = ComRegex.Match(name);
                    if (!match.Success)
                    {
                        continue;
                    }

                    var com = match.Groups[1].Value;
                    var isProlific = IsProlificDevice(obj["HardwareID"] as string[]);

                    dict[com] = (name, isProlific);
                }

                return dict;
            }
            catch
            {
                return new Dictionary<string, (string DisplayName, bool IsProlific)>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static bool IsProlificDevice(string[]? hardwareIds)
        {
            if (hardwareIds is null)
            {
                return false;
            }

            // Prolific VID is 067B. Many PL2303 variants show up as VID_067B.
            return hardwareIds.Any(id => id.Contains("VID_067B", StringComparison.OrdinalIgnoreCase)
                                         || id.Contains("PROLIFIC", StringComparison.OrdinalIgnoreCase)
                                         || id.Contains("PL2303", StringComparison.OrdinalIgnoreCase));
        }
    }
}
