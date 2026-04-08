using System;
using System.Collections.Generic;
using System.Diagnostics;
using Inventor;

namespace SymbolReplacer.Services
{
    /// <summary>
    /// Thực hiện replace SketchedSymbol trên bản vẽ Inventor.
    ///
    /// Quy trình replace mỗi instance:
    ///   1. Snapshot: Position, Rotation, Scale, Layer, AttributeSets
    ///   2. Delete old instance
    ///   3. Insert new instance với cùng Position + Rotation + Scale
    ///   4. Restore Layer
    ///   5. Copy attribute values (match by name)
    ///
    /// Tất cả bọc trong TransactionManager.StartTransaction() để Undo hoạt động.
    /// </summary>
    public class SymbolReplaceService : ISymbolReplaceService
    {
        // ─── Hằng số log ──────────────────────────────────────────────────────
        private const string LOG_PREFIX = "[SymbolReplaceService]";

        // ─── Fields ───────────────────────────────────────────────────────────
        private readonly Inventor.Application _app;

        // ─── Constructor ──────────────────────────────────────────────────────
        public SymbolReplaceService(Inventor.Application app)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
            Debug.WriteLine($"{LOG_PREFIX} Khởi tạo SymbolReplaceService.");
        }

        // ─── ISymbolReplaceService ────────────────────────────────────────────

        public bool ReplaceSingle(SketchedSymbol oldSymbol, SketchedSymbolDefinition newDef)
        {
            if (oldSymbol == null) throw new ArgumentNullException(nameof(oldSymbol));
            if (newDef    == null) throw new ArgumentNullException(nameof(newDef));

            Debug.WriteLine($"{LOG_PREFIX} ReplaceSingle: '{oldSymbol.Name}' → '{newDef.Name}'");

            var sheet = GetSheetFromSymbol(oldSymbol);
            if (sheet == null)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI: Không lấy được Sheet từ symbol.");
                return false;
            }

            var doc = sheet.Parent as DrawingDocument;
            if (doc == null)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI: Không lấy được DrawingDocument từ sheet.");
                return false;
            }

            var tx = _app.TransactionManager.StartTransaction((_Document)doc, "Replace Symbol");
            try
            {
                ReplaceOne(sheet, oldSymbol, newDef);
                tx.End();
                Debug.WriteLine($"{LOG_PREFIX} ReplaceSingle THÀNH CÔNG.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI ReplaceSingle: {ex.Message}");
                try { tx.Abort(); } catch { }
                return false;
            }
        }

        public int ReplaceAllOnSheet(Sheet sheet, SketchedSymbolDefinition oldDef, SketchedSymbolDefinition newDef)
        {
            if (sheet  == null) throw new ArgumentNullException(nameof(sheet));
            if (oldDef == null) throw new ArgumentNullException(nameof(oldDef));
            if (newDef == null) throw new ArgumentNullException(nameof(newDef));

            Debug.WriteLine($"{LOG_PREFIX} ReplaceAllOnSheet '{sheet.Name}': '{oldDef.Name}' → '{newDef.Name}'");

            var doc = sheet.Parent as DrawingDocument;
            if (doc == null)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI: Không lấy được DrawingDocument từ sheet.");
                return 0;
            }

            // Collect trước khi xóa (tránh COM iterator bị invalid)
            var targets = CollectMatchingSymbols(sheet, oldDef);
            if (targets.Count == 0)
            {
                Debug.WriteLine($"{LOG_PREFIX} Không tìm thấy instance nào trên sheet.");
                return 0;
            }

            var tx = _app.TransactionManager.StartTransaction((_Document)doc, "Replace All Symbols (Sheet)");
            int count = 0;
            try
            {
                foreach (var sym in targets)
                {
                    try
                    {
                        ReplaceOne(sheet, sym, newDef);
                        count++;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"{LOG_PREFIX} LỖI replace instance: {ex.Message}");
                    }
                }
                tx.End();
                Debug.WriteLine($"{LOG_PREFIX} ReplaceAllOnSheet THÀNH CÔNG: {count} instances.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI ReplaceAllOnSheet: {ex.Message}");
                try { tx.Abort(); } catch { }
                return 0;
            }
            return count;
        }

        public int ReplaceAllInDocument(DrawingDocument doc, SketchedSymbolDefinition oldDef, SketchedSymbolDefinition newDef)
        {
            if (doc    == null) throw new ArgumentNullException(nameof(doc));
            if (oldDef == null) throw new ArgumentNullException(nameof(oldDef));
            if (newDef == null) throw new ArgumentNullException(nameof(newDef));

            Debug.WriteLine($"{LOG_PREFIX} ReplaceAllInDocument: '{oldDef.Name}' → '{newDef.Name}'");

            // Collect tất cả (sheet, symbol) trước khi bắt đầu transaction
            var targets = new List<(Sheet sheet, SketchedSymbol sym)>();
            foreach (Sheet sheet in doc.Sheets)
            {
                var matches = CollectMatchingSymbols(sheet, oldDef);
                foreach (var sym in matches)
                    targets.Add((sheet, sym));
            }

            if (targets.Count == 0)
            {
                Debug.WriteLine($"{LOG_PREFIX} Không tìm thấy instance nào trong document.");
                return 0;
            }

            var tx = _app.TransactionManager.StartTransaction((_Document)doc, "Replace All Symbols (Document)");
            int count = 0;
            try
            {
                foreach (var (sheet, sym) in targets)
                {
                    try
                    {
                        ReplaceOne(sheet, sym, newDef);
                        count++;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"{LOG_PREFIX} LỖI replace instance: {ex.Message}");
                    }
                }
                tx.End();
                Debug.WriteLine($"{LOG_PREFIX} ReplaceAllInDocument THÀNH CÔNG: {count} instances.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI ReplaceAllInDocument: {ex.Message}");
                try { tx.Abort(); } catch { }
                return 0;
            }
            return count;
        }

        public bool InsertSymbol(Sheet sheet, SketchedSymbolDefinition definition, Point2d position,
                                 double rotation = 0.0, double scale = 1.0)
        {
            if (sheet      == null) throw new ArgumentNullException(nameof(sheet));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (position   == null) throw new ArgumentNullException(nameof(position));

            Debug.WriteLine($"{LOG_PREFIX} InsertSymbol: '{definition.Name}' tại ({position.X:F3},{position.Y:F3}) rot={rotation:F3}rad scale={scale:F3}");

            var doc = sheet.Parent as DrawingDocument;
            if (doc == null)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI: Không lấy được DrawingDocument từ sheet.");
                return false;
            }

            // Resolve definition trong active document (tránh cross-doc reference)
            var resolvedDef = ResolveDefinitionInDocument(doc, definition);

            // BUG 1 FIX: Truyền NameValueMap thay vì Type.Missing.
            // Khi definition có prompt fields, Add() ném E_INVALIDARG nếu nhận Type.Missing.
            var promptStrings = BuildPromptStrings(resolvedDef);

            var tx = _app.TransactionManager.StartTransaction((_Document)doc, "Insert Symbol");
            try
            {
                var newSym = sheet.SketchedSymbols.Add(resolvedDef, position, rotation, scale, promptStrings);
                tx.End();
                Debug.WriteLine($"{LOG_PREFIX} InsertSymbol THÀNH CÔNG: '{newSym.Name}'");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI InsertSymbol: {ex.Message}");
                try { tx.Abort(); } catch { }
                return false;
            }
        }

        // ─── Core replace logic ───────────────────────────────────────────────

        /// <summary>
        /// Replace một SketchedSymbol instance: snapshot → delete → insert → restore.
        /// Phải gọi trong phạm vi Transaction đang active.
        /// </summary>
        private void ReplaceOne(Sheet sheet, SketchedSymbol old, SketchedSymbolDefinition newDef)
        {
            // ── Bước 1: Snapshot toàn bộ properties cần preserve ──
            // QUAN TRỌNG: Lưu X/Y là double primitive, KHÔNG giữ COM object Point2d.
            // Sau khi old.Delete(), COM reference của Point2d sẽ bị stale → E_INVALIDARG.
            double posX  = old.Position.X;
            double posY  = old.Position.Y;
            double rot   = old.Rotation;
            double scale = old.Scale;
            var layer    = old.Layer;

            // BUG 2 FIX — Bước 1a: Capture Geometry thô từ GeometryIntent TRƯỚC khi Delete().
            // Sau old.Delete(), GeometryIntent COM wrapper bị stale (internal pointer bị hủy).
            // Nhưng Geometry mà nó trỏ tới (DrawingCurve, DrawingView...) KHÔNG bị xóa.
            // → Lưu Geometry thô + Intent, sau Delete() dùng Sheet.CreateGeometryIntent()
            //   để tạo lại GeometryIntent hợp lệ trỏ về cùng geometry.
            object attachedRawGeometry = null;
            object attachedRawIntent   = null;
            try
            {
                var gi = old._AttachedEntity;
                if (gi != null)
                {
                    attachedRawGeometry = gi.Geometry;
                    attachedRawIntent   = gi.Intent;
                }
            }
            catch { /* floating symbol — không có attachment */ }

            // Static = false: symbol tham gia cập nhật annotation (di chuyển cùng entity)
            // Static = true : symbol cố định tại sheet coordinates
            bool isStatic = false;
            try { isStatic = old.Static; }
            catch { }

            // Snapshot prompt text trước khi xóa
            var attrSnapshot = SnapshotPromptText(old);

            Debug.WriteLine($"{LOG_PREFIX}   Snapshot: pos=({posX:F3},{posY:F3}) rot={rot:F3} scale={scale:F3}" +
                            $" static={isStatic} hasAttach={attachedRawGeometry != null} attrs={attrSnapshot.Count}");

            // ── Bước 2: Resolve definition trong document hiện tại ──
            var doc = sheet.Parent as DrawingDocument;
            var resolvedDef = ResolveDefinitionInDocument(doc, newDef);
            Debug.WriteLine($"{LOG_PREFIX}   Resolved definition: '{resolvedDef.Name}'");

            // ── Bước 3: Delete old instance ──
            // Sau dòng này: attachedRawEntity vẫn valid (entity chưa bị xóa).
            old.Delete();
            Debug.WriteLine($"{LOG_PREFIX}   Deleted old instance.");

            // ── Bước 4: Tạo Point2d mới từ TransientGeometry (COM object cũ đã stale) ──
            var freshPos = _app.TransientGeometry.CreatePoint2d(posX, posY);

            // ── Bước 5: Build PromptStrings + Insert ──
            // BUG 1 FIX: Truyền NameValueMap thay vì Type.Missing.
            // Khi definition có prompt fields, Add() ném E_INVALIDARG nếu nhận Type.Missing.
            // Điền giá trị từ snapshot vào NameValueMap → prompt text được set ngay lúc tạo.
            var promptStrings = BuildPromptStrings(resolvedDef, attrSnapshot);
            var newSym = sheet.SketchedSymbols.Add(
                resolvedDef, freshPos, rot, scale, promptStrings);
            Debug.WriteLine($"{LOG_PREFIX}   Inserted: '{newSym.Name}'.");

            // ── Bước 6: Restore Static (trước _AttachedEntity) ──
            try { newSym.Static = isStatic; }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX}   CẢNH BÁO: Không restore Static: {ex.Message}");
            }

            // ── Bước 7: Tái tạo GeometryIntent và gán vào symbol mới ──
            // BUG 2 FIX: Không dùng lại GeometryIntent cũ (đã stale sau Delete()).
            // Dùng Sheet.CreateGeometryIntent(Geometry, Intent) để tạo mới từ raw geometry.
            if (attachedRawGeometry != null)
            {
                try
                {
                    var freshIntent = sheet.CreateGeometryIntent(attachedRawGeometry, attachedRawIntent);
                    newSym._AttachedEntity = freshIntent;
                    Debug.WriteLine($"{LOG_PREFIX}   GeometryIntent tái tạo thành công → symbol di chuyển cùng entity.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"{LOG_PREFIX}   CẢNH BÁO: Không restore _AttachedEntity: {ex.Message}");
                }
            }

            // ── Bước 8: Restore Layer ──
            try
            {
                if (layer != null)
                    newSym.Layer = layer;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX}   CẢNH BÁO: Không restore Layer: {ex.Message}");
            }

            // ── Bước 9: Restore prompt text (fallback nếu BuildPromptStrings key không khớp) ──
            RestorePromptText(newSym, attrSnapshot);
        }

        // ─── Private: View attachment helpers ────────────────────────────────

        /// <summary>
        /// Lấy Sheet từ SketchedSymbol.
        /// Trong Inventor 2023 API, SketchedSymbol.Parent luôn trả về Sheet
        /// (DrawingView không có collection SketchedSymbols riêng).
        /// </summary>
        private static Sheet GetSheetFromSymbol(SketchedSymbol symbol)
        {
            try { return symbol.Parent as Sheet; }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI GetSheetFromSymbol: {ex.Message}");
                return null;
            }
        }

        // ─── Attribute / Prompt text helpers ─────────────────────────────────

        /// <summary>
        /// Tạo argument PromptStrings cho SketchedSymbols.Add().
        ///
        /// Inventor API quirk:
        ///   - Symbol KHÔNG có prompt fields → phải truyền Type.Missing (không phải empty NVM)
        ///   - Symbol CÓ prompt fields       → phải truyền NameValueMap với entries đầy đủ
        ///   - Truyền empty NameValueMap khi không cần → E_INVALIDARG
        ///
        /// Trả về Type.Missing (boxed) hoặc NameValueMap đã populate.
        /// Key format trong NVM: 1-based index string ("1", "2", ...).
        /// </summary>
        private object BuildPromptStrings(SketchedSymbolDefinition def,
                                          Dictionary<string, string> snapshot = null)
        {
            try
            {
                var sketch = def?.Sketch;
                if (sketch == null) return Type.Missing;

                int totalBoxes = 0;
                try { totalBoxes = sketch.TextBoxes.Count; } catch { }

                if (totalBoxes == 0)
                {
                    Debug.WriteLine($"{LOG_PREFIX}   BuildPromptStrings: 0 TextBoxes → Type.Missing.");
                    return Type.Missing;
                }

                // Log toàn bộ TextBox content để hiểu format của prompt field
                int logIdx = 0;
                foreach (TextBox tb in sketch.TextBoxes)
                {
                    string rawText      = "<err>";
                    string formattedTxt = "<err>";
                    try { rawText      = tb.Text ?? "(null)"; }      catch (Exception ex) { rawText      = $"ERR:{ex.Message}"; }
                    try { formattedTxt = tb.FormattedText ?? "(null)"; } catch (Exception ex) { formattedTxt = $"ERR:{ex.Message}"; }
                    Debug.WriteLine($"{LOG_PREFIX}   TB[{logIdx}] Text='{rawText}' FormattedText='{formattedTxt}'");
                    logIdx++;
                }

                // Inventor lưu prompt field trong FormattedText:
                //   <Prompt ReadOnlyUniqueID='1'>DefaultText</Prompt>
                // NVM key   = ReadOnlyUniqueID value ("1", "2", ...)
                // NVM value = text cần điền (lấy từ snapshot khi replace, hoặc default text)
                var nvm = _app.TransientObjects.CreateNameValueMap();
                int nvmCount = 0;
                int tbIdx    = 0;

                foreach (TextBox tb in sketch.TextBoxes)
                {
                    try
                    {
                        string formatted = string.Empty;
                        try { formatted = tb.FormattedText ?? string.Empty; } catch { }

                        string uid = ExtractPromptUID(formatted);
                        if (uid == null)
                        {
                            tbIdx++;
                            continue;  // TextBox thường, không phải prompt field
                        }

                        // Lấy value từ snapshot (replace), hoặc default text từ TextBox.Text
                        string snapshotKey = $"tb_{tbIdx}_{(tb.Text?.Trim() ?? string.Empty)}";
                        string value       = tb.Text ?? string.Empty;  // default = existing text
                        snapshot?.TryGetValue(snapshotKey, out value);

                        nvm.Add(uid, value ?? string.Empty);
                        nvmCount++;
                        Debug.WriteLine($"{LOG_PREFIX}   TB[{tbIdx}] promptUID='{uid}' value='{value}'.");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"{LOG_PREFIX}   TB[{tbIdx}] LỖI: {ex.Message}");
                    }
                    tbIdx++;
                }

                if (nvmCount == 0)
                {
                    Debug.WriteLine($"{LOG_PREFIX}   BuildPromptStrings: không có prompt field → Type.Missing.");
                    return Type.Missing;
                }

                Debug.WriteLine($"{LOG_PREFIX}   BuildPromptStrings: {nvmCount} prompt entries.");
                return nvm;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX}   LỖI BuildPromptStrings: {ex.Message}");
                return Type.Missing;
            }
        }

        /// <summary>
        /// Trích xuất ReadOnlyUniqueID từ FormattedText của TextBox.
        /// Inventor lưu prompt field dưới dạng:
        ///   &lt;Prompt ReadOnlyUniqueID='N'&gt;DefaultText&lt;/Prompt&gt;
        /// NVM key khi gọi SketchedSymbols.Add() phải là giá trị N đó.
        /// Trả về null nếu TextBox không phải prompt field.
        /// </summary>
        private static string ExtractPromptUID(string formattedText)
        {
            if (string.IsNullOrWhiteSpace(formattedText)) return null;

            // Tìm ReadOnlyUniqueID='N' hoặc ReadOnlyUniqueID="N"
            const string marker = "ReadOnlyUniqueID=";
            int markerIdx = formattedText.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIdx < 0) return null;

            int quoteStart = markerIdx + marker.Length;
            if (quoteStart >= formattedText.Length) return null;

            char quote = formattedText[quoteStart];  // ' hoặc "
            if (quote != '\'' && quote != '"') return null;

            int valStart = quoteStart + 1;
            int valEnd   = formattedText.IndexOf(quote, valStart);
            if (valEnd <= valStart) return null;

            string uid = formattedText.Substring(valStart, valEnd - valStart).Trim();
            return string.IsNullOrEmpty(uid) ? null : uid;
        }

        /// <summary>
        /// Lấy toàn bộ prompt text từ symbol cũ.
        /// Key = TextBox.Name (nếu có) hoặc index.
        /// </summary>
        private Dictionary<string, string> SnapshotPromptText(SketchedSymbol symbol)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                // SketchedSymbol expose text qua GetResultText/SetPromptResultText
                // Iterate TextBoxes của definition để lấy tên
                var sketch = symbol.Definition?.Sketch;
                if (sketch == null) return result;

                int idx = 0;
                foreach (TextBox tb in sketch.TextBoxes)
                {
                    try
                    {
                        // Key phải khớp với snapshotKey trong BuildPromptStrings:
                        // "tb_{idx}_{tb.Text.Trim()}"
                        string text = symbol.GetResultText(tb);
                        string key  = $"tb_{idx}_{(tb.Text?.Trim() ?? string.Empty)}";
                        result[key] = text ?? string.Empty;
                    }
                    catch { }
                    idx++;
                }

                Debug.WriteLine($"{LOG_PREFIX}   SnapshotPromptText: {result.Count} entries.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX}   LỖI SnapshotPromptText: {ex.Message}");
            }
            return result;
        }

        /// <summary>
        /// Restore prompt text vào symbol mới.
        /// Match theo key (name + index) giữa old definition và new definition.
        /// </summary>
        private void RestorePromptText(SketchedSymbol newSymbol, Dictionary<string, string> snapshot)
        {
            if (snapshot.Count == 0) return;

            try
            {
                var sketch = newSymbol.Definition?.Sketch;
                if (sketch == null) return;

                int idx = 0;
                foreach (TextBox tb in sketch.TextBoxes)
                {
                    try
                    {
                        string key = !string.IsNullOrEmpty(tb.Text)
                            ? $"tb_{idx}_{tb.Text.Trim()}"
                            : $"tb_{idx}";

                        if (snapshot.TryGetValue(key, out string value) &&
                            !string.IsNullOrEmpty(value))
                        {
                            newSymbol.SetPromptResultText(tb, value);
                        }
                    }
                    catch { }
                    idx++;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX}   LỖI RestorePromptText: {ex.Message}");
            }
        }

        // ─── Private helpers ──────────────────────────────────────────────────

        /// <summary>
        /// Tìm definition trong document hiện tại theo tên.
        /// Nếu tìm thấy → trả về definition của document (tránh cross-doc reference).
        /// Nếu không tìm thấy → trả về definition gốc từ library (Inventor sẽ auto-import).
        /// </summary>
        private static SketchedSymbolDefinition ResolveDefinitionInDocument(
            DrawingDocument doc, SketchedSymbolDefinition libraryDef)
        {
            if (doc == null) return libraryDef;

            try
            {
                foreach (SketchedSymbolDefinition def in doc.SketchedSymbolDefinitions)
                {
                    if (string.Equals(def.Name, libraryDef.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.WriteLine($"[SymbolReplaceService] ResolveDefinition: tìm thấy '{def.Name}' trong active doc.");
                        return def;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SymbolReplaceService] ResolveDefinition LỖI: {ex.Message}");
            }

            // Không có trong active doc → dùng library def (Inventor sẽ tự import)
            Debug.WriteLine($"[SymbolReplaceService] ResolveDefinition: '{libraryDef.Name}' chưa có trong active doc, dùng library def.");
            return libraryDef;
        }

        /// <summary>Collect tất cả SketchedSymbol instance matching definition trên sheet.</summary>
        private static List<SketchedSymbol> CollectMatchingSymbols(
            Sheet sheet, SketchedSymbolDefinition targetDef)
        {
            var list = new List<SketchedSymbol>();
            try
            {
                foreach (SketchedSymbol sym in sheet.SketchedSymbols)
                {
                    try
                    {
                        // So sánh theo tên definition (cùng library hoặc bản vẽ hiện tại)
                        if (string.Equals(sym.Definition?.Name, targetDef.Name,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            list.Add(sym);
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SymbolReplaceService] LỖI CollectMatchingSymbols: {ex.Message}");
            }
            return list;
        }
    }
}
