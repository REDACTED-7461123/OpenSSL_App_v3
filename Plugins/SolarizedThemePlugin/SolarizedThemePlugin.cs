using OpenSSLGui.PluginAbstractions;
using System;
using System.Windows;
using System.Windows.Media;

namespace OpenSSLGui.Plugins;

public sealed class SolarizedThemePlugin : IThemePlugin
{
    public string Id => "theme-solarized";
    public string Name => "Solarized Theme";
    public string Description => "Solarized dark theme palette";
    public Version Version => new(1, 0, 0);
    public string ThemeKey => "Solarized";

    public void Initialize(IPluginContext context)
    {
        context.Log("[SolarizedTheme] ready");
    }

    public ResourceDictionary BuildTheme()
    {
        return new ResourceDictionary
        {
            ["AppBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#002B36")),
            ["PanelBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#073642")),
            ["TextBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EEE8D5")),
            ["MutedTextBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#93A1A1")),
            ["AccentBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#268BD2"))
        };
    }
}
