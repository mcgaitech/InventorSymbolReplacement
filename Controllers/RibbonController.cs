using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Inventor;
using SymbolReplacer.Helpers;
using SymbolReplacer.Views;

namespace SymbolReplacer.Controllers
{
    /// <summary>
    /// Chịu trách nhiệm:
    /// 1. Tạo/tìm tab "Custom Tools" trên Drawing ribbon
    /// 2. Tạo button "Symbol Replacer" với icon
    /// 3. Quản lý DockableWindow (tạo, toggle, resize, cleanup)
    /// </summary>
    public class RibbonController
    {
        // ─── Hằng số ──────────────────────────────────────────────────────────
        private const string LOG_PREFIX           = "[RibbonController]";
        private const string RIBBON_NAME          = "Drawing";               // Ribbon của môi trường Drawing
        private const string TAB_ID               = "id.Tab.CustomTools";
        private const string TAB_DISPLAY          = "Custom Tools";
        private const string PANEL_ID             = "id.Panel.SymbolTools";
        private const string PANEL_DISPLAY        = "Symbol Tools";
        private const string BUTTON_ID            = "id.Button.SymbolReplacer";
        private const string BUTTON_DISPLAY       = "Symbol\nReplacer";     // \n để xuống dòng trên ribbon
        private const string BUTTON_TOOLTIP       = "Symbol Replacer";
        private const string BUTTON_DESCRIPTION   = "Replace drawing symbols while preserving position and attributes";
        private const string DOCKWIN_ID           = "SymbolReplacer.DockableWindow";
        private const string DOCKWIN_TITLE        = "Symbol Replacer";
        private const string ADDIN_GUID           = "{7C3D8E4F-2A1B-4C5D-9E8F-1A2B3C4D5E6F}";

        // ─── Win32 API cho việc embed WinForms vào DockableWindow ────────────
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int nWidth, int nHeight, bool bRepaint);

        // ─── Fields ───────────────────────────────────────────────────────────
        private readonly Inventor.Application _app;
        private ButtonDefinition              _buttonDef;
        private DockableWindow                _dockableWindow;
        private DockableWindowsEvents         _dockableWindowsEvents;  // lấy qua DockableWindows.Events
        private DockWindowSizer               _dockWindowSizer;        // NativeWindow để bắt WM_SIZE
        private SymbolReplacerPanel           _panel;

        // ─── Constructor ──────────────────────────────────────────────────────
        public RibbonController(Inventor.Application app)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
            Debug.WriteLine($"{LOG_PREFIX} Khởi tạo RibbonController.");
        }

        // ─── Public properties ────────────────────────────────────────────────

        /// <summary>
        /// WinForms panel đã được nhúng vào DockableWindow.
        /// PaletteController dùng để đăng ký events và cập nhật ThumbnailGrid.
        /// Luôn được tạo sau CreateRibbonUI(), có thể chưa có HWND nếu chưa show.
        /// </summary>
        public SymbolReplacerPanel Panel => _panel;

        // ─── Public methods ───────────────────────────────────────────────────

        /// <summary>
        /// Tạo toàn bộ ribbon UI: tab, panel, button, dockable window.
        /// Chỉ gọi khi firstTime = true trong Activate().
        /// </summary>
        public void CreateRibbonUI()
        {
            Debug.WriteLine($"{LOG_PREFIX} Bắt đầu tạo Ribbon UI...");

            try
            {
                // Bước 1: Lấy Drawing ribbon
                var ribbon = GetDrawingRibbon();
                if (ribbon == null) return;

                // Bước 2: Tìm hoặc tạo tab "Custom Tools"
                var tab = GetOrCreateTab(ribbon);
                if (tab == null) return;

                // Bước 3: Tìm hoặc tạo panel "Symbol Tools" trong tab
                var panel = GetOrCreatePanel(tab);
                if (panel == null) return;

                // Bước 4: Tạo ButtonDefinition (command definition)
                CreateButtonDefinition();

                // Bước 5: Thêm button vào panel
                AddButtonToPanel(panel);

                // Bước 6: Tạo DockableWindow và embed WinForms panel
                CreateDockableWindow();

                Debug.WriteLine($"{LOG_PREFIX} Ribbon UI tạo THÀNH CÔNG.");
            }
            catch (Exception ex)
            {
                // Lỗi tạo ribbon UI
                Debug.WriteLine($"{LOG_PREFIX} LỖI tạo Ribbon UI: {ex.Message}");
                Debug.WriteLine($"{LOG_PREFIX} Stack trace:\n{ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Re-wire event handlers khi addin được reload (firstTime = false).
        /// </summary>
        public void RewireEvents()
        {
            Debug.WriteLine($"{LOG_PREFIX} Re-wire events...");

            try
            {
                // Tìm lại ButtonDefinition đã tồn tại
                _buttonDef = (ButtonDefinition)_app.CommandManager.ControlDefinitions[BUTTON_ID];
                if (_buttonDef != null)
                {
                    _buttonDef.OnExecute -= OnSymbolReplacerButtonExecute;
                    _buttonDef.OnExecute += OnSymbolReplacerButtonExecute;
                    Debug.WriteLine($"{LOG_PREFIX} Re-wire button event thành công.");
                }

                // Tìm lại DockableWindow và re-wire OnShow event
                try
                {
                    _dockableWindow = _app.UserInterfaceManager.DockableWindows[DOCKWIN_ID];
                    if (_dockableWindow != null)
                    {
                        _dockableWindowsEvents = _app.UserInterfaceManager.DockableWindows.Events;
                        _dockableWindowsEvents.OnShow -= OnDockableWindowShow;
                        _dockableWindowsEvents.OnShow += OnDockableWindowShow;
                        Debug.WriteLine($"{LOG_PREFIX} Re-wire dockable window OnShow event thành công.");
                    }
                }
                catch
                {
                    // DockableWindow chưa tồn tại — bỏ qua
                    Debug.WriteLine($"{LOG_PREFIX} DockableWindow chưa tồn tại khi re-wire, bỏ qua.");
                }
            }
            catch (Exception ex)
            {
                // Lỗi khi re-wire events
                Debug.WriteLine($"{LOG_PREFIX} LỖI re-wire events: {ex.Message}");
            }
        }

        /// <summary>
        /// Dọn dẹp tài nguyên khi addin bị tắt.
        /// </summary>
        public void Cleanup()
        {
            Debug.WriteLine($"{LOG_PREFIX} Bắt đầu cleanup...");

            try
            {
                // Detach button event
                if (_buttonDef != null)
                {
                    _buttonDef.OnExecute -= OnSymbolReplacerButtonExecute;
                    Debug.WriteLine($"{LOG_PREFIX} Đã detach button event.");
                }

                // Detach dockable window OnShow event
                if (_dockableWindowsEvents != null)
                {
                    _dockableWindowsEvents.OnShow -= OnDockableWindowShow;
                    Debug.WriteLine($"{LOG_PREFIX} Đã detach dockable window event.");
                }

                // Giải phóng NativeWindow sizer
                if (_dockWindowSizer != null)
                {
                    _dockWindowSizer.ReleaseHandle();
                    _dockWindowSizer = null;
                    Debug.WriteLine($"{LOG_PREFIX} Đã release DockWindowSizer.");
                }

                // Đóng và dispose WinForms panel
                if (_panel != null && !_panel.IsDisposed)
                {
                    _panel.Close();
                    _panel.Dispose();
                    _panel = null;
                    Debug.WriteLine($"{LOG_PREFIX} Đã dispose WinForms panel.");
                }

                Debug.WriteLine($"{LOG_PREFIX} Cleanup THÀNH CÔNG.");
            }
            catch (Exception ex)
            {
                // Lỗi khi cleanup
                Debug.WriteLine($"{LOG_PREFIX} LỖI cleanup: {ex.Message}");
            }
        }

        // ─── Private: Ribbon setup ────────────────────────────────────────────

        private Ribbon GetDrawingRibbon()
        {
            // Lấy ribbon của môi trường Drawing
            try
            {
                var ribbon = _app.UserInterfaceManager.Ribbons[RIBBON_NAME];
                Debug.WriteLine($"{LOG_PREFIX} Lấy được Drawing ribbon.");
                return ribbon;
            }
            catch (Exception ex)
            {
                // Không tìm thấy Drawing ribbon — có thể không ở trong môi trường Drawing
                Debug.WriteLine($"{LOG_PREFIX} LỖI lấy Drawing ribbon: {ex.Message}");
                return null;
            }
        }

        private RibbonTab GetOrCreateTab(Ribbon ribbon)
        {
            // Tìm tab "Custom Tools" nếu đã tồn tại, ngược lại tạo mới
            try
            {
                var tab = ribbon.RibbonTabs[TAB_ID];
                Debug.WriteLine($"{LOG_PREFIX} Tab '{TAB_DISPLAY}' đã tồn tại, tái sử dụng.");
                return tab;
            }
            catch
            {
                // Tab chưa tồn tại — tạo mới
                Debug.WriteLine($"{LOG_PREFIX} Tab '{TAB_DISPLAY}' chưa tồn tại, tạo mới...");
            }

            try
            {
                var tab = ribbon.RibbonTabs.Add(TAB_DISPLAY, TAB_ID, ADDIN_GUID);
                Debug.WriteLine($"{LOG_PREFIX} Tạo tab '{TAB_DISPLAY}' THÀNH CÔNG.");
                return tab;
            }
            catch (Exception ex)
            {
                // Lỗi tạo tab
                Debug.WriteLine($"{LOG_PREFIX} LỖI tạo tab: {ex.Message}");
                return null;
            }
        }

        private RibbonPanel GetOrCreatePanel(RibbonTab tab)
        {
            // Tìm panel "Symbol Tools" nếu đã tồn tại, ngược lại tạo mới
            try
            {
                var panel = tab.RibbonPanels[PANEL_ID];
                Debug.WriteLine($"{LOG_PREFIX} Panel '{PANEL_DISPLAY}' đã tồn tại, tái sử dụng.");
                return panel;
            }
            catch
            {
                // Panel chưa tồn tại — tạo mới
                Debug.WriteLine($"{LOG_PREFIX} Panel '{PANEL_DISPLAY}' chưa tồn tại, tạo mới...");
            }

            try
            {
                var panel = tab.RibbonPanels.Add(PANEL_DISPLAY, PANEL_ID, ADDIN_GUID);
                Debug.WriteLine($"{LOG_PREFIX} Tạo panel '{PANEL_DISPLAY}' THÀNH CÔNG.");
                return panel;
            }
            catch (Exception ex)
            {
                // Lỗi tạo panel
                Debug.WriteLine($"{LOG_PREFIX} LỖI tạo panel: {ex.Message}");
                return null;
            }
        }

        private void CreateButtonDefinition()
        {
            // Kiểm tra ButtonDefinition đã tồn tại chưa (tránh duplicate khi debug reload)
            try
            {
                _buttonDef = (ButtonDefinition)_app.CommandManager.ControlDefinitions[BUTTON_ID];
                Debug.WriteLine($"{LOG_PREFIX} ButtonDefinition '{BUTTON_ID}' đã tồn tại, tái sử dụng.");

                // Re-wire event phòng trường hợp bị mất
                _buttonDef.OnExecute -= OnSymbolReplacerButtonExecute;
                _buttonDef.OnExecute += OnSymbolReplacerButtonExecute;
                return;
            }
            catch
            {
                // Chưa tồn tại — sẽ tạo mới bên dưới
                Debug.WriteLine($"{LOG_PREFIX} ButtonDefinition chưa tồn tại, tạo mới...");
            }

            // Load icon từ embedded resources
            var icon32Bmp = PictureDispConverter.LoadFromResource("ReplaceSymbol_32.png");
            var icon16Bmp = PictureDispConverter.LoadFromResource("ReplaceSymbol_16.png");

            // Nếu không có icon file → tạo icon placeholder màu xanh để test
            if (icon32Bmp == null)
            {
                // Tạo icon placeholder để không bị crash khi chưa có file ảnh
                Debug.WriteLine($"{LOG_PREFIX} CẢNH BÁO: Không có icon file, tạo placeholder.");
                icon32Bmp = CreatePlaceholderIcon(32);
                icon16Bmp = CreatePlaceholderIcon(16);
            }

            var icon32 = PictureDispConverter.ToIPictureDisp(icon32Bmp);
            var icon16 = PictureDispConverter.ToIPictureDisp(icon16Bmp);

            // Tạo ButtonDefinition
            // Lưu ý: Inventor 2023 API dùng ButtonDisplayEnum thay vì ButtonStyleEnum
            _buttonDef = _app.CommandManager.ControlDefinitions.AddButtonDefinition(
                DisplayName:    BUTTON_DISPLAY,
                InternalName:   BUTTON_ID,
                Classification: CommandTypesEnum.kNonShapeEditCmdType,
                ClientId:       ADDIN_GUID,
                DescriptionText: BUTTON_DESCRIPTION,
                ToolTipText:    BUTTON_TOOLTIP,
                StandardIcon:   icon16,
                LargeIcon:      icon32,
                ButtonDisplay:  ButtonDisplayEnum.kAlwaysDisplayText
            );

            // Đăng ký event handler khi user click button
            _buttonDef.OnExecute += OnSymbolReplacerButtonExecute;

            Debug.WriteLine($"{LOG_PREFIX} ButtonDefinition tạo THÀNH CÔNG với ID: {BUTTON_ID}");

            // Giải phóng bitmap sau khi convert
            icon32Bmp?.Dispose();
            icon16Bmp?.Dispose();
        }

        private void AddButtonToPanel(RibbonPanel panel)
        {
            // Thêm button vào panel nếu chưa có
            try
            {
                // Kiểm tra xem control đã có trong panel chưa
                var existingControl = panel.CommandControls[BUTTON_ID];
                Debug.WriteLine($"{LOG_PREFIX} Button đã có trong panel, bỏ qua thêm mới.");
            }
            catch
            {
                // Chưa có trong panel — thêm vào
                panel.CommandControls.AddButton(_buttonDef, UseLargeIcon: true);
                Debug.WriteLine($"{LOG_PREFIX} Đã thêm button vào panel '{PANEL_DISPLAY}'.");
            }
        }

        // ─── Private: DockableWindow setup ───────────────────────────────────

        private void CreateDockableWindow()
        {
            Debug.WriteLine($"{LOG_PREFIX} Bắt đầu tạo DockableWindow...");

            var uiManager = _app.UserInterfaceManager;

            // DockableWindowsEvents lấy qua DockableWindows.Events (Inventor 2023 API)
            _dockableWindowsEvents = uiManager.DockableWindows.Events;

            // Kiểm tra DockableWindow đã tồn tại chưa
            try
            {
                _dockableWindow = uiManager.DockableWindows[DOCKWIN_ID];
                Debug.WriteLine($"{LOG_PREFIX} DockableWindow đã tồn tại, tái sử dụng.");

                // Re-wire OnShow event để đảm bảo panel được embed khi hiện
                _dockableWindowsEvents.OnShow -= OnDockableWindowShow;
                _dockableWindowsEvents.OnShow += OnDockableWindowShow;
                return;
            }
            catch
            {
                // Chưa tồn tại — tạo mới
                Debug.WriteLine($"{LOG_PREFIX} DockableWindow chưa tồn tại, tạo mới...");
            }

            // Tạo DockableWindow mới
            _dockableWindow = uiManager.DockableWindows.Add(
                ClientId:     ADDIN_GUID,
                InternalName: DOCKWIN_ID,
                Title:        DOCKWIN_TITLE
            );

            // Cấu hình DockableWindow
            _dockableWindow.ShowTitleBar = true;
            _dockableWindow.Visible      = false;  // Ẩn ban đầu, hiện khi click button
            _dockableWindow.SetMinimumSize(220, 400);

            // Dock bên phải theo mặc định
            _dockableWindow.DockingState = DockingStateEnum.kDockRight;

            Debug.WriteLine($"{LOG_PREFIX} DockableWindow tạo xong, đang thử embed WinForms panel...");

            // Thử embed ngay — nếu HWND đã có sẵn (không phải lúc nào cũng có khi Visible=false)
            EmbedWinFormsPanel();

            // Đăng ký OnShow để embed panel lần đầu khi HWND chưa có lúc tạo
            // và để re-attach NativeWindow sizer nếu cần
            _dockableWindowsEvents.OnShow += OnDockableWindowShow;

            Debug.WriteLine($"{LOG_PREFIX} DockableWindow tạo THÀNH CÔNG.");
        }

        private void EmbedWinFormsPanel()
        {
            // Tạo WinForms panel nếu chưa có
            if (_panel == null || _panel.IsDisposed)
            {
                _panel = new SymbolReplacerPanel();
                _panel.TopLevel = false;
                _panel.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            }

            // Lấy HWND của DockableWindow để làm parent
            int dockHwnd = _dockableWindow.HWND;
            Debug.WriteLine($"{LOG_PREFIX} DockableWindow HWND: {dockHwnd}");

            if (dockHwnd == 0)
            {
                // HWND = 0 nghĩa là DockableWindow chưa được tạo hoàn toàn (chưa hiển thị lần đầu)
                // OnDockableWindowShow sẽ gọi lại EmbedWinFormsPanel khi HWND có sẵn
                Debug.WriteLine($"{LOG_PREFIX} CẢNH BÁO: DockableWindow HWND = 0, sẽ embed khi OnShow.");
                return;
            }

            // Reparent WinForms panel vào DockableWindow
            SetParent(_panel.Handle, new IntPtr(dockHwnd));
            _panel.Show();

            // Resize panel cho khớp với DockableWindow hiện tại
            int w = _dockableWindow.Width;
            int h = _dockableWindow.Height;
            if (w > 0 && h > 0)
                MoveWindow(_panel.Handle, 0, 0, w, h, true);

            // Attach NativeWindow sizer để bắt WM_SIZE từ DockableWindow HWND
            if (_dockWindowSizer == null)
            {
                _dockWindowSizer = new DockWindowSizer(new IntPtr(dockHwnd), _panel);
                Debug.WriteLine($"{LOG_PREFIX} DockWindowSizer đã attach HWND {dockHwnd}.");
            }

            Debug.WriteLine($"{LOG_PREFIX} Embed WinForms panel THÀNH CÔNG. Panel HWND: {_panel.Handle}");
        }

        // ─── Private: Placeholder icon (dùng khi chưa có file ảnh) ──────────

        private static System.Drawing.Bitmap CreatePlaceholderIcon(int size)
        {
            // Tạo icon placeholder đơn giản với chữ "SR" để nhận biết button
            var bmp = new System.Drawing.Bitmap(size, size);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.FromArgb(0, 120, 212)); // Màu xanh Inventor

                // Vẽ chữ "SR" (Symbol Replacer) nhỏ
                float fontSize = size * 0.3f;
                using (var font = new System.Drawing.Font("Segoe UI", fontSize, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel))
                using (var brush = new System.Drawing.SolidBrush(System.Drawing.Color.White))
                {
                    var sf = new System.Drawing.StringFormat
                    {
                        Alignment     = System.Drawing.StringAlignment.Center,
                        LineAlignment = System.Drawing.StringAlignment.Center
                    };
                    g.DrawString("SR", font, brush, new System.Drawing.RectangleF(0, 0, size, size), sf);
                }
            }

            // Trả về bitmap đã tạo
            Debug.WriteLine($"[RibbonController] Tạo placeholder icon {size}x{size}.");
            return bmp;
        }

        // ─── Event handlers ───────────────────────────────────────────────────

        /// <summary>
        /// Xử lý khi user click button "Symbol Replacer" trên ribbon.
        /// Toggle hiện/ẩn DockableWindow.
        /// </summary>
        private void OnSymbolReplacerButtonExecute(NameValueMap context)
        {
            // User click button — toggle visibility của dockable window
            Debug.WriteLine($"{LOG_PREFIX} Button click — toggle DockableWindow visibility.");

            if (_dockableWindow == null)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI: _dockableWindow là null khi click button.");
                return;
            }

            bool newVisibility = !_dockableWindow.Visible;
            _dockableWindow.Visible = newVisibility;

            Debug.WriteLine($"{LOG_PREFIX} DockableWindow.Visible = {newVisibility}");

            // Cập nhật trạng thái pressed của button (toggle style)
            _buttonDef.Pressed = newVisibility;
        }

        /// <summary>
        /// Xử lý khi DockableWindow được show (lần đầu hoặc sau khi ẩn).
        /// Inventor 2023 API: DockableWindowsEvents.OnShow(DockableWindow, EventTimingEnum, NameValueMap, ref HandlingCodeEnum)
        /// Dùng để embed WinForms panel khi HWND lần đầu có sẵn, và re-attach sizer.
        /// </summary>
        private void OnDockableWindowShow(DockableWindow dockableWindow,
                                          EventTimingEnum beforeOrAfter,
                                          NameValueMap context,
                                          out HandlingCodeEnum handlingCode)
        {
            handlingCode = HandlingCodeEnum.kEventNotHandled;

            // Chỉ xử lý cho DockableWindow của addin này, sau khi đã show
            if (dockableWindow == null || dockableWindow.InternalName != DOCKWIN_ID) return;
            if (beforeOrAfter != EventTimingEnum.kAfter) return;

            Debug.WriteLine($"{LOG_PREFIX} OnDockableWindowShow — kiểm tra embed panel.");

            // Nếu panel chưa được embed (HWND=0 lúc tạo), embed bây giờ
            if (_panel == null || _panel.IsDisposed || _dockWindowSizer == null)
            {
                EmbedWinFormsPanel();
            }
            else
            {
                // Panel đã embed, chỉ resize cho khớp kích thước hiện tại
                int w = dockableWindow.Width;
                int h = dockableWindow.Height;
                if (w > 0 && h > 0)
                    MoveWindow(_panel.Handle, 0, 0, w, h, true);
            }
        }

        // ─── Nested class: NativeWindow sizer ────────────────────────────────

        /// <summary>
        /// Subclass HWND của DockableWindow để bắt WM_SIZE và resize WinForms panel.
        /// Inventor 2023 không có OnResize event trên DockableWindow — dùng Win32 thay thế.
        /// </summary>
        private sealed class DockWindowSizer : NativeWindow
        {
            private const int WM_SIZE = 0x0005;

            [DllImport("user32.dll", SetLastError = true)]
            private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int nWidth, int nHeight, bool bRepaint);

            private readonly SymbolReplacerPanel _panel;

            public DockWindowSizer(IntPtr hwnd, SymbolReplacerPanel panel)
            {
                _panel = panel;
                AssignHandle(hwnd);
            }

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);

                if (m.Msg == WM_SIZE && _panel != null && !_panel.IsDisposed)
                {
                    // LParam = MAKELPARAM(width, height)
                    int width  = (int)(m.LParam.ToInt64() & 0xFFFF);
                    int height = (int)((m.LParam.ToInt64() >> 16) & 0xFFFF);

                    if (width > 0 && height > 0)
                        MoveWindow(_panel.Handle, 0, 0, width, height, true);
                }
            }
        }
    }
}
