using System;
using System.Collections.Generic;
using System.Diagnostics;
using Inventor;

namespace MCGInventorPlugin.Services.SymbolHandler
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

        public bool ReplaceSingle(SketchedSymbol oldSymbol, SketchedSymbolDefinition newDef,
                                  Dictionary<int, string> promptValues = null)
        {
            if (oldSymbol == null) throw new ArgumentNullException(nameof(oldSymbol));
            if (newDef    == null) throw new ArgumentNullException(nameof(newDef));

            Debug.WriteLine($"{LOG_PREFIX} ReplaceSingle: '{oldSymbol.Name}' → '{newDef.Name}'" +
                            $" promptValues={promptValues?.Count ?? 0}");

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
                ReplaceOne(sheet, oldSymbol, newDef, promptValues);
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

        public int ReplaceAllOnSheet(Sheet sheet, SketchedSymbolDefinition oldDef, SketchedSymbolDefinition newDef,
                                     Dictionary<int, string> promptValues = null)
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
                        ReplaceOne(sheet, sym, newDef, promptValues);
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

        public int ReplaceAllInDocument(DrawingDocument doc, SketchedSymbolDefinition oldDef, SketchedSymbolDefinition newDef,
                                        Dictionary<int, string> promptValues = null)
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
                        ReplaceOne(sheet, sym, newDef, promptValues);
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
                                 double rotation = 0.0, double scale = 1.0, object attachGeometry = null,
                                 bool isStatic = false, bool symbolClipping = false,
                                 bool leaderEnabled = true, bool leaderVisible = true,
                                 Dictionary<int, string> promptValues = null)
        {
            if (sheet      == null) throw new ArgumentNullException(nameof(sheet));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (position   == null) throw new ArgumentNullException(nameof(position));

            Debug.WriteLine($"{LOG_PREFIX} InsertSymbol: '{definition.Name}' tại ({position.X:F3},{position.Y:F3})" +
                            $" rot={rotation:F3}rad scale={scale:F3} hasGeometry={attachGeometry != null}");

            var doc = sheet.Parent as DrawingDocument;
            if (doc == null)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI: Không lấy được DrawingDocument từ sheet.");
                return false;
            }

            // Đảm bảo definition đã tồn tại trong active document (AddByCopy nếu chưa)
            var resolvedDef = EnsureDefinitionInDocument(doc, definition);

            // Nếu có promptValues từ DataGrid → convert sang snapshot format cho BuildPromptStrings
            Dictionary<string, string> insertSnapshot = null;
            if (promptValues != null && promptValues.Count > 0)
            {
                insertSnapshot = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    int tbIdx = 0;
                    foreach (TextBox tb in resolvedDef.Sketch.TextBoxes)
                    {
                        if (promptValues.TryGetValue(tbIdx, out string val))
                        {
                            string key = $"tb_{tbIdx}_{(tb.Text?.Trim() ?? string.Empty)}";
                            insertSnapshot[key] = val;
                            Debug.WriteLine($"{LOG_PREFIX}   PromptValue override: TB[{tbIdx}] = '{val}'");
                        }
                        tbIdx++;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"{LOG_PREFIX}   LỖI build insertSnapshot: {ex.Message}");
                }
            }
            var promptStrings = BuildPromptStrings(resolvedDef, insertSnapshot);

            var tx = _app.TransactionManager.StartTransaction((_Document)doc, "Insert Symbol");
            try
            {
                var newSym = AddSymbolWithFallback(sheet, resolvedDef, position, rotation, scale, promptStrings);
                Debug.WriteLine($"{LOG_PREFIX}   Add() THÀNH CÔNG: '{newSym.Name}'");

                // Set Static — ẩn/hiện grip points (scale + rotate)
                try { newSym.Static = isStatic; }
                catch (Exception exS) { Debug.WriteLine($"{LOG_PREFIX}   CẢNH BÁO: Không set Static: {exS.Message}"); }

                // Set SymbolClipping — trim annotations bên ngoài
                try { newSym.SymbolClipping = symbolClipping; }
                catch (Exception exC) { Debug.WriteLine($"{LOG_PREFIX}   CẢNH BÁO: Không set SymbolClipping: {exC.Message}"); }

                // Leader: chỉ tạo khi leaderEnabled = true
                if (leaderEnabled)
                {
                    // Tìm geometry để attach leader:
                    //   - attachGeometry != null → dùng trực tiếp (từ SelectEvents)
                    //   - attachGeometry == null → tự tìm DrawingCurve gần nhất (MouseEvents + snap)
                    //     Nếu tìm được curve gần (< 0.5cm) → attached; không → floating
                    object geoToAttach = attachGeometry;
                    if (geoToAttach == null)
                    {
                        var nearestCurve = FindNearestDrawingCurve(sheet, position);
                        if (nearestCurve != null)
                        {
                            geoToAttach = nearestCurve;
                            Debug.WriteLine($"{LOG_PREFIX}   Auto-resolved DrawingCurve gần snap position.");
                        }
                        else
                        {
                            Debug.WriteLine($"{LOG_PREFIX}   Không tìm được geometry gần → floating insert.");
                        }
                    }

                    if (geoToAttach != null)
                    {
                        AttachLeaderToGeometry(newSym, sheet, geoToAttach, position, leaderVisible);
                    }
                }
                else
                {
                    Debug.WriteLine($"{LOG_PREFIX}   Leader disabled → floating insert (không tạo leader).");
                }

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

        // ─── Update prompt text (edit fields trực tiếp) ──────────────────────

        public bool UpdatePromptText(SketchedSymbol symbol, int textBoxIndex, string value)
        {
            if (symbol == null) throw new ArgumentNullException(nameof(symbol));

            Debug.WriteLine($"{LOG_PREFIX} UpdatePromptText: '{symbol.Name}' TB[{textBoxIndex}] = '{value}'");

            var sheet = GetSheetFromSymbol(symbol);
            var doc = sheet?.Parent as DrawingDocument;
            if (doc == null)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI: Không lấy được DrawingDocument.");
                return false;
            }

            var tx = _app.TransactionManager.StartTransaction((_Document)doc, "Edit Field Text");
            try
            {
                var sketch = symbol.Definition?.Sketch;
                if (sketch == null)
                {
                    Debug.WriteLine($"{LOG_PREFIX} LỖI: Definition không có Sketch.");
                    tx.Abort();
                    return false;
                }

                int idx = 0;
                foreach (TextBox tb in sketch.TextBoxes)
                {
                    if (idx == textBoxIndex)
                    {
                        symbol.SetPromptResultText(tb, value ?? string.Empty);
                        Debug.WriteLine($"{LOG_PREFIX}   SetPromptResultText TB[{idx}] = '{value}' → OK.");
                        tx.End();
                        return true;
                    }
                    idx++;
                }

                Debug.WriteLine($"{LOG_PREFIX}   CẢNH BÁO: TextBox index {textBoxIndex} không tìm thấy.");
                tx.Abort();
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI UpdatePromptText: {ex.Message}");
                try { tx.Abort(); } catch { }
                return false;
            }
        }

        // ─── Core replace logic ───────────────────────────────────────────────

        /// <summary>
        /// Snapshot dữ liệu leader của một SketchedSymbol.
        /// Tất cả COM object được tách ra thành primitive (double) để tránh stale reference sau Delete().
        /// </summary>
        private struct LeaderLeafSnapshot
        {
            public double PosX;
            public double PosY;
            public object Geometry;  // COM object — vẫn valid sau Delete() symbol
            public object Intent;    // COM object (Int32/Double/__ComObject) — vẫn valid sau Delete()
        }

        private struct LeaderSnapshot
        {
            public bool HasLeader;
            public LeaderLeafSnapshot[] Leaves;  // thường chỉ có 1 leaf
        }

        /// <summary>
        /// Tạo leader bám vào geometry cho symbol vừa insert.
        /// Thử nhiều intent format cho CreateGeometryIntent (Point2d → Type.Missing → 1-arg).
        /// Nếu tất cả fail → symbol giữ nguyên floating, log cảnh báo.
        /// </summary>
        private void AttachLeaderToGeometry(SketchedSymbol newSym, Sheet sheet,
                                             object geometry, Point2d position,
                                             bool leaderVisible = true)
        {
            string geoType = "<unknown>";
            try { geoType = geometry.GetType().Name; } catch { }
            Debug.WriteLine($"{LOG_PREFIX}   AttachLeader: geometry={geoType} pos=({position.X:F3},{position.Y:F3})");

            // Xác định COM type thực sự qua ObjectTypeEnum (.Type property)
            int comTypeEnum = 0;
            try
            {
                comTypeEnum = (int)geometry.GetType().InvokeMember("Type",
                    System.Reflection.BindingFlags.GetProperty, null, geometry, null);
                Debug.WriteLine($"{LOG_PREFIX}   COM ObjectTypeEnum = {comTypeEnum}");
            }
            catch { Debug.WriteLine($"{LOG_PREFIX}   Không đọc được .Type property."); }

            // Resolve geometry → cần DrawingCurve hoặc SketchLine (probe xác nhận cả 2 work)
            // Probe xác nhận:
            //   - kDrawingCurveSegmentObject → cần .Parent (DrawingCurve)
            //   - kDrawingSketchObject (117443328) → SelectEvents trả về DrawingSketch container
            //     khi pick sketch line → cần tìm SketchLine gần nhất bên trong
            //   - SketchLine → dùng trực tiếp
            //   - DrawingCurve → dùng trực tiếp
            object resolvedGeo = geometry;

            if (comTypeEnum == 117443328)  // kDrawingSketchObject
            {
                // SelectEvents trả về DrawingSketch (container) thay vì SketchLine cụ thể.
                // SketchLine dùng sketch coordinates (model units) ≠ sheet coordinates.
                // → Tìm DrawingCurve gần nhất trong view bằng sheet coordinates (đúng hệ tọa độ).
                Debug.WriteLine($"{LOG_PREFIX}   DrawingSketch detected → tìm DrawingCurve gần nhất (sheet coords)...");
                resolvedGeo = FindNearestDrawingCurve(sheet, position) ?? geometry;
            }
            else if (comTypeEnum == 117478144)  // kDrawingCurveSegmentObject
            {
                // DrawingCurveSegment → cần Parent (DrawingCurve)
                try
                {
                    var seg = (DrawingCurveSegment)geometry;
                    resolvedGeo = seg.Parent;
                    Debug.WriteLine($"{LOG_PREFIX}   DrawingCurveSegment → Parent DrawingCurve.");
                }
                catch
                {
                    // COM cast fail → thử InvokeMember
                    try
                    {
                        resolvedGeo = geometry.GetType().InvokeMember("Parent",
                            System.Reflection.BindingFlags.GetProperty, null, geometry, null) ?? geometry;
                        Debug.WriteLine($"{LOG_PREFIX}   DrawingCurveSegment (InvokeMember) → Parent.");
                    }
                    catch { Debug.WriteLine($"{LOG_PREFIX}   CẢNH BÁO: Không lấy được Parent."); }
                }
            }
            else
            {
                Debug.WriteLine($"{LOG_PREFIX}   TypeEnum={comTypeEnum} → dùng trực tiếp.");
            }

            // Tạo GeometryIntent — thử Point2d intent trước, fallback Type.Missing
            GeometryIntent gi = null;
            try
            {
                gi = sheet.CreateGeometryIntent(resolvedGeo, position);
                Debug.WriteLine($"{LOG_PREFIX}   CreateGeometryIntent(resolvedGeo, Point2d) → OK");
            }
            catch (Exception ex1)
            {
                Debug.WriteLine($"{LOG_PREFIX}   CreateGeometryIntent(resolvedGeo, Point2d) → FAIL: {ex1.Message}");
                try
                {
                    gi = sheet.CreateGeometryIntent(resolvedGeo, Type.Missing);
                    Debug.WriteLine($"{LOG_PREFIX}   CreateGeometryIntent(resolvedGeo, Missing) → OK");
                }
                catch (Exception ex2)
                {
                    Debug.WriteLine($"{LOG_PREFIX}   CreateGeometryIntent FAIL cả 2 → symbol floating: {ex2.Message}");
                    return;
                }
            }

            // AddLeader: [Point2d, GeometryIntent] — probe xác nhận
            // Retry logic: thử geometry trực tiếp → nếu fail, thử .Parent
            // (COM __ComObject không cast được bằng 'is' → phải thử runtime)
            if (!TryAddLeader(newSym, sheet, resolvedGeo, position, leaderVisible))
            {
                // Lần 2: thử .Parent (DrawingCurveSegment.Parent = DrawingCurve)
                object parentGeo = null;
                try
                {
                    parentGeo = resolvedGeo.GetType().InvokeMember("Parent",
                        System.Reflection.BindingFlags.GetProperty, null, resolvedGeo, null);
                }
                catch { }

                if (parentGeo != null)
                {
                    Debug.WriteLine($"{LOG_PREFIX}   Retry với Parent geometry...");
                    TryAddLeader(newSym, sheet, parentGeo, position, leaderVisible);
                }
                else
                {
                    Debug.WriteLine($"{LOG_PREFIX}   Không có Parent → symbol floating.");
                }
            }
        }

        /// <summary>
        /// Tìm DrawingCurve gần nhất với position (sheet coordinates) từ tất cả DrawingViews.
        /// Khi SelectEvents trả về DrawingSketch (container), cần resolve ra DrawingCurve cụ thể
        /// vì AddLeader chấp nhận DrawingCurve nhưng KHÔNG chấp nhận DrawingSketch.
        /// DrawingCurveSegment.StartPoint/EndPoint dùng sheet coordinates → so sánh chính xác.
        /// </summary>
        /// <summary>Ngưỡng khoảng cách tối đa (cm) để coi là "gần" geometry → attached insert.</summary>
        private const double ATTACH_DISTANCE_THRESHOLD = 0.5;

        private object FindNearestDrawingCurve(Sheet sheet, Point2d position)
        {
            try
            {
                object nearest = null;
                double minDist = double.MaxValue;

                foreach (DrawingView view in sheet.DrawingViews)
                {
                    foreach (DrawingCurve curve in view.DrawingCurves)
                    {
                        try
                        {
                            foreach (DrawingCurveSegment seg in curve.Segments)
                            {
                                try
                                {
                                    double sx = seg.StartPoint.X, sy = seg.StartPoint.Y;
                                    double ex = seg.EndPoint.X,   ey = seg.EndPoint.Y;
                                    double dist = PointToSegmentDistance(position.X, position.Y, sx, sy, ex, ey);

                                    if (dist < minDist)
                                    {
                                        minDist = dist;
                                        nearest = curve;  // DrawingCurve, KHÔNG phải segment
                                    }
                                }
                                catch { }
                            }
                        }
                        catch { }
                    }
                }

                if (nearest != null && minDist <= ATTACH_DISTANCE_THRESHOLD)
                {
                    Debug.WriteLine($"{LOG_PREFIX}   FindNearestDrawingCurve: dist={minDist:F4} ≤ {ATTACH_DISTANCE_THRESHOLD} → attached.");
                    return nearest;
                }

                Debug.WriteLine($"{LOG_PREFIX}   FindNearestDrawingCurve: {(nearest != null ? $"dist={minDist:F4} > threshold" : "không có curve")} → floating.");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX}   FindNearestDrawingCurve LỖI: {ex.Message}");
                return null;
            }
        }

        /// <summary>Khoảng cách từ điểm (px,py) đến đoạn thẳng (sx,sy)→(ex,ey).</summary>
        private static double PointToSegmentDistance(double px, double py,
                                                     double sx, double sy,
                                                     double ex, double ey)
        {
            double dx = ex - sx, dy = ey - sy;
            double lenSq = dx * dx + dy * dy;
            if (lenSq < 1e-20) return Math.Sqrt((px - sx) * (px - sx) + (py - sy) * (py - sy));

            double t = Math.Max(0, Math.Min(1, ((px - sx) * dx + (py - sy) * dy) / lenSq));
            double projX = sx + t * dx, projY = sy + t * dy;
            return Math.Sqrt((px - projX) * (px - projX) + (py - projY) * (py - projY));
        }

        /// <summary>
        /// Thử tạo GeometryIntent + AddLeader cho 1 geometry cụ thể.
        /// Trả về true nếu thành công, false nếu fail (caller sẽ retry với geometry khác).
        /// </summary>
        private bool TryAddLeader(SketchedSymbol newSym, Sheet sheet, object geo, Point2d position,
                                  bool leaderVisible = true)
        {
            GeometryIntent gi = null;
            try
            {
                gi = sheet.CreateGeometryIntent(geo, position);
            }
            catch
            {
                try { gi = sheet.CreateGeometryIntent(geo, Type.Missing); }
                catch (Exception ex)
                {
                    Debug.WriteLine($"{LOG_PREFIX}   TryAddLeader: CreateGeometryIntent FAIL: {ex.Message}");
                    return false;
                }
            }

            try
            {
                var pts = _app.TransientObjects.CreateObjectCollection();
                pts.Add(position);
                pts.Add(gi);
                newSym.Leader.AddLeader(pts);

                // Reset Position về đúng vị trí pick (AddLeader có thể dời symbol)
                newSym.Position = position;

                // Set leader properties — dùng param thay hardcode
                try { newSym.LeaderVisible  = leaderVisible; } catch { }
                try { newSym.LeaderClipping = true;          } catch { }
                // Filled solid đen giống dimension arrows
                try { newSym.Leader.ArrowheadType = ArrowheadTypeEnum.kFilledArrowheadType; } catch { }

                Debug.WriteLine($"{LOG_PREFIX}   TryAddLeader THÀNH CÔNG. HasRootNode={newSym.Leader.HasRootNode}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX}   TryAddLeader AddLeader FAIL: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Snapshot Leader structure từ symbol trước khi Delete().
        /// Kết quả chứa leaf positions và GeometryIntent components dưới dạng primitive / raw COM ref.
        /// </summary>
        private static LeaderSnapshot SnapshotLeader(SketchedSymbol symbol)
        {
            var snap = new LeaderSnapshot();
            try
            {
                snap.HasLeader = symbol.Leader.HasRootNode;
                if (!snap.HasLeader) return snap;

                var leaves = new System.Collections.Generic.List<LeaderLeafSnapshot>();
                foreach (LeaderNode leaf in symbol.Leader.AllLeafNodes)
                {
                    var ls = new LeaderLeafSnapshot();
                    try { ls.PosX = leaf.Position.X; ls.PosY = leaf.Position.Y; } catch { }
                    try
                    {
                        var gi = leaf.AttachedEntity;
                        if (gi != null)
                        {
                            ls.Geometry = gi.Geometry;
                            ls.Intent   = gi.Intent;
                        }
                    }
                    catch { }
                    leaves.Add(ls);
                }
                snap.Leaves = leaves.ToArray();
                Debug.WriteLine($"{LOG_PREFIX}   SnapshotLeader: {snap.Leaves.Length} leaf(s).");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX}   SnapshotLeader LỖI: {ex.Message}");
                snap.HasLeader = false;
            }
            return snap;
        }

        /// <summary>
        /// Restore Leader lên symbol mới sau khi Add().
        ///
        /// Inventor API (xác nhận qua probe):
        ///   - Sau Add(), symbol có HasRootNode=False (không có leader).
        ///   - AddLeader(ObjectCollection) PHẢI nhận [Point2d, GeometryIntent]:
        ///       pts[0] = Point2d tại vị trí leaf (đầu mũi tên)
        ///       pts[1] = GeometryIntent tạo bởi sheet.CreateGeometryIntent()
        ///   - AddLeader([GeometryIntent]) đơn lẻ → E_FAIL.
        ///   - symbol._AttachedEntity = value → "Value does not fall within the expected range".
        /// </summary>
        private void RestoreLeader(SketchedSymbol newSym, LeaderSnapshot snap, Sheet sheet,
                                   bool leaderVisible = true)
        {
            if (!snap.HasLeader || snap.Leaves == null || snap.Leaves.Length == 0) return;

            foreach (var leaf in snap.Leaves)
            {
                if (leaf.Geometry == null) continue;
                try
                {
                    // Tạo GeometryIntent mới — COM object cũ của leaf đã stale sau Delete()
                    var freshIntent = sheet.CreateGeometryIntent(leaf.Geometry, leaf.Intent);

                    // ObjectCollection: [Point2d tại vị trí leaf, GeometryIntent]
                    var pts = _app.TransientObjects.CreateObjectCollection();
                    pts.Add(_app.TransientGeometry.CreatePoint2d(leaf.PosX, leaf.PosY));
                    pts.Add(freshIntent);

                    newSym.Leader.AddLeader(pts);

                    // Set leader properties — restore từ symbol cũ
                    try { newSym.LeaderVisible  = leaderVisible; } catch { }
                    try { newSym.LeaderClipping = true;          } catch { }

                    Debug.WriteLine($"{LOG_PREFIX}   RestoreLeader: leaf ({leaf.PosX:F3},{leaf.PosY:F3}) → THÀNH CÔNG. HasRootNode={newSym.Leader.HasRootNode}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"{LOG_PREFIX}   RestoreLeader: leaf ({leaf.PosX:F3},{leaf.PosY:F3}) → LỖI: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Replace một SketchedSymbol instance: snapshot → delete → insert → restore.
        /// Phải gọi trong phạm vi Transaction đang active.
        /// </summary>
        /// <param name="promptValues">Optional: giá trị attribute từ DataGrid (TextBoxIndex → value) —
        /// override snapshot từ old symbol. Cần thiết khi cross-def replace (old không có attribute, new có).</param>
        private void ReplaceOne(Sheet sheet, SketchedSymbol old, SketchedSymbolDefinition newDef,
                                Dictionary<int, string> promptValues = null)
        {
            // ── Bước 1: Snapshot toàn bộ properties cần preserve ──
            // QUAN TRỌNG: Lưu X/Y là double primitive, KHÔNG giữ COM object Point2d.
            // Sau khi old.Delete(), COM reference của Point2d sẽ bị stale → E_INVALIDARG.
            double posX  = old.Position.X;
            double posY  = old.Position.Y;
            double rot   = old.Rotation;
            double scale = old.Scale;
            var layer    = old.Layer;

            // Smart rotation: khi replace cross-definition (def khác nhau), new symbol có
            // natural-up riêng — copy rotation từ old thường gây bị "ngược". Reset về 0
            // để symbol mới hiện theo đúng orientation trong definition của nó.
            // Same-definition replace (update cùng loại) → giữ rotation.
            string oldDefName = null;
            try { oldDefName = old.Definition?.Name; } catch { }
            string newDefName = null;
            try { newDefName = newDef?.Name; } catch { }
            bool isCrossDef = !string.Equals(oldDefName ?? "", newDefName ?? "", StringComparison.OrdinalIgnoreCase);
            if (isCrossDef)
            {
                Debug.WriteLine($"{LOG_PREFIX}   Cross-def replace ('{oldDefName}' → '{newDefName}'): reset rotation {rot:F3} → 0.");
                rot = 0;
            }

            // Bước 1a: Snapshot Leader (attachment + leader structure).
            // Probe đã xác nhận:
            //   - symbol._AttachedEntity = null với hầu hết symbols (dùng Leader mechanism)
            //   - symbol._AttachedEntity setter ném "Value does not fall within expected range"
            //   - Cơ chế đúng: Leader.AllLeafNodes[i].AttachedEntity + AddLeader([Point2d, GeometryIntent])
            var leaderSnap = SnapshotLeader(old);

            // Bước 1b: Static, SymbolClipping, LeaderVisible flags
            bool isStatic = false;
            try { isStatic = old.Static; } catch { }

            bool oldSymbolClipping = false;
            try { oldSymbolClipping = old.SymbolClipping; } catch { }

            bool oldLeaderVisible = true;
            try { oldLeaderVisible = old.LeaderVisible; } catch { }

            // Bước 1c: Snapshot prompt text trước khi xóa
            var attrSnapshot = SnapshotPromptText(old);

            Debug.WriteLine($"{LOG_PREFIX}   Snapshot: pos=({posX:F3},{posY:F3}) rot={rot:F3} scale={scale:F3}" +
                            $" static={isStatic} hasLeader={leaderSnap.HasLeader} attrs={attrSnapshot.Count}");

            // ── Bước 2: Đảm bảo definition tồn tại trong document hiện tại (AddByCopy nếu chưa) ──
            var doc = sheet.Parent as DrawingDocument;
            var resolvedDef = EnsureDefinitionInDocument(doc, newDef);
            Debug.WriteLine($"{LOG_PREFIX}   Resolved definition: '{resolvedDef.Name}'");

            // ── Bước 3: Delete old instance ──
            // Sau dòng này: leaf.Geometry / leaf.Intent vẫn valid (geometry chưa bị xóa).
            old.Delete();
            Debug.WriteLine($"{LOG_PREFIX}   Deleted old instance.");

            // ── Bước 4: Tạo Point2d mới từ TransientGeometry (COM object cũ đã stale) ──
            var freshPos = _app.TransientGeometry.CreatePoint2d(posX, posY);

            // ── Bước 5: Build PromptStrings + Insert ──
            // Merge ưu tiên: promptValues (DataGrid) > attrSnapshot (từ old symbol).
            // Key format phải khớp NEW def vì BuildPromptStrings iterate resolvedDef.Sketch.TextBoxes.
            // attrSnapshot từ old có key theo OLD def → chỉ match khi same-def.
            // promptValues có key theo NEW def (do DataGrid load từ new def) → luôn match.
            var effectiveSnapshot = new Dictionary<string, string>(attrSnapshot, StringComparer.OrdinalIgnoreCase);
            if (promptValues != null && promptValues.Count > 0)
            {
                try
                {
                    int tbIdx = 0;
                    foreach (TextBox tb in resolvedDef.Sketch.TextBoxes)
                    {
                        if (promptValues.TryGetValue(tbIdx, out string val))
                        {
                            string key = $"tb_{tbIdx}_{(tb.Text?.Trim() ?? string.Empty)}";
                            effectiveSnapshot[key] = val;
                            Debug.WriteLine($"{LOG_PREFIX}   PromptValue override (Replace): TB[{tbIdx}] = '{val}'");
                        }
                        tbIdx++;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"{LOG_PREFIX}   LỖI merge promptValues: {ex.Message}");
                }
            }

            // Dùng AddSymbolWithFallback để handle case old=0 fields → new=N fields:
            //   - Thử Add với string[] từ BuildPromptStrings (default text của new def)
            //   - Nếu fail (E_INVALIDARG) → retry Add với Type.Missing rồi SetPromptResultText thủ công
            var promptStrings = BuildPromptStrings(resolvedDef, effectiveSnapshot);
            var newSym = AddSymbolWithFallback(sheet, resolvedDef, freshPos, rot, scale, promptStrings);
            Debug.WriteLine($"{LOG_PREFIX}   Inserted: '{newSym.Name}'.");

            // ── Bước 6: Restore Static + SymbolClipping ──
            try { newSym.Static = isStatic; }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX}   CẢNH BÁO: Không restore Static: {ex.Message}");
            }
            try { newSym.SymbolClipping = oldSymbolClipping; }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX}   CẢNH BÁO: Không restore SymbolClipping: {ex.Message}");
            }

            // ── Bước 7: Restore Leader (attachment + visual leader line) ──
            // Dùng AddLeader([Point2d, GeometryIntent]) thay vì _AttachedEntity setter.
            RestoreLeader(newSym, leaderSnap, sheet, oldLeaderVisible);

            // ── Bước 7a: Reset Position sau AddLeader ──
            // AddLeader() dời symbol body (ROOT) về vị trí leaf → insertion point bị lệch.
            // Set lại Position để insertion point trở về đúng vị trí cũ.
            if (leaderSnap.HasLeader)
            {
                try
                {
                    newSym.Position = _app.TransientGeometry.CreatePoint2d(posX, posY);
                    Debug.WriteLine($"{LOG_PREFIX}   Position reset về ({posX:F3},{posY:F3}) sau AddLeader.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"{LOG_PREFIX}   CẢNH BÁO: Không reset Position: {ex.Message}");
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
            // Dùng effectiveSnapshot để promptValues từ DataGrid thắng snapshot từ old.
            RestorePromptText(newSym, effectiveSnapshot);
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

                // Inventor COM expect PromptStrings là string[] (SAFEARRAY of BSTR).
                // NameValueMap (VT_DISPATCH) bị reject với E_INVALIDARG.
                // VBA convention: Dim p(n) As String → p(0)="val1", p(1)="val2", ...
                // → build List<string> theo thứ tự TextBox có prompt UID.
                var values = new System.Collections.Generic.List<string>();
                int tbIdx  = 0;

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
                        // BUG FIX: TryGetValue(out) ghi đè value = null khi key không tồn tại
                        //          → phải check return trước khi ghi đè
                        string snapshotKey = $"tb_{tbIdx}_{(tb.Text?.Trim() ?? string.Empty)}";
                        string value       = tb.Text ?? string.Empty;
                        if (snapshot != null && snapshot.TryGetValue(snapshotKey, out string snapVal))
                            value = snapVal ?? string.Empty;

                        values.Add(value ?? string.Empty);
                        Debug.WriteLine($"{LOG_PREFIX}   TB[{tbIdx}] promptUID='{uid}' value='{value}'.");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"{LOG_PREFIX}   TB[{tbIdx}] LỖI: {ex.Message}");
                    }
                    tbIdx++;
                }

                if (values.Count == 0)
                {
                    Debug.WriteLine($"{LOG_PREFIX}   BuildPromptStrings: không có prompt field → Type.Missing.");
                    return Type.Missing;
                }

                Debug.WriteLine($"{LOG_PREFIX}   BuildPromptStrings: {values.Count} prompt entries → string[].");
                return values.ToArray();
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
        /// Đảm bảo definition tồn tại trong active document.
        ///   - Nếu đã có → trả về definition của document (tránh cross-doc reference)
        ///   - Nếu chưa có → copy từ library vào active doc qua AddByCopy()
        ///
        /// Quan trọng: `sheet.SketchedSymbols.Add(libraryDef)` với def từ document KHÁC
        /// → E_INVALIDARG. Phải import definition vào active doc trước.
        /// </summary>
        private SketchedSymbolDefinition EnsureDefinitionInDocument(
            DrawingDocument doc, SketchedSymbolDefinition libraryDef)
        {
            if (doc == null || libraryDef == null) return libraryDef;

            // Bước 1: kiểm tra đã có trong active doc chưa
            try
            {
                foreach (SketchedSymbolDefinition def in doc.SketchedSymbolDefinitions)
                {
                    if (string.Equals(def.Name, libraryDef.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.WriteLine($"{LOG_PREFIX}   EnsureDefinition: '{def.Name}' đã có trong active doc.");
                        return def;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX}   EnsureDefinition iterate LỖI: {ex.Message}");
            }

            // Bước 2: chưa có → import từ library qua SketchedSymbolDefinition.CopyTo(doc).
            // Method này xác nhận qua probe là API đúng trên Inventor 2023.
            // Interop wrapper KHÔNG expose CopyTo → phải dùng COM late-binding.
            try
            {
                var copied = libraryDef.GetType().InvokeMember(
                    "CopyTo",
                    System.Reflection.BindingFlags.InvokeMethod,
                    null,
                    libraryDef,
                    new object[] { doc });
                if (copied is SketchedSymbolDefinition def)
                {
                    Debug.WriteLine($"{LOG_PREFIX}   EnsureDefinition: CopyTo('{libraryDef.Name}') → '{def.Name}' THÀNH CÔNG.");
                    return def;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX}   EnsureDefinition: CopyTo FAIL: {ex.Message}");
            }

            // Fallback cuối: trả lại library def (Add có thể fail nhưng không crash)
            Debug.WriteLine($"{LOG_PREFIX}   EnsureDefinition: CopyTo FAIL → fallback library def.");
            return libraryDef;
        }

        /// <summary>
        /// Insert SketchedSymbol với cơ chế retry: nếu Add với string[] promptStrings fail
        /// (ví dụ old=0 fields → new=N fields → E_INVALIDARG vì format kỳ cục),
        /// thì retry Add với Type.Missing → SetPromptResultText thủ công từng field.
        ///
        /// Lợi ích: old symbol không có attribute vẫn replace được bằng new symbol có attribute;
        /// new symbol được insert với default values từ definition, user có thể edit sau qua DataGrid.
        /// </summary>
        private SketchedSymbol AddSymbolWithFallback(
            Sheet sheet, SketchedSymbolDefinition def,
            Point2d pos, double rot, double scale, object promptStrings)
        {
            // Bước 1: thử path chính
            try
            {
                var sym = sheet.SketchedSymbols.Add(def, pos, rot, scale, promptStrings);
                Debug.WriteLine($"{LOG_PREFIX}   AddSymbolWithFallback: primary path OK.");
                return sym;
            }
            catch (Exception ex1)
            {
                Debug.WriteLine($"{LOG_PREFIX}   AddSymbolWithFallback: primary FAIL ({ex1.Message}) → fallback.");
            }

            // Bước 2: fallback — Add với Type.Missing rồi set prompt values thủ công
            var newSym = sheet.SketchedSymbols.Add(def, pos, rot, scale, Type.Missing);
            Debug.WriteLine($"{LOG_PREFIX}   AddSymbolWithFallback: fallback Add(Type.Missing) OK.");

            if (promptStrings is string[] values && values.Length > 0)
            {
                ApplyPromptValues(newSym, def, values);
            }
            return newSym;
        }

        /// <summary>
        /// Set prompt values cho symbol đã insert qua SetPromptResultText.
        /// Match theo thứ tự prompt fields (TextBox có ReadOnlyUniqueID) trong definition.
        /// </summary>
        private void ApplyPromptValues(SketchedSymbol sym, SketchedSymbolDefinition def, string[] values)
        {
            try
            {
                int tbIdx  = 0;
                int arrIdx = 0;
                foreach (TextBox tb in def.Sketch.TextBoxes)
                {
                    try
                    {
                        string fmt = tb.FormattedText ?? string.Empty;
                        if (ExtractPromptUID(fmt) != null)
                        {
                            if (arrIdx < values.Length)
                            {
                                sym.SetPromptResultText(tb, values[arrIdx] ?? string.Empty);
                                Debug.WriteLine($"{LOG_PREFIX}   ApplyPromptValues[{arrIdx}] TB[{tbIdx}] = '{values[arrIdx]}'");
                            }
                            arrIdx++;
                        }
                    }
                    catch (Exception ex) { Debug.WriteLine($"{LOG_PREFIX}   ApplyPromptValues TB[{tbIdx}] LỖI: {ex.Message}"); }
                    tbIdx++;
                }
            }
            catch (Exception ex) { Debug.WriteLine($"{LOG_PREFIX}   ApplyPromptValues LỖI: {ex.Message}"); }
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
