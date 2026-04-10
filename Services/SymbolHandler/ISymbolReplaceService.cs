using Inventor;

namespace MCGInventorPlugin.Services.SymbolHandler
{
    /// <summary>
    /// Interface cho service thực hiện replace symbol trên bản vẽ.
    /// Mỗi thao tác được bao bởi Transaction để hỗ trợ Undo.
    /// </summary>
    public interface ISymbolReplaceService
    {
        /// <summary>
        /// Replace một instance cụ thể bằng definition mới.
        /// Giữ nguyên: Position, Rotation, Scale, Layer, Attribute values.
        /// Tạo 1 Transaction riêng → 1 Undo step.
        /// </summary>
        bool ReplaceSingle(SketchedSymbol oldSymbol, SketchedSymbolDefinition newDef);

        /// <summary>
        /// Replace tất cả instance của cùng definition trên sheet hiện tại.
        /// Toàn bộ nằm trong 1 Transaction → 1 Undo step.
        /// </summary>
        int ReplaceAllOnSheet(Sheet sheet, SketchedSymbolDefinition oldDef, SketchedSymbolDefinition newDef);

        /// <summary>
        /// Replace tất cả instance của cùng definition trên toàn bộ document.
        /// Toàn bộ nằm trong 1 Transaction → 1 Undo step.
        /// </summary>
        int ReplaceAllInDocument(DrawingDocument doc, SketchedSymbolDefinition oldDef, SketchedSymbolDefinition newDef);

        /// <summary>
        /// Insert một instance mới của definition vào sheet tại vị trí chỉ định.
        /// rotation tính bằng radian, scale là hệ số tỷ lệ.
        /// Nếu attachGeometry != null → tạo leader bám vào geometry đó.
        /// Tạo 1 Transaction → 1 Undo step.
        /// </summary>
        /// <param name="isStatic">true → ẩn grip points (scale/rotate), chỉ giữ insertion point.</param>
        /// <param name="symbolClipping">true → trim annotations bên ngoài khi đè lên symbol.</param>
        /// <param name="leaderEnabled">true → tạo leader bám vào geometry (nếu có).</param>
        /// <param name="leaderVisible">true → hiện leader line. Chỉ có ý nghĩa khi leaderEnabled = true.</param>
        bool InsertSymbol(Sheet sheet, SketchedSymbolDefinition definition, Point2d position,
                          double rotation = 0.0, double scale = 1.0, object attachGeometry = null,
                          bool isStatic = false, bool symbolClipping = false,
                          bool leaderEnabled = true, bool leaderVisible = true,
                          System.Collections.Generic.Dictionary<int, string> promptValues = null);

        /// <summary>
        /// Cập nhật prompt text (field values) trực tiếp trên symbol instance đang có trên bản vẽ.
        /// Dùng SetPromptResultText() — không cần delete/re-insert.
        /// Bọc trong 1 Transaction → 1 Undo step.
        /// </summary>
        /// <param name="symbol">Instance trên bản vẽ cần update.</param>
        /// <param name="textBoxIndex">Index của TextBox trong definition.</param>
        /// <param name="value">Giá trị mới.</param>
        bool UpdatePromptText(SketchedSymbol symbol, int textBoxIndex, string value);
    }
}
