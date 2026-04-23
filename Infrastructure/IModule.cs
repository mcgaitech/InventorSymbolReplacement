using System.Collections.Generic;
using Inventor;
using MCG.Inventor.Ribbon;

namespace MCGInventorPlugin.Infrastructure
{
    /// <summary>
    /// Interface chung cho mọi module trong MCGInventorPlugin.
    /// Mỗi module đại diện cho 1 nhóm công cụ (ví dụ SymbolHandler).
    /// Module không tự tạo ribbon — MCGRibbonManager làm chuyện đó qua IToolDescriptor.
    ///
    /// Lưu ý: IModule / ModuleManager là infrastructure nội bộ của addin này,
    /// KHÔNG nằm trong folder Core/ (portable SDK). Addin khác có thể dùng
    /// MCGRibbonManager trực tiếp mà không cần IModule.
    /// </summary>
    public interface IModule
    {
        /// <summary>Tên module hiển thị trong log.</summary>
        string Name { get; }

        /// <summary>Khởi tạo services và controllers. Gọi trước khi ribbon build.</summary>
        void Activate(Application app, bool firstTime);

        /// <summary>
        /// Trả về danh sách tool descriptor để MCGRibbonManager đăng ký với ribbon.
        /// Gọi sau Activate() và trước khi ribbon build.
        /// </summary>
        IEnumerable<IToolDescriptor> GetTools();

        /// <summary>Gọi sau khi ribbon đã build xong (wire controllers với palette nếu cần).</summary>
        void OnUIReady();

        /// <summary>Dọn dẹp khi addin bị tắt hoặc Inventor đóng.</summary>
        void Cleanup();
    }
}
