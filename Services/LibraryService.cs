using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;  // Directory, Path — aliased below to avoid conflict with Inventor.Path
using Inventor;
using SystemFile = System.IO.File;
using SystemPath = System.IO.Path;
using SystemDir  = System.IO.Directory;

namespace SymbolReplacer.Services
{
    /// <summary>
    /// Mở tất cả file .idw trong một folder và đọc SketchedSymbolDefinitions.
    /// Tái sử dụng document nếu đã mở trong Inventor, không mở lại.
    /// Giữ danh sách tất cả document đã mở để có thể đóng khi cleanup.
    /// </summary>
    public class LibraryService : ILibraryService
    {
        // ─── Hằng số log ──────────────────────────────────────────────────────
        private const string LOG_PREFIX = "[LibraryService]";

        // ─── Fields ───────────────────────────────────────────────────────────
        private readonly Inventor.Application _app;

        // Danh sách các document addin đã tự mở (cần tự đóng khi cleanup)
        private readonly List<Document> _openedByUs = new List<Document>();

        // ─── ILibraryService ──────────────────────────────────────────────────
        public string CurrentPath { get; private set; }

        // ─── Constructor ──────────────────────────────────────────────────────
        public LibraryService(Inventor.Application app)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
            Debug.WriteLine($"{LOG_PREFIX} Khởi tạo LibraryService.");
        }

        // ─── Public methods ───────────────────────────────────────────────────

        /// <summary>
        /// Đọc tất cả symbol từ folder (tất cả file .idw trong folder).
        /// Nếu path là file .idw cụ thể → chỉ đọc file đó.
        /// </summary>
        public IReadOnlyList<SketchedSymbolDefinition> LoadDefinitions(string libraryPath)
        {
            Debug.WriteLine($"{LOG_PREFIX} LoadDefinitions: {libraryPath}");

            var result = new List<SketchedSymbolDefinition>();

            if (string.IsNullOrWhiteSpace(libraryPath))
            {
                Debug.WriteLine($"{LOG_PREFIX} Path rỗng, bỏ qua.");
                return result;
            }

            // Đóng library cũ nếu path thay đổi
            if (CurrentPath != null &&
                !string.Equals(CurrentPath, libraryPath, StringComparison.OrdinalIgnoreCase))
            {
                CloseLibrary();
            }

            CurrentPath = libraryPath;

            // Xác định danh sách file .idw cần đọc
            var idwFiles = new List<string>();

            if (SystemDir.Exists(libraryPath))
            {
                // Là folder → quét tất cả .idw trong folder (không đệ quy)
                var files = SystemDir.GetFiles(libraryPath, "*.idw", SearchOption.TopDirectoryOnly);
                idwFiles.AddRange(files);
                Debug.WriteLine($"{LOG_PREFIX} Folder: tìm thấy {files.Length} file .idw.");
            }
            else if (SystemFile.Exists(libraryPath) &&
                     string.Equals(SystemPath.GetExtension(libraryPath), ".idw",
                         StringComparison.OrdinalIgnoreCase))
            {
                // Là file .idw cụ thể
                idwFiles.Add(libraryPath);
                Debug.WriteLine($"{LOG_PREFIX} File .idw đơn lẻ.");
            }
            else
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI: Path không tồn tại hoặc không phải .idw/folder: {libraryPath}");
                return result;
            }

            if (idwFiles.Count == 0)
            {
                Debug.WriteLine($"{LOG_PREFIX} Không tìm thấy file .idw nào.");
                return result;
            }

            // Đọc definitions từ từng file
            foreach (var filePath in idwFiles)
                LoadFromFile(filePath, result);

            Debug.WriteLine($"{LOG_PREFIX} Tổng: {result.Count} symbol definitions từ {idwFiles.Count} file.");
            return result;
        }

        /// <summary>
        /// Đọc symbols trực tiếp từ DrawingDocument đang mở trong Inventor (không mở file mới).
        /// Dùng cho tab "Local" trong palette.
        /// </summary>
        public IReadOnlyList<SketchedSymbolDefinition> LoadLocalDefinitions(DrawingDocument doc)
        {
            var result = new List<SketchedSymbolDefinition>();
            if (doc == null)
            {
                Debug.WriteLine($"{LOG_PREFIX} LoadLocalDefinitions: doc null, bỏ qua.");
                return result;
            }

            try
            {
                var defs = doc.SketchedSymbolDefinitions;
                Debug.WriteLine($"{LOG_PREFIX} LoadLocalDefinitions: {defs.Count} symbols từ '{SystemPath.GetFileName(doc.FullFileName)}'.");
                foreach (SketchedSymbolDefinition def in defs)
                    result.Add(def);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI LoadLocalDefinitions: {ex.Message}");
            }

            return result;
        }

        public void CloseLibrary()
        {
            Debug.WriteLine($"{LOG_PREFIX} CloseLibrary ({_openedByUs.Count} docs đã mở).");

            foreach (var doc in _openedByUs)
            {
                try
                {
                    // Kiểm tra document còn valid trước khi đóng.
                    // Khi Inventor đang shutdown, COM objects có thể đã bị release
                    // → doc.Close() sẽ crash với access violation.
                    string fileName = null;
                    try { fileName = doc.FullFileName; } catch { }

                    if (fileName != null)
                    {
                        doc.Close(true);
                        Debug.WriteLine($"{LOG_PREFIX} Đã đóng: {fileName}");
                    }
                    else
                    {
                        Debug.WriteLine($"{LOG_PREFIX} Document đã bị release bởi Inventor — bỏ qua.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"{LOG_PREFIX} LỖI đóng doc (có thể Inventor đang shutdown): {ex.Message}");
                }
            }

            _openedByUs.Clear();
            CurrentPath = null;
        }

        // ─── Private helpers ──────────────────────────────────────────────────

        private void LoadFromFile(string filePath, List<SketchedSymbolDefinition> result)
        {
            Debug.WriteLine($"{LOG_PREFIX} LoadFromFile: {SystemPath.GetFileName(filePath)}");
            Document doc = null;

            try
            {
                // Tái sử dụng nếu đã mở trong Inventor
                doc = FindOpenDocument(filePath);
                bool weOpened = false;

                if (doc != null)
                {
                    Debug.WriteLine($"{LOG_PREFIX}   Đã mở sẵn, tái sử dụng.");
                }
                else
                {
                    doc = _app.Documents.Open(filePath, false);
                    weOpened = true;
                    Debug.WriteLine($"{LOG_PREFIX}   Mở mới thành công.");
                }

                if (weOpened)
                    _openedByUs.Add(doc);

                var drawDoc = doc as DrawingDocument;
                if (drawDoc == null)
                {
                    Debug.WriteLine($"{LOG_PREFIX}   LỖI: Không phải DrawingDocument, bỏ qua.");
                    return;
                }

                var defs = drawDoc.SketchedSymbolDefinitions;
                Debug.WriteLine($"{LOG_PREFIX}   {defs.Count} symbols trong {SystemPath.GetFileName(filePath)}.");

                foreach (SketchedSymbolDefinition def in defs)
                    result.Add(def);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI LoadFromFile '{SystemPath.GetFileName(filePath)}': {ex.Message}");
            }
        }

        private Document FindOpenDocument(string filePath)
        {
            try
            {
                foreach (Document doc in _app.Documents)
                {
                    if (string.Equals(doc.FullFileName, filePath,
                        StringComparison.OrdinalIgnoreCase))
                        return doc;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI kiểm tra doc đã mở: {ex.Message}");
            }
            return null;
        }
    }
}
