using System;
using System.Diagnostics;
using Inventor;

namespace SymbolReplacer.Controllers
{
    /// <summary>
    /// Quản lý pick mode bằng Inventor InteractionEvents + SelectEvents.
    /// Cho phép user click chọn một SketchedSymbol trên bản vẽ.
    ///
    /// Flow:
    ///   1. EnterPickMode() → Start interaction, set filter kDrawingSketchedSymbolFilter
    ///   2. User click symbol → OnSelect → raise SymbolPicked event
    ///   3. ExitPickMode() tự động (sau khi pick) hoặc user nhấn ESC (OnTerminate)
    /// </summary>
    public class InteractionController
    {
        // ─── Hằng số log ──────────────────────────────────────────────────────
        private const string LOG_PREFIX = "[InteractionController]";

        // ─── Fields ───────────────────────────────────────────────────────────
        private readonly Inventor.Application _app;
        private InteractionEvents             _interactionEvents;
        private SelectEvents                  _selectEvents;
        private MouseEvents                   _mouseEvents;   // phải là field, không phải local — tránh GC
        private bool                          _isActive;
        private bool                          _isInsertMode;

        // ─── Events ───────────────────────────────────────────────────────────

        /// <summary>Raised khi user đã click chọn được một SketchedSymbol trên bản vẽ.</summary>
        public event EventHandler<SketchedSymbol> SymbolPicked;

        /// <summary>Raised khi pick mode kết thúc (picked hoặc ESC).</summary>
        public event EventHandler PickModeCancelled;

        /// <summary>Raised khi user click điểm trên bản vẽ trong insert mode.</summary>
        public event EventHandler<Point2d> InsertPointPicked;

        /// <summary>Raised khi insert mode kết thúc do ESC.</summary>
        public event EventHandler InsertModeCancelled;

        // ─── Properties ───────────────────────────────────────────────────────
        public bool IsActive => _isActive;

        // ─── Constructor ──────────────────────────────────────────────────────
        public InteractionController(Inventor.Application app)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
            Debug.WriteLine($"{LOG_PREFIX} Khởi tạo InteractionController.");
        }

        // ─── Public methods ───────────────────────────────────────────────────

        /// <summary>
        /// Bắt đầu pick mode — chờ user click vào SketchedSymbol trên bản vẽ.
        /// Nếu đang active, stop trước rồi start lại.
        /// </summary>
        public void EnterPickMode()
        {
            Debug.WriteLine($"{LOG_PREFIX} EnterPickMode.");

            // Nếu đang active → stop trước
            if (_isActive)
                StopInteraction();

            try
            {
                // Tạo InteractionEvents mới
                _interactionEvents = _app.CommandManager.CreateInteractionEvents();
                _interactionEvents.StatusBarText = "Pick symbol to replace — press ESC to cancel";

                // Đăng ký OnTerminate để biết khi user nhấn ESC
                _interactionEvents.OnTerminate += OnInteractionTerminate;

                // Lấy SelectEvents và cấu hình filter chỉ cho SketchedSymbol
                _selectEvents = _interactionEvents.SelectEvents;
                _selectEvents.AddSelectionFilter(SelectionFilterEnum.kDrawingSketchedSymbolFilter);
                _selectEvents.SingleSelectEnabled = true;    // Chỉ pick 1 cái mỗi lần

                // Đăng ký event khi user click
                _selectEvents.OnSelect += OnSymbolSelected;

                // Bắt đầu interaction
                _interactionEvents.Start();
                _isActive = true;

                Debug.WriteLine($"{LOG_PREFIX} Pick mode ACTIVE.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI EnterPickMode: {ex.Message}");
                StopInteraction();
            }
        }

        /// <summary>
        /// Bắt đầu insert mode — chờ user click điểm trên bản vẽ để đặt symbol.
        /// Dùng PointEvents thay vì SelectEvents.
        /// </summary>
        public void EnterInsertMode()
        {
            Debug.WriteLine($"{LOG_PREFIX} EnterInsertMode.");

            if (_isActive) StopInteraction();

            try
            {
                _interactionEvents = _app.CommandManager.CreateInteractionEvents();
                _interactionEvents.StatusBarText = "Click to place symbol — press ESC to cancel";
                _interactionEvents.OnTerminate   += OnInteractionTerminate;

                // Dùng MouseEvents.OnMouseClick để nhận tọa độ click trên bản vẽ
                // QUAN TRỌNG: lưu vào field _mouseEvents (không dùng local var) để tránh COM GC
                _mouseEvents = _interactionEvents.MouseEvents;
                _mouseEvents.MouseMoveEnabled = false;
                _mouseEvents.OnMouseClick += OnInsertMouseClick;

                _interactionEvents.Start();
                _isActive = true;
                _isInsertMode = true;

                Debug.WriteLine($"{LOG_PREFIX} Insert mode ACTIVE.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI EnterInsertMode: {ex.Message}");
                StopInteraction();
            }
        }

        /// <summary>Dừng pick mode từ bên ngoài (ví dụ user click Cancel).</summary>
        public void ExitPickMode()
        {
            Debug.WriteLine($"{LOG_PREFIX} ExitPickMode (từ bên ngoài).");
            StopInteraction();
            PickModeCancelled?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Dọn dẹp khi addin bị tắt.</summary>
        public void Cleanup()
        {
            Debug.WriteLine($"{LOG_PREFIX} Cleanup.");
            StopInteraction();
        }

        // ─── Private: Event handlers ──────────────────────────────────────────

        private void OnSymbolSelected(
            ObjectsEnumerator justSelectedEntities,
            SelectionDeviceEnum selectionDevice,
            Point modelPosition,
            Point2d viewPosition,
            View view)
        {
            Debug.WriteLine($"{LOG_PREFIX} OnSymbolSelected — count={justSelectedEntities.Count}");

            SketchedSymbol picked = null;

            try
            {
                // Lấy entity đầu tiên (SingleSelectEnabled = true nên chỉ có 1)
                foreach (var entity in justSelectedEntities)
                {
                    if (entity is SketchedSymbol sym)
                    {
                        picked = sym;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI đọc selected entity: {ex.Message}");
            }

            if (picked == null)
            {
                Debug.WriteLine($"{LOG_PREFIX} Entity được pick không phải SketchedSymbol, bỏ qua.");
                return;
            }

            Debug.WriteLine($"{LOG_PREFIX} Picked symbol: '{picked.Name}' def='{picked.Definition?.Name}'");

            // Dừng interaction SAU KHI đã đọc symbol (tránh mất reference)
            StopInteraction();

            // Raise event để ReplaceController xử lý replace
            SymbolPicked?.Invoke(this, picked);
        }

        private void OnInsertMouseClick(
            MouseButtonEnum button,
            ShiftStateEnum shiftKeys,
            Point modelPosition,
            Point2d viewPosition,
            View view)
        {
            // Chỉ xử lý left-click
            if (button != MouseButtonEnum.kLeftMouseButton) return;

            Debug.WriteLine($"{LOG_PREFIX} OnInsertMouseClick: ({modelPosition.X:F3}, {modelPosition.Y:F3})");

            // modelPosition là Point 3D nhưng trong Drawing thì Z=0, X/Y là sheet coords
            Point2d pos2d;
            try
            {
                pos2d = _app.TransientGeometry.CreatePoint2d(modelPosition.X, modelPosition.Y);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI tạo Point2d: {ex.Message}");
                return;
            }

            StopInteraction();      // kết thúc mode trước khi raise event
            InsertPointPicked?.Invoke(this, pos2d);
        }

        private void OnInteractionTerminate()
        {
            // User nhấn ESC hoặc Inventor kết thúc interaction
            Debug.WriteLine($"{LOG_PREFIX} OnInteractionTerminate (ESC hoặc API stop).");

            bool wasActive    = _isActive;
            bool wasInsert    = _isInsertMode;
            StopInteraction();

            if (wasActive)
            {
                if (wasInsert)
                    InsertModeCancelled?.Invoke(this, EventArgs.Empty);
                else
                    PickModeCancelled?.Invoke(this, EventArgs.Empty);
            }
        }

        // ─── Private: Cleanup interaction ─────────────────────────────────────

        private void StopInteraction()
        {
            if (!_isActive && _interactionEvents == null) return;

            Debug.WriteLine($"{LOG_PREFIX} StopInteraction.");

            try
            {
                if (_selectEvents != null)
                {
                    _selectEvents.OnSelect -= OnSymbolSelected;
                    _selectEvents = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI detach SelectEvents: {ex.Message}");
            }

            try
            {
                if (_mouseEvents != null)
                {
                    _mouseEvents.OnMouseClick -= OnInsertMouseClick;
                    _mouseEvents = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI detach MouseEvents: {ex.Message}");
            }

            try
            {
                if (_interactionEvents != null)
                {
                    _interactionEvents.OnTerminate -= OnInteractionTerminate;
                    // Bọc Stop() trong try riêng — có thể throw khi Inventor đang shutdown
                    try { _interactionEvents.Stop(); } catch { }
                    _interactionEvents = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI stop InteractionEvents: {ex.Message}");
            }

            _isActive     = false;
            _isInsertMode = false;
        }
    }
}
