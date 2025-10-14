using System;
using System.IO;
namespace SurgicalVisualization.Services
{
    public class LoggerService
    {
        private readonly string _logPath;
        private readonly object _sync = new object();
        public LoggerService()
        {
            var dir = SurgicalVisualization.Helpers.FileDialogHelper.EnsureLogsFolder();
            _logPath = Path.Combine(dir, "system_log.txt");
        }
        public void Info(string message) => Write("INFO", message);
        public void Warn(string message) => Write("WARN", message);
        public void Error(string message) => Write("ERROR", message);
        private void Write(string level, string message)
        {
            var line = $"{DateTime.UtcNow:O}\t{level}\t{message}";
            lock (_sync) { File.AppendAllText(_logPath, line + Environment.NewLine); }
        }
    }
}