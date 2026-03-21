using System.ComponentModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace OpenSSL_App_v3
{
    public class PluginCatalog
    {
        public List<ThemeOption> Themes { get; } = new();
        public List<EncryptionAlgorithmOption> EncryptionAlgorithms { get; } = new();
        public List<HashAlgorithmOption> HashAlgorithms { get; } = new();
        public List<string> LoadMessages { get; } = new();

        public static PluginCatalog Load(string baseDirectory)
        {
            var catalog = new PluginCatalog();
            catalog.LoadBuiltIns(baseDirectory);    //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
            catalog.LoadExternalPlugins(baseDirectory);
            return catalog;
        }

        private void LoadBuiltIns(string baseDirectory)
        {
            Themes.AddRange(BuiltInPluginData.CreateThemes(baseDirectory));
        }

        private void LoadExternalPlugins(string baseDirectory)
        {
            string pluginsDir = Path.Combine(baseDirectory, "Plugins");
            if (!Directory.Exists(pluginsDir))
                return;

            foreach (string manifestPath in Directory.EnumerateFiles(pluginsDir, "plugin.json", SearchOption.AllDirectories))
            {
                try
                {
                    string root = Path.GetDirectoryName(manifestPath) ?? pluginsDir;
                    string json = File.ReadAllText(manifestPath);
                    var manifest = JsonSerializer.Deserialize<PluginManifest>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (manifest == null || string.IsNullOrWhiteSpace(manifest.Id))
                    {
                        LoadMessages.Add($"[Plugin] Skipped invalid manifest: {manifestPath}");
                        continue;
                    }

                    AddManifest(root, manifest);
                }
                catch (Exception ex)
                {
                    LoadMessages.Add($"[Plugin] Failed to load {manifestPath}: {ex.Message}");
                }
            }
        }

        private void AddManifest(string root, PluginManifest manifest)
        {
            foreach (ThemePluginManifest item in manifest.Themes)
            {
                string path = ResolvePath(root, item.Resource);
                Themes.Add(new ThemeOption($"{manifest.Id}:{item.Id}", item.Name, path, false));
            }
        }

        private static string ResolvePath(string root, string value)
        {
            if (Path.IsPathRooted(value))
                return value;

            return Path.GetFullPath(Path.Combine(root, value));
        }
    }

    public static class BuiltInPluginData
    {
        public const string LightThemeId = "builtin:theme-light";
        public const string DarkThemeId = "builtin:theme-dark";
        public const string BundledOpenSslProviderId = "builtin:openssl-bundled";
        public const string DefaultPasswordCheckerId = "builtin:checker-default";
        public const string DefaultPasswordGeneratorId = "builtin:generator-default";
        public const string DefaultEncryptionAlgorithmId = "builtin:enc-aes-256-cbc";
        public const string DefaultHashAlgorithmId = "builtin:hash-sha256";
        public const string BundledOpenSslSha256 = "3412f2c4a3d0367bf6212c965df30658758575aebe8e74d12e2d9382e5a00170";

        public static IEnumerable<ThemeOption> CreateThemes(string baseDirectory)
        {
            yield return new ThemeOption(LightThemeId, "Light", "/Light.xaml", true);
            yield return new ThemeOption(DarkThemeId, "Dark", "/Dark.xaml", true);
        }
    }

    public record HashAlgorithmOption(string DisplayName, string CommandName, bool IsDangerous, string Id, string WarningMessage) 
    {
        public override string ToString() => DisplayName;
    }
    public record EncryptionAlgorithmOption(string Id, string DisplayName, bool IsSymmetric, bool SupportsSalt, bool IsDangerous, string CommandName, string WarningMessage)
    {
        public override string ToString() => DisplayName;
    }

    public record ThemeOption(string Id, string DisplayName, string ResourcePath, bool IsBuiltIn)
    {
        public override string ToString() => DisplayName;
    }

    public static class ThemeManager
    {
        public static void ApplyTheme(ThemeOption theme)
        {
            var app = Application.Current;
            if (app == null)
                return;

            var dicts = app.Resources.MergedDictionaries;
            dicts.Clear();
            dicts.Add(new ResourceDictionary
            {
                Source = theme.IsBuiltIn
                    ? new Uri(theme.ResourcePath, UriKind.Relative)
                    : new Uri(theme.ResourcePath, UriKind.Absolute)
            });
        }
    }
}
