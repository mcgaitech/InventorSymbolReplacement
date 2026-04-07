using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using SymbolReplacer.Models;

namespace SymbolReplacer.Views
{
    /// <summary>
    /// Wrapper WPF xung quanh ListBox để expose interface giống ThumbnailGridControl (WinForms cũ).
    /// PaletteController dùng qua SymbolReplacerPanel.SymbolGrid:
    ///   - SelectedItem    : symbol đang được chọn
    ///   - SelectionChanged: event khi selection thay đổi
    ///   - SetItems()      : cập nhật danh sách hiển thị
    /// </summary>
    public class WpfSymbolGrid
    {
        // ─── Hằng số log ──────────────────────────────────────────────────────
        private const string LOG_PREFIX = "[WpfSymbolGrid]";

        // ─── ListBox được wrap ────────────────────────────────────────────────
        private readonly ListBox _listBox;

        // ─── API surface (giống ThumbnailGridControl WinForms) ───────────────

        /// <summary>Raised khi user chọn hoặc bỏ chọn một item.</summary>
        public event EventHandler SelectionChanged;

        /// <summary>Symbol đang được chọn; null nếu không có selection.</summary>
        public SymbolDefinitionModel SelectedItem
            => (_listBox.SelectedItem as ThumbnailItemVm)?.Model;

        // ─── Constructor ──────────────────────────────────────────────────────

        /// <summary>
        /// Tạo WpfSymbolGrid bao quanh listBox đã khai báo trong XAML.
        /// Gọi từ SymbolReplacerPanel constructor sau InitializeComponent().
        /// </summary>
        internal WpfSymbolGrid(ListBox listBox)
        {
            _listBox = listBox ?? throw new ArgumentNullException(nameof(listBox));

            // Chuyển SelectionChanged của ListBox thành event của class này
            _listBox.SelectionChanged += (s, e) =>
                SelectionChanged?.Invoke(this, EventArgs.Empty);

            Debug.WriteLine($"{LOG_PREFIX} WpfSymbolGrid khởi tạo xong.");
        }

        // ─── Public methods ───────────────────────────────────────────────────

        /// <summary>
        /// Cập nhật danh sách symbols hiển thị trong grid.
        /// null hoặc rỗng → xóa sạch grid.
        /// Tự động dispatch về UI thread.
        /// </summary>
        public void SetItems(IList<SymbolDefinitionModel> items)
        {
            _listBox.Dispatcher.Invoke(() =>
            {
                if (items == null || items.Count == 0)
                {
                    _listBox.ItemsSource = null;
                    Debug.WriteLine($"{LOG_PREFIX} SetItems: xóa grid (0 items).");
                    return;
                }

                // Convert từng SymbolDefinitionModel thành ViewModel có ImageSource cho WPF binding
                var viewModels = items
                    .Select(m => new ThumbnailItemVm
                    {
                        Model       = m,
                        Name        = m.Name,
                        ImageSource = m.Thumbnail != null
                            ? ConvertBitmapToSource(m.Thumbnail)
                            : null
                    })
                    .ToList();

                _listBox.ItemsSource = viewModels;
                Debug.WriteLine($"{LOG_PREFIX} SetItems: hiển thị {viewModels.Count} symbols.");
            });
        }

        // ─── Internal helper: Bitmap → BitmapSource ───────────────────────────

        /// <summary>
        /// Convert System.Drawing.Bitmap (GDI+) sang WPF BitmapSource.
        /// Internal vì cũng được dùng bởi SymbolReplacerPanel.SetSelectedSymbolProperties().
        /// </summary>
        internal static BitmapSource ConvertBitmapToSource(System.Drawing.Bitmap bmp)
        {
            if (bmp == null) return null;

            try
            {
                // Encode qua PNG stream để convert GDI+ → WPF BitmapImage
                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    ms.Position = 0;

                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = ms;
                    bitmapImage.CacheOption  = BitmapCacheOption.OnLoad;  // Đọc toàn bộ vào bộ nhớ trước khi đóng stream
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();  // Thread-safe: cho phép dùng trên các thread khác
                    return bitmapImage;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI ConvertBitmapToSource: {ex.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// ViewModel nội bộ cho từng item trong ListBox.
    /// Chứa Model gốc và ImageSource đã được convert cho WPF binding.
    /// </summary>
    internal class ThumbnailItemVm
    {
        public SymbolDefinitionModel Model       { get; set; }
        public string                Name        { get; set; }
        public BitmapSource          ImageSource { get; set; }
    }
}
