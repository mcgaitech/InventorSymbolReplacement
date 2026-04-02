using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;
using SymbolReplacer.Models;

namespace SymbolReplacer.Views
{
    /// <summary>
    /// Control hiển thị danh sách symbol dạng grid thumbnail.
    /// Giống giao diện Drawing Resources → Sketched Symbols của Inventor.
    ///
    /// - Double-buffered để tránh flickering
    /// - AutoScroll dọc
    /// - Highlight selection với viền accent
    /// - Hover state với nền nhạt
    /// </summary>
    public class ThumbnailGridControl : Panel
    {
        // ─── Hằng số log ──────────────────────────────────────────────────────
        private const string LOG_PREFIX = "[ThumbnailGridControl]";

        // ─── Kích thước cell ──────────────────────────────────────────────────
        private const int THUMB_SIZE = 80;   // Kích thước thumbnail
        private const int CELL_PAD   = 6;    // Padding xung quanh thumbnail
        private const int LABEL_H    = 26;   // Chiều cao phần tên bên dưới thumbnail
        private const int CELL_W     = THUMB_SIZE + CELL_PAD * 2;         // 92px
        private const int CELL_H     = THUMB_SIZE + CELL_PAD * 2 + LABEL_H; // 118px

        // ─── Màu sắc (Inventor style) ─────────────────────────────────────────
        private static readonly Color ColorCellBg      = Color.White;
        private static readonly Color ColorSelected    = Color.FromArgb(210, 230, 255);
        private static readonly Color ColorSelBorder   = Color.FromArgb(0, 120, 212);
        private static readonly Color ColorHover       = Color.FromArgb(235, 243, 255);
        private static readonly Color ColorText        = Color.FromArgb(50, 50, 50);
        private static readonly Color ColorThumbBorder = Color.FromArgb(200, 200, 200);
        private static readonly Color ColorEmpty       = Color.FromArgb(150, 150, 150);

        // ─── Font ─────────────────────────────────────────────────────────────
        private static readonly Font LabelFont = new Font("Segoe UI", 7.5f);
        private static readonly Font EmptyFont  = new Font("Segoe UI", 8.5f);

        // ─── Data ─────────────────────────────────────────────────────────────
        private List<SymbolDefinitionModel> _items = new List<SymbolDefinitionModel>();
        private int _selectedIndex = -1;
        private int _hoveredIndex  = -1;

        // ─── Events ───────────────────────────────────────────────────────────

        /// <summary>Raised khi user click chọn một symbol.</summary>
        public event EventHandler SelectionChanged;

        // ─── Properties ───────────────────────────────────────────────────────

        /// <summary>Symbol đang được chọn, null nếu chưa chọn.</summary>
        public SymbolDefinitionModel SelectedItem
            => (_selectedIndex >= 0 && _selectedIndex < _items.Count)
                ? _items[_selectedIndex]
                : null;

        // ─── Constructor ──────────────────────────────────────────────────────

        public ThumbnailGridControl()
        {
            // Bật double-buffering để tránh flickering khi scroll
            DoubleBuffered   = true;
            AutoScroll       = true;
            BackColor        = ColorCellBg;
            ResizeRedraw     = true;
            BorderStyle      = BorderStyle.None;

            SetStyle(
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint  |
                ControlStyles.UserPaint,
                true);

            Debug.WriteLine($"{LOG_PREFIX} ThumbnailGridControl khởi tạo.");
        }

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Thay thế toàn bộ danh sách items.
        /// Gọi từ PaletteController sau khi load/filter symbols.
        /// </summary>
        public void SetItems(IEnumerable<SymbolDefinitionModel> items)
        {
            _items         = items?.ToList() ?? new List<SymbolDefinitionModel>();
            _selectedIndex = -1;
            _hoveredIndex  = -1;
            UpdateScrollHeight();
            Invalidate();
            Debug.WriteLine($"{LOG_PREFIX} SetItems: {_items.Count} symbols.");
        }

        /// <summary>Bỏ chọn hiện tại.</summary>
        public void ClearSelection()
        {
            if (_selectedIndex < 0) return;
            _selectedIndex = -1;
            Invalidate();
        }

        // ─── Paint ───────────────────────────────────────────────────────────

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            if (_items.Count == 0)
            {
                DrawEmptyState(g);
                return;
            }

            int cols = GetColumnCount();

            for (int i = 0; i < _items.Count; i++)
            {
                int col     = i % cols;
                int row     = i / cols;
                int screenX = col * CELL_W;
                int screenY = row * CELL_H + AutoScrollPosition.Y;

                // Culling: bỏ qua cell ngoài vùng nhìn thấy
                if (screenY + CELL_H < 0 || screenY > ClientSize.Height) continue;

                DrawCell(g, i, screenX, screenY);
            }
        }

        private void DrawCell(Graphics g, int index, int x, int y)
        {
            var item       = _items[index];
            bool isSelected = index == _selectedIndex;
            bool isHovered  = index == _hoveredIndex;

            // ── Background ──
            var bgColor = isSelected ? ColorSelected
                        : isHovered  ? ColorHover
                                     : ColorCellBg;
            using (var br = new SolidBrush(bgColor))
                g.FillRectangle(br, x, y, CELL_W, CELL_H);

            // ── Selection border ──
            if (isSelected)
            {
                using (var selPen = new Pen(ColorSelBorder, 2f))
                    g.DrawRectangle(selPen, x + 1, y + 1, CELL_W - 2, CELL_H - 2);
            }

            // ── Thumbnail ──
            var thumbRect = new Rectangle(x + CELL_PAD, y + CELL_PAD, THUMB_SIZE, THUMB_SIZE);

            if (item.Thumbnail != null)
            {
                g.DrawImage(item.Thumbnail, thumbRect);
            }
            else
            {
                // Nền xám nhạt khi chưa có thumbnail
                using (var br = new SolidBrush(Color.FromArgb(240, 240, 240)))
                    g.FillRectangle(br, thumbRect);
            }

            // Viền thumbnail
            using (var borderPen = new Pen(ColorThumbBorder, 1f))
                g.DrawRectangle(borderPen, thumbRect);

            // ── Label tên ──
            var labelRect = new RectangleF(
                x + 2,
                y + CELL_PAD + THUMB_SIZE + 2,
                CELL_W - 4,
                LABEL_H - 4);

            using (var sf = new StringFormat
            {
                Alignment     = StringAlignment.Center,
                LineAlignment = StringAlignment.Near,
                Trimming      = StringTrimming.EllipsisCharacter,
                FormatFlags   = StringFormatFlags.LineLimit
            })
            using (var br = new SolidBrush(ColorText))
                g.DrawString(item.Name, LabelFont, br, labelRect, sf);
        }

        private void DrawEmptyState(Graphics g)
        {
            // Hiện thông báo khi chưa load symbol nào
            using (var br = new SolidBrush(ColorEmpty))
            using (var sf = new StringFormat
            {
                Alignment     = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            })
            {
                g.DrawString(
                    "No symbols loaded.\nSelect a library file (⚙).",
                    EmptyFont, br,
                    new RectangleF(0, 0, ClientSize.Width, ClientSize.Height),
                    sf);
            }
        }

        // ─── Mouse events ─────────────────────────────────────────────────────

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;

            int idx = HitTest(e.Location);
            if (idx == _selectedIndex) return;  // Đã chọn rồi, không cần update

            _selectedIndex = idx;
            Invalidate();

            Debug.WriteLine($"{LOG_PREFIX} Selection → index={idx}, name='{SelectedItem?.Name}'");
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int idx = HitTest(e.Location);
            if (idx == _hoveredIndex) return;
            _hoveredIndex = idx;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredIndex < 0) return;
            _hoveredIndex = -1;
            Invalidate();
        }

        // ─── Resize ───────────────────────────────────────────────────────────

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateScrollHeight();
        }

        // ─── Private helpers ──────────────────────────────────────────────────

        /// <summary>Số cột dựa theo chiều rộng control.</summary>
        private int GetColumnCount()
            => Math.Max(1, ClientSize.Width / CELL_W);

        /// <summary>Cập nhật AutoScrollMinSize để scroll vừa đủ nội dung.</summary>
        private void UpdateScrollHeight()
        {
            int cols  = GetColumnCount();
            int rows  = _items.Count == 0 ? 0 : (_items.Count + cols - 1) / cols;
            AutoScrollMinSize = new Size(0, rows * CELL_H + CELL_PAD);
        }

        /// <summary>
        /// Tìm index item tại vị trí mouse.
        /// Trả về -1 nếu không có item tại vị trí đó.
        /// </summary>
        private int HitTest(Point mousePos)
        {
            int cols = GetColumnCount();

            // Chuyển screen coords → content coords (trừ scroll offset)
            int contentX = mousePos.X;
            int contentY = mousePos.Y - AutoScrollPosition.Y;

            if (contentX < 0 || contentY < 0) return -1;

            int col = contentX / CELL_W;
            int row = contentY / CELL_H;

            if (col >= cols) return -1;

            int index = row * cols + col;
            return (index >= 0 && index < _items.Count) ? index : -1;
        }

        // ─── Dispose ──────────────────────────────────────────────────────────

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                LabelFont?.Dispose();
                EmptyFont?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
