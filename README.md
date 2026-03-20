# OpenSSL_App_v2

## Plugin system

The app now scans `Plugins/**/plugin.json` at startup and merges those extensions with the built-in options.

Supported extension types:

- themes via external XAML resource dictionaries
- OpenSSL providers via custom executable paths with optional SHA-256 pinning
- encryption algorithms
- hash algorithms
- password strength checkers
- password generators

### Folder layout

```text
Plugins/
  MyPlugin/
    plugin.json
    MyTheme.xaml
    tools/
      openssl-custom.exe
```

### Minimal manifest example

```json
{
  "id": "myplugin",
  "name": "My Plugin",
  "themes": [
    { "id": "ocean", "name": "Ocean", "resource": "Ocean.xaml" }
  ],
  "opensslProviders": [
    { "id": "openssl3", "name": "OpenSSL 3", "path": "tools/openssl-custom.exe", "sha256": "OPTIONAL_SHA256" }
  ],
  "encryptionAlgorithms": [
    { "id": "aes-256-cfb", "name": "aes-256-cfb", "command": "aes-256-cfb", "supportsSalt": true }
  ],
  "hashAlgorithms": [
    { "id": "sha3-256", "name": "sha3-256", "command": "sha3-256" }
  ],
  "passwordCheckers": [
    {
      "id": "strict",
      "name": "Strict",
      "minLength": 14,
      "requireUpper": true,
      "requireLower": true,
      "requireDigit": true,
      "requireSymbol": true
    }
  ],
  "passwordGenerators": [
    {
      "id": "long-random",
      "name": "Long random",
      "length": 24,
      "includeUpper": true,
      "includeLower": true,
      "includeDigits": true,
      "includeSymbols": true,
      "symbols": "!@#$%^&*()-_=+[]{}"
    }
  ]
}
```

A working sample plugin is included in [Plugins\SamplePack\plugin.json](/C:/Dev/Projects/OpenSSL_App_v2/Plugins/SamplePack/plugin.json).
