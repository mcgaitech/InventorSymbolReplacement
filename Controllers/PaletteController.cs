using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Inventor;
using SymbolReplacer.Models;
using SymbolReplacer.Services;
using SymbolReplacer.Views;

namespace SymbolReplacer.Controllers
{
    /// <summary>
    /// Kết nối LibraryService + ThumbnailService với SymbolReplacerPanel.
    /// Chịu trách nhiệm:
    ///   - Load symbols từ library khi khởi động và khi user đổi path
    ///   - Filter danh sách theo search query
    ///   - Cập nhật symbol grid
    ///   - Scan active sheet và highlight symbols không có trong palette
    /// </summary>
    public class PaletteController
    {
        // ─── Hằng số log ──────────────────────────────────────────────────────
        private const string LOG_PREFIX = "[PaletteController]";

        // ─── Dependencies ─────────────────────────────────────────────────────
        private readonly Inventor.Application _app;
        private readonly ILibraryService      _libraryService;
        private readonly IThumbnailService    _thumbnailService;
        private readonly IConfigService       _configService;

        // ─── State ────────────────────────────────────────────────────────────
        private SymbolReplacerPanel         _panel;
        private List<SymbolDefinitionModel> _allSymbols  = new List<SymbolDefinitionModel>();
        private string                      _searchQuery = string.Empty;
        // Document được scan lần cuối — dùng để clear SelectSet khi Clear Highlight
        private DrawingDocument             _lastScannedDoc;

        // ─── Public: selected symbol ──────────────────────────────────────────
        public SymbolDefinitionModel SelectedSymbol
            => _panel?.SymbolGrid?.SelectedItem;

        // ─── Constructor ──────────────────────────────────────────────────────

        public PaletteController(
            Inventor.Application app,
            ILibraryService      libraryService,
            IThumbnailService    thumbnailService,
            IConfigService       configService)
        {
            _app              = app              ?? throw new ArgumentNullException(nameof(app));
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
            DetachPanel();
            _panel = panel;

            if (_panel != null)
            {
                _panel.LibraryPathChangeRequested += OnLibraryPathChangeRequested;
                _panel.SearchQueryChanged         += OnSearchQueryChanged;
                _panel.ScanSheetRequested         += OnScanSheetRequested;
                _panel.ClearHighlightRequested    += OnClearHighlightRequested;
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
            LoadLibrary(config.LibraryPath);
        }

        /// <summary>Re-wire events khi addin reload (firstTime=false).</summary>
        public void RewireEvents()
        {
            Debug.WriteLine($"{LOG_PREFIX} RewireEvents...");
            if (_panel != null)
                SetPanel(_panel);
        }

        /// <summary>Dọn dẹp khi addin bị tắt.</summary>
        public void Cleanup()
        {
            Debug.WriteLine($"{LOG_PREFIX} Cleanup...");
            ClearHighlight();
            DetachPanel();
            _thumbnailService.ClearCache();

            foreach (var sym in _allSymbols)
                sym.Dispose();
            _allSymbols.Clear();

            _libraryService.CloseLibrary();
        }

        // ─── Private: Library loading ─────────────────────────────────────────

        private void LoadLibrary(string path)
        {
            Debug.WriteLine($"{LOG_PREFIX} LoadLibrary: {path}");

            _thumbnailService.ClearCache();

            foreach (var sym in _allSymbols)
                sym.Dispose();
            _allSymbols.Clear();

            _panel?.SymbolGrid?.SetItems(null);

            if (string.IsNullOrWhiteSpace(path))
            {
                _panel?.SetStatusIdle();
                return;
            }

            try
            {
                var definitions = _libraryService.LoadDefinitions(path);

                if (definitions.Count == 0)
                {
                    Debug.WriteLine($"{LOG_PREFIX} Không có symbol nào trong library.");
                    _panel?.SetStatusInfo("Library loaded — no symbols found.");
                    return;
                }

                foreach (var def in definitions)
                {
                    var model = new SymbolDefinitionModel
                    {
                        Name        = def.Name,
                        Definition  = def,
                        Thumbnail   = _thumbnailService.GetThumbnail(def),
                        SourceFile  = GetSourceFile(def),
                        PromptCount = CountPromptFields(def)
                    };
                    _allSymbols.Add(model);
                }

                Debug.WriteLine($"{LOG_PREFIX} Loaded {_allSymbols.Count} symbols.");
                _panel?.SetStatusInfo($"Loaded {_allSymbols.Count} symbol(s).");
                ApplyFilter();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI LoadLibrary: {ex.Message}");
                _panel?.SetStatusError($"Cannot load library: {ex.Message}");
            }
        }

        /// <summary>Lọc theo _searchQuery và cập nhật grid.</summary>
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
            Debug.WriteLine($"{LOG_PREFIX} ApplyFilter: {list.Count}/{_allSymbols.Count} symbols.");
        }

        // ─── Private: Scan & Highlight ────────────────────────────────────────

        private void OnScanSheetRequested(object sender, EventArgs e)
        {
            Debug.WriteLine($"{LOG_PREFIX} ScanSheetRequested.");

            // Xóa highlight cũ trước
            ClearHighlight();

            var doc = _app.ActiveDocument as DrawingDocument;
            if (doc == null)
            {
                _panel?.SetStatusError("No active Drawing document.");
                return;
            }

            ScanAndHighlight(doc.ActiveSheet);
        }

        private void OnClearHighlightRequested(object sender, EventArgs e)
        {
            Debug.WriteLine($"{LOG_PREFIX} ClearHighlightRequested.");
            ClearHighlight();
            _panel?.SetStatusIdle();
        }

        /// <summary>
        /// Quét sheet và highlight các symbol không có trong palette
        /// bằng Inventor SelectSet (Inventor selection highlight — màu xanh).
        /// SelectSet.Select() tích lũy selection mà không clear — gọi Clear() trước để reset.
        /// </summary>
        private void ScanAndHighlight(Sheet sheet)
        {
            if (sheet == null)
            {
                _panel?.SetStatusError("No active sheet.");
                return;
            }

            Debug.WriteLine($"{LOG_PREFIX} ScanAndHighlight: sheet='{sheet.Name}'");

            var doc = _app.ActiveDocument as DrawingDocument;
            if (doc == null)
            {
                _panel?.SetStatusError("No active Drawing document.");
                return;
            }

            // Tập hợp tên symbol trong palette (so sánh case-insensitive)
            var paletteNames = new HashSet<string>(
                _allSymbols.Select(m => m.Name),
                StringComparer.OrdinalIgnoreCase);

            var nonPalette = new List<SketchedSymbol>();
            int total = 0;

            try
            {
                foreach (SketchedSymbol sym in sheet.SketchedSymbols)
                {
                    total++;
                    string defName = "";
                    try { defName = sym.Definition?.Name ?? ""; } catch { }

                    if (!paletteNames.Contains(defName))
                        nonPalette.Add(sym);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI duyệt SketchedSymbols: {ex.Message}");
                _panel?.SetStatusError($"Scan error: {ex.Message}");
                return;
            }

            Debug.WriteLine($"{LOG_PREFIX} Scan: {total} symbols total, {nonPalette.Count} not in palette.");

            if (nonPalette.Count == 0)
            {
                _panel?.SetStatusInfo($"All {total} symbol(s) on sheet match palette.");
                _panel?.SetHighlightActive(false);
                return;
            }

            // Highlight qua Inventor SelectSet — multi-select bằng cách gọi Select() tuần tự
            // SelectSet.Select() tích lũy vào selection hiện tại (không clear tự động)
            try
            {
                doc.SelectSet.Clear();
                int added = 0;
                foreach (var sym in nonPalette)
                {
                    try
                    {
                        doc.SelectSet.Select(sym);
                        added++;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"{LOG_PREFIX} SelectSet.Select failed for '{sym.Name}': {ex.Message}");
                    }
                }

                _lastScannedDoc = doc;
                Debug.WriteLine($"{LOG_PREFIX} SelectSet: {added}/{nonPalette.Count} symbols selected.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI SelectSet: {ex.Message}");
            }

            _panel?.SetStatusWarning(
                $"{nonPalette.Count} of {total} symbols NOT in palette (highlighted)");
            _panel?.SetHighlightActive(true);
        }

        /// <summary>Xóa highlight (clear SelectSet của document đã scan).</summary>
        private void ClearHighlight()
        {
            try
            {
                var doc = _lastScannedDoc ?? _app.ActiveDocument as DrawingDocument;
                doc?.SelectSet?.Clear();
                Debug.WriteLine($"{LOG_PREFIX} SelectSet cleared.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI clear SelectSet: {ex.Message}");
            }
            finally
            {
                _lastScannedDoc = null;
                _panel?.SetHighlightActive(false);
            }
        }

        // ─── Event handlers ───────────────────────────────────────────────────

        private void OnLibraryPathChangeRequested(object sender, string newPath)
        {
            Debug.WriteLine($"{LOG_PREFIX} Library path: {newPath}");
            _configService.Save(new LibraryConfigModel { LibraryPath = newPath });
            _panel?.SetLibraryPath(newPath);
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
            _panel?.UpdateActionButtonsState(selected != null);
            _panel?.SetSelectedSymbolProperties(selected);
        }

        // ─── Private helpers ──────────────────────────────────────────────────

        private static string GetSourceFile(Inventor.SketchedSymbolDefinition def)
        {
            try { return (def.Parent?.Parent as Inventor.DrawingDocument)?.FullFileName ?? ""; }
            catch { return ""; }
        }

        private static int CountPromptFields(Inventor.SketchedSymbolDefinition def)
        {
            try { return def.Sketch?.TextBoxes?.Count ?? 0; }
            catch { return 0; }
        }

        private void DetachPanel()
        {
            if (_panel == null) return;

            _panel.LibraryPathChangeRequested -= OnLibraryPathChangeRequested;
            _panel.SearchQueryChanged         -= OnSearchQueryChanged;
            _panel.ScanSheetRequested         -= OnScanSheetRequested;
            _panel.ClearHighlightRequested    -= OnClearHighlightRequested;

            if (_panel.SymbolGrid != null)
                _panel.SymbolGrid.SelectionChanged -= OnSymbolSelectionChanged;

            _panel = null;
            Debug.WriteLine($"{LOG_PREFIX} Đã detach panel.");
        }
    }
}
