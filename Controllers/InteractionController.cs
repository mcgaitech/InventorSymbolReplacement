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
        private bool                          _isActive;

        // ─── Events ───────────────────────────────────────────────────────────

        /// <summary>Raised khi user đã click chọn được một SketchedSymbol trên bản vẽ.</summary>
        public event EventHandler<SketchedSymbol> SymbolPicked;

        /// <summary>Raised khi pick mode kết thúc (picked hoặc ESC).</summary>
        public event EventHandler PickModeCancelled;

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

        private void OnInteractionTerminate()
        {
            // User nhấn ESC hoặc Inventor kết thúc interaction
            Debug.WriteLine($"{LOG_PREFIX} OnInteractionTerminate (ESC hoặc API stop).");

            bool wasActive = _isActive;
            StopInteraction();

            if (wasActive)
                PickModeCancelled?.Invoke(this, EventArgs.Empty);
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
                if (_interactionEvents != null)
                {
                    _interactionEvents.OnTerminate -= OnInteractionTerminate;
                    _interactionEvents.Stop();
                    _interactionEvents = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI stop InteractionEvents: {ex.Message}");
            }

            _isActive = false;
        }
    }
}
