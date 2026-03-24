using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Json;

namespace OpenSSL_App_v3
{
    public sealed class OperationLogger
    {
        private readonly string _logPath;
        public ObservableCollection<LogEntry> Items { get; } = new ObservableCollection<LogEntry>();

        public OperationLogger(string logPath)
        {
            _logPath = logPath;
        }

        public void Load()
        {
            Items.Clear();
            if (!File.Exists(_logPath)) return;

            foreach (var line in File.ReadAllLines(_logPath, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var entry = JsonSerializer.Deserialize<LogEntry>(line);
                    if (entry != null) Items.Add(entry);
                }
                catch
                {
                }
            }
        }

        public void Append(LogEntry entry)
        {
            Items.Add(entry);
            var json = JsonSerializer.Serialize(entry);
            File.AppendAllText(_logPath, json + Environment.NewLine, Encoding.UTF8);
        }

        public void Clear()
        {
            Items.Clear();
            try { File.Delete(_logPath); } catch { }
        }

        public void ExportCsv(string csvPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Timestamp,Operation,InputPath,OutputPath,Algorithm,Status,Message");

            foreach (var it in Items)
            {
                sb.AppendLine(string.Join(",",
                    Esc(it.Timestamp),
                    Esc(it.Operation),
                    Esc(it.InputPath),
                    Esc(it.OutputPath),
                    Esc(it.Algorithm),
                    Esc(it.Status),
                    Esc(it.Message)
                ));
            }

            File.WriteAllText(csvPath, sb.ToString(), Encoding.UTF8);

            static string Esc(string s)
            {
                s ??= "";
                s = s.Replace("\"", "\"\"");
                return $"\"{s}\"";
            }
        }
    }
}
