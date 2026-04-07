using System.Collections.Generic;
using Inventor;

namespace SymbolReplacer.Services
{
    /// <summary>
    /// Interface cho service mở file .idw library và đọc SketchedSymbolDefinitions.
    /// </summary>
    public interface ILibraryService
    {
        /// <summary>Đường dẫn file library hiện đang mở</summary>
        string CurrentPath { get; }

        /// <summary>
        /// Mở file library và trả về danh sách definitions.
        /// Nếu file đã mở sẵn trong Inventor → tái sử dụng, không mở lại.
        /// </summary>
        IReadOnlyList<SketchedSymbolDefinition> LoadDefinitions(string libraryPath);

        /// <summary>
        /// Đọc SketchedSymbolDefinitions từ DrawingDocument đang mở (Local source).
        /// Không mở file — chỉ đọc từ document đã có trong Inventor.
        /// </summary>
        IReadOnlyList<SketchedSymbolDefinition> LoadLocalDefinitions(DrawingDocument doc);

        /// <summary>Đóng library document (nếu addin đã mở nó)</summary>
        void CloseLibrary();
    }
}
