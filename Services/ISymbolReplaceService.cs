using Inventor;

namespace SymbolReplacer.Services
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
        /// Tạo 1 Transaction → 1 Undo step.
        /// </summary>
        bool InsertSymbol(Sheet sheet, SketchedSymbolDefinition definition, Point2d position,
                          double rotation = 0.0, double scale = 1.0);
    }
}
