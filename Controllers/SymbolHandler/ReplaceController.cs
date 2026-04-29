using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using Inventor;
using MCGInventorPlugin.Models.SymbolHandler;
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
        private readonly SelectionListener       _selectionListener;

        // ─── State ────────────────────────────────────────────────────────────
        private SymbolHandlerPanel _panel;
        /// <summary>Symbol instance đang được edit fields (pick từ bản vẽ).</summary>
        private SketchedSymbol _editingSymbol;

        // ─── Constructor ──────────────────────────────────────────────────────
        public ReplaceController(
            Inventor.Application   app,
            ISymbolReplaceService  replaceService,
            InteractionController  interactionCtrl,
            PaletteController      paletteCtrl,
            SelectionListener      selectionListener = null)
        {
            _app               = app             ?? throw new ArgumentNullException(nameof(app));
            _replaceService    = replaceService  ?? throw new ArgumentNullException(nameof(replaceService));
            _interactionCtrl   = interactionCtrl ?? throw new ArgumentNullException(nameof(interactionCtrl));
            _paletteCtrl       = paletteCtrl     ?? throw new ArgumentNullException(nameof(paletteCtrl));
            _selectionListener = selectionListener;

            // Đăng ký event từ InteractionController
            _interactionCtrl.SymbolPicked      += OnSymbolPicked;
            _interactionCtrl.PickModeCancelled += OnPickModeCancelled;
            _interactionCtrl.InsertPointPicked += OnInsertPointPicked;
            _interactionCtrl.InsertModeCancelled += OnInsertModeCancelled;

            // Đăng ký event từ SelectionListener (pick symbol bất kỳ lúc nào)
            if (_selectionListener != null)
            {
                _selectionListener.SymbolSelected    += OnDrawingSymbolSelected;
                _selectionListener.NonSymbolSelected += OnDrawingNonSymbolSelected;
            }

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
            if (_selectionListener != null)
            {
                _selectionListener.SymbolSelected    -= OnDrawingSymbolSelected;
                _selectionListener.NonSymbolSelected -= OnDrawingNonSymbolSelected;
            }
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
                // So sánh prompt fields trước replace
                int oldFieldCount = CountPromptFields(pickedSymbol.Definition);
                int newFieldCount = CountPromptFields(newDef);
                bool fieldsDiffer = oldFieldCount != newFieldCount;

                // Đọc prompt values từ DataGrid — ưu tiên hơn snapshot từ old symbol.
                // Quan trọng cho case old=0 fields → new=N fields: old không có attribute
                // để snapshot, user đã fill giá trị vào DataGrid của new symbol trước khi click Replace.
                Dictionary<int, string> promptValues = GetPromptValuesFromPanel();

                // Replace single
                bool ok = _replaceService.ReplaceSingle(pickedSymbol, newDef, promptValues);
                if (ok)
                {
                    if (fieldsDiffer)
                    {
                        string msg = (oldFieldCount == 0)
                            ? $"1 symbol replaced. New symbol has {newFieldCount} attribute(s) — click it to edit values."
                            : $"1 symbol replaced. Fields differ: old={oldFieldCount}, new={newFieldCount} — default values used for unmatched fields.";
                        _panel?.SetStatusWarning(msg);
                    }
                    else
                        _panel?.SetStatusSuccess(1);
                }
                else
                {
                    if (fieldsDiffer)
                    {
                        string oldName = "";
                        try { oldName = pickedSymbol.Definition?.Name ?? ""; } catch { }
                        string newName = "";
                        try { newName = newDef.Name ?? ""; } catch { }
                        _panel?.SetStatusError(
                            $"Cannot replace: '{oldName}' has {oldFieldCount} field(s), '{newName}' has {newFieldCount} field(s). Fields are incompatible.");
                    }
                    else
                        _panel?.SetStatusError("Replace failed. Check DebugView for details.");
                }
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

            // Đọc scale, rotation và checkbox options từ Properties panel
            double scale          = _panel?.InsertScale          ?? 1.0;
            double rotation       = _panel?.InsertRotationRad    ?? 0.0;
            bool   isStatic       = _panel?.InsertStatic         ?? false;
            bool   symbolClipping = _panel?.InsertSymbolClipping ?? false;
            bool   leaderEnabled  = _panel?.InsertLeaderEnabled  ?? true;
            bool   leaderVisible  = _panel?.InsertLeaderVisible  ?? true;

            Debug.WriteLine($"{LOG_PREFIX} Insert scale={scale:F3} rotation={rotation:F3}rad" +
                            $" static={isStatic} clipping={symbolClipping} leader={leaderEnabled} leaderVis={leaderVisible}");

            // Đọc prompt values từ DataGrid (nếu user đã edit)
            Dictionary<int, string> promptValues = GetPromptValuesFromPanel();

            // Service tự ensure definition có trong active doc (AddByCopy nếu cần)
            bool ok = _replaceService.InsertSymbol(sheet, newDef, args.Position,
                                                   rotation, scale, args.PickedGeometry,
                                                   isStatic, symbolClipping,
                                                   leaderEnabled, leaderVisible,
                                                   promptValues);

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

            bool fieldsDiffer = CountPromptFields(oldDef) != CountPromptFields(newDef);
            Dictionary<int, string> promptValues = GetPromptValuesFromPanel();
            int count = _replaceService.ReplaceAllOnSheet(sheet, oldDef, newDef, promptValues);
            if (count > 0)
            {
                if (fieldsDiffer)
                    _panel?.SetStatusWarning(
                        $"{count} symbol(s) replaced. Fields differ — default values used for unmatched fields.");
                else
                    _panel?.SetStatusSuccess(count);
            }
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

            bool fieldsDiffer = CountPromptFields(oldDef) != CountPromptFields(newDef);
            Dictionary<int, string> promptValues = GetPromptValuesFromPanel();
            int replaced = _replaceService.ReplaceAllInDocument(doc, oldDef, newDef, promptValues);
            if (replaced > 0)
            {
                if (fieldsDiffer)
                    _panel?.SetStatusWarning(
                        $"{replaced} symbol(s) replaced. Fields differ — default values used for unmatched fields.");
                else
                    _panel?.SetStatusSuccess(replaced);
            }
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

        // ─── Nested enum ──────────────────────────────────────────────────────
        // ─── Event handlers: SelectionListener (edit fields trực tiếp) ────────

        private void OnDrawingNonSymbolSelected(object sender, EventArgs e)
        {
            // User chọn đối tượng không phải symbol → clear DataGrid + editing state
            if (_interactionCtrl.IsActive) return;

            _editingSymbol = null;
            _panel?.SetPromptFields(null);
            Debug.WriteLine($"{LOG_PREFIX} Non-symbol selected → cleared fields.");
        }

        private void OnDrawingSymbolSelected(object sender, SketchedSymbol sym)
        {
            if (sym == null) return;

            // Không xử lý nếu đang trong pick/insert mode (InteractionController đang active)
            if (_interactionCtrl.IsActive) return;

            Debug.WriteLine($"{LOG_PREFIX} Drawing symbol selected: '{sym.Name}' → load fields.");

            _editingSymbol = sym;

            // Trích xuất prompt fields từ instance (actual values)
            var fields = ExtractPromptFieldsFromInstance(sym);
            _panel?.SetPromptFields(fields);

            // Đăng ký PropertyChanged trên mỗi field → realtime update
            foreach (var f in fields)
            {
                f.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(PromptFieldModel.Value) && _editingSymbol != null)
                    {
                        var field = (PromptFieldModel)s;
                        Debug.WriteLine($"{LOG_PREFIX} Realtime update: TB[{field.TextBoxIndex}] = '{field.Value}'");
                        _replaceService.UpdatePromptText(_editingSymbol, field.TextBoxIndex, field.Value);
                    }
                };
            }

            // Hiển thị tên symbol trên status bar
            _panel?.SetStatusInfo($"Editing fields: '{sym.Name}' ({fields.Count} field(s))");
        }

        /// <summary>
        /// Trích xuất prompt fields từ symbol instance (actual values trên bản vẽ).
        /// Dùng GetResultText() thay vì default value từ definition.
        /// </summary>
        private static List<PromptFieldModel> ExtractPromptFieldsFromInstance(SketchedSymbol symbol)
        {
            var fields = new List<PromptFieldModel>();
            if (symbol == null) return fields;

            try
            {
                var sketch = symbol.Definition?.Sketch;
                if (sketch == null) return fields;

                int idx = 0;
                foreach (Inventor.TextBox tb in sketch.TextBoxes)
                {
                    try
                    {
                        string formatted = string.Empty;
                        try { formatted = tb.FormattedText ?? string.Empty; } catch { }

                        // Chỉ lấy prompt fields (có ReadOnlyUniqueID)
                        if (formatted.IndexOf("ReadOnlyUniqueID", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            idx++;
                            continue;
                        }

                        string name = tb.Text?.Trim() ?? $"Field_{idx}";
                        // Đọc actual value từ instance
                        string actualValue = string.Empty;
                        try { actualValue = symbol.GetResultText(tb) ?? string.Empty; } catch { }

                        fields.Add(new PromptFieldModel
                        {
                            Name         = name,
                            Value        = actualValue,
                            TextBoxIndex = idx
                        });
                    }
                    catch { }
                    idx++;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ReplaceController] LỖI ExtractPromptFieldsFromInstance: {ex.Message}");
            }

            return fields;
        }

        /// <summary>
        /// Đọc prompt values từ DataGrid trong Properties panel.
        /// Trả null nếu panel không có field nào (old=non-attribute → snapshot rỗng → giữ default từ def mới).
        /// </summary>
        private Dictionary<int, string> GetPromptValuesFromPanel()
        {
            var fields = _panel?.GetPromptFields();
            if (fields == null || fields.Count == 0) return null;

            var result = new Dictionary<int, string>();
            foreach (var f in fields)
                result[f.TextBoxIndex] = f.Value ?? string.Empty;
            return result;
        }

        /// <summary>Đếm số prompt fields (TextBox có ReadOnlyUniqueID) trong definition.</summary>
        private static int CountPromptFields(SketchedSymbolDefinition def)
        {
            if (def == null) return 0;
            try
            {
                int count = 0;
                var sketch = def.Sketch;
                if (sketch == null) return 0;
                foreach (Inventor.TextBox tb in sketch.TextBoxes)
                {
                    try
                    {
                        string fmt = tb.FormattedText ?? string.Empty;
                        if (fmt.IndexOf("ReadOnlyUniqueID", StringComparison.OrdinalIgnoreCase) >= 0)
                            count++;
                    }
                    catch { }
                }
                return count;
            }
            catch { return 0; }
        }

        private enum ReplaceAllMode { None, CurrentSheet, AllSheets }
    }
}
