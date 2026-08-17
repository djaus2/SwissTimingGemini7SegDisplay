using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwissTimingDisplay.Properties
{
    public sealed class Settings
    {
        private static Lazy<Settings> _default = new Lazy<Settings>(Load);

        public static Settings Default => _default.Value;

        public static void Reset()
        {
            _default = new Lazy<Settings>(() =>
            {
                var s = new Settings();
                s.Save();
                return s;
            });
            _ = _default.Value;
        }

        public bool useSiriccoSimulator { get; set; }
        [JsonIgnore]
        public bool ShowSiriccoControls { get; set; }

        private static string SettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SwissTimingDisplay",
            "settings.json");

        public void Save()
        {
            string path = SettingsPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }

        private static Settings Load()
        {
            string path = SettingsPath;
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
                }
                catch
                {
                    return new Settings();
                }
            }

            return new Settings();
        }
    }
}
