using System;
using System.Drawing;
using Inventor;

namespace MCGInventorPlugin.Models.SymbolHandler
{
    /// <summary>
    /// Chứa thông tin của một Sketched Symbol definition từ file library.
    /// Được tạo bởi PaletteController và hiển thị trên ThumbnailGridControl.
    /// </summary>
    public class SymbolDefinitionModel : IDisposable
    {
        // ─── Properties ───────────────────────────────────────────────────────

        /// <summary>Tên symbol (từ SketchedSymbolDefinition.Name)</summary>
        public string Name { get; set; }

        /// <summary>Thumbnail đã render từ sketch geometry — 80×80px</summary>
        public Bitmap Thumbnail { get; set; }

        /// <summary>Reference đến Inventor object — dùng cho replace operation</summary>
        public SketchedSymbolDefinition Definition { get; set; }

        /// <summary>Đường dẫn file .idw nguồn chứa definition này.</summary>
        public string SourceFile { get; set; }

        /// <summary>Số lượng TextBox prompt fields trong definition.</summary>
        public int PromptCount { get; set; }

        // ─── IDisposable ──────────────────────────────────────────────────────

        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                Thumbnail?.Dispose();
                _disposed = true;
            }
        }
    }
}
