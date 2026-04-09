using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;                      // NativeWindow (DockWindowSizer)
using System.Windows.Interop;                    // HwndSource, HwndSourceParameters
using Inventor;
using SymbolReplacer.Helpers;
using SymbolReplacer.Views;

namespace SymbolReplacer.Controllers
{
    /// <summary>
    /// Chịu trách nhiệm:
    ///   1. Tạo/tìm tab "Custom Tools" trên Drawing ribbon
    ///   2. Tạo button "Symbol Replacer" với icon
    ///   3. Quản lý DockableWindow — nhúng WPF UserControl qua HwndSource
    ///
    /// Tại sao dùng HwndSource thay vì SetParent + SetWindowLong:
    ///   HwndSource tạo Win32 HWND với WS_CHILD ngay từ đầu (qua HwndSourceParameters.WindowStyle).
    ///   Không cần các hack WS_POPUP → WS_CHILD, không cần SWP_FRAMECHANGED, RedrawWindow, v.v.
    ///   WPF rendering pipeline tự xử lý layout, painting, và resize thông qua Dispatcher.
    /// </summary>
    public class RibbonController
    {
        // ─── Hằng số ──────────────────────────────────────────────────────────
        private const string LOG_PREFIX           = "[RibbonController]";
        private const string RIBBON_NAME          = "Drawing";
        private const string TAB_ID               = "id.Tab.CustomTools";
        private const string TAB_DISPLAY          = "Custom Tools";
        private const string PANEL_ID             = "id.Panel.SymbolTools";
        private const string PANEL_DISPLAY        = "Symbol Tools";
        private const string BUTTON_ID            = "id.Button.SymbolReplacer";
        private const string BUTTON_DISPLAY       = "Symbol\nReplacer";
        private const string BUTTON_TOOLTIP       = "Symbol Replacer";
        private const string BUTTON_DESCRIPTION   = "Replace drawing symbols while preserving position and attributes";
        private const string DOCKWIN_ID           = "SymbolReplacer.DockableWindow";
        private const string DOCKWIN_TITLE        = "Symbol Replacer";
        private const string ADDIN_GUID           = "{7C3D8E4F-2A1B-4C5D-9E8F-1A2B3C4D5E6F}";

        // ─── Win32 — chỉ giữ những gì cần thiết ──────────────────────────────
        // GetClientRect: lấy kích thước thực của DockableWindow khi tạo HwndSource
        // MoveWindow   : resize HwndSource HWND khi DockableWindow resize (qua DockWindowSizer)

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        // WS_CHILD | WS_VISIBLE — đặt ngay khi tạo HwndSource (không cần SetWindowLong sau)
        private const int WS_CHILD   = 0x40000000;
        private const int WS_VISIBLE = 0x10000000;

        // ─── Fields ───────────────────────────────────────────────────────────
        private readonly Inventor.Application _app;
        private ButtonDefinition              _buttonDef;
        private DockableWindow                _dockableWindow;
        private DockableWindowsEvents         _dockableWindowsEvents;
        private HwndSource                    _hwndSource;        // WPF host window nhúng vào DockableWindow
        private DockWindowSizer               _dockWindowSizer;   // NativeWindow bắt WM_SIZE
        private SymbolReplacerPanel           _panel;             // WPF UserControl
        private System.Windows.Forms.Timer    _embedRetryTimer;   // Retry khi DockableWindow.HWND=0 ở kAfter

        // ─── Constructor ──────────────────────────────────────────────────────
        public RibbonController(Inventor.Application app)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
            Debug.WriteLine($"{LOG_PREFIX} Khởi tạo RibbonController.");
        }

        // ─── Public properties ────────────────────────────────────────────────

        /// <summary>
        /// WPF panel đã được nhúng vào DockableWindow.
        /// PaletteController dùng để đăng ký events và cập nhật SymbolGrid.
        /// </summary>
        public SymbolReplacerPanel Panel => _panel;

        // ─── Public methods ───────────────────────────────────────────────────

        /// <summary>Tạo toàn bộ Ribbon UI: tab, panel, button, DockableWindow.</summary>
        public void CreateRibbonUI()
        {
            Debug.WriteLine($"{LOG_PREFIX} Bắt đầu tạo Ribbon UI...");

            try
            {
                var ribbon = GetDrawingRibbon();
                if (ribbon == null) return;

                var tab = GetOrCreateTab(ribbon);
                if (tab == null) return;

                var panel = GetOrCreatePanel(tab);
                if (panel == null) return;

                CreateButtonDefinition();
                AddButtonToPanel(panel);
                CreateDockableWindow();

                Debug.WriteLine($"{LOG_PREFIX} Ribbon UI tạo THÀNH CÔNG.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI tạo Ribbon UI: {ex.Message}");
                Debug.WriteLine($"{LOG_PREFIX} Stack trace:\n{ex.StackTrace}");
                throw;
            }
        }

        /// <summary>Re-wire event handlers khi addin được reload (firstTime=false).</summary>
        public void RewireEvents()
        {
            Debug.WriteLine($"{LOG_PREFIX} Re-wire events...");

            try
            {
                _buttonDef = (ButtonDefinition)_app.CommandManager.ControlDefinitions[BUTTON_ID];
                if (_buttonDef != null)
                {
                    _buttonDef.OnExecute -= OnSymbolReplacerButtonExecute;
                    _buttonDef.OnExecute += OnSymbolReplacerButtonExecute;
                    Debug.WriteLine($"{LOG_PREFIX} Re-wire button event thành công.");
                }

                try
                {
                    _dockableWindow = _app.UserInterfaceManager.DockableWindows[DOCKWIN_ID];
                    if (_dockableWindow != null)
                    {
                        _dockableWindowsEvents = _app.UserInterfaceManager.DockableWindows.Events;
                        _dockableWindowsEvents.OnShow -= OnDockableWindowShow;
                        _dockableWindowsEvents.OnShow += OnDockableWindowShow;
                        Debug.WriteLine($"{LOG_PREFIX} Re-wire DockableWindow OnShow event thành công.");
                    }
                }
                catch
                {
                    Debug.WriteLine($"{LOG_PREFIX} DockableWindow chưa tồn tại khi re-wire, bỏ qua.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI re-wire events: {ex.Message}");
            }
        }

        /// <summary>Dọn dẹp tài nguyên khi addin bị tắt.</summary>
        public void Cleanup()
        {
            Debug.WriteLine($"{LOG_PREFIX} Bắt đầu cleanup...");

            try
            {
                if (_buttonDef != null)
                    _buttonDef.OnExecute -= OnSymbolReplacerButtonExecute;
            }
            catch (Exception ex) { Debug.WriteLine($"{LOG_PREFIX} LỖI detach button: {ex.Message}"); }

            try
            {
                if (_dockableWindowsEvents != null)
                    _dockableWindowsEvents.OnShow -= OnDockableWindowShow;
            }
            catch (Exception ex) { Debug.WriteLine($"{LOG_PREFIX} LỖI detach dockable events: {ex.Message}"); }

            try
            {
                if (_embedRetryTimer != null)
                {
                    _embedRetryTimer.Stop();
                    _embedRetryTimer.Dispose();
                    _embedRetryTimer = null;
                }
            }
            catch (Exception ex) { Debug.WriteLine($"{LOG_PREFIX} LỖI dispose retry timer: {ex.Message}"); }

            try
            {
                if (_dockWindowSizer != null)
                {
                    _dockWindowSizer.ReleaseHandle();
                    _dockWindowSizer = null;
                }
            }
            catch (Exception ex) { Debug.WriteLine($"{LOG_PREFIX} LỖI release sizer: {ex.Message}"); }

            // Unhook keyboard hook TRƯỚC KHI dispose HwndSource/panel
            // (nếu hook callback fire sau GC → crash Inventor khi đóng)
            try { Views.SymbolReplacerPanel.CleanupKeyboardHook(); }
            catch (Exception ex) { Debug.WriteLine($"{LOG_PREFIX} LỖI cleanup keyboard hook: {ex.Message}"); }

            try
            {
                if (_hwndSource != null)
                {
                    _hwndSource.Dispose();
                    _hwndSource = null;
                }
            }
            catch (Exception ex) { Debug.WriteLine($"{LOG_PREFIX} LỖI dispose HwndSource: {ex.Message}"); }

            // WPF UserControl không có Dispose — chỉ cần bỏ reference
            _panel = null;

            Debug.WriteLine($"{LOG_PREFIX} Cleanup THÀNH CÔNG.");
        }

        // ─── Private: Ribbon setup ────────────────────────────────────────────

        private Ribbon GetDrawingRibbon()
        {
            try
            {
                var ribbon = _app.UserInterfaceManager.Ribbons[RIBBON_NAME];
                Debug.WriteLine($"{LOG_PREFIX} Lấy được Drawing ribbon.");
                return ribbon;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI lấy Drawing ribbon: {ex.Message}");
                return null;
            }
        }

        private RibbonTab GetOrCreateTab(Ribbon ribbon)
        {
            try
            {
                var tab = ribbon.RibbonTabs[TAB_ID];
                Debug.WriteLine($"{LOG_PREFIX} Tab '{TAB_DISPLAY}' đã tồn tại, tái sử dụng.");
                return tab;
            }
            catch { Debug.WriteLine($"{LOG_PREFIX} Tab '{TAB_DISPLAY}' chưa tồn tại, tạo mới..."); }

            try
            {
                var tab = ribbon.RibbonTabs.Add(TAB_DISPLAY, TAB_ID, ADDIN_GUID);
                Debug.WriteLine($"{LOG_PREFIX} Tạo tab '{TAB_DISPLAY}' THÀNH CÔNG.");
                return tab;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI tạo tab: {ex.Message}");
                return null;
            }
        }

        private RibbonPanel GetOrCreatePanel(RibbonTab tab)
        {
            try
            {
                var panel = tab.RibbonPanels[PANEL_ID];
                Debug.WriteLine($"{LOG_PREFIX} Panel '{PANEL_DISPLAY}' đã tồn tại, tái sử dụng.");
                return panel;
            }
            catch { Debug.WriteLine($"{LOG_PREFIX} Panel '{PANEL_DISPLAY}' chưa tồn tại, tạo mới..."); }

            try
            {
                var panel = tab.RibbonPanels.Add(PANEL_DISPLAY, PANEL_ID, ADDIN_GUID);
                Debug.WriteLine($"{LOG_PREFIX} Tạo panel '{PANEL_DISPLAY}' THÀNH CÔNG.");
                return panel;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI tạo panel: {ex.Message}");
                return null;
            }
        }

        private void CreateButtonDefinition()
        {
            try
            {
                _buttonDef = (ButtonDefinition)_app.CommandManager.ControlDefinitions[BUTTON_ID];
                Debug.WriteLine($"{LOG_PREFIX} ButtonDefinition đã tồn tại, tái sử dụng.");
                _buttonDef.OnExecute -= OnSymbolReplacerButtonExecute;
                _buttonDef.OnExecute += OnSymbolReplacerButtonExecute;
                return;
            }
            catch { Debug.WriteLine($"{LOG_PREFIX} ButtonDefinition chưa tồn tại, tạo mới..."); }

            var icon32Bmp = PictureDispConverter.LoadFromResource("ReplaceSymbol_32.png");
            var icon16Bmp = PictureDispConverter.LoadFromResource("ReplaceSymbol_16.png");

            if (icon32Bmp == null)
            {
                Debug.WriteLine($"{LOG_PREFIX} CẢNH BÁO: Không có icon, dùng placeholder.");
                icon32Bmp = CreatePlaceholderIcon(32);
                icon16Bmp = CreatePlaceholderIcon(16);
            }

            var icon32 = PictureDispConverter.ToIPictureDisp(icon32Bmp);
            var icon16 = PictureDispConverter.ToIPictureDisp(icon16Bmp);

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

            _buttonDef.OnExecute += OnSymbolReplacerButtonExecute;
            Debug.WriteLine($"{LOG_PREFIX} ButtonDefinition tạo THÀNH CÔNG: {BUTTON_ID}");

            icon32Bmp?.Dispose();
            icon16Bmp?.Dispose();
        }

        private void AddButtonToPanel(RibbonPanel panel)
        {
            try
            {
                var _ = panel.CommandControls[BUTTON_ID];
                Debug.WriteLine($"{LOG_PREFIX} Button đã có trong panel, bỏ qua thêm mới.");
            }
            catch
            {
                panel.CommandControls.AddButton(_buttonDef, UseLargeIcon: true);
                Debug.WriteLine($"{LOG_PREFIX} Đã thêm button vào panel '{PANEL_DISPLAY}'.");
            }
        }

        // ─── Private: DockableWindow + WPF embed ─────────────────────────────

        private void CreateDockableWindow()
        {
            Debug.WriteLine($"{LOG_PREFIX} Bắt đầu tạo DockableWindow...");

            var uiManager = _app.UserInterfaceManager;
            _dockableWindowsEvents = uiManager.DockableWindows.Events;

            try
            {
                _dockableWindow = uiManager.DockableWindows[DOCKWIN_ID];
                Debug.WriteLine($"{LOG_PREFIX} DockableWindow đã tồn tại, tái sử dụng.");
                _dockableWindowsEvents.OnShow -= OnDockableWindowShow;
                _dockableWindowsEvents.OnShow += OnDockableWindowShow;
                return;
            }
            catch { Debug.WriteLine($"{LOG_PREFIX} DockableWindow chưa tồn tại, tạo mới..."); }

            _dockableWindow = uiManager.DockableWindows.Add(
                ClientId:     ADDIN_GUID,
                InternalName: DOCKWIN_ID,
                Title:        DOCKWIN_TITLE
            );

            _dockableWindow.ShowTitleBar = true;
            _dockableWindow.Visible      = false;
            _dockableWindow.SetMinimumSize(220, 400);
            _dockableWindow.DockingState = DockingStateEnum.kDockRight;

            Debug.WriteLine($"{LOG_PREFIX} DockableWindow tạo xong — thử embed WPF panel...");

            // Thử embed ngay; nếu HWND=0 (chưa show) sẽ retry trong OnDockableWindowShow
            EmbedWpfPanel();

            _dockableWindowsEvents.OnShow += OnDockableWindowShow;

            Debug.WriteLine($"{LOG_PREFIX} DockableWindow tạo THÀNH CÔNG.");
        }

        /// <summary>
        /// Nhúng WPF UserControl vào DockableWindow qua HwndSource.
        ///
        /// Tại sao HwndSource tốt hơn SetParent + SetWindowLong:
        ///   - HwndSourceParameters.WindowStyle = WS_CHILD | WS_VISIBLE tạo HWND đúng ngay từ đầu.
        ///   - Không cần hack WS_POPUP → WS_CHILD, không cần SWP_FRAMECHANGED.
        ///   - WPF Dispatcher xử lý layout/paint tự động, không cần RedrawWindow.
        ///   - HwndSourceParameters.ParentWindow thiết lập parent đúng cho COM environment.
        ///
        /// Ba nguyên nhân gây palette trống — tất cả được xử lý tại đây:
        ///   1. Không có Application.Current → WPF không render nội dung trong COM host.
        ///   2. DockableWindow.HWND = 0 ngay cả trong kAfter → HwndSource chưa được tạo → retry timer.
        ///   3. Chưa gọi UpdateLayout() sau khi gán RootVisual → WPF không vẽ lần đầu.
        /// </summary>
        private void EmbedWpfPanel()
        {
            // ── Fix 1: Đảm bảo WPF Application instance tồn tại ─────────────────
            // Bắt buộc khi host WPF trong COM/non-WPF process (như Inventor addin).
            // Không có Application.Current → ResourceDictionary / Theme / Renderer không hoạt động
            // → WPF control được tạo nhưng không vẽ gì.
            if (System.Windows.Application.Current == null)
            {
                new System.Windows.Application
                {
                    ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
                };
                Debug.WriteLine($"{LOG_PREFIX} WPF Application instance đã được tạo.");
            }

            // ── Tạo WPF panel nếu chưa có ────────────────────────────────────────
            if (_panel == null)
            {
                // Fix BAML "Set connectionId threw an exception" trong COM host:
                // BAML loader dùng Assembly.Load() để tìm assembly chứa code-behind.
                // Trong Inventor process, assembly đã load nhưng không nằm trong search path,
                // nên BAML không kết nối được event handler → lỗi connectionId.
                // AppDomain.AssemblyResolve cho phép trả về assembly đã load từ cache.
                AppDomain.CurrentDomain.AssemblyResolve += OnBamlAssemblyResolve;
                try
                {
                    _panel = new SymbolReplacerPanel();
                    Debug.WriteLine($"{LOG_PREFIX} WPF SymbolReplacerPanel đã tạo.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"{LOG_PREFIX} LỖI khi tạo SymbolReplacerPanel: {ex.Message}");
                    Debug.WriteLine($"{LOG_PREFIX} Stack:\n{ex.StackTrace}");
                    return;
                }
                finally
                {
                    AppDomain.CurrentDomain.AssemblyResolve -= OnBamlAssemblyResolve;
                }
            }

            // ── Fix 2: Kiểm tra HWND — nếu = 0 thì schedule retry ────────────────
            // Inventor quirk: DockableWindow.HWND có thể = 0 ngay cả trong OnShow(kAfter).
            // Giải pháp: retry sau 200 ms qua WinForms Timer (chạy trên UI thread).
            int dockHwnd = _dockableWindow.HWND;
            Debug.WriteLine($"{LOG_PREFIX} DockableWindow HWND: {dockHwnd}");

            if (dockHwnd == 0)
            {
                Debug.WriteLine($"{LOG_PREFIX} HWND = 0 — schedule retry sau 200 ms.");
                ScheduleEmbedRetry();
                return;
            }

            // HWND hợp lệ — hủy retry timer nếu đang chạy
            StopEmbedRetry();

            IntPtr hwndParent = new IntPtr(dockHwnd);

            // Lấy kích thước client area thực từ Win32 (Inventor API thường trả về 0)
            int w = 220, h = 400;
            RECT rect;
            if (GetClientRect(hwndParent, out rect))
            {
                w = Math.Max(rect.Right - rect.Left, 220);
                h = Math.Max(rect.Bottom - rect.Top, 400);
                Debug.WriteLine($"{LOG_PREFIX} ClientRect: {w}×{h}px");
            }

            // Dispose HwndSource cũ nếu đang re-embed
            if (_hwndSource != null)
            {
                _hwndSource.Dispose();
                _hwndSource = null;
                Debug.WriteLine($"{LOG_PREFIX} HwndSource cũ đã dispose.");
            }

            // Tạo HwndSource — Win32 HWND có WS_CHILD | WS_VISIBLE, parent = DockableWindow
            var parameters = new HwndSourceParameters("SymbolReplacer.WpfHost")
            {
                ParentWindow = hwndParent,
                WindowStyle  = WS_CHILD | WS_VISIBLE,
                Width        = w,
                Height       = h,
                PositionX    = 0,
                PositionY    = 0,
            };

            _hwndSource = new HwndSource(parameters);
            _hwndSource.RootVisual = _panel;

            // ── Fix 3: Kích hoạt layout pass đầu tiên ────────────────────────────
            // Trong COM host không có WPF message loop riêng, WPF không tự layout
            // sau khi RootVisual được gán. Gọi UpdateLayout() để vẽ ngay lập tức.
            _panel.UpdateLayout();

            Debug.WriteLine($"{LOG_PREFIX} WPF embed THÀNH CÔNG: HWND={_hwndSource.Handle}, {w}×{h}px");

            // Subclass DockableWindow HWND để bắt WM_SIZE và resize HwndSource theo
            if (_dockWindowSizer == null)
            {
                _dockWindowSizer = new DockWindowSizer(hwndParent, _hwndSource.Handle);
                Debug.WriteLine($"{LOG_PREFIX} DockWindowSizer attached.");
            }
            else
            {
                _dockWindowSizer.UpdateChildHandle(_hwndSource.Handle);
                Debug.WriteLine($"{LOG_PREFIX} DockWindowSizer child handle updated.");
            }
        }

        /// <summary>Tạo/khởi động retry timer để gọi lại EmbedWpfPanel sau 200 ms.</summary>
        private void ScheduleEmbedRetry()
        {
            if (_embedRetryTimer == null)
            {
                _embedRetryTimer = new System.Windows.Forms.Timer { Interval = 200 };
                _embedRetryTimer.Tick += (s, e) =>
                {
                    Debug.WriteLine($"{LOG_PREFIX} Retry EmbedWpfPanel (timer tick)...");
                    _embedRetryTimer.Stop();
                    EmbedWpfPanel();
                };
            }
            // Restart timer (trường hợp đang chạy rồi)
            _embedRetryTimer.Stop();
            _embedRetryTimer.Start();
        }

        /// <summary>Dừng retry timer nếu đang chạy.</summary>
        private void StopEmbedRetry()
        {
            if (_embedRetryTimer != null)
            {
                _embedRetryTimer.Stop();
                Debug.WriteLine($"{LOG_PREFIX} Retry timer dừng (HWND hợp lệ).");
            }
        }

        // ─── Private: BAML assembly resolution helper ─────────────────────────

        /// <summary>
        /// Giúp BAML loader tìm assembly trong COM host khi nó không nằm trong search path.
        /// Đăng ký tạm thời quanh new SymbolReplacerPanel() để tránh ảnh hưởng toàn bộ AppDomain.
        /// </summary>
        private static Assembly OnBamlAssemblyResolve(object sender, ResolveEventArgs args)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.FullName == args.Name)
                    return asm;
            }
            return null;
        }

        // ─── Private: Placeholder icon ────────────────────────────────────────

        private static Bitmap CreatePlaceholderIcon(int size)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.FromArgb(0, 120, 212));
                float fontSize = size * 0.3f;
                using (var font = new System.Drawing.Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel))
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
            return bmp;
        }

        // ─── Event handlers ───────────────────────────────────────────────────

        private void OnSymbolReplacerButtonExecute(NameValueMap context)
        {
            Debug.WriteLine($"{LOG_PREFIX} Button click — toggle DockableWindow visibility.");

            if (_dockableWindow == null)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI: _dockableWindow null khi click button.");
                return;
            }

            bool newVisibility = !_dockableWindow.Visible;
            _dockableWindow.Visible = newVisibility;
            _buttonDef.Pressed = newVisibility;

            Debug.WriteLine($"{LOG_PREFIX} DockableWindow.Visible = {newVisibility}");
        }

        /// <summary>
        /// Gọi khi DockableWindow được show — đây là thời điểm HWND chắc chắn tồn tại.
        /// Embed WPF panel nếu chưa làm, hoặc resize nếu đã embed rồi.
        /// </summary>
        private void OnDockableWindowShow(DockableWindow dockableWindow,
                                          EventTimingEnum beforeOrAfter,
                                          NameValueMap context,
                                          out HandlingCodeEnum handlingCode)
        {
            handlingCode = HandlingCodeEnum.kEventNotHandled;

            if (dockableWindow == null || dockableWindow.InternalName != DOCKWIN_ID) return;
            if (beforeOrAfter != EventTimingEnum.kAfter) return;

            Debug.WriteLine($"{LOG_PREFIX} OnDockableWindowShow — kiểm tra embed WPF panel.");

            if (_hwndSource == null || _panel == null)
            {
                // Chưa embed — thực hiện ngay bây giờ khi HWND đã có
                EmbedWpfPanel();
            }
            else
            {
                // Đã embed — resize HwndSource theo kích thước thực (dùng Win32 thay Inventor API hay trả 0)
                int dockHwnd = dockableWindow.HWND;
                if (dockHwnd != 0)
                {
                    RECT r;
                    if (GetClientRect(new IntPtr(dockHwnd), out r))
                    {
                        int w = Math.Max(r.Right - r.Left, 220);
                        int h = Math.Max(r.Bottom - r.Top, 400);
                        MoveWindow(_hwndSource.Handle, 0, 0, w, h, true);
                        _panel.UpdateLayout();
                        Debug.WriteLine($"{LOG_PREFIX} OnShow resize: {w}×{h}px");
                    }
                }
            }
        }

        // ─── Nested class: resize HwndSource khi DockableWindow resize ──────

        /// <summary>
        /// Subclass HWND của DockableWindow để bắt WM_SIZE.
        /// Khi DockableWindow resize, resize HwndSource (WPF host) theo.
        /// Dùng WinForms NativeWindow vì không có Inventor resize event.
        /// </summary>
        private sealed class DockWindowSizer : NativeWindow
        {
            private const int WM_SIZE = 0x0005;

            [DllImport("user32.dll", SetLastError = true)]
            private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int nWidth, int nHeight, bool bRepaint);

            private IntPtr _hwndChild;  // HWND của HwndSource (WPF host window)

            public DockWindowSizer(IntPtr hwndParent, IntPtr hwndChild)
            {
                _hwndChild = hwndChild;
                AssignHandle(hwndParent);
                Debug.WriteLine($"[DockWindowSizer] Assigned hwndParent={hwndParent}, hwndChild={hwndChild}");
            }

            /// <summary>Cập nhật HWND của WPF host khi re-embed (ví dụ dispose + tạo lại HwndSource).</summary>
            public void UpdateChildHandle(IntPtr hwndChild)
            {
                _hwndChild = hwndChild;
            }

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);

                if (m.Msg == WM_SIZE && _hwndChild != IntPtr.Zero)
                {
                    // LParam = MAKELPARAM(new_width, new_height)
                    int width  = (int)(m.LParam.ToInt64() & 0xFFFF);
                    int height = (int)((m.LParam.ToInt64() >> 16) & 0xFFFF);

                    if (width > 0 && height > 0)
                        MoveWindow(_hwndChild, 0, 0, width, height, true);
                }
            }
        }
    }
}
