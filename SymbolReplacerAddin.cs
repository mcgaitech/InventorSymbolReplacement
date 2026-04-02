using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Inventor;
using SymbolReplacer.Controllers;
using SymbolReplacer.Services;

namespace SymbolReplacer
{
    /// <summary>
    /// Entry point của AddIn. Inventor tìm class này qua COM GUID.
    /// GUID phải khớp với ClassId trong file SymbolReplacer.addin
    ///
    /// DI manual: tất cả services và controllers được khởi tạo tại đây trong Activate().
    /// </summary>
    [Guid("7C3D8E4F-2A1B-4C5D-9E8F-1A2B3C4D5E6F")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    public class SymbolReplacerAddin : ApplicationAddInServer
    {
        // ─── Hằng số log ─────────────────────────────────────────────────────
        private const string LOG_PREFIX = "[SymbolReplacerAddin]";

        // ─── Fields ───────────────────────────────────────────────────────────
        private Inventor.Application _app;

        // Services
        private IConfigService    _configService;
        private ILibraryService   _libraryService;
        private IThumbnailService _thumbnailService;

        // Controllers
        private RibbonController   _ribbonController;
        private PaletteController  _paletteController;

        // ─── ApplicationAddInServer interface ────────────────────────────────

        /// <summary>
        /// Inventor gọi Activate() khi load addin.
        /// firstTime = true nếu đây là lần đầu tiên addin được load trong session này.
        /// </summary>
        public void Activate(ApplicationAddInSite addInSiteObject, bool firstTime)
        {
            Debug.WriteLine($"{LOG_PREFIX} ===== Bắt đầu kích hoạt addin =====");
            Debug.WriteLine($"{LOG_PREFIX} firstTime = {firstTime}");

            try
            {
                // Lưu reference đến Inventor Application
                _app = addInSiteObject.Application;
                Debug.WriteLine($"{LOG_PREFIX} Inventor version: {_app.SoftwareVersion.DisplayVersion}");

                // ── DI: Khởi tạo Services ──
                _configService    = new ConfigService();
                _libraryService   = new LibraryService(_app);
                _thumbnailService = new ThumbnailService();
                Debug.WriteLine($"{LOG_PREFIX} Services đã khởi tạo.");

                // ── DI: Khởi tạo Controllers ──
                _ribbonController  = new RibbonController(_app);
                _paletteController = new PaletteController(
                    _libraryService,
                    _thumbnailService,
                    _configService);
                Debug.WriteLine($"{LOG_PREFIX} Controllers đã khởi tạo.");

                if (firstTime)
                {
                    // Tạo Ribbon UI và DockableWindow (embed panel)
                    _ribbonController.CreateRibbonUI();
                    Debug.WriteLine($"{LOG_PREFIX} Ribbon UI đã được tạo.");

                    // Gán panel cho PaletteController và load library ban đầu
                    // Panel luôn được tạo trong CreateRibbonUI() (dù HWND=0 hay không)
                    _paletteController.SetPanel(_ribbonController.Panel);
                    _paletteController.Initialize();
                    Debug.WriteLine($"{LOG_PREFIX} PaletteController đã Initialize.");
                }
                else
                {
                    // Reload addin — ribbon và panel đã tồn tại
                    Debug.WriteLine($"{LOG_PREFIX} Reload addin — re-wire events.");
                    _ribbonController.RewireEvents();

                    // Re-attach panel (đã tồn tại từ session trước)
                    _paletteController.SetPanel(_ribbonController.Panel);
                    _paletteController.Initialize();
                }

                Debug.WriteLine($"{LOG_PREFIX} ===== Addin kích hoạt THÀNH CÔNG =====");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI NGHIÊM TRỌNG khi kích hoạt: {ex.Message}");
                Debug.WriteLine($"{LOG_PREFIX} Stack trace:\n{ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Inventor gọi Deactivate() khi user tắt addin hoặc đóng Inventor.
        /// </summary>
        public void Deactivate()
        {
            Debug.WriteLine($"{LOG_PREFIX} ===== Bắt đầu tắt addin =====");

            try
            {
                // Dọn dẹp PaletteController (dispose models, xóa cache, đóng library)
                _paletteController?.Cleanup();
                Debug.WriteLine($"{LOG_PREFIX} PaletteController đã cleanup.");

                // Dọn dẹp Ribbon và DockableWindow
                _ribbonController?.Cleanup();
                Debug.WriteLine($"{LOG_PREFIX} RibbonController đã cleanup.");

                // Giải phóng references
                _app = null;

                // Ép GC thu dọn COM objects
                GC.Collect();
                GC.WaitForPendingFinalizers();

                Debug.WriteLine($"{LOG_PREFIX} ===== Addin tắt THÀNH CÔNG =====");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI khi tắt addin: {ex.Message}");
            }
        }

        /// <summary>Không dùng trong addin này.</summary>
        public void ExecuteCommand(int commandID)
        {
            Debug.WriteLine($"{LOG_PREFIX} ExecuteCommand commandID={commandID}");
        }

        /// <summary>Không expose automation object.</summary>
        public object Automation => null;
    }
}
