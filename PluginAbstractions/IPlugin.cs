using System;
using System.Windows;

namespace OpenSSLGui.PluginAbstractions;

public interface IPlugin
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    Version Version { get; }
    void Initialize(IPluginContext context);
}

public interface IPluginContext
{
    string PluginDirectory { get; }
    string DataDirectory { get; }
    void Log(string message);
    void SetStatus(string message);
}

public interface IPasswordPolicyPlugin : IPlugin
{
    PasswordPolicyResult Evaluate(string password);
}

public sealed record PasswordPolicyResult(
    bool IsAllowed,
    string Message,
    int ScoreAdjustment = 0
);

public interface IThemePlugin : IPlugin
{
    string ThemeKey { get; }
    ResourceDictionary BuildTheme();
}
