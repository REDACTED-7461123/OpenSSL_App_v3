# OpenSSL_App_v2

## Plugin support (working implementation)

This app now supports loading plugins from a runtime `plugins` folder next to the executable.

### Supported plugin types
- `IPasswordPolicyPlugin` — can block weak/known passwords.
- `IThemePlugin` — can provide additional UI themes.

Plugin contracts are in `OpenSSLGui.PluginAbstractions.csproj` (`PluginAbstractions/IPlugin.cs`).

### Runtime behavior
- At startup, `MainWindow` loads `*.dll` from `./plugins`.
- Password plugins are checked in `EnsurePasswordOk()` before encrypt/decrypt.
- Theme plugins are added to the theme cycle used by the **Switch theme** button.

### Included example plugin projects
- `Plugins/RockYouPasswordPolicyPlugin`
- `Plugins/SolarizedThemePlugin`

### How to try
1. Build plugin projects.
2. Copy resulting DLLs into the app output folder's `plugins` directory.
3. (Optional) place `rockyou.txt` in `plugins/data/rockyou.txt`.
4. Run the app.

If a plugin fails to load, the app logs the reason in the output panel and keeps running.
