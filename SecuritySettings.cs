using System;
using System.IO;
using System.Text.Json;

namespace OpenSSL_App_v3
{
    public sealed class SecuritySettings
    {
        public bool ClearPasswordAfterOperation { get; set; } = true;
        public bool PreventClipboardCopy { get; set; } = true;
        public bool ConfirmDangerousOperations { get; set; } = true;

        public string PreferredThemeId { get; set; } = BuiltInPluginData.LightThemeId;
        public string PreferredOpenSslProviderId { get; set; } = BuiltInPluginData.BundledOpenSslProviderId;
        public string PreferredPasswordCheckerId { get; set; } = BuiltInPluginData.DefaultPasswordCheckerId;
        public string PreferredPasswordGeneratorId { get; set; } = BuiltInPluginData.DefaultPasswordGeneratorId;
        public string DefaultEncryptionAlgorithmId { get; set; } = BuiltInPluginData.DefaultEncryptionAlgorithmId;
        public string DefaultHashAlgorithmId { get; set; } = BuiltInPluginData.DefaultHashAlgorithmId;

        public static SecuritySettings Default() => new SecuritySettings();
    }

    public sealed class SecuritySettingsStore
    {
        private readonly string filePath;

        public SecuritySettingsStore(string filePath)
        {
            this.filePath = filePath;
        }

        public SecuritySettings Load()
        {
            try
            {
                if (!File.Exists(filePath))
                    return SecuritySettings.Default();

                string json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<SecuritySettings>(json) ?? SecuritySettings.Default();
            }
            catch
            {
                return SecuritySettings.Default();
            }
        }

        public void Save(SecuritySettings settings)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? AppDomain.CurrentDomain.BaseDirectory);
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }
    }
}
