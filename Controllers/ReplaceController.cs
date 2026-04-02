using System;
using System.Diagnostics;
using System.Windows.Forms;
using Inventor;
using SymbolReplacer.Services;
using SymbolReplacer.Views;

namespace SymbolReplacer.Controllers
{
    /// <summary>
    /// Điều phối quy trình replace:
    ///   1. User chọn new symbol trên palette (PaletteController.SelectedSymbol)
    ///   2. User click "Replace" → gọi EnterPickMode()
    ///   3. User click old symbol trên bản vẽ → InteractionController raises SymbolPicked
    ///   4. Gọi SymbolReplaceService.ReplaceSingle()
    ///   5. Cập nhật status bar
    ///
    /// Replace All:
    ///   - Current Sheet: tìm definition matching tên của new symbol → replace all trên sheet
    ///   - All Sheets: replace all trên toàn document
    /// </summary>
    public class ReplaceController
    {
        // ─── Hằng số log ──────────────────────────────────────────────────────
        private const string LOG_PREFIX = "[ReplaceController]";

        // ─── Dependencies ─────────────────────────────────────────────────────
        private readonly Inventor.Application    _app;
        private readonly ISymbolReplaceService   _replaceService;
        private readonly InteractionController   _interactionCtrl;
        private readonly PaletteController       _paletteCtrl;

        // ─── State ────────────────────────────────────────────────────────────
        private SymbolReplacerPanel _panel;

        // ─── Constructor ──────────────────────────────────────────────────────
        public ReplaceController(
            Inventor.Application   app,
            ISymbolReplaceService  replaceService,
            InteractionController  interactionCtrl,
            PaletteController      paletteCtrl)
        {
            _app             = app             ?? throw new ArgumentNullException(nameof(app));
            _replaceService  = replaceService  ?? throw new ArgumentNullException(nameof(replaceService));
            _interactionCtrl = interactionCtrl ?? throw new ArgumentNullException(nameof(interactionCtrl));
            _paletteCtrl     = paletteCtrl     ?? throw new ArgumentNullException(nameof(paletteCtrl));

            // Đăng ký event từ InteractionController
            _interactionCtrl.SymbolPicked    += OnSymbolPicked;
            _interactionCtrl.PickModeCancelled += OnPickModeCancelled;

            Debug.WriteLine($"{LOG_PREFIX} Khởi tạo ReplaceController.");
        }

        // ─── Public methods ───────────────────────────────────────────────────

        /// <summary>Gán panel để update status bar. Gọi sau SetPanel của PaletteController.</summary>
        public void SetPanel(SymbolReplacerPanel panel)
        {
            DetachPanel();
            _panel = panel;

            if (_panel != null)
            {
                _panel.ReplaceRequested              += OnReplaceRequested;
                _panel.ReplaceAllCurrentSheetRequested += OnReplaceAllCurrentSheetRequested;
                _panel.ReplaceAllAllSheetsRequested    += OnReplaceAllAllSheetsRequested;
                Debug.WriteLine($"{LOG_PREFIX} Đã attach panel.");
            }
        }

        /// <summary>Dọn dẹp khi addin tắt.</summary>
        public void Cleanup()
        {
            Debug.WriteLine($"{LOG_PREFIX} Cleanup.");
            _interactionCtrl.SymbolPicked     -= OnSymbolPicked;
            _interactionCtrl.PickModeCancelled -= OnPickModeCancelled;
            _interactionCtrl.Cleanup();
            DetachPanel();
        }

        // ─── Event handlers: panel buttons ───────────────────────────────────

        private void OnReplaceRequested(object sender, EventArgs e)
        {
            Debug.WriteLine($"{LOG_PREFIX} Replace button clicked.");

            if (_paletteCtrl.SelectedSymbol == null)
            {
                _panel?.SetStatusError("Please select a new symbol first.");
                return;
            }

            if (!IsDrawingDocumentActive())
            {
                _panel?.SetStatusError("No active Drawing document.");
                return;
            }

            // Enter pick mode — chờ user click old symbol
            _panel?.SetStatusPickMode();
            _interactionCtrl.EnterPickMode();
        }

        private void OnReplaceAllCurrentSheetRequested(object sender, EventArgs e)
        {
            Debug.WriteLine($"{LOG_PREFIX} Replace All — Current Sheet.");

            var newDef = _paletteCtrl.SelectedSymbol?.Definition;
            if (newDef == null)
            {
                _panel?.SetStatusError("Please select a new symbol first.");
                return;
            }

            var doc = GetActiveDrawingDoc();
            if (doc == null) return;

            var sheet = doc.ActiveSheet;
            if (sheet == null)
            {
                _panel?.SetStatusError("No active sheet.");
                return;
            }

            // Xác định old definition: tìm symbol cùng tên trong document hiện tại
            // Nếu không có → hỏi user pick (ESC = cancel)
            var oldDef = FindMatchingDefinitionInDoc(doc, newDef.Name);
            if (oldDef == null)
            {
                // Không tìm được definition cùng tên → cần user pick một instance
                _panel?.SetStatusPickMode();
                // Lưu tạm context để biết đây là Replace All Sheet sau khi pick
                _pendingReplaceAllMode = ReplaceAllMode.CurrentSheet;
                _interactionCtrl.EnterPickMode();
                return;
            }

            ExecuteReplaceAllSheet(sheet, doc, oldDef, newDef);
        }

        private void OnReplaceAllAllSheetsRequested(object sender, EventArgs e)
        {
            Debug.WriteLine($"{LOG_PREFIX} Replace All — All Sheets.");

            var newDef = _paletteCtrl.SelectedSymbol?.Definition;
            if (newDef == null)
            {
                _panel?.SetStatusError("Please select a new symbol first.");
                return;
            }

            var doc = GetActiveDrawingDoc();
            if (doc == null) return;

            var oldDef = FindMatchingDefinitionInDoc(doc, newDef.Name);
            if (oldDef == null)
            {
                _panel?.SetStatusPickMode();
                _pendingReplaceAllMode = ReplaceAllMode.AllSheets;
                _interactionCtrl.EnterPickMode();
                return;
            }

            ExecuteReplaceAllDoc(doc, oldDef, newDef);
        }

        // ─── Event handlers: InteractionController ────────────────────────────

        private ReplaceAllMode _pendingReplaceAllMode = ReplaceAllMode.None;

        private void OnSymbolPicked(object sender, SketchedSymbol pickedSymbol)
        {
            Debug.WriteLine($"{LOG_PREFIX} OnSymbolPicked: '{pickedSymbol?.Name}'");

            var newDef = _paletteCtrl.SelectedSymbol?.Definition;
            if (newDef == null)
            {
                _panel?.SetStatusError("New symbol selection lost. Please re-select.");
                return;
            }

            if (pickedSymbol == null)
            {
                _panel?.SetStatusIdle();
                return;
            }

            var mode = _pendingReplaceAllMode;
            _pendingReplaceAllMode = ReplaceAllMode.None;

            if (mode == ReplaceAllMode.None)
            {
                // Replace single
                bool ok = _replaceService.ReplaceSingle(pickedSymbol, newDef);
                if (ok)
                    _panel?.SetStatusSuccess(1);
                else
                    _panel?.SetStatusError("Replace failed. Check DebugView for details.");
            }
            else
            {
                // Replace All — dùng picked symbol để xác định old definition
                var oldDef = pickedSymbol.Definition;
                var doc    = pickedSymbol.Parent?.Parent as DrawingDocument;
                if (doc == null)
                {
                    _panel?.SetStatusError("Cannot determine document from selected symbol.");
                    return;
                }

                if (mode == ReplaceAllMode.CurrentSheet)
                    ExecuteReplaceAllSheet(doc.ActiveSheet, doc, oldDef, newDef);
                else
                    ExecuteReplaceAllDoc(doc, oldDef, newDef);
            }
        }

        private void OnPickModeCancelled(object sender, EventArgs e)
        {
            Debug.WriteLine($"{LOG_PREFIX} Pick mode cancelled (ESC).");
            _pendingReplaceAllMode = ReplaceAllMode.None;
            _panel?.SetStatusIdle();
        }

        // ─── Private: Execute replace ─────────────────────────────────────────

        private void ExecuteReplaceAllSheet(
            Sheet sheet, DrawingDocument doc,
            SketchedSymbolDefinition oldDef, SketchedSymbolDefinition newDef)
        {
            // Confirm dialog
            int estimated = CountMatchingOnSheet(sheet, oldDef);
            var answer = MessageBox.Show(
                $"Replace {estimated} instance(s) of '{oldDef.Name}'\n" +
                $"with '{newDef.Name}' on sheet '{sheet.Name}'?\n\n" +
                "This can be undone with Ctrl+Z.",
                "Replace All — Current Sheet",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question);

            if (answer != DialogResult.OK)
            {
                _panel?.SetStatusIdle();
                return;
            }

            int count = _replaceService.ReplaceAllOnSheet(sheet, oldDef, newDef);
            if (count > 0)
                _panel?.SetStatusSuccess(count);
            else
                _panel?.SetStatusError("No symbols replaced. Check DebugView.");
        }

        private void ExecuteReplaceAllDoc(
            DrawingDocument doc,
            SketchedSymbolDefinition oldDef, SketchedSymbolDefinition newDef)
        {
            // Tính tổng số instance trên toàn document để hiện confirm
            int total = 0;
            foreach (Sheet s in doc.Sheets)
                total += CountMatchingOnSheet(s, oldDef);

            var answer = MessageBox.Show(
                $"Replace {total} instance(s) of '{oldDef.Name}'\n" +
                $"with '{newDef.Name}' across ALL sheets?\n\n" +
                "This can be undone with Ctrl+Z.",
                "Replace All — All Sheets",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question);

            if (answer != DialogResult.OK)
            {
                _panel?.SetStatusIdle();
                return;
            }

            int count = _replaceService.ReplaceAllInDocument(doc, oldDef, newDef);
            if (count > 0)
                _panel?.SetStatusSuccess(count);
            else
                _panel?.SetStatusError("No symbols replaced. Check DebugView.");
        }

        // ─── Private: Helpers ─────────────────────────────────────────────────

        private bool IsDrawingDocumentActive()
            => _app.ActiveDocument is DrawingDocument;

        private DrawingDocument GetActiveDrawingDoc()
        {
            var doc = _app.ActiveDocument as DrawingDocument;
            if (doc == null)
                _panel?.SetStatusError("No active Drawing document.");
            return doc;
        }

        /// <summary>
        /// Tìm SketchedSymbolDefinition trong document hiện tại có tên match với newDef.
        /// Dùng khi Replace All mà không cần user pick instance.
        /// </summary>
        private SketchedSymbolDefinition FindMatchingDefinitionInDoc(
            DrawingDocument doc, string defName)
        {
            try
            {
                foreach (SketchedSymbolDefinition def in doc.SketchedSymbolDefinitions)
                {
                    if (string.Equals(def.Name, defName, StringComparison.OrdinalIgnoreCase))
                        return def;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI FindMatchingDefinition: {ex.Message}");
            }
            return null;
        }

        private static int CountMatchingOnSheet(Sheet sheet, SketchedSymbolDefinition targetDef)
        {
            int count = 0;
            try
            {
                foreach (SketchedSymbol sym in sheet.SketchedSymbols)
                {
                    try
                    {
                        if (string.Equals(sym.Definition?.Name, targetDef.Name,
                            StringComparison.OrdinalIgnoreCase))
                            count++;
                    }
                    catch { }
                }
            }
            catch { }
            return count;
        }

        private void DetachPanel()
        {
            if (_panel == null) return;
            _panel.ReplaceRequested              -= OnReplaceRequested;
            _panel.ReplaceAllCurrentSheetRequested -= OnReplaceAllCurrentSheetRequested;
            _panel.ReplaceAllAllSheetsRequested    -= OnReplaceAllAllSheetsRequested;
            _panel = null;
        }

        // ─── Nested enum ──────────────────────────────────────────────────────
        private enum ReplaceAllMode { None, CurrentSheet, AllSheets }
    }
}
