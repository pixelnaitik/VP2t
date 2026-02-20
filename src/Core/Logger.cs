using System;
using System.IO;

namespace VPT.Core
{
    public static class Logger
    {
        private static readonly object Sync = new();
        private static readonly string LogDir = Path.Combine(AppContext.BaseDirectory, "logs");
        private static readonly string LogFile;

        static Logger()
        {
            try
            {
                Directory.CreateDirectory(LogDir);
                LogFile = Path.Combine(LogDir, $"app_{DateTime.Now:yyyy-MM-dd}.log");
            }
            catch { LogFile = "app.log"; }
        }

        public static void Log(string message)
        {
            try
            {
                string entry = $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
                lock (Sync)
                {
                    File.AppendAllText(LogFile, entry);
                }
            }
            catch
            {
                // Avoid throwing from logger.
            }
        }

        public static void Error(string message, Exception? ex = null)
        {
            string entry = $"[ERROR] {message}";
            if (ex != null) entry += $"\n{ex}";
            Log(entry);
        }
    }
}
