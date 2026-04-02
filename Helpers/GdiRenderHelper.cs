using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using Inventor;
using DrawingColor = System.Drawing.Color;

namespace SymbolReplacer.Helpers
{
    /// <summary>
    /// Render thumbnail 80×80px từ DrawingSketch của một SketchedSymbolDefinition.
    ///
    /// Pipeline:
    ///   1. Duyệt tất cả entities (bỏ qua Construction + Reference)
    ///   2. Tính bounding box tổng từ RangeBox của từng entity
    ///   3. Tạo SketchTransform: fit + center + flip-Y
    ///   4. Render từng loại entity lên Bitmap bằng GDI+
    ///
    /// Hỗ trợ: Lines, Arcs, Circles, Ellipses, EllipticalArcs,
    ///         Splines (through-point, control-point, fixed, offset), TextBoxes.
    /// </summary>
    public static class GdiRenderHelper
    {
        // ─── Hằng số ──────────────────────────────────────────────────────────
        private const string LOG_PREFIX  = "[GdiRenderHelper]";
        private const float  PEN_WIDTH   = 1.0f;
        private const int    SPLINE_SEGS = 32;   // Số đoạn tessellate spline
        private const int    ARC_SEGS    = 36;   // Số đoạn tessellate arc/ellipse

        // ─── Public entry point ───────────────────────────────────────────────

        /// <summary>
        /// Render DrawingSketch thành Bitmap.
        /// Trả về placeholder nếu sketch rỗng hoặc gặp lỗi nghiêm trọng.
        /// </summary>
        public static Bitmap RenderSymbol(DrawingSketch sketch, int size, int padding)
        {
            try
            {
                // Bước 1: Tính bounding box
                var bbox = new BBoxAccumulator();
                CollectBoundingBox(sketch, bbox);

                if (bbox.IsEmpty)
                {
                    Debug.WriteLine($"{LOG_PREFIX} Sketch rỗng hoặc chỉ có construction lines.");
                    return CreatePlaceholder(size);
                }

                // Bước 2: Tạo transform
                var xform = new SketchTransform(
                    bbox.MinX, bbox.MinY, bbox.MaxX, bbox.MaxY, size, padding);

                // Bước 3: Render
                var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode       = SmoothingMode.AntiAlias;
                    g.TextRenderingHint   = TextRenderingHint.ClearTypeGridFit;
                    g.InterpolationMode   = InterpolationMode.HighQualityBicubic;
                    g.Clear(DrawingColor.White);

                    RenderLines(g, sketch, xform);
                    RenderArcs(g, sketch, xform);
                    RenderCircles(g, sketch, xform);
                    RenderEllipses(g, sketch, xform);
                    RenderEllipticalArcs(g, sketch, xform);
                    RenderSplines(g, sketch, xform);
                    RenderControlPointSplines(g, sketch, xform);
                    RenderFixedSplines(g, sketch, xform);
                    RenderOffsetSplines(g, sketch, xform);
                    RenderTextBoxes(g, sketch, xform);
                }

                return bmp;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI render symbol: {ex.Message}");
                return CreatePlaceholder(size);
            }
        }

        /// <summary>Tạo bitmap placeholder khi không render được.</summary>
        public static Bitmap CreatePlaceholder(int size)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(DrawingColor.FromArgb(245, 245, 245));
                using (var pen  = new Pen(DrawingColor.FromArgb(180, 180, 180), 1f))
                using (var font = new Font("Segoe UI", size * 0.12f, FontStyle.Regular, GraphicsUnit.Pixel))
                using (var brush = new SolidBrush(DrawingColor.FromArgb(160, 160, 160)))
                {
                    // Dấu chéo × thể hiện không có preview
                    g.DrawLine(pen, 4, 4, size - 4, size - 4);
                    g.DrawLine(pen, size - 4, 4, 4, size - 4);
                    var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    g.DrawString("?", font, brush,
                        new RectangleF(0, 0, size, size), sf);
                }
            }
            return bmp;
        }

        // ─── Bounding box collection ──────────────────────────────────────────

        private static void CollectBoundingBox(DrawingSketch sketch, BBoxAccumulator bbox)
        {
            // Lines
            TryCollect(() =>
            {
                foreach (SketchLine e in sketch.SketchLines)
                    TryCollect(() => { if (!e.Construction && !e.Reference) bbox.Include(e.RangeBox); });
            });

            // Arcs
            TryCollect(() =>
            {
                foreach (SketchArc e in sketch.SketchArcs)
                    TryCollect(() => { if (!e.Construction && !e.Reference) bbox.Include(e.RangeBox); });
            });

            // Circles
            TryCollect(() =>
            {
                foreach (SketchCircle e in sketch.SketchCircles)
                    TryCollect(() => { if (!e.Construction && !e.Reference) bbox.Include(e.RangeBox); });
            });

            // Ellipses
            TryCollect(() =>
            {
                foreach (SketchEllipse e in sketch.SketchEllipses)
                    TryCollect(() => { if (!e.Construction && !e.Reference) bbox.Include(e.RangeBox); });
            });

            // Elliptical arcs
            TryCollect(() =>
            {
                foreach (SketchEllipticalArc e in sketch.SketchEllipticalArcs)
                    TryCollect(() => { if (!e.Construction && !e.Reference) bbox.Include(e.RangeBox); });
            });

            // Splines (tất cả loại)
            TryCollect(() =>
            {
                foreach (SketchSpline e in sketch.SketchSplines)
                    TryCollect(() => { if (!e.Construction && !e.Reference) bbox.Include(e.RangeBox); });
            });
            TryCollect(() =>
            {
                foreach (SketchControlPointSpline e in sketch.SketchControlPointSplines)
                    TryCollect(() => { if (!e.Construction && !e.Reference) bbox.Include(e.RangeBox); });
            });
            TryCollect(() =>
            {
                foreach (SketchFixedSpline e in sketch.SketchFixedSplines)
                    TryCollect(() => { if (!e.Construction) bbox.Include(e.RangeBox); });
            });
            TryCollect(() =>
            {
                foreach (SketchOffsetSpline e in sketch.SketchOffsetSplines)
                    TryCollect(() => { if (!e.Construction) bbox.Include(e.RangeBox); });
            });

            // TextBoxes (không có thuộc tính Construction)
            TryCollect(() =>
            {
                foreach (TextBox tb in sketch.TextBoxes)
                    TryCollect(() => bbox.Include(tb.RangeBox));
            });
        }

        // ─── Render methods ───────────────────────────────────────────────────

        private static void RenderLines(Graphics g, DrawingSketch sketch, SketchTransform xform)
        {
            TryCollect(() =>
            {
                foreach (SketchLine e in sketch.SketchLines)
                {
                    TryCollect(() =>
                    {
                        if (e.Construction || e.Reference) return;
                        using (var pen = MakePen(e.Layer))
                        {
                            var p1 = xform.ToGdi(e.Geometry.StartPoint);
                            var p2 = xform.ToGdi(e.Geometry.EndPoint);
                            g.DrawLine(pen, p1, p2);
                        }
                    });
                }
            });
        }

        private static void RenderArcs(Graphics g, DrawingSketch sketch, SketchTransform xform)
        {
            TryCollect(() =>
            {
                foreach (SketchArc e in sketch.SketchArcs)
                {
                    TryCollect(() =>
                    {
                        if (e.Construction || e.Reference) return;
                        var geom = e.Geometry;  // Arc2d
                        var pts  = TessellateArc(
                            geom.Center.X, geom.Center.Y,
                            geom.Radius,
                            geom.StartAngle, geom.SweepAngle,
                            xform);
                        if (pts == null || pts.Length < 2) return;
                        using (var pen = MakePen(e.Layer))
                            g.DrawLines(pen, pts);
                    });
                }
            });
        }

        private static void RenderCircles(Graphics g, DrawingSketch sketch, SketchTransform xform)
        {
            TryCollect(() =>
            {
                foreach (SketchCircle e in sketch.SketchCircles)
                {
                    TryCollect(() =>
                    {
                        if (e.Construction || e.Reference) return;
                        var geom   = e.Geometry;  // Circle2d
                        var center = xform.ToGdi(geom.Center);
                        float r    = (float)(geom.Radius * xform.Scale);
                        if (r < 0.5f) return;
                        using (var pen = MakePen(e.Layer))
                            g.DrawEllipse(pen, center.X - r, center.Y - r, 2 * r, 2 * r);
                    });
                }
            });
        }

        private static void RenderEllipses(Graphics g, DrawingSketch sketch, SketchTransform xform)
        {
            TryCollect(() =>
            {
                foreach (SketchEllipse e in sketch.SketchEllipses)
                {
                    TryCollect(() =>
                    {
                        if (e.Construction || e.Reference) return;
                        var geom      = e.Geometry;  // EllipseFull2d
                        double axisAng = Math.Atan2(e.MajorAxisVector.Y, e.MajorAxisVector.X);
                        var pts = TessellateEllipse(
                            geom.Center.X, geom.Center.Y,
                            e.MajorRadius, e.MinorRadius,
                            axisAng, xform, ARC_SEGS);
                        if (pts == null || pts.Length < 3) return;
                        using (var pen = MakePen(e.Layer))
                            g.DrawPolygon(pen, pts);  // DrawPolygon tự đóng hình
                    });
                }
            });
        }

        private static void RenderEllipticalArcs(Graphics g, DrawingSketch sketch, SketchTransform xform)
        {
            TryCollect(() =>
            {
                foreach (SketchEllipticalArc e in sketch.SketchEllipticalArcs)
                {
                    TryCollect(() =>
                    {
                        if (e.Construction || e.Reference) return;
                        var geom      = e.Geometry;  // EllipticalArc2d
                        double axisAng = Math.Atan2(e.MajorAxisVector.Y, e.MajorAxisVector.X);
                        int n = Math.Max(8, Math.Min(ARC_SEGS, (int)(Math.Abs(geom.SweepAngle) * 12)));
                        var pts = TessellateEllipseArc(
                            geom.Center.X, geom.Center.Y,
                            e.MajorRadius, e.MinorRadius,
                            axisAng,
                            geom.StartAngle, geom.SweepAngle,
                            xform, n);
                        if (pts == null || pts.Length < 2) return;
                        using (var pen = MakePen(e.Layer))
                            g.DrawLines(pen, pts);
                    });
                }
            });
        }

        private static void RenderSplines(Graphics g, DrawingSketch sketch, SketchTransform xform)
        {
            TryCollect(() =>
            {
                foreach (SketchSpline e in sketch.SketchSplines)
                {
                    TryCollect(() =>
                    {
                        if (e.Construction || e.Reference) return;
                        var pts = TessellateEvaluator(e.Geometry.Evaluator, xform, SPLINE_SEGS);
                        if (pts == null || pts.Length < 2) return;
                        using (var pen = MakePen(e.Layer))
                            g.DrawLines(pen, pts);
                    });
                }
            });
        }

        private static void RenderControlPointSplines(Graphics g, DrawingSketch sketch, SketchTransform xform)
        {
            TryCollect(() =>
            {
                foreach (SketchControlPointSpline e in sketch.SketchControlPointSplines)
                {
                    TryCollect(() =>
                    {
                        if (e.Construction || e.Reference) return;
                        var pts = TessellateEvaluator(e.Geometry.Evaluator, xform, SPLINE_SEGS);
                        if (pts == null || pts.Length < 2) return;
                        using (var pen = MakePen(e.Layer))
                            g.DrawLines(pen, pts);
                    });
                }
            });
        }

        private static void RenderFixedSplines(Graphics g, DrawingSketch sketch, SketchTransform xform)
        {
            TryCollect(() =>
            {
                foreach (SketchFixedSpline e in sketch.SketchFixedSplines)
                {
                    TryCollect(() =>
                    {
                        if (e.Construction) return;
                        var pts = TessellateEvaluator(e.Geometry.Evaluator, xform, SPLINE_SEGS);
                        if (pts == null || pts.Length < 2) return;
                        using (var pen = MakePen(e.Layer))
                            g.DrawLines(pen, pts);
                    });
                }
            });
        }

        private static void RenderOffsetSplines(Graphics g, DrawingSketch sketch, SketchTransform xform)
        {
            TryCollect(() =>
            {
                foreach (SketchOffsetSpline e in sketch.SketchOffsetSplines)
                {
                    TryCollect(() =>
                    {
                        if (e.Construction) return;
                        var pts = TessellateEvaluator(e.Geometry.Evaluator, xform, SPLINE_SEGS);
                        if (pts == null || pts.Length < 2) return;
                        using (var pen = MakePen(e.Layer))
                            g.DrawLines(pen, pts);
                    });
                }
            });
        }

        private static void RenderTextBoxes(Graphics g, DrawingSketch sketch, SketchTransform xform)
        {
            TryCollect(() =>
            {
                foreach (TextBox tb in sketch.TextBoxes)
                {
                    TryCollect(() =>
                    {
                        string text = tb.Text ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(text)) return;

                        // Cắt text dài — thumbnail nhỏ, chỉ cần nhận biết
                        if (text.Length > 24) text = text.Substring(0, 21) + "...";

                        var origin = xform.ToGdi(tb.Origin);
                        float fontPx = Math.Max(5f, (float)(tb.FittedTextHeight * xform.Scale * 0.8));
                        fontPx = Math.Min(fontPx, 10f);  // Giới hạn kích thước tối đa

                        using (var font  = new Font("Segoe UI", fontPx, FontStyle.Regular, GraphicsUnit.Pixel))
                        using (var brush = new SolidBrush(DrawingColor.FromArgb(30, 30, 30)))
                        {
                            // Áp dụng rotation của text (Inventor: radian, CCW)
                            double rotDeg = -tb.Rotation * 180.0 / Math.PI;  // negate vì flip-Y
                            var state = g.Save();
                            g.TranslateTransform(origin.X, origin.Y);
                            g.RotateTransform((float)rotDeg);
                            g.DrawString(text, font, brush, PointF.Empty);
                            g.Restore(state);
                        }
                    });
                }
            });
        }

        // ─── Tessellation helpers ─────────────────────────────────────────────

        /// <summary>Tessellate arc bằng parametric sampling.</summary>
        private static PointF[] TessellateArc(
            double cx, double cy,
            double r,
            double startAngle, double sweepAngle,
            SketchTransform xform,
            int nSegs = -1)
        {
            if (nSegs < 0)
                nSegs = Math.Max(8, Math.Min(ARC_SEGS, (int)(Math.Abs(sweepAngle) * 12)));

            var pts = new PointF[nSegs + 1];
            for (int i = 0; i <= nSegs; i++)
            {
                double angle = startAngle + sweepAngle * i / nSegs;
                pts[i] = xform.ToGdi(
                    cx + r * Math.Cos(angle),
                    cy + r * Math.Sin(angle));
            }
            return pts;
        }

        /// <summary>Tessellate ellipse thành polygon (đường kín) với rotation.</summary>
        private static PointF[] TessellateEllipse(
            double cx, double cy,
            double rx, double ry,
            double axisAngle,
            SketchTransform xform,
            int nSegs = ARC_SEGS)
        {
            double cosA = Math.Cos(axisAngle);
            double sinA = Math.Sin(axisAngle);
            var pts = new PointF[nSegs];
            for (int i = 0; i < nSegs; i++)
            {
                double t  = 2.0 * Math.PI * i / nSegs;
                double ex = rx * Math.Cos(t);
                double ey = ry * Math.Sin(t);
                // Rotate vào world frame
                pts[i] = xform.ToGdi(
                    cx + ex * cosA - ey * sinA,
                    cy + ex * sinA + ey * cosA);
            }
            return pts;
        }

        /// <summary>Tessellate elliptical arc (cung hở) với rotation.</summary>
        private static PointF[] TessellateEllipseArc(
            double cx, double cy,
            double rx, double ry,
            double axisAngle,
            double startAngle, double sweepAngle,
            SketchTransform xform,
            int nSegs = ARC_SEGS)
        {
            double cosA = Math.Cos(axisAngle);
            double sinA = Math.Sin(axisAngle);
            var pts = new PointF[nSegs + 1];
            for (int i = 0; i <= nSegs; i++)
            {
                double t  = startAngle + sweepAngle * i / nSegs;
                double ex = rx * Math.Cos(t);
                double ey = ry * Math.Sin(t);
                pts[i] = xform.ToGdi(
                    cx + ex * cosA - ey * sinA,
                    cy + ex * sinA + ey * cosA);
            }
            return pts;
        }

        /// <summary>
        /// Tessellate bất kỳ curve nào qua Curve2dEvaluator.
        /// Dùng cho splines (BSplineCurve2d).
        /// </summary>
        private static PointF[] TessellateEvaluator(
            Curve2dEvaluator eval,
            SketchTransform xform,
            int nSegs = SPLINE_SEGS)
        {
            try
            {
                // Lấy khoảng tham số
                double startP, endP;
                eval.GetParamExtents(out startP, out endP);

                // Tạo mảng tham số để sample đồng đều
                double[] paramArr = new double[nSegs + 1];
                for (int i = 0; i <= nSegs; i++)
                    paramArr[i] = startP + (endP - startP) * i / nSegs;

                // Lấy tọa độ các điểm từ API
                // Inventor trả về mảng phẳng [x0, y0, x1, y1, ...] (interleaved)
                double[] ptsArr = null;
                eval.GetPointAtParam(ref paramArr, ref ptsArr);

                if (ptsArr == null || ptsArr.Length < 2 * (nSegs + 1))
                    return null;

                var result = new PointF[nSegs + 1];
                for (int i = 0; i <= nSegs; i++)
                    result[i] = xform.ToGdi(ptsArr[2 * i], ptsArr[2 * i + 1]);

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} TessellateEvaluator lỗi: {ex.Message}");
                return null;
            }
        }

        // ─── Pen factory ──────────────────────────────────────────────────────

        /// <summary>Tạo Pen từ Layer color. Fallback về màu xám đậm nếu lỗi.</summary>
        private static Pen MakePen(Layer layer, float width = PEN_WIDTH)
        {
            try
            {
                if (layer?.Color != null)
                {
                    var c = layer.Color;
                    // Màu trắng thuần trên nền trắng sẽ không thấy — dùng màu tối thay thế
                    if (c.Red > 240 && c.Green > 240 && c.Blue > 240)
                        return new Pen(DrawingColor.FromArgb(60, 60, 60), width);
                    return new Pen(DrawingColor.FromArgb(c.Red, c.Green, c.Blue), width);
                }
            }
            catch { }
            return new Pen(DrawingColor.FromArgb(30, 30, 30), width);
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        /// <summary>Execute action, nuốt exception để không crash toàn bộ render.</summary>
        private static void TryCollect(Action action)
        {
            try { action(); }
            catch { }
        }

        // ─── Nested: BBoxAccumulator ──────────────────────────────────────────

        /// <summary>Tích lũy bounding box từ nhiều entities.</summary>
        private class BBoxAccumulator
        {
            public double MinX = double.MaxValue;
            public double MinY = double.MaxValue;
            public double MaxX = double.MinValue;
            public double MaxY = double.MinValue;

            public bool IsEmpty => MinX == double.MaxValue;

            public void Include(Box2d box)
            {
                if (box == null) return;
                try
                {
                    Include(box.MinPoint.X, box.MinPoint.Y);
                    Include(box.MaxPoint.X, box.MaxPoint.Y);
                }
                catch { }
            }

            private void Include(double x, double y)
            {
                if (x < MinX) MinX = x;
                if (y < MinY) MinY = y;
                if (x > MaxX) MaxX = x;
                if (y > MaxY) MaxY = y;
            }
        }
    }
}
