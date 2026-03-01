using OpenSSLGui.PluginAbstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenSSLGui.Plugins;

public sealed class RockYouPasswordPolicyPlugin : IPasswordPolicyPlugin
{
    private HashSet<string> _commonPasswords = new(StringComparer.Ordinal);

    public string Id => "rockyou-policy";
    public string Name => "RockYou Password Blocklist";
    public string Description => "Rejects passwords present in rockyou.txt.";
    public Version Version => new(1, 0, 0);

    public void Initialize(IPluginContext context)
    {
        string path = Path.Combine(context.DataDirectory, "rockyou.txt");
        if (!File.Exists(path))
        {
            context.Log("[RockYou] File not found in plugins/data/rockyou.txt. Plugin will stay passive.");
            return;
        }

        _commonPasswords = File.ReadLines(path)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToHashSet(StringComparer.Ordinal);

        context.Log($"[RockYou] Loaded {_commonPasswords.Count} entries.");
    }

    public PasswordPolicyResult Evaluate(string password)
    {
        if (_commonPasswords.Count == 0)
        {
            return new PasswordPolicyResult(true, "No rockyou dataset loaded.");
        }

        if (_commonPasswords.Contains(password))
        {
            return new PasswordPolicyResult(false, "Password exists in rockyou.txt list.", -2);
        }

        return new PasswordPolicyResult(true, "OK");
    }
}
