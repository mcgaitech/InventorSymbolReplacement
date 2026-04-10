using System;
using System.Diagnostics;
using Inventor;
using MCGInventorPlugin.Core;
using MCGInventorPlugin.Controllers.SymbolHandler;
using MCGInventorPlugin.Services.SymbolHandler;
using MCGInventorPlugin.Views.SymbolHandler;

namespace MCGInventorPlugin.Modules
{
    /// <summary>
    /// Module Symbol Handler — quản lý insert/replace SketchedSymbol trên Drawing.
    /// Implements IModule để đăng ký với ModuleManager.
    /// </summary>
    public class SymbolHandlerModule : IModule
    {
        private const string LOG_PREFIX = "[SymbolHandlerModule]";

        public string Name        => "Symbol Handler";
        public string Environment => "Drawing";

        // Services
        private IConfigService        _configService;
        private ILibraryService       _libraryService;
        private IThumbnailService     _thumbnailService;
        private ISymbolReplaceService _symbolReplaceService;

        // Controllers
        private RibbonController      _ribbonController;
        private PaletteController     _paletteController;
        private InteractionController _interactionController;
        private ReplaceController     _replaceController;
        private SelectionListener     _selectionListener;

        private bool _uiCreated = false;

        public void Activate(Application app, bool firstTime)
        {
            Debug.WriteLine($"{LOG_PREFIX} Activate (firstTime={firstTime})");

            _configService        = new ConfigService();
            _libraryService       = new LibraryService(app);
            _thumbnailService     = new ThumbnailService();
            _symbolReplaceService = new SymbolReplaceService(app);

            _ribbonController      = new RibbonController(app);
            _paletteController     = new PaletteController(app, _libraryService, _thumbnailService, _configService);
            _interactionController = new InteractionController(app);
            _selectionListener     = new SelectionListener(app);
            _replaceController     = new ReplaceController(app, _symbolReplaceService, _interactionController, _paletteController, _selectionListener);

            Debug.WriteLine($"{LOG_PREFIX} Services + Controllers khởi tạo.");
        }

        public void CreateUI(Application app)
        {
            if (_uiCreated) return;

            try
            {
                _ribbonController.CreateRibbonUI();
                _paletteController.SetPanel(_ribbonController.Panel);
                _paletteController.Initialize();
                _replaceController.SetPanel(_ribbonController.Panel);
                _selectionListener.Start();
                _uiCreated = true;
                Debug.WriteLine($"{LOG_PREFIX} Ribbon UI + Panel wiring THÀNH CÔNG.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI CreateUI: {ex.Message}");
            }
        }

        public void Cleanup()
        {
            Debug.WriteLine($"{LOG_PREFIX} Cleanup...");

            // Unhook keyboard NGAY ĐẦU TIÊN
            try { SymbolHandlerPanel.CleanupKeyboardHook(); }
            catch (Exception ex) { Debug.WriteLine($"{LOG_PREFIX} LỖI keyboard cleanup: {ex.Message}"); }

            // Controllers cleanup — thứ tự ngược với Activate (LIFO)
            try { _replaceController?.Cleanup(); }      catch (Exception ex) { Debug.WriteLine($"{LOG_PREFIX} LỖI ReplaceController: {ex.Message}"); }
            try { _selectionListener?.Stop(); }          catch (Exception ex) { Debug.WriteLine($"{LOG_PREFIX} LỖI SelectionListener: {ex.Message}"); }
            try { _interactionController?.Cleanup(); }   catch (Exception ex) { Debug.WriteLine($"{LOG_PREFIX} LỖI InteractionController: {ex.Message}"); }
            try { _paletteController?.Cleanup(); }       catch (Exception ex) { Debug.WriteLine($"{LOG_PREFIX} LỖI PaletteController: {ex.Message}"); }
            try { _ribbonController?.Cleanup(); }        catch (Exception ex) { Debug.WriteLine($"{LOG_PREFIX} LỖI RibbonController: {ex.Message}"); }

            // Giải phóng references — Services không có Cleanup() riêng nhưng cần null hóa
            _replaceController     = null;
            _selectionListener     = null;
            _interactionController = null;
            _paletteController     = null;
            _ribbonController      = null;
            _symbolReplaceService  = null;
            _thumbnailService      = null;
            _libraryService        = null;
            _configService         = null;

            Debug.WriteLine($"{LOG_PREFIX} Cleanup THÀNH CÔNG.");
        }
    }
}
