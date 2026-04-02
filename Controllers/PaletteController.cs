using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SymbolReplacer.Models;
using SymbolReplacer.Services;
using SymbolReplacer.Views;

namespace SymbolReplacer.Controllers
{
    /// <summary>
    /// Kết nối LibraryService + ThumbnailService với SymbolReplacerPanel.
    /// Chịu trách nhiệm:
    ///   - Load symbols từ library file khi khởi động và khi user đổi path
    ///   - Filter danh sách theo search query
    ///   - Cập nhật ThumbnailGridControl
    ///   - Expose symbol đang được chọn cho ReplaceController (Phase 3+)
    /// </summary>
    public class PaletteController
    {
        // ─── Hằng số log ──────────────────────────────────────────────────────
        private const string LOG_PREFIX = "[PaletteController]";

        // ─── Dependencies ─────────────────────────────────────────────────────
        private readonly ILibraryService   _libraryService;
        private readonly IThumbnailService _thumbnailService;
        private readonly IConfigService    _configService;

        // ─── State ────────────────────────────────────────────────────────────
        private SymbolReplacerPanel             _panel;
        private List<SymbolDefinitionModel>     _allSymbols = new List<SymbolDefinitionModel>();
        private string                          _searchQuery = string.Empty;

        // ─── Public: Symbol đang chọn (Phase 3+ dùng) ─────────────────────────
        public SymbolDefinitionModel SelectedSymbol
            => _panel?.SymbolGrid?.SelectedItem;

        // ─── Constructor ──────────────────────────────────────────────────────

        public PaletteController(
            ILibraryService   libraryService,
            IThumbnailService thumbnailService,
            IConfigService    configService)
        {
            _libraryService   = libraryService   ?? throw new ArgumentNullException(nameof(libraryService));
            _thumbnailService = thumbnailService ?? throw new ArgumentNullException(nameof(thumbnailService));
            _configService    = configService    ?? throw new ArgumentNullException(nameof(configService));

            Debug.WriteLine($"{LOG_PREFIX} Khởi tạo PaletteController.");
        }

        // ─── Public methods ───────────────────────────────────────────────────

        /// <summary>
        /// Gán panel và đăng ký events.
        /// Gọi sau khi RibbonController.CreateRibbonUI() xong.
        /// </summary>
        public void SetPanel(SymbolReplacerPanel panel)
        {
            // Hủy đăng ký panel cũ
            DetachPanel();

            _panel = panel;

            if (_panel != null)
            {
                _panel.LibraryPathChangeRequested += OnLibraryPathChangeRequested;
                _panel.SearchQueryChanged         += OnSearchQueryChanged;
                _panel.SymbolGrid.SelectionChanged += OnSymbolSelectionChanged;
                Debug.WriteLine($"{LOG_PREFIX} Đã attach panel.");
            }
        }

        /// <summary>
        /// Khởi động: đọc config, hiển thị path, load library ban đầu.
        /// Gọi sau SetPanel().
        /// </summary>
        public void Initialize()
        {
            Debug.WriteLine($"{LOG_PREFIX} Initialize...");

            var config = _configService.Load();
            _panel?.SetLibraryPath(config.LibraryPath);

            // Load ngay khi khởi động
            LoadLibrary(config.LibraryPath);
        }

        /// <summary>
        /// Re-wire events khi addin reload (firstTime=false).
        /// Panel và data vẫn còn, chỉ cần re-subscribe events.
        /// </summary>
        public void RewireEvents()
        {
            Debug.WriteLine($"{LOG_PREFIX} RewireEvents...");
            if (_panel != null)
                SetPanel(_panel);  // re-attach cùng panel
        }

        /// <summary>Dọn dẹp khi addin bị tắt.</summary>
        public void Cleanup()
        {
            Debug.WriteLine($"{LOG_PREFIX} Cleanup...");
            DetachPanel();
            _thumbnailService.ClearCache();

            // Dispose all symbol models
            foreach (var sym in _allSymbols)
                sym.Dispose();
            _allSymbols.Clear();

            _libraryService.CloseLibrary();
        }

        // ─── Private: Library loading ─────────────────────────────────────────

        private void LoadLibrary(string path)
        {
            Debug.WriteLine($"{LOG_PREFIX} LoadLibrary: {path}");

            // Xóa cache thumbnail khi đổi library
            _thumbnailService.ClearCache();

            // Dispose symbol models cũ
            foreach (var sym in _allSymbols)
                sym.Dispose();
            _allSymbols.Clear();

            // Reset grid
            _panel?.SymbolGrid?.SetItems(null);

            if (string.IsNullOrWhiteSpace(path))
            {
                _panel?.SetStatusIdle();
                return;
            }

            try
            {
                // Load definitions từ LibraryService
                var definitions = _libraryService.LoadDefinitions(path);

                if (definitions.Count == 0)
                {
                    Debug.WriteLine($"{LOG_PREFIX} Không có symbol nào trong library.");
                    _panel?.SetStatusIdle();
                    return;
                }

                // Render thumbnail cho từng definition
                foreach (var def in definitions)
                {
                    var model = new SymbolDefinitionModel
                    {
                        Name       = def.Name,
                        Definition = def,
                        Thumbnail  = _thumbnailService.GetThumbnail(def)
                    };
                    _allSymbols.Add(model);
                }

                Debug.WriteLine($"{LOG_PREFIX} Loaded {_allSymbols.Count} symbols.");
                _panel?.SetStatusIdle();

                // Áp dụng filter hiện tại và cập nhật grid
                ApplyFilter();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI LoadLibrary: {ex.Message}");
                _panel?.SetStatusError($"Cannot load library: {ex.Message}");
            }
        }

        /// <summary>Lọc danh sách theo _searchQuery và cập nhật grid.</summary>
        private void ApplyFilter()
        {
            IEnumerable<SymbolDefinitionModel> filtered = _allSymbols;

            if (!string.IsNullOrWhiteSpace(_searchQuery))
            {
                filtered = _allSymbols.Where(s =>
                    s.Name.IndexOf(_searchQuery, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            var list = filtered.ToList();
            _panel?.SymbolGrid?.SetItems(list);

            Debug.WriteLine($"{LOG_PREFIX} ApplyFilter: hiện {list.Count}/{_allSymbols.Count} symbols.");
        }

        // ─── Event handlers ───────────────────────────────────────────────────

        private void OnLibraryPathChangeRequested(object sender, string newPath)
        {
            Debug.WriteLine($"{LOG_PREFIX} Library path thay đổi: {newPath}");

            // Lưu config
            _configService.Save(new LibraryConfigModel { LibraryPath = newPath });

            // Cập nhật hiển thị path trên panel
            _panel?.SetLibraryPath(newPath);

            // Reload library
            LoadLibrary(newPath);
        }

        private void OnSearchQueryChanged(object sender, string query)
        {
            _searchQuery = query ?? string.Empty;
            ApplyFilter();
        }

        private void OnSymbolSelectionChanged(object sender, EventArgs e)
        {
            var selected = SelectedSymbol;
            Debug.WriteLine($"{LOG_PREFIX} Symbol selected: '{selected?.Name ?? "none"}'");

            // Bật nút Replace khi có symbol được chọn
            _panel?.UpdateActionButtonsState(selected != null);
        }

        // ─── Private helpers ──────────────────────────────────────────────────

        private void DetachPanel()
        {
            if (_panel == null) return;

            _panel.LibraryPathChangeRequested -= OnLibraryPathChangeRequested;
            _panel.SearchQueryChanged         -= OnSearchQueryChanged;

            if (_panel.SymbolGrid != null)
                _panel.SymbolGrid.SelectionChanged -= OnSymbolSelectionChanged;

            Debug.WriteLine($"{LOG_PREFIX} Đã detach panel.");
        }
    }
}
