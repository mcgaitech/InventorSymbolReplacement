using System.Drawing;
using Inventor;

namespace SymbolReplacer.Services
{
    /// <summary>
    /// Interface cho service render thumbnail từ SketchedSymbolDefinition.
    /// Cache thumbnail sau lần render đầu tiên để tránh render lại.
    /// </summary>
    public interface IThumbnailService
    {
        /// <summary>
        /// Lấy thumbnail 80×80px của symbol.
        /// Render từ sketch geometry lần đầu, cache cho các lần sau.
        /// </summary>
        Bitmap GetThumbnail(SketchedSymbolDefinition definition, int size = 80);

        /// <summary>Xóa toàn bộ cache (khi đổi library)</summary>
        void ClearCache();
    }
}
