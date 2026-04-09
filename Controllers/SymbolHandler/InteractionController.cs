using System;
using System.Diagnostics;
using Inventor;

namespace MCGInventorPlugin.Controllers.SymbolHandler
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

        /// <summary>Raised khi user pick geometry trên bản vẽ trong insert mode.</summary>
        public event EventHandler<InsertPickEventArgs> InsertPointPicked;

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
        /// Bắt đầu insert mode — dual mode:
        ///   - SelectEvents: pick trên geometry → attached insert (entity highlight + snap)
        ///   - MouseEvents: click vùng trống → service tự tìm DrawingCurve gần nhất
        ///     Nếu gần geometry → attached; xa → floating
        /// Cả 2 path đều gửi position (đã snap) + entity (có thể null) → InsertSymbol xử lý.
        /// </summary>
        public void EnterInsertMode()
        {
            Debug.WriteLine($"{LOG_PREFIX} EnterInsertMode.");

            if (_isActive) StopInteraction();

            try
            {
                _interactionEvents = _app.CommandManager.CreateInteractionEvents();
                _interactionEvents.StatusBarText = "Pick geometry to attach, or click empty area for floating — ESC to cancel";
                _interactionEvents.OnTerminate   += OnInteractionTerminate;

                // SelectEvents: pick geometry → entity highlight khi hover + snap
                _selectEvents = _interactionEvents.SelectEvents;
                _selectEvents.AddSelectionFilter(SelectionFilterEnum.kDrawingCurveSegmentFilter);
                _selectEvents.AddSelectionFilter(SelectionFilterEnum.kDrawingCenterlineFilter);
                _selectEvents.AddSelectionFilter(SelectionFilterEnum.kDrawingCentermarkFilter);
                _selectEvents.AddSelectionFilter(SelectionFilterEnum.kSketchCurveFilter);
                _selectEvents.AddSelectionFilter(SelectionFilterEnum.kSketchPointFilter);
                _selectEvents.AddSelectionFilter(SelectionFilterEnum.kWorkPointFilter);
                _selectEvents.AddSelectionFilter(SelectionFilterEnum.kDrawingSketchedSymbolFilter);
                _selectEvents.SingleSelectEnabled = true;
                _selectEvents.OnSelect += OnInsertGeometrySelected;

                // MouseEvents: click vùng trống (SelectEvents không match)
                _mouseEvents = _interactionEvents.MouseEvents;
                _mouseEvents.MouseMoveEnabled = false;
                _mouseEvents.OnMouseClick += OnInsertMouseClickWithSnap;

                _interactionEvents.Start();
                _isActive = true;
                _isInsertMode = true;

                Debug.WriteLine($"{LOG_PREFIX} Insert mode ACTIVE (SelectEvents + MouseEvents dual mode).");
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

        private void OnInsertGeometrySelected(
            ObjectsEnumerator justSelectedEntities,
            SelectionDeviceEnum selectionDevice,
            Point modelPosition,
            Point2d viewPosition,
            View view)
        {
            Debug.WriteLine($"{LOG_PREFIX} OnInsertGeometrySelected: ({modelPosition.X:F3}, {modelPosition.Y:F3})");

            Point2d pos2d;
            try { pos2d = _app.TransientGeometry.CreatePoint2d(modelPosition.X, modelPosition.Y); }
            catch { return; }

            StopInteraction();

            // Gửi position + null geometry → service tự tìm DrawingCurve gần nhất
            // (entity từ SelectEvents không đáng tin — DrawingSketch, __ComObject, cast fail)
            var args = new InsertPickEventArgs(pos2d, null);
            InsertPointPicked?.Invoke(this, args);
        }

        /// <summary>
        /// MouseEvents handler — click vùng trống (SelectEvents không match entity nào).
        /// </summary>
        private void OnInsertMouseClickWithSnap(
            MouseButtonEnum button,
            ShiftStateEnum shiftKeys,
            Point modelPosition,
            Point2d viewPosition,
            View view)
        {
            if (button != MouseButtonEnum.kLeftMouseButton) return;

            Debug.WriteLine($"{LOG_PREFIX} OnInsertMouseClickWithSnap: ({modelPosition.X:F3}, {modelPosition.Y:F3})");

            Point2d pos2d;
            try
            {
                pos2d = _app.TransientGeometry.CreatePoint2d(modelPosition.X, modelPosition.Y);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX}   LỖI tạo Point2d: {ex.Message}");
                return;
            }

            StopInteraction();

            // PickedGeometry = null — service sẽ tự tìm DrawingCurve gần nhất
            var args = new InsertPickEventArgs(pos2d, null);
            InsertPointPicked?.Invoke(this, args);
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

        // ─── Private: Cleanup + StopInteraction ──────────────────────────────

        private void StopInteraction()
        {
            if (!_isActive && _interactionEvents == null) return;

            Debug.WriteLine($"{LOG_PREFIX} StopInteraction.");

            try
            {
                if (_selectEvents != null)
                {
                    _selectEvents.OnSelect -= OnSymbolSelected;
                    _selectEvents.OnSelect -= OnInsertGeometrySelected;
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
                    _mouseEvents.OnMouseClick -= OnInsertMouseClickWithSnap;
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

    /// <summary>
    /// Event args cho Insert mode — mang cả vị trí pick và geometry entity để tạo leader.
    /// </summary>
    public class InsertPickEventArgs : EventArgs
    {
        /// <summary>Vị trí pick trên sheet (sheet coordinates).</summary>
        public Point2d Position { get; }

        /// <summary>
        /// Geometry entity mà user pick (DrawingCurveSegment, etc.).
        /// Null nếu user click vào vùng trống (floating insert).
        /// </summary>
        public object PickedGeometry { get; }

        public InsertPickEventArgs(Point2d position, object pickedGeometry = null)
        {
            Position = position;
            PickedGeometry = pickedGeometry;
        }
    }
}
