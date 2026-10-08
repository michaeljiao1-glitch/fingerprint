using System;
using System.Configuration;
using System.IO;

namespace SampleApplication.Services
{
    /// <summary>
    /// Typed access to the &lt;appSettings&gt; section of App.config.
    /// </summary>
    public class AppSettings
    {
        public string DataDirectory { get; private set; }
        public int ScanTimeoutSeconds { get; private set; }
        public TimeSpan DuplicateScanWindow { get; private set; }

        public bool CybermedEnabled { get; private set; }
        public string CybermedBaseUrl { get; private set; }
        public string CybermedApiKey { get; private set; }

        public static AppSettings Load()
        {
            var dataDir = Get("DataDirectory", "");
            if (String.IsNullOrWhiteSpace(dataDir))
            {
                dataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "PatientCheckIn");
            }

            return new AppSettings
            {
                DataDirectory = Environment.ExpandEnvironmentVariables(dataDir),
                ScanTimeoutSeconds = GetInt("ScanTimeoutSeconds", 30),
                DuplicateScanWindow = TimeSpan.FromMinutes(GetInt("DuplicateScanWindowMinutes", 2)),
                CybermedEnabled = GetBool("Cybermed.Enabled", true),
                CybermedBaseUrl = Get("Cybermed.BaseUrl", ""),
                CybermedApiKey = Get("Cybermed.ApiKey", ""),
            };
        }

        private static string Get(string key, string fallback)
        {
            return ConfigurationManager.AppSettings[key] ?? fallback;
        }

        private static int GetInt(string key, int fallback)
        {
            int value;
            return Int32.TryParse(Get(key, ""), out value) ? value : fallback;
        }

        private static bool GetBool(string key, bool fallback)
        {
            bool value;
            return Boolean.TryParse(Get(key, ""), out value) ? value : fallback;
        }
    }
}
