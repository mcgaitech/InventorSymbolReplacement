using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Inventor;
using MCGInventorPlugin.Infrastructure;
using MCGInventorPlugin.Modules;

namespace MCGInventorPlugin
{
    /// <summary>
    /// Entry point duy nhất của MCGInventorPlugin.
    /// Inventor tìm class này qua COM GUID trong file .addin.
    /// Tất cả modules được đăng ký tại đây — ModuleManager quản lý lifecycle.
    /// </summary>
    [Guid("7C3D8E4F-2A1B-4C5D-9E8F-1A2B3C4D5E6F")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    public class MCGInventorPluginAddin : ApplicationAddInServer
    {
        private const string LOG_PREFIX  = "[MCGInventorPlugin]";
        internal const string ADDIN_GUID = "{7C3D8E4F-2A1B-4C5D-9E8F-1A2B3C4D5E6F}";

        // MCG_FIX: file log để debug khi Inventor ẩn console — bất kỳ exception nào
        // thoát khỏi Activate/Deactivate có thể làm Inventor lỡ load Vault/ContentCenter,
        // vì vậy bắt buộc nuốt mọi exception và ghi ra file.
        private const string LOG_DIR  = @"C:\CustomTools\Inventor\logs";
        private const string LOG_FILE = @"C:\CustomTools\Inventor\logs\MCG_InventorSymbolHandler.log";

        private Inventor.Application _app;
        private ModuleManager _moduleManager;

        public void Activate(ApplicationAddInSite addInSiteObject, bool firstTime)
        {
            Debug.WriteLine($"{LOG_PREFIX} ===== Bắt đầu kích hoạt addin =====");
            Debug.WriteLine($"{LOG_PREFIX} firstTime = {firstTime}");
            // MCG_FIX: log INFO để biết Activate có được gọi không (kể cả khi không có exception)
            LogInfo($"===== Activate START (firstTime={firstTime}) =====");

            try
            {
                _app = addInSiteObject.Application;
                Debug.WriteLine($"{LOG_PREFIX} Inventor version: {_app.SoftwareVersion.DisplayVersion}");
                LogInfo($"Inventor version: {_app.SoftwareVersion.DisplayVersion}");

                _moduleManager = new ModuleManager();
                _moduleManager.Register(new SymbolHandlerModule());
                // Tương lai: đăng ký modules khác
                // _moduleManager.Register(new PartToolsModule());
                // _moduleManager.Register(new AssemblyToolsModule());
                // _moduleManager.Register(new UtilityToolsModule());
                LogInfo("Modules registered: SymbolHandler");

                _moduleManager.ActivateAll(_app, ADDIN_GUID, firstTime);
                LogInfo("ActivateAll completed");

                Debug.WriteLine($"{LOG_PREFIX} ===== Addin kích hoạt THÀNH CÔNG =====");
                LogInfo("===== Activate SUCCESS =====");
            }
            catch (Exception ex)
            {
                // MCG_FIX: KHÔNG re-throw — exception thoát khỏi Activate có thể
                // ngăn Inventor load các addin khác (Vault, Content Centre).
                Debug.WriteLine($"{LOG_PREFIX} LỖI NGHIÊM TRỌNG: {ex.Message}");
                Debug.WriteLine($"{LOG_PREFIX} Stack trace:\n{ex.StackTrace}");
                LogToFile("Activate", ex);
            }
        }

        public void Deactivate()
        {
            Debug.WriteLine($"{LOG_PREFIX} ===== Bắt đầu tắt addin =====");
            LogInfo("===== Deactivate START =====");

            // MCG_FIX: top-level try/catch bao trọn body Deactivate để bảo đảm
            // không có exception nào thoát ra Inventor (gây disable Vault/CC).
            try
            {
                try { _moduleManager?.CleanupAll(); }
                catch (Exception ex) { Debug.WriteLine($"{LOG_PREFIX} LỖI CleanupAll: {ex.Message}"); }

                _app = null;

                Debug.WriteLine($"{LOG_PREFIX} ===== Addin tắt THÀNH CÔNG =====");
                LogInfo("===== Deactivate SUCCESS =====");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI Deactivate: {ex.Message}");
                Debug.WriteLine($"{LOG_PREFIX} Stack trace:\n{ex.StackTrace}");
                LogToFile("Deactivate", ex);
            }
        }

        public void ExecuteCommand(int commandID)
        {
            Debug.WriteLine($"{LOG_PREFIX} ExecuteCommand commandID={commandID}");
        }

        public object Automation => null;

        // ─── MCG_FIX: file logger cho Activate/Deactivate ─────────────────────
        // Ghi exception ra file — KHÔNG bao giờ throw từ helper này.
        // Fully-qualify System.IO.File và System.Environment để tránh xung đột
        // với Inventor.File và Inventor.Environment (do `using Inventor;`).
        private static void LogToFile(string phase, Exception ex)
        {
            try
            {
                if (!Directory.Exists(LOG_DIR)) Directory.CreateDirectory(LOG_DIR);
                string nl = System.Environment.NewLine;
                System.IO.File.AppendAllText(LOG_FILE,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {phase} ERROR: {ex.Message}{nl}" +
                    $"{ex.StackTrace}{nl}{nl}");
            }
            catch { /* nuốt lỗi I/O — không bao giờ throw từ logger */ }
        }

        // MCG_FIX: log INFO (success path) — để xác nhận addin có chạy + đến đâu.
        // Tạo file log ngay cả khi không có exception, giúp phân biệt:
        //   (a) addin không được load   → log file không tồn tại
        //   (b) addin load nhưng đứng giữa chừng → log có 1 phần
        //   (c) addin load thành công   → log có dòng "===== Activate SUCCESS ====="
        private static void LogInfo(string message)
        {
            try
            {
                if (!Directory.Exists(LOG_DIR)) Directory.CreateDirectory(LOG_DIR);
                string nl = System.Environment.NewLine;
                System.IO.File.AppendAllText(LOG_FILE,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] INFO: {message}{nl}");
            }
            catch { /* nuốt lỗi I/O */ }
        }
    }
}
