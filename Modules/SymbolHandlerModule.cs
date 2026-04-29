using System;
using System.Collections.Generic;
using System.Diagnostics;
using Inventor;
using MCG.Inventor.Ribbon;
using MCGInventorPlugin.Controllers.SymbolHandler;
using MCGInventorPlugin.Infrastructure;
using MCGInventorPlugin.Modules.SymbolHandler;
using MCGInventorPlugin.Services.SymbolHandler;
using MCGInventorPlugin.Views.SymbolHandler;

namespace MCGInventorPlugin.Modules
{
    /// <summary>
    /// Module Symbol Handler — quản lý insert/replace SketchedSymbol trên Drawing.
    /// Module chỉ trả về IToolDescriptor — MCGRibbonManager tự tạo ribbon/button/palette.
    /// </summary>
    public class SymbolHandlerModule : IModule
    {
        private const string LOG_PREFIX = "[SymbolHandlerModule]";

        public string Name => "Symbol Handler";

        // Services
        private IConfigService        _configService;
        private ILibraryService       _libraryService;
        private IThumbnailService     _thumbnailService;
        private ISymbolReplaceService _symbolReplaceService;

        // Controllers
        private PaletteController     _paletteController;
        private InteractionController _interactionController;
        private ReplaceController     _replaceController;
        private SelectionListener     _selectionListener;

        // Tool descriptor — giữ reference để access panel sau khi ribbon build
        private SymbolHandlerToolDescriptor _toolDescriptor;

        private bool _uiWired = false;

        public void Activate(Application app, bool firstTime)
        {
            Debug.WriteLine($"{LOG_PREFIX} Activate (firstTime={firstTime})");

            _configService        = new ConfigService();
            _libraryService       = new LibraryService(app);
            _thumbnailService     = new ThumbnailService();
            _symbolReplaceService = new SymbolReplaceService(app);

            _paletteController     = new PaletteController(app, _libraryService, _thumbnailService, _configService);
            _interactionController = new InteractionController(app);
            _selectionListener     = new SelectionListener(app);
            _replaceController     = new ReplaceController(app, _symbolReplaceService, _interactionController, _paletteController, _selectionListener);

            Debug.WriteLine($"{LOG_PREFIX} Services + Controllers khởi tạo.");
        }

        public IEnumerable<IToolDescriptor> GetTools()
        {
            _toolDescriptor = new SymbolHandlerToolDescriptor();
            return new IToolDescriptor[] { _toolDescriptor };
        }

        public void OnUIReady()
        {
            if (_uiWired) return;
            if (_toolDescriptor == null) return;

            try
            {
                var panel = _toolDescriptor.CreatedPanel;
                if (panel == null)
                {
                    Debug.WriteLine($"{LOG_PREFIX} CẢNH BÁO: CreatedPanel null — chưa có content embed.");
                    return;
                }

                _paletteController.SetPanel(panel);
                _paletteController.AttachSelectionListener(_selectionListener);
                _paletteController.Initialize();
                _replaceController.SetPanel(panel);
                _selectionListener.Start();
                _uiWired = true;

                Debug.WriteLine($"{LOG_PREFIX} UI wiring THÀNH CÔNG.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI OnUIReady: {ex.Message}");
            }
        }

        public void Cleanup()
        {
            Debug.WriteLine($"{LOG_PREFIX} Cleanup...");

            // Unhook keyboard NGAY ĐẦU TIÊN (trước khi WPF panel bị dispose)
            try { SymbolHandlerPanel.CleanupKeyboardHook(); }
            catch (Exception ex) { Debug.WriteLine($"{LOG_PREFIX} LỖI keyboard cleanup: {ex.Message}"); }

            // Controllers cleanup — LIFO
            try { _replaceController?.Cleanup(); }     catch (Exception ex) { Debug.WriteLine($"{LOG_PREFIX} LỖI ReplaceController: {ex.Message}"); }
            try { _selectionListener?.Stop(); }         catch (Exception ex) { Debug.WriteLine($"{LOG_PREFIX} LỖI SelectionListener: {ex.Message}"); }
            try { _interactionController?.Cleanup(); }  catch (Exception ex) { Debug.WriteLine($"{LOG_PREFIX} LỖI InteractionController: {ex.Message}"); }
            try { _paletteController?.Cleanup(); }      catch (Exception ex) { Debug.WriteLine($"{LOG_PREFIX} LỖI PaletteController: {ex.Message}"); }

            _replaceController     = null;
            _selectionListener     = null;
            _interactionController = null;
            _paletteController     = null;
            _symbolReplaceService  = null;
            _thumbnailService      = null;
            _libraryService        = null;
            _configService         = null;
            _toolDescriptor        = null;

            Debug.WriteLine($"{LOG_PREFIX} Cleanup THÀNH CÔNG.");
        }
    }
}
