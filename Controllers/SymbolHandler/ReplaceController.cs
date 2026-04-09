using System;
using System.Diagnostics;
using System.Windows.Forms;
using Inventor;
using MCGInventorPlugin.Services.SymbolHandler;
using MCGInventorPlugin.Views.SymbolHandler;

namespace MCGInventorPlugin.Controllers.SymbolHandler
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
        private SymbolHandlerPanel _panel;

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
            _interactionCtrl.SymbolPicked      += OnSymbolPicked;
            _interactionCtrl.PickModeCancelled += OnPickModeCancelled;
            _interactionCtrl.InsertPointPicked += OnInsertPointPicked;
            _interactionCtrl.InsertModeCancelled += OnInsertModeCancelled;

            Debug.WriteLine($"{LOG_PREFIX} Khởi tạo ReplaceController.");
        }

        // ─── Public methods ───────────────────────────────────────────────────

        /// <summary>Gán panel để update status bar. Gọi sau SetPanel của PaletteController.</summary>
        public void SetPanel(SymbolHandlerPanel panel)
        {
            DetachPanel();
            _panel = panel;

            if (_panel != null)
            {
                _panel.ReplaceRequested                += OnReplaceRequested;
                _panel.ReplaceAllCurrentSheetRequested += OnReplaceAllCurrentSheetRequested;
                _panel.ReplaceAllAllSheetsRequested    += OnReplaceAllAllSheetsRequested;
                _panel.InsertRequested                 += OnInsertRequested;
                Debug.WriteLine($"{LOG_PREFIX} Đã attach panel.");
            }
        }

        /// <summary>Dọn dẹp khi addin tắt.</summary>
        public void Cleanup()
        {
            Debug.WriteLine($"{LOG_PREFIX} Cleanup.");
            _interactionCtrl.SymbolPicked        -= OnSymbolPicked;
            _interactionCtrl.PickModeCancelled   -= OnPickModeCancelled;
            _interactionCtrl.InsertPointPicked   -= OnInsertPointPicked;
            _interactionCtrl.InsertModeCancelled -= OnInsertModeCancelled;
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

            // Luôn vào pick mode: user chỉ định old symbol bằng cách click trực tiếp
            _pendingReplaceAllMode = ReplaceAllMode.CurrentSheet;
            _panel?.SetStatusPickMode();
            _interactionCtrl.EnterPickMode();
        }

        private void OnReplaceAllAllSheetsRequested(object sender, EventArgs e)
        {
            Debug.WriteLine($"{LOG_PREFIX} Replace All — All Sheets.");

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

            // Luôn vào pick mode: user chỉ định old symbol bằng cách click trực tiếp
            _pendingReplaceAllMode = ReplaceAllMode.AllSheets;
            _panel?.SetStatusPickMode();
            _interactionCtrl.EnterPickMode();
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

        // ─── Event handlers: Insert ───────────────────────────────────────────

        private void OnInsertRequested(object sender, EventArgs e)
        {
            Debug.WriteLine($"{LOG_PREFIX} Insert requested.");

            if (_paletteCtrl.SelectedSymbol == null)
            {
                _panel?.SetStatusError("Please select a symbol first.");
                return;
            }

            if (!IsDrawingDocumentActive())
            {
                _panel?.SetStatusError("No active Drawing document.");
                return;
            }

            _panel?.SetStatusInsertMode();
            _interactionCtrl.EnterInsertMode();
        }

        private void OnInsertPointPicked(object sender, InsertPickEventArgs args)
        {
            Debug.WriteLine($"{LOG_PREFIX} Insert point picked: ({args.Position.X:F3}, {args.Position.Y:F3}) hasGeometry={args.PickedGeometry != null}");

            var newDef = _paletteCtrl.SelectedSymbol?.Definition;
            if (newDef == null)
            {
                _panel?.SetStatusError("Symbol selection lost. Please re-select.");
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

            // Đọc scale và rotation từ Properties panel
            double scale    = _panel?.InsertScale       ?? 1.0;
            double rotation = _panel?.InsertRotationRad ?? 0.0;

            Debug.WriteLine($"{LOG_PREFIX} Insert scale={scale:F3} rotation={rotation:F3}rad");

            var resolvedDef = ResolveDefinitionInDocument(doc, newDef);
            bool ok = _replaceService.InsertSymbol(sheet, resolvedDef, args.Position,
                                                   rotation, scale, args.PickedGeometry);

            if (ok)
            {
                _panel?.SetStatusSuccess(1);

                // Tự động vào lại insert mode → user insert liên tục cho đến khi nhấn ESC.
                // Dùng BeginInvoke để delay — cho Inventor xử lý xong transaction trước.
                // Nếu gọi EnterInsertMode() ngay → Inventor chưa sẵn sàng → crash.
                _panel?.Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    _panel?.SetStatusInsertMode();
                    _interactionCtrl.EnterInsertMode();
                    Debug.WriteLine($"{LOG_PREFIX} Re-enter insert mode (delayed).");
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
            else
                _panel?.SetStatusError("Insert failed. Check DebugView for details.");
        }

        private void OnInsertModeCancelled(object sender, EventArgs e)
        {
            Debug.WriteLine($"{LOG_PREFIX} Insert mode cancelled (ESC).");
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
            // Đếm số instance trên từng sheet để hiện breakdown trong confirm dialog
            var sheetCounts = new System.Collections.Generic.List<(string Name, int Count)>();
            int total = 0;

            foreach (Sheet s in doc.Sheets)
            {
                int n = CountMatchingOnSheet(s, oldDef);
                if (n > 0)
                    sheetCounts.Add((s.Name, n));
                total += n;
            }

            if (total == 0)
            {
                _panel?.SetStatusError($"No instances of '{oldDef.Name}' found in document.");
                return;
            }

            // Build message với breakdown per-sheet
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Replace {total} instance(s) of '{oldDef.Name}'");
            sb.AppendLine($"with '{newDef.Name}' across ALL sheets?");
            sb.AppendLine();
            foreach (var (name, count) in sheetCounts)
                sb.AppendLine($"  {name}: {count} instance(s)");
            sb.AppendLine();
            sb.Append("This can be undone with Ctrl+Z.");

            var answer = MessageBox.Show(
                sb.ToString(),
                "Replace All — All Sheets",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question);

            if (answer != DialogResult.OK)
            {
                _panel?.SetStatusIdle();
                return;
            }

            int replaced = _replaceService.ReplaceAllInDocument(doc, oldDef, newDef);
            if (replaced > 0)
                _panel?.SetStatusSuccess(replaced);
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
            _panel.ReplaceRequested                -= OnReplaceRequested;
            _panel.ReplaceAllCurrentSheetRequested -= OnReplaceAllCurrentSheetRequested;
            _panel.ReplaceAllAllSheetsRequested    -= OnReplaceAllAllSheetsRequested;
            _panel.InsertRequested                 -= OnInsertRequested;
            _panel = null;
        }

        /// <summary>
        /// Tìm definition cùng tên trong active document.
        /// Tránh cross-document reference gây E_INVALIDARG.
        /// </summary>
        private static SketchedSymbolDefinition ResolveDefinitionInDocument(
            DrawingDocument doc, SketchedSymbolDefinition libraryDef)
        {
            if (doc == null) return libraryDef;
            try
            {
                foreach (SketchedSymbolDefinition def in doc.SketchedSymbolDefinitions)
                    if (string.Equals(def.Name, libraryDef.Name, StringComparison.OrdinalIgnoreCase))
                        return def;
            }
            catch { }
            return libraryDef;
        }

        // ─── Nested enum ──────────────────────────────────────────────────────
        private enum ReplaceAllMode { None, CurrentSheet, AllSheets }
    }
}
