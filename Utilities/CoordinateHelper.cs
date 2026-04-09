using System;
using System.Drawing;
using Inventor;

namespace MCGInventorPlugin.Utilities
{
    /// <summary>
    /// Chuyển đổi tọa độ từ Inventor sketch space sang GDI+ screen space.
    ///
    /// Inventor sketch:
    ///   - Trục Y hướng lên (dương = lên)
    ///   - Đơn vị: cm
    ///
    /// GDI+ screen:
    ///   - Trục Y hướng xuống (dương = xuống)
    ///   - Đơn vị: pixel
    ///
    /// Transform:
    ///   gdiX = offsetX + (invX - minX) * scale
    ///   gdiY = offsetY + (maxY  - invY) * scale   ← flip Y
    /// </summary>
    public class SketchTransform
    {
        // ─── Fields ───────────────────────────────────────────────────────────
        private readonly double _minX;
        private readonly double _maxY;
        private readonly double _scale;
        private readonly float  _offsetX;
        private readonly float  _offsetY;

        /// <summary>Scale factor: Inventor units → pixels</summary>
        public double Scale => _scale;

        // ─── Constructor ──────────────────────────────────────────────────────

        /// <summary>
        /// Tính transform để fit nội dung sketch vào ô pixelSize×pixelSize,
        /// với padding pixel mỗi bên, giữ nguyên tỉ lệ và căn giữa.
        /// </summary>
        public SketchTransform(
            double minX, double minY,
            double maxX, double maxY,
            int    pixelSize,
            int    padding)
        {
            double rangeX = maxX - minX;
            double rangeY = maxY - minY;

            // Tránh chia cho 0 khi sketch chỉ có 1 điểm
            if (rangeX < 1e-10) rangeX = 1e-10;
            if (rangeY < 1e-10) rangeY = 1e-10;

            // Vùng pixel có thể dùng (trừ padding hai phía)
            int available = pixelSize - 2 * padding;

            // Giữ tỉ lệ: chọn scale nhỏ hơn để không bị vỡ
            double scaleX = available / rangeX;
            double scaleY = available / rangeY;
            _scale = Math.Min(scaleX, scaleY);

            // Căn giữa nội dung trong ô pixelSize×pixelSize
            _offsetX = (float)(padding + (available - rangeX * _scale) / 2.0);
            _offsetY = (float)(padding + (available - rangeY * _scale) / 2.0);

            _minX = minX;
            _maxY = maxY;
        }

        // ─── Public methods ───────────────────────────────────────────────────

        /// <summary>Chuyển đổi tọa độ Inventor → GDI+ PointF</summary>
        public PointF ToGdi(double invX, double invY)
        {
            float gdiX = _offsetX + (float)((invX - _minX) * _scale);
            float gdiY = _offsetY + (float)((_maxY - invY) * _scale);  // flip Y
            return new PointF(gdiX, gdiY);
        }

        /// <summary>Chuyển đổi tọa độ từ Inventor Point2d → GDI+ PointF</summary>
        public PointF ToGdi(Point2d pt) => ToGdi(pt.X, pt.Y);
    }
}
