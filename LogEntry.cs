namespace OpenSSL_App_v3
{
    public sealed class LogEntry
    {
        public string Timestamp { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        public string Operation { get; set; } = "";
        public string InputPath { get; set; } = "";
        public string OutputPath { get; set; } = "";
        public string Algorithm { get; set; } = "";
        public string Status { get; set; } = "";
        public string Message { get; set; } = "";
    }
}
