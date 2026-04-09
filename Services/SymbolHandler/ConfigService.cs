using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using MCGInventorPlugin.Models.SymbolHandler;

namespace MCGInventorPlugin.Services.SymbolHandler
{
    /// <summary>
    /// Đọc/ghi config.json tại %AppData%\SymbolHandler\config.json.
    /// Dùng JSON thủ công (không cần NuGet) vì chỉ có 1 field đơn giản.
    /// </summary>
    public class ConfigService : IConfigService
    {
        // ─── Hằng số log ──────────────────────────────────────────────────────
        private const string LOG_PREFIX = "[ConfigService]";

        // ─── Fields ───────────────────────────────────────────────────────────
        private readonly string _configPath;

        // ─── Constructor ──────────────────────────────────────────────────────
        public ConfigService()
        {
            // Tạo thư mục %AppData%\SymbolHandler\ nếu chưa có
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dir     = Path.Combine(appData, "SymbolHandler");
            Directory.CreateDirectory(dir);
            _configPath = Path.Combine(dir, "config.json");

            Debug.WriteLine($"{LOG_PREFIX} Config path: {_configPath}");
        }

        // ─── IConfigService ───────────────────────────────────────────────────

        public LibraryConfigModel Load()
        {
            Debug.WriteLine($"{LOG_PREFIX} Load config...");

            try
            {
                if (!File.Exists(_configPath))
                {
                    // Lần đầu chạy — trả về default config
                    Debug.WriteLine($"{LOG_PREFIX} Chưa có config file, dùng default.");
                    return new LibraryConfigModel();
                }

                string json = File.ReadAllText(_configPath);

                // Parse JSON thủ công: {"LibraryPath":"..."}
                // Hỗ trợ escaped backslash (\\ → \)
                var match = Regex.Match(json,
                    @"""LibraryPath""\s*:\s*""((?:[^""\\]|\\.)*)""");

                if (match.Success)
                {
                    // Unescape: \\ → \, \" → "
                    string path = match.Groups[1].Value
                        .Replace("\\\\", "\x00BACKSLASH\x00")
                        .Replace("\\\"", "\"")
                        .Replace("\x00BACKSLASH\x00", "\\");

                    Debug.WriteLine($"{LOG_PREFIX} Load OK: {path}");
                    return new LibraryConfigModel { LibraryPath = path };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI load config: {ex.Message}");
            }

            return new LibraryConfigModel();
        }

        public void Save(LibraryConfigModel config)
        {
            Debug.WriteLine($"{LOG_PREFIX} Save config: {config?.LibraryPath}");

            try
            {
                if (config == null) return;

                // Escape backslash và quote trong path
                string escaped = (config.LibraryPath ?? string.Empty)
                    .Replace("\\", "\\\\")
                    .Replace("\"", "\\\"");

                string json = $"{{\"LibraryPath\":\"{escaped}\"}}";
                File.WriteAllText(_configPath, json);

                Debug.WriteLine($"{LOG_PREFIX} Save config THÀNH CÔNG.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI save config: {ex.Message}");
            }
        }
    }
}
