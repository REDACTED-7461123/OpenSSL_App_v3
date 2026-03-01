# Plugin System Design for `OpenSSL_App_v2`

This application is currently a single executable with all behavior inside `MainWindow.xaml.cs`. To make it extendable, split extensibility into **contracts**, **plugin loading**, and **UI integration points**.

## 1) Define plugin contracts in a shared assembly

Create a new class library project, for example `OpenSSLGui.PluginAbstractions`, with interfaces that plugin DLLs must implement.

Suggested contract surface:

```csharp
namespace OpenSSLGui.Plugins;

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
    void Log(string message);
    void SetStatus(string message);
    ResourceDictionary AppResources { get; }
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
```

Why this split:
- `IPlugin` provides common metadata and startup behavior.
- `IPasswordPolicyPlugin` allows custom password checks (e.g., rockyou lookup).
- `IThemePlugin` allows additional themes beyond built-in `Dark.xaml`/`Light.xaml`.

## 2) Load plugins from a `plugins/` folder

In the main app, add a loader service that scans `plugins/*.dll`, loads assemblies, and instantiates classes implementing known interfaces.

Minimal loader strategy:
1. `Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins")`
2. For each DLL, `AssemblyLoadContext.Default.LoadFromAssemblyPath(...)`
3. Find non-abstract types assignable to `IPlugin`
4. Instantiate via parameterless constructor
5. Call `Initialize(context)`
6. Register by implemented interface (`IPasswordPolicyPlugin`, `IThemePlugin`, etc.)

Important safety checks:
- Catch and isolate plugin exceptions so one broken plugin does not kill startup.
- Keep a plugin load report (success/failure + error message).
- Consider version checks for contract compatibility.

## 3) Integrate password policy plugins

Today, password checks are hardcoded via `PasswordStrength.Evaluate(...)` and `EnsurePasswordOk()`. Keep that, but add a plugin pass.

Recommended flow in `EnsurePasswordOk()`:
1. Run existing local policy.
2. For each `IPasswordPolicyPlugin`, call `Evaluate(password)`.
3. If any plugin returns `IsAllowed = false`, block operation and show message with plugin name.
4. Aggregate optional score adjustments if you want plugin influence on the strength meter.

Pseudo-code:

```csharp
foreach (var plugin in _passwordPolicyPlugins)
{
    var result = plugin.Evaluate(pwd);
    if (!result.IsAllowed)
    {
        MessageBox.Show($"{plugin.Name}: {result.Message}", "Weak password", ...);
        return false;
    }
}
```

## 4) Example plugin: `rockyou.txt` password check

Create a plugin project `Plugins/RockYouPasswordPolicyPlugin` referencing `OpenSSLGui.PluginAbstractions`.

Implementation idea:
- At plugin initialize:
  - Read `rockyou.txt` from plugin data directory.
  - Normalize entries (trim, optional lowercase).
  - Store in `HashSet<string>` for O(1) lookup.
- At evaluate:
  - If password in set => reject with message "Password found in breached/common list".

Example skeleton:

```csharp
public sealed class RockYouPasswordPolicyPlugin : IPasswordPolicyPlugin
{
    private HashSet<string> _common = new(StringComparer.Ordinal);

    public string Id => "rockyou-policy";
    public string Name => "RockYou Password Blocklist";
    public string Description => "Rejects passwords found in rockyou.txt";
    public Version Version => new(1, 0, 0);

    public void Initialize(IPluginContext context)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "plugins", "data", "rockyou.txt");
        if (!File.Exists(path))
        {
            context.Log("RockYou list not found; plugin is passive.");
            return;
        }

        _common = File.ReadLines(path)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToHashSet(StringComparer.Ordinal);
    }

    public PasswordPolicyResult Evaluate(string password)
    {
        if (_common.Contains(password))
            return new PasswordPolicyResult(false, "Password appears in rockyou list.");

        return new PasswordPolicyResult(true, "OK");
    }
}
```

Operational notes:
- `rockyou.txt` is large; avoid loading on UI thread.
- Consider compressed/binary preprocessed format for faster startup.
- Ensure legal/compliance policy for distributing leaked password datasets.

## 5) Example plugin: theme extension

Create a plugin implementing `IThemePlugin` that returns a `ResourceDictionary`.

Example:

```csharp
public sealed class SolarizedThemePlugin : IThemePlugin
{
    public string Id => "theme-solarized";
    public string Name => "Solarized Theme";
    public string Description => "Adds a Solarized color palette.";
    public Version Version => new(1, 0, 0);
    public string ThemeKey => "Solarized";

    public void Initialize(IPluginContext context) { }

    public ResourceDictionary BuildTheme()
    {
        return new ResourceDictionary
        {
            ["WindowBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#002B36")),
            ["PanelBg"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#073642")),
            ["TextFg"] = Brushes.White
        };
    }
}
```

UI integration:
- Add a ComboBox listing built-in + plugin theme keys.
- On selection, clear current merged dictionaries and apply selected theme dictionary.

## 6) Suggested folder structure

```text
/OpenSSL_App_v2
  /plugins
    /RockYouPasswordPolicyPlugin
      RockYouPasswordPolicyPlugin.dll
    /SolarizedThemePlugin
      SolarizedThemePlugin.dll
    /data
      rockyou.txt
  /src (or current root for existing app files)
  /OpenSSLGui.PluginAbstractions
```

## 7) Security model recommendation

Loading arbitrary DLLs is equivalent to running arbitrary code.

Minimum controls:
- Only load signed plugins from trusted publishers.
- Keep plugin directory writable only by admins.
- Add a plugin allowlist (`plugins.json`) with explicit IDs + versions.
- Show loaded plugins in UI with status and source path.

If you need stronger isolation, move plugins to out-of-process workers and communicate via IPC.

## 8) Incremental implementation plan

1. Extract contracts to `OpenSSLGui.PluginAbstractions`.
2. Implement `PluginManager` + loader + diagnostics.
3. Wire password policy extension point first.
4. Build and validate `RockYouPasswordPolicyPlugin`.
5. Add theme extension point and theme selector UI.
6. Add plugin settings page (enable/disable per plugin).

This lets you start with your two concrete plugin types while leaving room for future operations (e.g., new hash providers, key checks, reporting exporters).
