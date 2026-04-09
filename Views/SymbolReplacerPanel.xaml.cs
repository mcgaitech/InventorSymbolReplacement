using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using SymbolReplacer.Models;

namespace SymbolReplacer.Views
{
    /// <summary>
    /// WPF UserControl nhúng vào Inventor DockableWindow qua HwndSource.
    ///
    /// API surface (dùng bởi PaletteController và ReplaceController):
    ///   Events  : LibraryPathChangeRequested, SearchQueryChanged, Replace*, Insert*, Scan*, ClearHighlight*
    ///   Props   : SymbolGrid, InsertScale, InsertRotationRad
    ///   Methods : SetLibraryPath, SetStatus*, UpdateActionButtonsState, SetSelectedSymbolProperties,
    ///             SetHighlightActive
    /// </summary>
    public partial class SymbolReplacerPanel : UserControl
    {
        // ─── Log prefix ───────────────────────────────────────────────────────
        private const string LOG_PREFIX = "[SymbolReplacerPanel]";

        // ─── SymbolGrid wrapper ───────────────────────────────────────────────
        private WpfSymbolGrid _symbolGrid;

        /// <summary>Grid chứa thumbnails — PaletteController dùng để SetItems() và lấy SelectedItem.</summary>
        public WpfSymbolGrid SymbolGrid => _symbolGrid;

        // ─── Events ───────────────────────────────────────────────────────────

        /// <summary>Raised khi user gõ path và nhấn Enter, hoặc chọn file qua dialog.</summary>
        public event EventHandler<string> LibraryPathChangeRequested;

        /// <summary>Raised khi user gõ vào search box (không raised khi placeholder).</summary>
        public event EventHandler<string> SearchQueryChanged;

        /// <summary>Raised khi user click "Replace (Pick Mode)".</summary>
        public event EventHandler ReplaceRequested;

        /// <summary>Raised khi user chọn "Current Sheet Only" từ Replace All menu.</summary>
        public event EventHandler ReplaceAllCurrentSheetRequested;

        /// <summary>Raised khi user chọn "All Sheets" từ Replace All menu.</summary>
        public event EventHandler ReplaceAllAllSheetsRequested;

        /// <summary>Raised khi user chọn "Insert into Drawing" qua right-click context menu.</summary>
        public event EventHandler InsertRequested;

        /// <summary>Raised khi user click "Scan Sheet".</summary>
        public event EventHandler ScanSheetRequested;

        /// <summary>Raised khi user click "Clear Highlight".</summary>
        public event EventHandler ClearHighlightRequested;

        /// <summary>Raised khi user chọn tab "Local" — PaletteController load từ active document.</summary>
        public event EventHandler LocalSourceRequested;

        // ─── Public properties ────────────────────────────────────────────────

        /// <summary>Scale từ textbox — mặc định 1.0 nếu không hợp lệ.</summary>
        public double InsertScale
        {
            get
            {
                if (double.TryParse(txtScale.Text.Trim(),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double v) && v > 0)
                    return v;
                return 1.0;
            }
        }

        /// <summary>Rotation radians từ textbox (UI nhập degrees).</summary>
        public double InsertRotationRad
        {
            get
            {
                if (double.TryParse(txtRotate.Text.Trim(),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double deg))
                    return deg * Math.PI / 180.0;
                return 0.0;
            }
        }

        // ─── Constructor ──────────────────────────────────────────────────────

        private static IntPtr _hwndSourceHandle = IntPtr.Zero;

        public SymbolReplacerPanel()
        {
            InitializeComponent();
            _symbolGrid = new WpfSymbolGrid(listSymbols);
            SetSearchPlaceholder();
            listSymbols.PreviewMouseRightButtonDown += ListSymbols_PreviewMouseRightButtonDown;

            // Keyboard fix cho WPF TextBox trong COM host (Inventor):
            // WM_GETDLGCODE → DLGC_WANTALLKEYS: báo Inventor rằng HwndSource muốn nhận keyboard.
            // KHÔNG dùng WH_GETMESSAGE hook (gây crash Inventor khi đóng).
            Loaded += (s, e) =>
            {
                var source = PresentationSource.FromVisual(this) as HwndSource;
                if (source != null)
                {
                    _hwndSourceHandle = source.Handle;
                    source.AddHook(WndProcHook);
                    Debug.WriteLine($"{LOG_PREFIX} Keyboard fix installed (WM_GETDLGCODE).");
                }
            };

            // Chỉ cho nhập số vào Scale và Rotate
            WireNumericFilter(txtScale);
            WireNumericFilter(txtRotate);

            Debug.WriteLine($"{LOG_PREFIX} WPF SymbolReplacerPanel khởi tạo THÀNH CÔNG.");
        }

        private const int WM_GETDLGCODE = 0x0087;
        private const int DLGC_WANTALLKEYS = 0x0004;
        private const int DLGC_WANTCHARS = 0x0080;
        private const int DLGC_WANTTAB = 0x0002;
        private const int DLGC_WANTARROWS = 0x0001;

        /// <summary>
        /// HwndSource WndProc hook: trả DLGC_WANTALLKEYS cho WM_GETDLGCODE
        /// → ngăn Inventor's TranslateAccelerator nuốt keyboard messages.
        /// </summary>
        private static IntPtr WndProcHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_GETDLGCODE && Keyboard.FocusedElement is TextBox)
            {
                handled = true;
                return (IntPtr)(DLGC_WANTALLKEYS | DLGC_WANTCHARS | DLGC_WANTTAB | DLGC_WANTARROWS);
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// Gọi từ Deactivate() khi Inventor đóng. Clear static references.
        /// </summary>
        public static void CleanupKeyboardHook()
        {
            _hwndSourceHandle = IntPtr.Zero;
        }

        /// <summary>Chỉ cho nhập số: digits, dấu chấm, dấu trừ.</summary>
        private static void WireNumericFilter(TextBox tb)
        {
            if (tb == null) return;
            tb.PreviewTextInput += (s, e) =>
            {
                e.Handled = !Regex.IsMatch(e.Text, @"^[\d.\-]$");
            };
            DataObject.AddPastingHandler(tb, (s, e) =>
            {
                if (e.DataObject.GetDataPresent(typeof(string)))
                {
                    string text = (string)e.DataObject.GetData(typeof(string));
                    if (!Regex.IsMatch(text, @"^[\d.\-]+$")) e.CancelCommand();
                }
                else e.CancelCommand();
            });
        }

        // ─── Public methods ───────────────────────────────────────────────────

        /// <summary>Cập nhật tên file hiển thị trong Local source row.</summary>
        public void SetLocalInfo(string docName)
        {
            Dispatcher.Invoke(() =>
            {
                txtLocalInfo.Text = string.IsNullOrWhiteSpace(docName)
                    ? "No active drawing"
                    : docName;
            });
        }

        /// <summary>Cập nhật đường dẫn library hiển thị trong header TextBox.</summary>
        public void SetLibraryPath(string path)
        {
            Dispatcher.Invoke(() =>
            {
                txtLibraryPath.Text    = string.IsNullOrWhiteSpace(path) ? "" : path;
                txtLibraryPath.ToolTip = string.IsNullOrWhiteSpace(path)
                    ? "Type or paste a file/folder path and press Enter"
                    : path;
                Debug.WriteLine($"{LOG_PREFIX} SetLibraryPath: {path}");
            });
        }

        /// <summary>Status: idle/ready (xám).</summary>
        public void SetStatusIdle()
        {
            Dispatcher.Invoke(() =>
            {
                txtStatus.Text       = "Ready";
                txtStatus.Foreground = Brushes.DimGray;
            });
        }

        /// <summary>Status: pick mode (cam).</summary>
        public void SetStatusPickMode()
        {
            Dispatcher.Invoke(() =>
            {
                txtStatus.Text       = "🟡  PICK MODE: Click old symbol on drawing...";
                txtStatus.Foreground = new SolidColorBrush(Color.FromRgb(200, 100, 0));
            });
        }

        /// <summary>Status: insert mode (xanh dương).</summary>
        public void SetStatusInsertMode()
        {
            Dispatcher.Invoke(() =>
            {
                txtStatus.Text       = "⊕  INSERT MODE: Click insert point on drawing...";
                txtStatus.Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 212));
            });
        }

        /// <summary>Status: thành công (xanh lá).</summary>
        public void SetStatusSuccess(int count)
        {
            Dispatcher.Invoke(() =>
            {
                txtStatus.Text       = $"✔  {count} symbol(s) replaced successfully.";
                txtStatus.Foreground = new SolidColorBrush(Color.FromRgb(0, 128, 0));
            });
        }

        /// <summary>Status: lỗi (đỏ).</summary>
        public void SetStatusError(string message)
        {
            Dispatcher.Invoke(() =>
            {
                txtStatus.Text       = $"✖  {message}";
                txtStatus.Foreground = Brushes.Red;
                Debug.WriteLine($"{LOG_PREFIX} SetStatusError: {message}");
            });
        }

        /// <summary>Status: thông tin (cam) — dùng cho kết quả scan.</summary>
        public void SetStatusWarning(string message)
        {
            Dispatcher.Invoke(() =>
            {
                txtStatus.Text       = $"⚠  {message}";
                txtStatus.Foreground = new SolidColorBrush(Color.FromRgb(200, 100, 0));
            });
        }

        /// <summary>Status: thông tin chung (xanh dương).</summary>
        public void SetStatusInfo(string message)
        {
            Dispatcher.Invoke(() =>
            {
                txtStatus.Text       = $"ℹ  {message}";
                txtStatus.Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 212));
            });
        }

        /// <summary>Bật/tắt các nút Replace và Replace All theo trạng thái selection.</summary>
        public void UpdateActionButtonsState(bool enabled)
        {
            Dispatcher.Invoke(() =>
            {
                btnReplace.IsEnabled    = enabled;
                btnReplaceAll.IsEnabled = enabled;
            });
        }

        /// <summary>Bật/tắt nút Clear Highlight.</summary>
        public void SetHighlightActive(bool active)
        {
            Dispatcher.Invoke(() => btnClearHighlight.IsEnabled = active);
        }

        /// <summary>Cập nhật Properties panel khi user chọn symbol khác.</summary>
        public void SetSelectedSymbolProperties(SymbolDefinitionModel model)
        {
            Dispatcher.Invoke(() =>
            {
                if (model == null)
                {
                    txtSymbolName.Text = "(select a symbol)";
                    imgPreview.Source  = null;
                    return;
                }
                txtSymbolName.Text = model.Name;
                imgPreview.Source  = model.Thumbnail != null
                    ? WpfSymbolGrid.ConvertBitmapToSource(model.Thumbnail)
                    : null;
            });
        }

        // ─── Source mode toggle (File / Local) ───────────────────────────────

        private void SourceMode_Changed(object sender, RoutedEventArgs e)
        {
            if (pnlFileSource == null) return;  // Gọi trước InitializeComponent hoàn tất

            bool isFile = rbSourceFile?.IsChecked == true;

            pnlFileSource.Visibility = isFile ? Visibility.Visible : Visibility.Collapsed;
            txtLocalInfo.Visibility  = isFile ? Visibility.Collapsed : Visibility.Visible;

            Debug.WriteLine($"{LOG_PREFIX} Source mode: {(isFile ? "File" : "Local")}");

            if (!isFile)
                LocalSourceRequested?.Invoke(this, EventArgs.Empty);
        }

        // ─── Library path TextBox ─────────────────────────────────────────────

        private void TxtLibraryPath_KeyDown(object sender, KeyEventArgs e)
        {
            // Enter → tải library từ path người dùng gõ vào
            if (e.Key == Key.Enter)
            {
                string path = txtLibraryPath.Text.Trim();
                if (!string.IsNullOrEmpty(path))
                {
                    Debug.WriteLine($"{LOG_PREFIX} Path nhập trực tiếp: {path}");
                    LibraryPathChangeRequested?.Invoke(this, path);
                }
                Keyboard.ClearFocus();
                e.Handled = true;
            }
        }

        // ─── Search box placeholder ───────────────────────────────────────────

        private bool _searchIsPlaceholder = true;

        private void SetSearchPlaceholder()
        {
            // QUAN TRỌNG: set flag TRƯỚC khi đổi Text
            // Nếu đổi Text trước → TextChanged fires → _searchIsPlaceholder vẫn false
            // → SearchQueryChanged("Search...") → filter sai → list trống
            _searchIsPlaceholder = true;
            txtSearch.Text       = "Search...";
            txtSearch.Foreground = new SolidColorBrush(Color.FromRgb(153, 153, 153));
        }

        private void TxtSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            if (_searchIsPlaceholder)
            {
                txtSearch.Text       = string.Empty;
                txtSearch.Foreground = new SolidColorBrush(Color.FromRgb(60, 60, 60));
                _searchIsPlaceholder = false;
            }
        }

        private void TxtSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtSearch.Text))
                SetSearchPlaceholder();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_searchIsPlaceholder)
                SearchQueryChanged?.Invoke(this, txtSearch.Text);
        }

        // ─── View mode toggle (Grid / List) ───────────────────────────────────

        /// <summary>Raised khi view mode thay đổi → PaletteController cần re-apply items.</summary>
        public event EventHandler ViewModeChanged;

        private void ViewMode_Changed(object sender, RoutedEventArgs e)
        {
            // Có thể được gọi trước InitializeComponent hoàn tất
            if (listSymbols == null) return;

            bool isGrid = rbGridView?.IsChecked == true;

            // Clear ItemsSource trước khi thay đổi template
            listSymbols.ItemsSource = null;

            if (isGrid)
            {
                listSymbols.SetValue(VirtualizingPanel.IsVirtualizingProperty, false);
                listSymbols.ItemTemplate              = (DataTemplate)Resources["GridItemTemplate"];
                listSymbols.ItemsPanel                = (ItemsPanelTemplate)Resources["GridPanel"];
                listSymbols.HorizontalContentAlignment = HorizontalAlignment.Left;
                listSymbols.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty,
                                     ScrollBarVisibility.Disabled);
            }
            else
            {
                listSymbols.SetValue(VirtualizingPanel.IsVirtualizingProperty, true);
                listSymbols.ItemTemplate              = (DataTemplate)Resources["ListItemTemplate"];
                listSymbols.ItemsPanel                = (ItemsPanelTemplate)Resources["ListPanel"];
                listSymbols.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                listSymbols.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty,
                                     ScrollBarVisibility.Disabled);
            }

            Debug.WriteLine($"{LOG_PREFIX} View mode: {(isGrid ? "Grid" : "List")}");

            // Yêu cầu PaletteController re-apply items (tạo lại ThumbnailItemVm list)
            ViewModeChanged?.Invoke(this, EventArgs.Empty);
        }

        // ─── ListBox: right-click selects item first ──────────────────────────

        private void ListSymbols_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Chọn item trước khi context menu mở — đảm bảo InsertRequested dùng đúng symbol
            var dep = e.OriginalSource as DependencyObject;
            if (dep == null) return;
            var item = ItemsControl.ContainerFromElement(listSymbols, dep) as ListBoxItem;
            if (item == null) return;

            item.IsSelected = true;
            item.Focus();

            // Gán ContextMenu lần đầu tiên (lazy) — tránh wiring trong XAML Style
            // vì BAML connectionId không hoạt động trong COM host (Inventor addin)
            if (item.ContextMenu == null)
            {
                var ctx = new ContextMenu();
                var mi  = new MenuItem { Header = "Insert into Drawing" };
                mi.Click += ContextMenuInsert_Click;
                ctx.Items.Add(mi);
                item.ContextMenu = ctx;
            }
        }

        private void ListSymbols_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Xử lý bởi WpfSymbolGrid wrapper
        }

        // ─── Context menu: Insert into Drawing ───────────────────────────────

        private void ContextMenuInsert_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine($"{LOG_PREFIX} Context menu: Insert into Drawing.");
            InsertRequested?.Invoke(this, EventArgs.Empty);
        }

        // ─── Replace buttons ──────────────────────────────────────────────────

        private void BtnReplace_Click(object sender, RoutedEventArgs e)
            => ReplaceRequested?.Invoke(this, EventArgs.Empty);

        private void BtnReplaceAll_Click(object sender, RoutedEventArgs e)
        {
            // Mở dropdown menu ở dưới nút
            if (btnReplaceAll.ContextMenu != null)
            {
                btnReplaceAll.ContextMenu.PlacementTarget = btnReplaceAll;
                btnReplaceAll.ContextMenu.Placement       = PlacementMode.Bottom;
                btnReplaceAll.ContextMenu.IsOpen          = true;
            }
        }

        private void BtnReplaceAllCurrent_Click(object sender, RoutedEventArgs e)
            => ReplaceAllCurrentSheetRequested?.Invoke(this, EventArgs.Empty);

        private void BtnReplaceAllDoc_Click(object sender, RoutedEventArgs e)
            => ReplaceAllAllSheetsRequested?.Invoke(this, EventArgs.Empty);

        // ─── Scan / Highlight buttons ─────────────────────────────────────────

        private void BtnScanSheet_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine($"{LOG_PREFIX} Scan Sheet clicked.");
            ScanSheetRequested?.Invoke(this, EventArgs.Empty);
        }

        private void BtnClearHighlight_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine($"{LOG_PREFIX} Clear Highlight clicked.");
            ClearHighlightRequested?.Invoke(this, EventArgs.Empty);
        }

        // ─── Change library button ────────────────────────────────────────────

        private void BtnChangeLibrary_Click(object sender, RoutedEventArgs e)
        {
            // OpenFileDialog để chọn file .idw
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title      = "Select Symbol Library File",
                Filter     = "Inventor Drawing (*.idw)|*.idw|All Files (*.*)|*.*",
                DefaultExt = ".idw"
            };

            // Điền path hiện tại vào dialog nếu đã có
            string current = txtLibraryPath.Text.Trim();
            if (!string.IsNullOrEmpty(current))
            {
                try
                {
                    if (System.IO.File.Exists(current))
                        dlg.InitialDirectory = System.IO.Path.GetDirectoryName(current);
                    else if (System.IO.Directory.Exists(current))
                        dlg.InitialDirectory = current;
                }
                catch { }
            }

            if (dlg.ShowDialog() == true)
            {
                string path = dlg.FileName;
                // Hiển thị path đầy đủ trong TextBox
                txtLibraryPath.Text = path;
                Debug.WriteLine($"{LOG_PREFIX} Chọn file qua dialog: {path}");
                LibraryPathChangeRequested?.Invoke(this, path);
            }
        }

        // ─── Properties panel ─────────────────────────────────────────────────

        private void ChkLeader_Changed(object sender, RoutedEventArgs e)
        {
            if (chkLeaderVisible != null)
                chkLeaderVisible.IsEnabled = chkLeader.IsChecked == true;
        }
    }
}
