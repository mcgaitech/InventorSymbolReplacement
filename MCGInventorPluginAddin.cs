using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
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
    [Guid("C331C6DF-8DEA-40DA-9DEA-4B3E5AEB5C0F")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    public class MCGInventorPluginAddin : ApplicationAddInServer
    {
        private const string LOG_PREFIX  = "[MCGInventorPlugin]";
        internal const string ADDIN_GUID = "{C331C6DF-8DEA-40DA-9DEA-4B3E5AEB5C0F}";

        // MCG_FIX: file log để debug khi Inventor ẩn console — bất kỳ exception nào
        // thoát khỏi Activate/Deactivate có thể làm Inventor lỡ load Vault/ContentCenter,
        // vì vậy bắt buộc nuốt mọi exception và ghi ra file.
        private const string LOG_DIR  = @"C:\CustomTools\Inventor\logs";
        private const string LOG_FILE = @"C:\CustomTools\Inventor\logs\MCG_InventorSymbolHandler.log";

        private Inventor.Application _app;
        private ModuleManager _moduleManager;

        // ─── ENTRY POINT ──────────────────────────────────────────────────────
        // MCG_FIX (TASK 1): Activate chỉ làm 2 việc — gọi ActivateInternal trong
        // try/catch + log. Toàn bộ logic instantiate object (new ModuleManager,
        // new SymbolHandlerModule, ...) nằm trong ActivateInternal.
        // Lý do: JIT compile method body LẦN ĐẦU khi method được gọi. Nếu thiếu
        // DLL phụ thuộc, JIT exception sẽ văng ra TRƯỚC KHI thân method được
        // chạy — nghĩa là TRƯỚC try/catch ở cùng method. Bằng cách tách sang
        // method khác + [MethodImpl(NoInlining)], lỗi JIT của ActivateInternal
        // sẽ propagate UP vào try/catch của Activate, không thoát ra Inventor.
        public void Activate(ApplicationAddInSite addInSiteObject, bool firstTime)
        {
            Debug.WriteLine($"{LOG_PREFIX} ===== Bắt đầu kích hoạt addin =====");
            Debug.WriteLine($"{LOG_PREFIX} firstTime = {firstTime}");
            LogInfo($"===== Activate START (firstTime={firstTime}) =====");

            try
            {
                ActivateInternal(addInSiteObject, firstTime);
                Debug.WriteLine($"{LOG_PREFIX} ===== Addin kích hoạt THÀNH CÔNG =====");
                // MCG_FIX: Activate thành công → log file không còn cần thiết.
                // Quy ước mới: file log CHỈ tồn tại khi có lỗi. User mở thư mục
                // logs → file có nghĩa là addin gặp vấn đề → mở để xem chi tiết.
                DeleteLogFile();
            }
            catch (Exception ex)
            {
                // KHÔNG re-throw — exception thoát khỏi Activate có thể ngăn
                // Inventor load các addin khác (Vault, Content Centre).
                Debug.WriteLine($"{LOG_PREFIX} LỖI NGHIÊM TRỌNG: {ex.Message}");
                Debug.WriteLine($"{LOG_PREFIX} Stack trace:\n{ex.StackTrace}");
                LogToFile("Activate", ex);
            }
        }

        // MCG_FIX (TASK 1): NoInlining bắt buộc — nếu JIT inline lại vào
        // Activate() thì pattern try/catch-around-call mất tác dụng.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ActivateInternal(ApplicationAddInSite addInSiteObject, bool firstTime)
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
        }

        public void Deactivate()
        {
            Debug.WriteLine($"{LOG_PREFIX} ===== Bắt đầu tắt addin =====");

            // MCG_FIX: Không còn ghi LogInfo trong Deactivate — nếu Activate
            // đã thành công và xoá log, ghi INFO ở Deactivate sẽ tái tạo file
            // → phá quy ước "log file chỉ tồn tại khi có lỗi". Vẫn ghi nếu có
            // exception thoát ra.
            try
            {
                DeactivateInternal();
                Debug.WriteLine($"{LOG_PREFIX} ===== Addin tắt THÀNH CÔNG =====");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI Deactivate: {ex.Message}");
                Debug.WriteLine($"{LOG_PREFIX} Stack trace:\n{ex.StackTrace}");
                LogToFile("Deactivate", ex);
            }
        }

        // MCG_FIX (TASK 1): tách body Deactivate sang method NoInlining để
        // bảo vệ khỏi JIT compile errors (đối xứng với ActivateInternal).
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void DeactivateInternal()
        {
            try { _moduleManager?.CleanupAll(); }
            catch (Exception ex) { Debug.WriteLine($"{LOG_PREFIX} LỖI CleanupAll: {ex.Message}"); }

            _moduleManager = null;
            _app = null;
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

        // MCG_FIX: log INFO (interim progress trong ActivateInternal).
        // Quy ước: file được giữ lại CHỈ khi Activate thất bại — DeleteLogFile()
        // ở cuối success path sẽ xoá file (kể cả các INFO đã ghi). Vì vậy:
        //   (a) Activate succeed → file không tồn tại
        //   (b) Activate dừng giữa chừng → file có INFO entries cho đến điểm dừng
        //   (c) Activate throw → file có INFO + ERROR + stack trace
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

        // MCG_FIX: gọi sau khi Activate thành công — file log chỉ còn lại nếu lỗi.
        private static void DeleteLogFile()
        {
            try
            {
                if (System.IO.File.Exists(LOG_FILE))
                    System.IO.File.Delete(LOG_FILE);
            }
            catch { /* nuốt lỗi I/O — file đang bị lock cũng không sao */ }
        }
    }
}
