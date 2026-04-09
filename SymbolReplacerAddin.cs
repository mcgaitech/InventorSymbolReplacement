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
        private IConfigService       _configService;
        private ILibraryService      _libraryService;
        private IThumbnailService    _thumbnailService;
        private ISymbolReplaceService _symbolReplaceService;

        // Controllers
        private RibbonController      _ribbonController;
        private PaletteController     _paletteController;
        private InteractionController _interactionController;
        private ReplaceController     _replaceController;

        private bool _ribbonUICreated = false;

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
                _configService        = new ConfigService();
                _libraryService       = new LibraryService(_app);
                _thumbnailService     = new ThumbnailService();
                _symbolReplaceService = new SymbolReplaceService(_app);
                Debug.WriteLine($"{LOG_PREFIX} Services đã khởi tạo.");

                // ── DI: Khởi tạo Controllers ──
                _ribbonController      = new RibbonController(_app);
                _paletteController     = new PaletteController(
                    _app,
                    _libraryService,
                    _thumbnailService,
                    _configService);
                _interactionController = new InteractionController(_app);
                _replaceController     = new ReplaceController(
                    _app,
                    _symbolReplaceService,
                    _interactionController,
                    _paletteController);
                Debug.WriteLine($"{LOG_PREFIX} Controllers đã khởi tạo.");

                if (firstTime)
                {
                    TryCreateRibbonUIAndWire();
                }
                else
                {
                    // Reload addin — ribbon và panel đã tồn tại
                    Debug.WriteLine($"{LOG_PREFIX} Reload addin — re-wire events.");
                    _ribbonController.RewireEvents();

                    // Re-attach panel (đã tồn tại từ session trước)
                    _paletteController.SetPanel(_ribbonController.Panel);
                    _paletteController.Initialize();
                    _replaceController.SetPanel(_ribbonController.Panel);
                    _ribbonUICreated = true;
                    Debug.WriteLine($"{LOG_PREFIX} Re-wire hoàn tất.");
                }

                Debug.WriteLine($"{LOG_PREFIX} ===== Addin kích hoạt THÀNH CÔNG =====");
            }
            catch (Exception ex)
            {
                // KHÔNG re-throw — nếu Activate() throws ra ngoài, Inventor không gọi Deactivate()
                // → COM objects leak → "TerminateProcess: N AddIn objects not released" → Inventor crash.
                // Log lỗi và để Inventor tiếp tục (addin sẽ bị disabled nhưng Inventor không crash).
                Debug.WriteLine($"{LOG_PREFIX} LỖI NGHIÊM TRỌNG khi kích hoạt: {ex.Message}");
                Debug.WriteLine($"{LOG_PREFIX} Stack trace:\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Tạo Ribbon UI và wire controllers vào panel.
        /// </summary>
        private void TryCreateRibbonUIAndWire()
        {
            if (_ribbonUICreated) return;

            try
            {
                _ribbonController.CreateRibbonUI();

                _paletteController.SetPanel(_ribbonController.Panel);
                _paletteController.Initialize();
                _replaceController.SetPanel(_ribbonController.Panel);
                _ribbonUICreated = true;
                Debug.WriteLine($"{LOG_PREFIX} Ribbon UI + Panel wiring THÀNH CÔNG.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI TryCreateRibbonUIAndWire: {ex.Message}");
            }
        }

        /// <summary>
        /// Inventor gọi Deactivate() khi user tắt addin hoặc đóng Inventor.
        /// </summary>
        public void Deactivate()
        {
            Debug.WriteLine($"{LOG_PREFIX} ===== Bắt đầu tắt addin =====");

            // QUAN TRỌNG: unhook keyboard NGAY ĐẦU TIÊN — trước mọi cleanup khác.
            // WH_GETMESSAGE hook callback fire sau khi managed objects bị GC → crash Inventor.
            try
            {
                Views.SymbolReplacerPanel.CleanupKeyboardHook();
                Debug.WriteLine($"{LOG_PREFIX} Keyboard hook cleaned up.");
            }
            catch (Exception ex) { Debug.WriteLine($"{LOG_PREFIX} LỖI cleanup keyboard hook: {ex.Message}"); }

            // Mỗi bước cleanup độc lập — lỗi 1 bước không chặn bước khác
            try { _replaceController?.Cleanup(); }
            catch (Exception ex) { Debug.WriteLine($"{LOG_PREFIX} LỖI ReplaceController.Cleanup: {ex.Message}"); }

            try { _paletteController?.Cleanup(); }
            catch (Exception ex) { Debug.WriteLine($"{LOG_PREFIX} LỖI PaletteController.Cleanup: {ex.Message}"); }

            try { _ribbonController?.Cleanup(); }
            catch (Exception ex) { Debug.WriteLine($"{LOG_PREFIX} LỖI RibbonController.Cleanup: {ex.Message}"); }

            _app = null;

            Debug.WriteLine($"{LOG_PREFIX} ===== Addin tắt THÀNH CÔNG =====");
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
