using Microsoft.Win32; // OpenFileDialog, SaveFileDialog
using System.Diagnostics; // Stopwatch
using System.IO;    // File, FileStream
using System.Security.Cryptography; // SHA256
using System.Text;  // StringBuilder
using System.Windows; // Window, MessageBox
using System.Windows.Controls; // ComboBox, TextBox, ...

namespace OpenSSL_App_v3
{
    public partial class MainWindow : Window
    {
        private readonly OperationLogger logger;
        private readonly SecuritySettingsStore securitySettingsStore;
        private readonly PluginCatalog pluginCatalog;
        private SecuritySettings securitySettings = SecuritySettings.Default();

        string provider = "openssl.exe";
        const string expectedhash = "hash";

        private readonly string ProviderHash = "3412f2c4a3d0367bf6212c965df30658758575aebe8e74d12e2d9382e5a00170";

        public MainWindow()
        {
            InitializeComponent();

            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            logger = new OperationLogger(Path.Combine(baseDirectory, "history.jsonl"));
            logger.Load();
            HistoryGrid.ItemsSource = logger.Items;

            securitySettingsStore = new SecuritySettingsStore(Path.Combine(baseDirectory, "security-settings.json"));
            pluginCatalog = PluginCatalog.Load(baseDirectory);

            LoadSecuritySettings();
            PopulatePluginDrivenUi();
            ApplySecuritySettingsToUi();
            WireSecuritySettingsHandlers();

            foreach (string message in pluginCatalog.LoadMessages)
                AppendOutput(message);

            if (!EnsureOpenSslAvailable())
            {
                Application.Current.Shutdown();
                return;
            }

            ApplySelectedTheme();
            UpdatePasswordStrength();
            UpdateOpenSslSummary();

            // Set up combo boxes 

            List<string> EncAlgos = new List<string>
            {
                "aes-256-cbc",
                "kuznechik-cbc"
            };

            List<string> HashAlgos = new List<string>
            {
                "sha256",
                "md5"
            };

            AlgoBox.ItemsSource = EncAlgos;
            AlgoBox.SelectedIndex = 0;

            HashAlgoBox.ItemsSource = HashAlgos;
            HashAlgoBox.SelectedIndex = 0;
        }
        private ThemeOption CurrentTheme => GetSelectedOption(
            ThemeCombo,
            pluginCatalog.Themes,
            securitySettings.PreferredThemeId,
            BuiltInPluginData.LightThemeId);

        private EncryptionAlgorithmOption CurrentEncryptionAlgorithm => GetSelectedOption(
            AlgoBox,
            pluginCatalog.EncryptionAlgorithms,
            securitySettings.DefaultEncryptionAlgorithmId,
            BuiltInPluginData.DefaultEncryptionAlgorithmId);
        
        private void ModeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PanelEncrypt == null || StatusText == null) return;

            int i = ModeList.SelectedIndex;
            PanelEncrypt.Visibility = i == 0 ? Visibility.Visible : Visibility.Collapsed;
            PanelHash.Visibility = i == 1 ? Visibility.Visible : Visibility.Collapsed;
            PanelKeys.Visibility = i == 2 ? Visibility.Visible : Visibility.Collapsed;
            PanelHistory.Visibility = i == 3 ? Visibility.Visible : Visibility.Collapsed;
            PanelSettings.Visibility = Visibility.Collapsed;

            StatusText.Text = i switch
            {
                0 => "Mode: Encryption",
                1 => "Mode: Hashing",
                2 => "Mode: Keys",
                3 => "Mode: History",
                _ => "Ready"
            };
        }

        private void Settings_Tab(object sender, RoutedEventArgs e)
        {
            PanelEncrypt.Visibility = Visibility.Collapsed;
            PanelHash.Visibility = Visibility.Collapsed;
            PanelKeys.Visibility = Visibility.Collapsed;
            PanelHistory.Visibility = Visibility.Collapsed;
            PanelSettings.Visibility = Visibility.Visible;
            StatusText.Text = "Settings";
        }

        private void AppendOutput(string text)
        {
            OutputBox.AppendText(text + Environment.NewLine);
            OutputBox.ScrollToEnd();
        }

        private static T GetSelectedOption<T>(ComboBox comboBox, System.Collections.Generic.IReadOnlyList<T> items, string preferredId, string fallbackId)
            where T : class
        {
            if (comboBox.SelectedItem is T selected)
                return selected;

            foreach (T item in items)
            {
                string id = (string)(item!.GetType().GetProperty("Id")!.GetValue(item)!);
                if (string.Equals(id, preferredId, StringComparison.OrdinalIgnoreCase))
                    return item;
            }

            foreach (T item in items)
            {
                string id = (string)(item!.GetType().GetProperty("Id")!.GetValue(item)!);
                if (string.Equals(id, fallbackId, StringComparison.OrdinalIgnoreCase))
                    return item;
            }

            return items.First();
        }


        private async Task<(int exitCode, string output)> RunOpenSSLAsync(params string[] args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "openssl.exe",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (string argument in args)
                psi.ArgumentList.Add(argument); // Uses ArgumentList.Add for security

            using var p = new Process { StartInfo = psi };
            p.Start();

            string stdout = await p.StandardOutput.ReadToEndAsync();
            string stderr = await p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync();

            return (p.ExitCode, (stdout + stderr).Trim());
        }


        //                                                                               //
        //                                                                               //
        //                                                                               //
        // ================================== HELPERS ================================== //
        //                                                                               //
        //                                                                               //
        //                                                                               //


        private static string ComputeSha256OfFile(string filePath)
        {
            using FileStream stream = File.OpenRead(filePath);
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(stream);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private bool EnsureOpenSslAvailable() // Remove hardcoded hash
        {
            string provider = "openssl.exe";
            if (!File.Exists(provider))
            {
                MessageBox.Show($"Required OpenSSL executable not found:\n{provider}", "OpenSSL not found", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(ProviderHash))
            {
                string actualSha256 = ComputeSha256OfFile(provider);
                if (!string.Equals(actualSha256, ProviderHash, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show($"OpenSSL provider hash is incorrect.\n\nExpected:\n{ProviderHash}\n\nActual:\n{actualSha256}", "Integrity check failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }

            return true;
        }

        private void UpdateOpenSslSummary()
        {
            string provider = "openssl.exe";
            if (OpenSslPathBox == null) return;
            OpenSslPathBox.Text = provider;
            OpenSslProviderInfo.Text = string.IsNullOrWhiteSpace(expectedhash) ? "Hash pin: not configured by plugin" : "Hash pin: configured";
        }


        private void PopulatePluginDrivenUi()
        {
            BindCombo(ThemeCombo, pluginCatalog.Themes, securitySettings.PreferredThemeId);
        }

        private static void BindCombo<T>(ComboBox comboBox, System.Collections.Generic.IReadOnlyList<T> items, string preferredId)
            where T : class
        {
            comboBox.ItemsSource = items;
            comboBox.DisplayMemberPath = "DisplayName";

            object? selected = items.FirstOrDefault(x =>
                string.Equals((string?)x!.GetType().GetProperty("Id")?.GetValue(x), preferredId, StringComparison.OrdinalIgnoreCase));

            comboBox.SelectedItem = selected ?? items.FirstOrDefault();
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            UpdatePasswordStrength();
        }

        private void UpdatePasswordStrength()
        {
            int score;
            string label;
            if (PwdStrengthBar == null || PwdStrengthText == null) return;
            (score, label) = Password_helpers.Evaluate(PasswordBox.Password);
            PwdStrengthBar.Value = score;
            PwdStrengthText.Text = label;
        }

        private bool EnsurePasswordOk()
        {
            int score;
            string label;

            string pwd = PasswordBox.Password;
            if (string.IsNullOrWhiteSpace(pwd))
            {
                MessageBox.Show("Please provide a password.", "Password", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            (score, label) = Password_helpers.Evaluate(pwd);
            if (score <= 1 && AllowWeakPassword.IsChecked != true)
            {
                MessageBox.Show($"Password too weak according to '{"General password checker"}'", "Weak password", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (score <= 1 && AllowWeakPassword.IsChecked == true && !ConfirmDangerousOperation($"Proceed with weak password ({label})?"))
                return false;

            return true;
        }

        private bool ConfirmDangerousOperation(string message)
        {
            if (!securitySettings.ConfirmDangerousOperations)
                return true;

            return MessageBox.Show(message, "Confirm risky action", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
        }

        private void TryCopyToClipboard(string text, string label)
        {
            if (securitySettings.PreventClipboardCopy || string.IsNullOrWhiteSpace(text))
                return;

            try
            {
                Clipboard.SetText(text);
                AppendOutput($"[{label}] copied to clipboard");
            }
            catch (Exception ex)
            {
                AppendOutput($"[Clipboard] copy failed: {ex.Message}");
            }
        }

        private void GeneratePassword_Click(object sender, RoutedEventArgs e)
        {
            string generated = Password_helpers.Generate(12);
            PasswordBox.Password = generated;
            TryCopyToClipboard(generated, "Password");
            AppendOutput($"[Password] Generated by {"General"}");
        }

        private void BrowseEncFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog();
            if (dlg.ShowDialog() == true)
                EncFilePathBox.Text = dlg.FileName;
        }

        private void BrowseHashFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog();
            if (dlg.ShowDialog() == true)
                HashFilePathBox.Text = dlg.FileName;
        }

        private void BrowseHashFileB_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog();
            if (dlg.ShowDialog() == true)
                HashFilePathBoxB.Text = dlg.FileName;
        }

        private void BrowseKeyOut_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                FileName = "rsa_key.pem",
                Filter = "PEM files (*.pem)|*.pem|All files (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true)
                KeyOutPathBox.Text = dlg.FileName;
        }

        private void ShowEncCommand_Click(object sender, RoutedEventArgs e)
        {
            string file = EncFilePathBox.Text;
            //EncryptionAlgorithmOption algo = CurrentEncryptionAlgorithm;
            //bool salt = UseSalt.IsChecked == true && algo.SupportsSalt;
            //string outFile = file + ".enc";
            //string cmd = $"\"{provider}\" {algo.CommandName} {(salt ? "-salt " : "")}-in \"{file}\" -out \"{outFile}\" -k \"<password>\"";
            //MessageBox.Show(cmd, "OpenSSL Command", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ShowHashCommand_Click(object sender, RoutedEventArgs e)
        {
            //string file = HashFilePathBox.Text;
            //HashAlgorithmOption hash = CurrentHashAlgorithm;
            //string cmd = $"\"{provider}\" dgst -{hash.CommandName} \"{file}\"";
            //MessageBox.Show(cmd, "OpenSSL Command", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ShowKeyCommand_Click(object sender, RoutedEventArgs e)
        {
            string bits = GetSelectedRsaBits();
            string outFile = KeyOutPathBox.Text;
            string cmd = $"\"{provider}\" genrsa -out \"{outFile}\" {bits}";
            MessageBox.Show(cmd, "OpenSSL Command", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        //                                                                                 //
        //                                                                                 //
        //                                                                                 //
        // ================================== MAIN PART ================================== //
        //                                                                                 //
        //                                                                                 //
        //                                                                                 //

        private async void Encrypt_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string file = EncFilePathBox.Text;
                if (!File.Exists(file))
                {
                    MessageBox.Show("File not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!EnsurePasswordOk()) return;

                string algo = AlgoBox.SelectedItem.ToString();

                //EncryptionAlgorithmOption algo = CurrentEncryptionAlgorithm;
                //bool salt = UseSalt.IsChecked == true && algo.SupportsSalt;
                bool salt = true;

                //if (algo.IsDangerous && !ConfirmDangerousOperation(algo.WarningMessage))
                //    return;

                string outFile = file + ".enc";
                string pwd = PasswordBox.Password;
                StatusText.Text = "Encrypting...";
                AppendOutput($"[Encrypt] {algo} via {provider} -> {outFile}");

                int code;
                string output;
                if (algo == "kuznechik-cbc")
                    (code, output) = await RunOpenSSLAsync("enc", "-engine", "gost", "-grasshopper-cbc", "-e", "-salt", "-in", file, "-out", outFile, "-k", pwd);
                else if (salt)
                    (code, output) = await RunOpenSSLAsync("enc", $"-{algo}", "-salt", "-in", file, "-out", outFile, "-k", pwd);
                else
                    (code, output) = await RunOpenSSLAsync("enc", $"-{algo}", "-in", file, "-out", outFile, "-k", pwd);

                string status = code == 0 ? "OK" : "ERROR";
                AppendOutput(output.Length == 0 ? $"ExitCode={code}" : output);
                StatusText.Text = status == "OK" ? "Ready" : "Error (see log below)";

                logger.Append(new LogEntry
                {
                    Operation = "Encrypt",
                    InputPath = file,
                    OutputPath = outFile,
                    Algorithm = algo,
                    Status = status,
                    Message = output.Length > 300 ? output.Substring(0, 300) : output
                });

                if (securitySettings.ClearPasswordAfterOperation)
                    PasswordBox.Clear();
            }
            catch (Exception ex)
            {
                AppendOutput("EXCEPTION: " + ex.Message);
                StatusText.Text = "Error";
            }
        }

        private async void Decrypt_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string file = EncFilePathBox.Text;
                if (!File.Exists(file))
                {
                    MessageBox.Show("File not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!EnsurePasswordOk()) return;

                EncryptionAlgorithmOption algo = CurrentEncryptionAlgorithm;
                string outFile = file + ".dec";
                string pwd = PasswordBox.Password;

                if (algo.IsDangerous && !ConfirmDangerousOperation(algo.WarningMessage))
                    return;

                StatusText.Text = "Decrypting...";
                AppendOutput($"[Decrypt] {algo.CommandName} via {provider} -> {outFile}");
                var (code, output) = await RunOpenSSLAsync(algo.CommandName, "-d", "-in", file, "-out", outFile, "-k", pwd);
                string status = code == 0 ? "OK" : "ERROR";
                AppendOutput(output.Length == 0 ? $"ExitCode={code}" : output);
                StatusText.Text = status == "OK" ? "Ready" : "Error (see log below)";

                logger.Append(new LogEntry
                {
                    Operation = "Decrypt",
                    InputPath = file,
                    OutputPath = outFile,
                    Algorithm = algo.CommandName,
                    Status = status,
                    Message = output.Length > 300 ? output.Substring(0, 300) : output
                });

                if (securitySettings.ClearPasswordAfterOperation)
                    PasswordBox.Clear();
            }
            catch (Exception ex)
            {
                AppendOutput("EXCEPTION: " + ex.Message);
                StatusText.Text = "Error";
            }
        }

        private async void Hash_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string file = HashFilePathBox.Text;
                if (!File.Exists(file))
                {
                    MessageBox.Show("File not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (HashAlgoBox.SelectedItem == null)
                {
                    MessageBox.Show("Select a hash algorithm.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string hash_name = HashAlgoBox.SelectedItem.ToString();

                //HashAlgorithmOption hash = CurrentHashAlgorithm;
                //if (hash.IsDangerous && !ConfirmDangerousOperation(hash.WarningMessage))
                //    return;

                StatusText.Text = "Hashing...";
                AppendOutput($"[Hash] {hash_name} -> {file}");
                var sw = Stopwatch.StartNew();
                var (code, output) = await RunOpenSSLAsync("dgst", $"-{hash_name}", file);
                sw.Stop();

                string timeInfo = $"Time: {sw.ElapsedMilliseconds} ms";
                string status = code == 0 ? "OK" : "ERROR";

                AppendOutput(output.Length == 0 ? $"ExitCode={code}" : output);
                AppendOutput($"[{status}] {timeInfo}");
                TryCopyToClipboard(ExtractDigestFromOpenSslOutput(output), "Hash");
                StatusText.Text = status == "OK" ? "Ready" : "Error (see log below)";

                logger.Append(new LogEntry
                {
                    Operation = "Hash",
                    InputPath = file,
                    OutputPath = "",
                    Algorithm = hash_name,
                    Status = status,
                    Message = output.Length > 300 ? output.Substring(0, 300) : output
                });
            }
            catch (Exception ex)
            {
                AppendOutput("EXCEPTION: " + ex.Message);
                StatusText.Text = "Error";
            }
        }

        private void HashModeChanged(object sender, RoutedEventArgs e)
        {
            if (RbHashComparison == null) return;
            bool compare = RbHashComparison.IsChecked == true;
            HashComparePanel.Visibility = compare ? Visibility.Visible : Visibility.Collapsed;
            BtnHashSingle.Visibility = compare ? Visibility.Collapsed : Visibility.Visible;
            BtnHashCompare.Visibility = compare ? Visibility.Visible : Visibility.Collapsed;
            CompareResultText.Text = "";
        }

        private async void CompareHash_Click(object sender, RoutedEventArgs e)  // Remove stopwatch, it's not needed
        {
            try
            {
                string fileA = HashFilePathBox.Text;
                string fileB = HashFilePathBoxB.Text;

                if (!File.Exists(fileA) || !File.Exists(fileB))
                {
                    MessageBox.Show("Please check paths to files A and B.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (HashAlgoBox.SelectedItem == null)
                {
                    MessageBox.Show("Select a hash algorithm.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string hash_name = HashAlgoBox.SelectedItem.ToString();

                //if (hash.IsDangerous && !ConfirmDangerousOperation(hash.WarningMessage))
                //    return;

                StatusText.Text = "Comparing hashes...";
                AppendOutput($"[HashCompare] {hash_name}");
                AppendOutput($"A: {fileA}");
                AppendOutput($"B: {fileB}");

                var sw = Stopwatch.StartNew();
                var (codeA, outA) = await RunOpenSSLAsync("dgst", $"-{hash_name}", fileA);
                var (codeB, outB) = await RunOpenSSLAsync("dgst", $"-{hash_name}", fileB);
                sw.Stop();

                if (codeA != 0 || codeB != 0)
                {
                    AppendOutput(outA);
                    AppendOutput(outB);
                    CompareResultText.Text = "Error computing hash (see log).";
                    StatusText.Text = "Error";
                    return;
                }

                string hashA = ExtractDigestFromOpenSslOutput(outA);
                string hashB = ExtractDigestFromOpenSslOutput(outB);
                bool equal = !string.IsNullOrEmpty(hashA) && hashA.Equals(hashB, StringComparison.OrdinalIgnoreCase);

                string timeInfo = sw.Elapsed.TotalMilliseconds < 1000 ? $"Time: {sw.ElapsedMilliseconds} ms" : $"Time: {sw.Elapsed.TotalSeconds:F2} s";
                AppendOutput($"Hash A: {hashA}");
                AppendOutput($"Hash B: {hashB}");
                AppendOutput($"Result: {(equal ? "MATCH" : "DIFFERENT")} | {timeInfo}");
                TryCopyToClipboard(equal ? hashA : $"A: {hashA}{Environment.NewLine}B: {hashB}", "Hash compare");

                CompareResultText.Text = equal ? "Files match by hash" : "Files differ (hash mismatch)";
                StatusText.Text = "Ready";

                logger.Append(new LogEntry
                {
                    Operation = "HashCompare",
                    InputPath = fileA,
                    OutputPath = fileB,
                    Algorithm = hash_name,
                    Status = "OK",
                    Message = $"{(equal ? "MATCH" : "DIFFERENT")} | {timeInfo}"
                });
            }
            catch (Exception ex)
            {
                AppendOutput("EXCEPTION: " + ex.Message);
                StatusText.Text = "Error";
            }
        }

        private static string ExtractDigestFromOpenSslOutput(string output)
        {
            int idx = output.LastIndexOf("= ");
            if (idx >= 0 && idx + 2 < output.Length)
                return output.Substring(idx + 2).Trim();

            var parts = output.Trim().Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0) return parts[^1];
            return "";
        }

        private async void GenRsa_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string bits = GetSelectedRsaBits();
                string outFile = KeyOutPathBox.Text;

                if (string.IsNullOrWhiteSpace(outFile))
                {
                    MessageBox.Show("Specify a path to save the key.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                StatusText.Text = "Generating key...";
                AppendOutput($"[KeyGen] RSA {bits} via {provider} -> {outFile}");
                var (code, output) = await RunOpenSSLAsync("genrsa", "-out", outFile, bits);
                string status = code == 0 ? "OK" : "ERROR";

                AppendOutput(output.Length == 0 ? $"ExitCode={code}" : output);
                StatusText.Text = status == "OK" ? "Ready" : "Error (see log below)";

                logger.Append(new LogEntry
                {
                    Operation = "KeyGen",
                    InputPath = "",
                    OutputPath = outFile,
                    Algorithm = $"RSA-{bits}",
                    Status = status,
                    Message = output.Length > 300 ? output.Substring(0, 300) : output
                });

                TryCopyToClipboard(outFile, "Key path");
            }
            catch (Exception ex)
            {
                AppendOutput("EXCEPTION: " + ex.Message);
                StatusText.Text = "Error";
            }
        }

        private string GetSelectedRsaBits()
        {
            if (RsaBitsBox.SelectedItem is ComboBoxItem item && item.Content != null)
                return item.Content.ToString() ?? "2048";

            return "2048";
        }

        private void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to clear history?", "History", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                logger.Clear();
                AppendOutput("[History] cleared");
            }
        }

        private void ExportHistoryCsv_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                FileName = "history.csv",
                Filter = "CSV (*.csv)|*.csv|All files (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                logger.ExportCsv(dlg.FileName);
                AppendOutput("[History] exported: " + dlg.FileName);
            }
        }

        //                                                                               //
        //                                                                               //
        //                                                                               //
        // ================================== SETTINGS ================================= //
        //                                                                               //
        //                                                                               //
        //                                                                               //

        private void ResetSettings_Click(object sender, RoutedEventArgs e)
        {
            securitySettings = SecuritySettings.Default();
            PopulatePluginDrivenUi();
            ApplySecuritySettingsToUi();
            PersistSecuritySettings();
            ApplySelectedTheme();
            UpdatePasswordStrength();
            UpdateOpenSslSummary();
            StatusText.Text = "Settings reset";
            AppendOutput("[Settings] Reset to defaults.");
        }

        private void WireSecuritySettingsHandlers()
        {
            ClearPasswordAfterOpCheck.Checked += SecuritySettingChanged;
            ClearPasswordAfterOpCheck.Unchecked += SecuritySettingChanged;
            PreventClipboardCheck.Checked += SecuritySettingChanged;
            PreventClipboardCheck.Unchecked += SecuritySettingChanged;
            ConfirmDangerousOpsCheck.Checked += SecuritySettingChanged;
            ConfirmDangerousOpsCheck.Unchecked += SecuritySettingChanged;

            ThemeCombo.SelectionChanged += PluginSelectionChanged;
            OpenSslProviderCombo.SelectionChanged += PluginSelectionChanged;
            PasswordCheckerCombo.SelectionChanged += PluginSelectionChanged;
            PasswordGeneratorCombo.SelectionChanged += PluginSelectionChanged;
            DefaultEncAlgoBox.SelectionChanged += PluginSelectionChanged;
            DefaultHashAlgoBox.SelectionChanged += PluginSelectionChanged;

            AlgoBox.SelectionChanged += RuntimeAlgorithmSelectionChanged;
            HashAlgoBox.SelectionChanged += RuntimeAlgorithmSelectionChanged;
        }

        private void RuntimeAlgorithmSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //UseSalt.IsEnabled = CurrentEncryptionAlgorithm.SupportsSalt;
            //if (!CurrentEncryptionAlgorithm.SupportsSalt)
            //    UseSalt.IsChecked = false;
        }

        private void PluginSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender == DefaultEncAlgoBox && DefaultEncAlgoBox.SelectedItem != null)
                AlgoBox.SelectedItem = DefaultEncAlgoBox.SelectedItem;

            if (sender == DefaultHashAlgoBox && DefaultHashAlgoBox.SelectedItem != null)
                HashAlgoBox.SelectedItem = DefaultHashAlgoBox.SelectedItem;

            SaveSettingsFromUi();
            PersistSecuritySettings();
            ApplySelectedTheme();
            UpdatePasswordStrength();
            UpdateOpenSslSummary();
        }

        private void SecuritySettingChanged(object sender, RoutedEventArgs e)
        {
            SaveSettingsFromUi();
            PersistSecuritySettings();
        }

        private void LoadSecuritySettings()
        {
            securitySettings = securitySettingsStore.Load();
        }

        private void ApplySecuritySettingsToUi()
        {
            if (ClearPasswordAfterOpCheck == null)
                return;

            ClearPasswordAfterOpCheck.IsChecked = securitySettings.ClearPasswordAfterOperation;
            PreventClipboardCheck.IsChecked = securitySettings.PreventClipboardCopy;
            ConfirmDangerousOpsCheck.IsChecked = securitySettings.ConfirmDangerousOperations;
            UseSalt.IsChecked = true;
            //UseSalt.IsEnabled = CurrentEncryptionAlgorithm.SupportsSalt;
        }

        private void SaveSettingsFromUi()
        {
            securitySettings.ClearPasswordAfterOperation = ClearPasswordAfterOpCheck.IsChecked == true;
            securitySettings.PreventClipboardCopy = PreventClipboardCheck.IsChecked == true;
            securitySettings.ConfirmDangerousOperations = ConfirmDangerousOpsCheck.IsChecked == true;
            securitySettings.PreferredThemeId = CurrentTheme.Id;
        }

        private void ApplySelectedTheme()
        {
            ThemeManager.ApplyTheme(CurrentTheme);
            StatusText.Text = $"Theme: {CurrentTheme.DisplayName}";
            AppendOutput($"[Theme] {CurrentTheme.DisplayName}");
        }

        private void PersistSecuritySettings()
        {
            securitySettingsStore.Save(securitySettings);
        }
    }
}
