using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace SymbolReplacer.Views
{
    /// <summary>
    /// WinForms UserControl được nhúng vào Inventor DockableWindow.
    /// Phase 1: Layout shell đầy đủ, thumbnail area là placeholder.
    /// Phase 2+: Thumbnail grid thật, search, replace logic.
    ///
    /// Layout (TableLayoutPanel, 3 rows):
    ///   Row 0 (Auto)  : Library source header + Search box
    ///   Row 1 (Fill)  : Thumbnail grid area
    ///   Row 2 (Auto)  : Replace buttons
    ///   Row 3 (Auto)  : Status strip
    /// </summary>
    public partial class SymbolReplacerPanel : Form
    {
        // ─── Hằng số log ──────────────────────────────────────────────────────
        private const string LOG_PREFIX = "[SymbolReplacerPanel]";

        // ─── Inventor-style color palette ─────────────────────────────────────
        private static readonly Color ColorBackground     = Color.FromArgb(240, 240, 240);
        private static readonly Color ColorSectionBg      = Color.FromArgb(214, 214, 214);
        private static readonly Color ColorSectionText    = Color.FromArgb(60,  60,  60);
        private static readonly Color ColorAccent         = Color.FromArgb(0,   120, 212);
        private static readonly Color ColorAccentHover    = Color.FromArgb(0,   102, 180);
        private static readonly Color ColorBorder         = Color.FromArgb(180, 180, 180);
        private static readonly Color ColorPanelDark      = Color.FromArgb(100, 100, 100);
        private static readonly Color ColorStatusOk       = Color.FromArgb(0,   128, 0);
        private static readonly Color ColorStatusPick     = Color.FromArgb(200, 100, 0);
        private static readonly Color ColorStatusIdle     = Color.FromArgb(80,  80,  80);
        private static readonly Font  FontUI              = new Font("Segoe UI", 8.5f, FontStyle.Regular);
        private static readonly Font  FontUIBold          = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        private static readonly Font  FontSection         = new Font("Segoe UI", 7.5f, FontStyle.Bold);

        // ─── Controls ─────────────────────────────────────────────────────────
        private TableLayoutPanel _mainLayout;

        // -- Row 0: Library + Search
        private Panel        _pnlTopSection;
        private Panel        _pnlLibraryHeader;
        private Label        _lblLibraryTitle;
        private Label        _lblLibraryPath;
        private Button       _btnChangeLibrary;
        private Panel        _pnlSearchBar;
        private TextBox      _txtSearch;
        private Label        _lblSearchIcon;

        // -- Row 1: Thumbnail grid (Phase 2)
        private ThumbnailGridControl _thumbnailGrid;

        // -- Row 2: Replace actions
        private Panel        _pnlActions;
        private Panel        _pnlActionsHeader;
        private Label        _lblActionsTitle;
        private Button       _btnReplace;
        private Button       _btnReplaceAll;
        private ContextMenuStrip _menuReplaceAll;

        // -- Row 3: Status strip
        private Panel        _pnlStatus;
        private Label        _lblStatusIcon;
        private Label        _lblStatusText;

        // ─── Events (PaletteController subscribe) ────────────────────────────

        /// <summary>Raised khi user click ⚙ và chọn library file mới.</summary>
        public event EventHandler<string> LibraryPathChangeRequested;

        /// <summary>Raised khi user gõ vào search box.</summary>
        public event EventHandler<string> SearchQueryChanged;

        // ─── Public properties (PaletteController đọc/ghi) ───────────────────

        /// <summary>Grid control — PaletteController dùng để SetItems()</summary>
        public ThumbnailGridControl SymbolGrid => _thumbnailGrid;

        /// <summary>Cập nhật hiển thị library path trên panel.</summary>
        public void SetLibraryPath(string path)
        {
            if (_lblLibraryPath == null) return;
            _lblLibraryPath.Text = path ?? string.Empty;
        }

        // ─── Constructor ──────────────────────────────────────────────────────
        public SymbolReplacerPanel()
        {
            // Khởi tạo form và build toàn bộ layout
            Debug.WriteLine($"{LOG_PREFIX} Khởi tạo SymbolReplacerPanel...");

            this.SuspendLayout();
            InitializeFormProperties();
            BuildLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

            SetStatusIdle();

            Debug.WriteLine($"{LOG_PREFIX} SymbolReplacerPanel khởi tạo THÀNH CÔNG.");
        }

        // ─── Form properties ──────────────────────────────────────────────────

        private void InitializeFormProperties()
        {
            this.Text            = "Symbol Replacer";
            this.BackColor       = ColorBackground;
            this.Font            = FontUI;
            this.MinimumSize     = new Size(220, 400);
            this.AutoScaleMode   = AutoScaleMode.Dpi;
            this.Padding         = new Padding(0);
            this.Margin          = new Padding(0);
        }

        // ─── Layout builder ───────────────────────────────────────────────────

        private void BuildLayout()
        {
            // TableLayoutPanel chính: 1 cột, 4 hàng
            // Hàng 0 (Auto)  : Library + Search
            // Hàng 1 (100%)  : Thumbnail area — chiếm hết không gian còn lại
            // Hàng 2 (Auto)  : Replace buttons
            // Hàng 3 (Auto)  : Status bar
            _mainLayout = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = 4,
                Padding     = new Padding(0),
                Margin      = new Padding(0),
                BackColor   = ColorBackground
            };

            _mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // Row 0
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));     // Row 1 — Fill
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // Row 2
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f));     // Row 3 — Status

            // Build từng row
            BuildRow0_LibraryAndSearch();
            BuildRow1_ThumbnailArea();
            BuildRow2_Actions();
            BuildRow3_Status();

            this.Controls.Add(_mainLayout);

            Debug.WriteLine($"{LOG_PREFIX} Layout đã được build xong.");
        }

        // ─── Row 0: Library source + Search ──────────────────────────────────

        private void BuildRow0_LibraryAndSearch()
        {
            _pnlTopSection = new Panel
            {
                Dock      = DockStyle.Fill,
                AutoSize  = true,
                BackColor = ColorBackground,
                Margin    = new Padding(0)
            };

            // ── Library header ──
            _pnlLibraryHeader = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 22,
                BackColor = ColorSectionBg,
                Padding   = new Padding(6, 0, 0, 0),
                Margin    = new Padding(0)
            };

            _lblLibraryTitle = new Label
            {
                Text      = "LIBRARY SOURCE",
                Font      = FontSection,
                ForeColor = ColorSectionText,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            _pnlLibraryHeader.Controls.Add(_lblLibraryTitle);

            // ── Library path + button ──
            var pnlLibraryPath = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 40,
                BackColor = ColorBackground,
                Padding   = new Padding(6, 4, 4, 4)
            };

            _btnChangeLibrary = new Button
            {
                Text      = "⚙",
                Width     = 28,
                Height    = 28,
                Dock      = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 10f),
                BackColor = ColorBackground,
                ForeColor = ColorPanelDark,
                Cursor    = Cursors.Hand,
                Margin    = new Padding(0),
                Padding   = new Padding(0)
            };
            _btnChangeLibrary.FlatAppearance.BorderColor = ColorBorder;
            _btnChangeLibrary.FlatAppearance.BorderSize  = 1;
            _btnChangeLibrary.Click    += OnChangeLibraryClick;
            ToolTipFor(_btnChangeLibrary, "Đổi đường dẫn Library file (.idw)");

            _lblLibraryPath = new Label
            {
                Text      = "C:\\server\\System\\2023\\Inventor\\Library",
                Dock      = DockStyle.Fill,
                Font      = new Font("Segoe UI", 7.5f),
                ForeColor = ColorPanelDark,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true  // Cắt bớt nếu path quá dài
            };

            pnlLibraryPath.Controls.Add(_lblLibraryPath);
            pnlLibraryPath.Controls.Add(_btnChangeLibrary);

            // ── Separator ──
            var separator1 = CreateSeparator();

            // ── Search box ──
            _pnlSearchBar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 32,
                BackColor = ColorBackground,
                Padding   = new Padding(6, 4, 6, 4)
            };

            _lblSearchIcon = new Label
            {
                Text      = "🔍",
                Width     = 22,
                Dock      = DockStyle.Left,
                TextAlign = ContentAlignment.MiddleCenter,
                Font      = new Font("Segoe UI", 9f),
                ForeColor = ColorPanelDark,
                BackColor = Color.Transparent
            };

            _txtSearch = new TextBox
            {
                Dock            = DockStyle.Fill,
                Font            = FontUI,
                BorderStyle     = BorderStyle.FixedSingle,
                BackColor       = Color.White,
                ForeColor       = Color.DimGray,
                Text            = "Search symbols..."
            };
            _txtSearch.Enter  += OnSearchEnter;
            _txtSearch.Leave  += OnSearchLeave;
            _txtSearch.TextChanged += OnSearchTextChanged;

            _pnlSearchBar.Controls.Add(_txtSearch);
            _pnlSearchBar.Controls.Add(_lblSearchIcon);

            // ── Separator ──
            var separator2 = CreateSeparator();

            // Thêm vào pnlTopSection theo thứ tự NGƯỢC (DockStyle.Top xử lý theo Z-order)
            // Control được thêm SAU sẽ ở TRÊN khi dùng Dock.Top
            _pnlTopSection.Controls.Add(separator2);
            _pnlTopSection.Controls.Add(_pnlSearchBar);
            _pnlTopSection.Controls.Add(separator1);
            _pnlTopSection.Controls.Add(pnlLibraryPath);
            _pnlTopSection.Controls.Add(_pnlLibraryHeader);

            _mainLayout.Controls.Add(_pnlTopSection, 0, 0);

            Debug.WriteLine($"{LOG_PREFIX} Row 0 (Library + Search) đã được build.");
        }

        // ─── Row 1: Thumbnail grid (Phase 2) ─────────────────────────────────

        private void BuildRow1_ThumbnailArea()
        {
            // ThumbnailGridControl: double-buffered, auto-scroll, hiển thị symbols
            _thumbnailGrid = new ThumbnailGridControl
            {
                Dock    = DockStyle.Fill,
                Margin  = new Padding(0)
            };

            _mainLayout.Controls.Add(_thumbnailGrid, 0, 1);

            Debug.WriteLine($"{LOG_PREFIX} Row 1 (ThumbnailGridControl) đã được build.");
        }

        // ─── Row 2: Replace actions ───────────────────────────────────────────

        private void BuildRow2_Actions()
        {
            _pnlActions = new Panel
            {
                Dock      = DockStyle.Fill,
                AutoSize  = true,
                BackColor = ColorBackground,
                Margin    = new Padding(0)
            };

            // ── Section header ──
            _pnlActionsHeader = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 22,
                BackColor = ColorSectionBg,
                Padding   = new Padding(6, 0, 0, 0)
            };

            _lblActionsTitle = new Label
            {
                Text      = "REPLACE",
                Font      = FontSection,
                ForeColor = ColorSectionText,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            _pnlActionsHeader.Controls.Add(_lblActionsTitle);

            // ── Buttons ──
            var pnlButtons = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 72,
                BackColor = ColorBackground,
                Padding   = new Padding(6, 6, 6, 0)
            };

            // Context menu cho Replace All
            _menuReplaceAll = new ContextMenuStrip();
            _menuReplaceAll.Font = FontUI;
            var menuCurrentSheet = new ToolStripMenuItem("Current Sheet...");
            var menuAllSheets    = new ToolStripMenuItem("All Sheets...");
            menuCurrentSheet.Click += OnReplaceAllCurrentSheetClick;
            menuAllSheets.Click    += OnReplaceAllAllSheetsClick;
            _menuReplaceAll.Items.Add(menuCurrentSheet);
            _menuReplaceAll.Items.Add(menuAllSheets);

            // Button: Replace (Pick mode)
            _btnReplace = CreateActionButton("🔄  Replace  (Pick mode)", isPrimary: true);
            _btnReplace.Click   += OnReplaceClick;
            _btnReplace.Height  = 30;
            _btnReplace.Dock    = DockStyle.Top;
            ToolTipFor(_btnReplace, "Chọn symbol mới, sau đó click vào symbol cũ trên bản vẽ để replace.\nESC để thoát.");

            // Spacer
            var spacer = new Panel { Height = 4, Dock = DockStyle.Top, BackColor = ColorBackground };

            // Button: Replace All (dropdown)
            _btnReplaceAll = CreateActionButton("🔄  Replace All  ▼", isPrimary: false);
            _btnReplaceAll.Height  = 30;
            _btnReplaceAll.Dock    = DockStyle.Top;
            _btnReplaceAll.Click  += OnReplaceAllButtonClick;
            ToolTipFor(_btnReplaceAll, "Replace tất cả symbol cùng loại.\nChọn phạm vi: Current Sheet hoặc All Sheets.");

            // Thêm buttons (thứ tự ngược với Dock.Top)
            pnlButtons.Controls.Add(_btnReplaceAll);
            pnlButtons.Controls.Add(spacer);
            pnlButtons.Controls.Add(_btnReplace);

            // Disable khi chưa chọn symbol
            UpdateActionButtonsState(enabled: false);

            var separator = CreateSeparator();

            // Thêm vào pnlActions
            _pnlActions.Controls.Add(pnlButtons);
            _pnlActions.Controls.Add(_pnlActionsHeader);

            _mainLayout.Controls.Add(_pnlActions, 0, 2);

            Debug.WriteLine($"{LOG_PREFIX} Row 2 (Actions) đã được build.");
        }

        // ─── Row 3: Status bar ────────────────────────────────────────────────

        private void BuildRow3_Status()
        {
            _pnlStatus = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = ColorSectionBg,
                Padding   = new Padding(6, 0, 6, 0),
                Margin    = new Padding(0)
            };

            // Vẽ border trên cùng để phân cách với area bên trên
            _pnlStatus.Paint += (s, e) =>
            {
                using (var pen = new Pen(ColorBorder))
                    e.Graphics.DrawLine(pen, 0, 0, _pnlStatus.Width, 0);
            };

            _lblStatusIcon = new Label
            {
                Text      = "●",
                Width     = 16,
                Dock      = DockStyle.Left,
                TextAlign = ContentAlignment.MiddleCenter,
                Font      = new Font("Segoe UI", 8f),
                ForeColor = ColorStatusIdle,
                BackColor = Color.Transparent
            };

            _lblStatusText = new Label
            {
                Text      = "Idle",
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font      = FontUI,
                ForeColor = ColorStatusIdle,
                BackColor = Color.Transparent
            };

            _pnlStatus.Controls.Add(_lblStatusText);
            _pnlStatus.Controls.Add(_lblStatusIcon);

            _mainLayout.Controls.Add(_pnlStatus, 0, 3);

            Debug.WriteLine($"{LOG_PREFIX} Row 3 (Status bar) đã được build.");
        }

        // ─── Public: Status management (sẽ được gọi từ Controller) ──────────

        /// <summary>Hiện trạng thái Idle</summary>
        public void SetStatusIdle()
        {
            UpdateStatus("Ready — Select a new symbol from the list.", ColorStatusIdle);
        }

        /// <summary>Hiện trạng thái Pick Mode (đang chờ user click trên bản vẽ)</summary>
        public void SetStatusPickMode()
        {
            UpdateStatus("PICK MODE — Click on symbol to replace. ESC to cancel.", ColorStatusPick);
        }

        /// <summary>Hiện trạng thái thành công sau khi replace</summary>
        public void SetStatusSuccess(int count)
        {
            UpdateStatus($"Done — Replaced {count} symbol(s) successfully.", ColorStatusOk);
        }

        /// <summary>Hiện thông báo lỗi</summary>
        public void SetStatusError(string message)
        {
            UpdateStatus($"Error: {message}", Color.Red);
        }

        /// <summary>Bật/tắt nút Replace (dùng khi chưa/đã chọn symbol mới)</summary>
        public void UpdateActionButtonsState(bool enabled)
        {
            if (_btnReplace != null)
            {
                _btnReplace.Enabled    = enabled;
                _btnReplaceAll.Enabled = enabled;

                // Cập nhật visual style theo trạng thái
                _btnReplace.BackColor    = enabled ? ColorAccent    : Color.FromArgb(200, 200, 200);
                _btnReplace.ForeColor    = enabled ? Color.White     : Color.FromArgb(130, 130, 130);
                _btnReplaceAll.BackColor = enabled ? ColorBackground : Color.FromArgb(200, 200, 200);
                _btnReplaceAll.ForeColor = enabled ? ColorPanelDark  : Color.FromArgb(130, 130, 130);
            }
        }

        // ─── Private: Event handlers ──────────────────────────────────────────

        private void OnChangeLibraryClick(object sender, EventArgs e)
        {
            // User click nút ⚙ để đổi library — mở dialog chọn file
            Debug.WriteLine($"{LOG_PREFIX} User click 'Change Library'.");

            using (var dlg = new OpenFileDialog())
            {
                dlg.Title  = "Select Symbol Library File";
                dlg.Filter = "Inventor Drawing (*.idw)|*.idw";

                // Mở tại thư mục hiện tại nếu hợp lệ
                string currentDir = System.IO.Path.GetDirectoryName(_lblLibraryPath.Text);
                if (!string.IsNullOrEmpty(currentDir) && System.IO.Directory.Exists(currentDir))
                    dlg.InitialDirectory = currentDir;

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    Debug.WriteLine($"{LOG_PREFIX} Library path mới: {dlg.FileName}");
                    // Raise event để PaletteController xử lý (lưu config + reload)
                    LibraryPathChangeRequested?.Invoke(this, dlg.FileName);
                }
            }
        }

        private void OnSearchEnter(object sender, EventArgs e)
        {
            // Clear placeholder text khi focus vào search box
            if (_txtSearch.Text == "Search symbols..." && _txtSearch.ForeColor == Color.DimGray)
            {
                _txtSearch.Text      = string.Empty;
                _txtSearch.ForeColor = Color.Black;
            }
        }

        private void OnSearchLeave(object sender, EventArgs e)
        {
            // Restore placeholder text nếu search box trống
            if (string.IsNullOrWhiteSpace(_txtSearch.Text))
            {
                _txtSearch.Text      = "Search symbols...";
                _txtSearch.ForeColor = Color.DimGray;
            }
        }

        private void OnSearchTextChanged(object sender, EventArgs e)
        {
            // Bỏ qua placeholder text khi fire event
            string query = (_txtSearch.Text == "Search symbols..." || _txtSearch.ForeColor == Color.DimGray)
                ? string.Empty
                : _txtSearch.Text;

            Debug.WriteLine($"{LOG_PREFIX} Search query: '{query}'");

            // Raise event để PaletteController filter
            SearchQueryChanged?.Invoke(this, query);
        }

        private void OnReplaceClick(object sender, EventArgs e)
        {
            // User click nút Replace — enter pick mode
            Debug.WriteLine($"{LOG_PREFIX} User click 'Replace' — enter pick mode.");
            SetStatusPickMode();
            // TODO Phase 3: Gọi InteractionController.EnterPickMode()
        }

        private void OnReplaceAllButtonClick(object sender, EventArgs e)
        {
            // User click nút Replace All — hiện dropdown menu
            Debug.WriteLine($"{LOG_PREFIX} User click 'Replace All' — hiện context menu.");
            _menuReplaceAll.Show(_btnReplaceAll, new Point(0, _btnReplaceAll.Height));
        }

        private void OnReplaceAllCurrentSheetClick(object sender, EventArgs e)
        {
            // User chọn Replace All → Current Sheet
            Debug.WriteLine($"{LOG_PREFIX} User chọn 'Replace All — Current Sheet'.");
            // TODO Phase 4: Gọi ReplaceController.ReplaceAllCurrentSheet()
        }

        private void OnReplaceAllAllSheetsClick(object sender, EventArgs e)
        {
            // User chọn Replace All → All Sheets
            Debug.WriteLine($"{LOG_PREFIX} User chọn 'Replace All — All Sheets'.");
            // TODO Phase 4: Gọi ReplaceController.ReplaceAllSheets()
        }

        // ─── Private: Helpers ─────────────────────────────────────────────────

        private void UpdateStatus(string message, Color color)
        {
            if (_lblStatusText == null || _lblStatusIcon == null) return;

            // Cập nhật status bar — gọi trên UI thread nếu cần
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateStatus(message, color)));
                return;
            }

            _lblStatusText.Text      = message;
            _lblStatusText.ForeColor = color;
            _lblStatusIcon.ForeColor = color;

            Debug.WriteLine($"{LOG_PREFIX} Status: '{message}'");
        }

        private Button CreateActionButton(string text, bool isPrimary)
        {
            // Tạo button theo Inventor flat style
            var btn = new Button
            {
                Text      = text,
                FlatStyle = FlatStyle.Flat,
                Font      = FontUIBold,
                Cursor    = Cursors.Hand,
                Padding   = new Padding(4, 0, 4, 0),
                BackColor = isPrimary ? ColorAccent : ColorBackground,
                ForeColor = isPrimary ? Color.White  : ColorPanelDark
            };
            btn.FlatAppearance.BorderColor     = isPrimary ? ColorAccentHover : ColorBorder;
            btn.FlatAppearance.BorderSize       = 1;
            btn.FlatAppearance.MouseOverBackColor = isPrimary ? ColorAccentHover
                                                              : Color.FromArgb(220, 220, 220);
            return btn;
        }

        private Panel CreateSeparator()
        {
            // Tạo đường phân cách mỏng 1px
            return new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 1,
                BackColor = ColorBorder,
                Margin    = new Padding(0)
            };
        }

        private void ToolTipFor(Control control, string text)
        {
            // Gán tooltip cho control
            var tip = new ToolTip { ShowAlways = true };
            tip.SetToolTip(control, text);
        }

        // ─── Dispose ──────────────────────────────────────────────────────────

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Giải phóng font objects khi dispose
                FontUI?.Dispose();
                FontUIBold?.Dispose();
                FontSection?.Dispose();
                _menuReplaceAll?.Dispose();
                Debug.WriteLine($"{LOG_PREFIX} Panel đã được dispose.");
            }
            base.Dispose(disposing);
        }
    }
}
