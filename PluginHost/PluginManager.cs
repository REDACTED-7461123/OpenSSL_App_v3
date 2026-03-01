using OpenSSLGui.PluginAbstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Windows;

namespace OpenSSLGui.PluginHost;

public sealed class PluginManager
{
    private readonly List<IPasswordPolicyPlugin> _passwordPlugins = new();
    private readonly Dictionary<string, IThemePlugin> _themePlugins = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _loadMessages = new();

    public IReadOnlyList<IPasswordPolicyPlugin> PasswordPolicyPlugins => _passwordPlugins;
    public IReadOnlyDictionary<string, IThemePlugin> ThemePlugins => _themePlugins;
    public IReadOnlyList<string> LoadMessages => _loadMessages;

    public void LoadPlugins(Action<string> log, Action<string> setStatus)
    {
        _passwordPlugins.Clear();
        _themePlugins.Clear();
        _loadMessages.Clear();

        string pluginDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
        Directory.CreateDirectory(pluginDir);

        var context = new WpfPluginContext(
            pluginDir,
            Path.Combine(pluginDir, "data"),
            log,
            setStatus);

        Directory.CreateDirectory(context.DataDirectory);

        foreach (string dll in Directory.EnumerateFiles(pluginDir, "*.dll", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(dll));
                LoadFromAssembly(asm, context);
                _loadMessages.Add($"Loaded: {Path.GetFileName(dll)}");
            }
            catch (Exception ex)
            {
                _loadMessages.Add($"Failed: {Path.GetFileName(dll)} | {ex.Message}");
                log($"[Plugin] Failed to load '{dll}': {ex.Message}");
            }
        }

        if (_loadMessages.Count == 0)
        {
            _loadMessages.Add("No plugins found in ./plugins");
        }
    }

    private void LoadFromAssembly(Assembly assembly, IPluginContext context)
    {
        foreach (Type type in assembly.GetTypes().Where(t => !t.IsAbstract && typeof(IPlugin).IsAssignableFrom(t)))
        {
            try
            {
                if (Activator.CreateInstance(type) is not IPlugin plugin)
                {
                    continue;
                }

                plugin.Initialize(context);

                if (plugin is IPasswordPolicyPlugin pwdPlugin)
                {
                    _passwordPlugins.Add(pwdPlugin);
                }

                if (plugin is IThemePlugin themePlugin)
                {
                    _themePlugins[themePlugin.ThemeKey] = themePlugin;
                }

                _loadMessages.Add($"Plugin: {plugin.Name} v{plugin.Version}");
                context.Log($"[Plugin] {plugin.Name} initialized");
            }
            catch (Exception ex)
            {
                _loadMessages.Add($"Plugin type failed: {type.FullName} | {ex.Message}");
                context.Log($"[Plugin] Type load failed '{type.FullName}': {ex.Message}");
            }
        }
    }

    private sealed class WpfPluginContext(
        string pluginDirectory,
        string dataDirectory,
        Action<string> log,
        Action<string> setStatus) : IPluginContext
    {
        public string PluginDirectory { get; } = pluginDirectory;
        public string DataDirectory { get; } = dataDirectory;

        public void Log(string message) => log(message);

        public void SetStatus(string message) => setStatus(message);
    }
}
