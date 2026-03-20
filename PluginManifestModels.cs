using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OpenSSL_App_v3
{
    public class PluginManifest
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("themes")]
        public List<ThemePluginManifest> Themes { get; set; } = new();
    }

    public class ThemePluginManifest
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("resource")]
        public string Resource { get; set; } = "";
    }
}