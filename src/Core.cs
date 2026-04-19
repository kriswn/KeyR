using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using System.Collections.Generic;

namespace KeyR
{
    public enum ConditionType
    {
        TimePassed,
        ImageDetected,
        TextDetected
    }

    public class RestartCondition
    {
        public string Name { get; set; } = "";
        public ConditionType Type { get; set; }
        public bool IsEnabled { get; set; } = true;
        
        // Time
        public int TimePassedSeconds { get; set; }
        
        // Image & Text
        public bool IsFullScreen { get; set; } = true;
        public int X1 { get; set; }
        public int Y1 { get; set; }
        public int X2 { get; set; }
        public int Y2 { get; set; }
        
        // Image
        public string ImagePath { get; set; } = "";
        public int Tolerance { get; set; } = 30; // 0-255

        // Text
        public string MatchedText { get; set; } = "";
        public bool UseRegex { get; set; } = false;
        public bool InvertMatch { get; set; } = false;
    }

    public class Settings
    {
        public string RecHotkey { get; set; } = "F8";
        public string PlayHotkey { get; set; } = "F9";
        public string PauseHotkey { get; set; } = "F12";
        public double CustomSpeed { get; set; } = 1.0;
        public int LoopCount { get; set; } = 1;
        public bool AlwaysOnTop { get; set; } = true;
        public bool LoopContinuous { get; set; } = false;
        public bool UseCustomSpeed { get; set; } = false;
        public double X { get; set; } = -1;
        public double Y { get; set; } = -1;
        public double ExpandedHeight { get; set; } = -1;
        public bool IsDarkTheme { get; set; } = true;
        public double SettingsWindowX { get; set; } = -1;
        public double SettingsWindowY { get; set; } = -1;
        public double ConditionWindowX { get; set; } = -1;
        public double ConditionWindowY { get; set; } = -1;
        
        public bool HideDeleteConfirmation { get; set; } = false;
        public bool HasCompletedFirstBoot { get; set; } = false;
        public int ConditionsPollingInterval { get; set; } = 1000; // ms
        public bool UseSmartRestart { get; set; } = false;
        public bool WaitConditionToRestart { get; set; } = false;
        public bool MatchAllConditions { get; set; } = false;
        public double FontScale { get; set; } = 1.0;
        public bool UseBoldText { get; set; } = false;
        public List<RestartCondition> RestartConditions { get; set; } = new List<RestartCondition>();

        // Cached options — avoids allocation on every serialize/deserialize call
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { WriteIndented = true };

        private static string GetSettingsPath()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KeyR");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return Path.Combine(dir, "settings.json");
        }

        public static Settings Load()
        {
            var path = GetSettingsPath();
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    return JsonSerializer.Deserialize<Settings>(json, _jsonOptions) ?? new Settings();
                }
                catch { }
            }
            return new Settings();
        }

        public void Save()
        {
            var path = GetSettingsPath();
            var json = JsonSerializer.Serialize(this, _jsonOptions);
            File.WriteAllText(path, json);
        }
    }

    public static class TT2FileManager
    {
        private static readonly byte[] Key = Encoding.UTF8.GetBytes("T1nyT@skPlu$K3y1"); // 16 bytes
        private static readonly byte[] IV = Encoding.UTF8.GetBytes("T1nyT@skPlu$1V00"); // 16 bytes

        public static void Save(string path, string jsonContent)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = Key;
                aes.IV = IV;
                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                
                using (FileStream fs = new FileStream(path, FileMode.Create))
                using (CryptoStream cs = new CryptoStream(fs, encryptor, CryptoStreamMode.Write))
                using (StreamWriter sw = new StreamWriter(cs))
                {
                    sw.Write(jsonContent);
                }
            }
        }

        public static string Load(string path)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = Key;
                aes.IV = IV;
                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                using (FileStream fs = new FileStream(path, FileMode.Open))
                using (CryptoStream cs = new CryptoStream(fs, decryptor, CryptoStreamMode.Read))
                using (StreamReader sr = new StreamReader(cs))
                {
                    return sr.ReadToEnd();
                }
            }
        }
    }
}
