using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace OpenSSLGui
{
    public partial class MainWindow : Window
    {
        private readonly string openssl = "openssl.exe";
        private readonly OperationLogger logger;

        public MainWindow()
        {
            InitializeComponent();

            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "history.jsonl");
            logger = new OperationLogger(logPath);
            logger.Load();
            HistoryGrid.ItemsSource = logger.Items;

            UpdatePasswordStrength();
        }

        // ---------- Mode switch ----------
        private void ModeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PanelEncrypt == null || StatusText == null) return; // guard during initialization

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
            // Hide all mode panels and show the settings panel
            PanelEncrypt.Visibility = Visibility.Collapsed;
            PanelHash.Visibility = Visibility.Collapsed;
            PanelKeys.Visibility = Visibility.Collapsed;
            PanelHistory.Visibility = Visibility.Collapsed;
            PanelSettings.Visibility = Visibility.Visible;

            StatusText.Text = "Settings";
        } 


        // ---------- Helpers ----------
        private string GetSelectedComboText(ComboBox box)
        {
            if (box.SelectedItem is ComboBoxItem item && item.Content != null)
                return item.Content.ToString()!;
            return "";
        }

        private void AppendOutput(string text)
        {
            OutputBox.AppendText(text + Environment.NewLine);
            OutputBox.ScrollToEnd();
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
            {
                psi.ArgumentList.Add(argument);
            }
            using var p = new Process { StartInfo = psi };
            p.Start();

            string stdout = await p.StandardOutput.ReadToEndAsync();
            string stderr = await p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync();

            return (p.ExitCode, (stdout + stderr).Trim());
        }

        // ---------- Password strength ----------
        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            UpdatePasswordStrength();
        }

        private void UpdatePasswordStrength()
        {
            var (score, label) = PasswordStrength.Evaluate(PasswordBox.Password);
            PwdStrengthBar.Value = score;
            PwdStrengthText.Text = label;
        }

        private bool EnsurePasswordOk()
        {
            string pwd = PasswordBox.Password;
            if (string.IsNullOrWhiteSpace(pwd))
            {
                MessageBox.Show("Please provide a password.", "Password", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            var (score, label) = PasswordStrength.Evaluate(pwd);
            if (score <= 1 && AllowWeakPassword.IsChecked != true)
            {
                MessageBox.Show($"Password too weak: {label}\nEither strengthen the password or enable 'Allow weak password'.",
                    "Weak password", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        // ---------- Browse buttons ----------
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

        // ---------- Show commands ----------
        private void ShowEncCommand_Click(object sender, RoutedEventArgs e)
        {
            string file = EncFilePathBox.Text;
            string algo = GetSelectedComboText(AlgoBox);
            bool salt = UseSalt.IsChecked == true;
            string outFile = file + ".enc";
            string cmd = $"openssl {algo} {(salt ? "-salt " : "")}-in \"{file}\" -out \"{outFile}\" -k \"<password>\"";
            MessageBox.Show(cmd, "OpenSSL Command", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ShowHashCommand_Click(object sender, RoutedEventArgs e)
        {
            string file = HashFilePathBox.Text;
            string h = GetSelectedComboText(HashAlgoBox);
            string cmd = $"openssl dgst -{h} \"{file}\"";
            MessageBox.Show(cmd, "OpenSSL Command", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ShowKeyCommand_Click(object sender, RoutedEventArgs e)
        {
            string bits = GetSelectedComboText(RsaBitsBox);
            string outFile = KeyOutPathBox.Text;
            string cmd = $"openssl genrsa -out \"{outFile}\" {bits}";
            MessageBox.Show(cmd, "OpenSSL Command", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ---------- Encrypt / Decrypt ----------
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

                string algo = GetSelectedComboText(AlgoBox);
                bool salt = UseSalt.IsChecked == true;

                string outFile = file + ".enc";
                string pwd = PasswordBox.Password;
                StatusText.Text = "Encrypting...";
                AppendOutput($"[Encrypt] {algo} -> {outFile}");

                int code;
                string output;

                if (salt)
                {
                    (code, output) = await RunOpenSSLAsync(algo, "-salt", "-in", file, "-out", outFile, "-k", pwd);
                }
                else
                {
                    (code, output) = await RunOpenSSLAsync(algo, "-in", file, "-out", outFile, "-k", pwd);
                }

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

                string algo = GetSelectedComboText(AlgoBox);
                string outFile = file + ".dec";
                string pwd = PasswordBox.Password;
                StatusText.Text = "Decrypting...";
                AppendOutput($"[Decrypt] {algo} -> {outFile}");
                var (code, output) = await RunOpenSSLAsync(algo, "-d", "-in", file, "-out", outFile, "-k", pwd);
                string status = code == 0 ? "OK" : "ERROR";
                AppendOutput(output.Length == 0 ? $"ExitCode={code}" : output);
                StatusText.Text = status == "OK" ? "Ready" : "Error (see log below)";
                logger.Append(new LogEntry
                {
                    Operation = "Decrypt",
                    InputPath = file,
                    OutputPath = outFile,
                    Algorithm = algo,
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

        // ---------- Hash ----------
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
                string h = GetSelectedComboText(HashAlgoBox);
                StatusText.Text = "Hashing...";
                AppendOutput($"[Hash] {h} -> {file}");
                var sw = Stopwatch.StartNew();
                var (code, output) = await RunOpenSSLAsync("dgst", $"-{h}", file);
                sw.Stop();

                string timeInfo = $"Time: {sw.ElapsedMilliseconds} ms";
                string status = code == 0 ? "OK" : "ERROR";

                AppendOutput(output.Length == 0 ? $"ExitCode={code}" : output);
                AppendOutput($"[{status}] {timeInfo}");
                StatusText.Text = status == "OK" ? "Ready" : "Error (see log below)";

                logger.Append(new LogEntry
                {
                    Operation = "Hash",
                    InputPath = file,
                    OutputPath = "",
                    Algorithm = h,
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

        // Hash comapre
        private void HashModeChanged(object sender, RoutedEventArgs e)
        {
            if (RbHashComparison == null) return;
            bool compare = RbHashComparison.IsChecked == true;

            HashComparePanel.Visibility = compare ? Visibility.Visible : Visibility.Collapsed;
            BtnHashSingle.Visibility = compare ? Visibility.Collapsed : Visibility.Visible;
            BtnHashCompare.Visibility = compare ? Visibility.Visible : Visibility.Collapsed;

            CompareResultText.Text = "";
        }

        private void BrowseHashFileB_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog();
            if (dlg.ShowDialog() == true)
                HashFilePathBoxB.Text = dlg.FileName;
        }

        private async void CompareHash_Click(object sender, RoutedEventArgs e)
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
                string h = GetSelectedComboText(HashAlgoBox);
                StatusText.Text = "Comparing hashes...";
                AppendOutput($"[HashCompare] {h}");
                AppendOutput($"A: {fileA}");
                AppendOutput($"B: {fileB}");

                var sw = System.Diagnostics.Stopwatch.StartNew();
                var (codeA, outA) = await RunOpenSSLAsync("dgst", $"-{h}", fileA);
                var (codeB, outB) = await RunOpenSSLAsync("dgst", $"-{h}", fileB);
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

                string timeInfo = sw.Elapsed.TotalMilliseconds < 1000
                    ? $"Time: {sw.ElapsedMilliseconds} ms"
                    : $"Time: {sw.Elapsed.TotalSeconds:F2} s";

                AppendOutput($"Hash A: {hashA}");
                AppendOutput($"Hash B: {hashB}");
                AppendOutput($"Result: {(equal ? "MATCH" : "DIFFERENT")} | {timeInfo}");

                CompareResultText.Text = equal ? "Files match by hash" : "Files differ (hash mismatch)";
                StatusText.Text = "Ready";

                logger.Append(new LogEntry
                {
                    Operation = "HashCompare",
                    InputPath = fileA,
                    OutputPath = fileB,
                    Algorithm = h,
                    Status = equal ? "OK" : "OK",
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
            if (string.IsNullOrWhiteSpace(output)) return "";

            int idx = output.LastIndexOf("= ");
            if (idx >= 0 && idx + 2 < output.Length)
                return output.Substring(idx + 2).Trim();

            var parts = output.Trim().Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0) return parts[^1];

            return "";
        }

        // ---------- Keys ----------
        private async void GenRsa_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string bits = GetSelectedComboText(RsaBitsBox);
                string outFile = KeyOutPathBox.Text;

                if (string.IsNullOrWhiteSpace(outFile))
                {
                    MessageBox.Show("Specify a path to save the key.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                StatusText.Text = "Generating key...";
                AppendOutput($"[KeyGen] RSA {bits} -> {outFile}");
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
            }
            catch (Exception ex)
            {
                AppendOutput("EXCEPTION: " + ex.Message);
                StatusText.Text = "Error";
            }
        }

        // ---------- History actions ----------
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

        // ---------- Theme toggle ----------

        private bool _isDarkTheme = false;

        private void ToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            _isDarkTheme = !_isDarkTheme;

            var app = Application.Current;
            if (app == null) return;

            // Replace the first merged dictionary (our theme)
            var dicts = app.Resources.MergedDictionaries;
            dicts.Clear();

            dicts.Add(new ResourceDictionary
            {
                Source = new Uri(_isDarkTheme ? "/Dark.xaml" : "/Light.xaml", UriKind.Relative)
            });

            StatusText.Text = _isDarkTheme ? "Theme: Dark" : "Theme: Light";
            AppendOutput($"[Theme] {(_isDarkTheme ? "Dark" : "Light")}");
        }

        // ---------------- Settings -----------------

        private void ResetSettings_Click(object sender, RoutedEventArgs e)
        {
        }
    }
}
