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

            var doc = oldSymbol.Parent?.Parent as DrawingDocument;
            if (doc == null)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI: Không lấy được DrawingDocument từ symbol.");
                return false;
            }

            var sheet = oldSymbol.Parent as Sheet;
            if (sheet == null)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI: Không lấy được Sheet từ symbol.");
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

            var tx = _app.TransactionManager.StartTransaction((_Document)doc, "Insert Symbol");
            try
            {
                var newSym = sheet.SketchedSymbols.Add(resolvedDef, position, rotation, scale, Type.Missing);
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
            double posX   = old.Position.X;
            double posY   = old.Position.Y;
            double rot    = old.Rotation;
            double scale  = old.Scale;
            var layer     = old.Layer;
            bool isStatic = old.Static;

            // Snapshot attribute values trước khi xóa
            var attrSnapshot = SnapshotPromptText(old);

            Debug.WriteLine($"{LOG_PREFIX}   Snapshot: pos=({posX:F3},{posY:F3}) rot={rot:F3} scale={scale:F3} attrs={attrSnapshot.Count}");

            // ── Bước 2: Resolve definition trong document hiện tại ──
            // newDef có thể là từ library document (khác document với sheet).
            // Inventor yêu cầu definition phải thuộc cùng document với sheet.
            // → Kiểm tra xem doc hiện tại đã có definition cùng tên chưa.
            var doc = sheet.Parent as DrawingDocument;
            var resolvedDef = ResolveDefinitionInDocument(doc, newDef);
            Debug.WriteLine($"{LOG_PREFIX}   Resolved definition: '{resolvedDef.Name}' (from {(resolvedDef == newDef ? "library" : "active doc")})");

            // ── Bước 3: Delete old instance ──
            old.Delete();
            Debug.WriteLine($"{LOG_PREFIX}   Deleted old instance.");

            // ── Bước 4: Tạo Point2d mới từ TransientGeometry (COM object cũ đã stale) ──
            var freshPos = _app.TransientGeometry.CreatePoint2d(posX, posY);

            // ── Bước 5: Insert new instance với cùng position/rotation/scale ──
            var newSym = sheet.SketchedSymbols.Add(
                resolvedDef,
                freshPos,
                rot,
                scale,
                Type.Missing);  // PromptStrings = không cần, set sau

            Debug.WriteLine($"{LOG_PREFIX}   Inserted new instance: '{newSym.Name}'.");

            // ── Bước 4: Restore Layer ──
            try
            {
                if (layer != null)
                    newSym.Layer = layer;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX}   CẢNH BÁO: Không restore được Layer: {ex.Message}");
            }

            // ── Bước 5: Copy attribute values (prompt text) ──
            RestorePromptText(newSym, attrSnapshot);
        }

        // ─── Attribute / Prompt text helpers ─────────────────────────────────

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
                        string text = symbol.GetResultText(tb);
                        string key  = !string.IsNullOrEmpty(tb.Text)
                            ? $"tb_{idx}_{tb.Text.Trim()}"
                            : $"tb_{idx}";
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
