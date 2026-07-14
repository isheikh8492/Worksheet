using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Worksheet.Core.Services
{
    // Minimal file logger to capture exceptions without introducing external dependencies.
    public static class AppLog
    {
        private static readonly object _lock = new();
        private static string? _logDir;
        private static string _sessionTimestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

        public static void Initialize(string? logDirectory = null)
        {
            try
            {
                _sessionTimestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                _logDir = ResolveLogDirectory(logDirectory);
                Directory.CreateDirectory(_logDir);
                Info("AppLog initialized", $"dir={_logDir}");
            }
            catch
            {
                // Logging must never crash the app.
            }
        }

        public static void Info(string message, string? details = null) =>
            Write("INFO", message, details);

        public static void Error(string message, string? details = null) =>
            Write("ERROR", message, details);

        public static void Exception(Exception ex, string context)
        {
            try
            {
                var details = ex.ToString();
                Write("EX", context, details);
            }
            catch
            {
                // never throw from logger
            }
        }

        private static void Write(string level, string message, string? details)
        {
            try
            {
                var dir = _logDir;
                if (string.IsNullOrWhiteSpace(dir))
                {
                    // Lazy init for safety in case Initialize() wasn't called.
                    Initialize();
                    dir = _logDir;
                }

                if (string.IsNullOrWhiteSpace(dir))
                    return;

                string file = Path.Combine(dir, $"{_sessionTimestamp}.log");
                string line = $"{DateTime.UtcNow:O} [{level}] [T{Thread.CurrentThread.ManagedThreadId}] {message}";
                if (!string.IsNullOrWhiteSpace(details))
                    line += Environment.NewLine + details;

                lock (_lock)
                {
                    File.AppendAllText(file, line + Environment.NewLine + Environment.NewLine);
                }

                Debug.WriteLine(line);
            }
            catch
            {
                // swallow
            }
        }

        private static string ResolveLogDirectory(string? requested)
        {
            // 1) Explicit call-site override
            if (!string.IsNullOrWhiteSpace(requested))
                return requested!;

            // 2) Environment override (useful for deployment/CI)
            var env = Environment.GetEnvironmentVariable("WORKSHEET_LOG_DIR");
            if (!string.IsNullOrWhiteSpace(env))
                return env!;

            // 3) Prefer repo-local logs/ (what you asked for) if we can find the project root and it is writable
            if (TryFindRepoRoot(out var repoRoot))
            {
                var candidate = Path.Combine(repoRoot, "logs");
                if (TryEnsureWritable(candidate))
                    return candidate;
            }

            // 4) Fallback: app directory logs/ (often writable for debug runs)
            try
            {
                var appDir = AppContext.BaseDirectory;
                var candidate = Path.Combine(appDir, "logs");
                if (TryEnsureWritable(candidate))
                    return candidate;
            }
            catch
            {
                // ignore
            }

            // 5) Safe fallback: per-user directory
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(baseDir, "Worksheet", "logs");
        }

        private static bool TryFindRepoRoot(out string repoRoot)
        {
            static bool IsRepoRoot(string dir) =>
                File.Exists(Path.Combine(dir, "Worksheet.sln")) ||
                File.Exists(Path.Combine(dir, "Worksheet.App", "Worksheet.App.csproj")) ||
                Directory.Exists(Path.Combine(dir, ".git"));

            // Search upward from current directory first (when running from VS, this is often the repo root)
            foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
            {
                try
                {
                    var di = new DirectoryInfo(start);
                    for (int i = 0; i < 10 && di != null; i++)
                    {
                        if (IsRepoRoot(di.FullName))
                        {
                            repoRoot = di.FullName;
                            return true;
                        }
                        di = di.Parent;
                    }
                }
                catch
                {
                    // ignore and try next start directory
                }
            }

            repoRoot = string.Empty;
            return false;
        }

        private static bool TryEnsureWritable(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                // Probe write permission
                var probe = Path.Combine(dir, ".write_test");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
