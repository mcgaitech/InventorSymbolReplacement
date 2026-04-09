using Inventor;

namespace MCGInventorPlugin.Core
{
    /// <summary>
    /// Interface chung cho mọi module trong MCGInventorPlugin.
    /// Mỗi module đại diện cho 1 nhóm công cụ (ví dụ SymbolHandler cho Drawing).
    /// </summary>
    public interface IModule
    {
        /// <summary>Tên module hiển thị trong log.</summary>
        string Name { get; }

        /// <summary>Môi trường Inventor: "Drawing", "Part", "Assembly".</summary>
        string Environment { get; }

        /// <summary>Khởi tạo services và controllers.</summary>
        void Activate(Application app, bool firstTime);

        /// <summary>Tạo ribbon UI (buttons, panels) cho module.</summary>
        void CreateUI(Application app);

        /// <summary>Dọn dẹp khi addin bị tắt hoặc Inventor đóng.</summary>
        void Cleanup();
    }
}
