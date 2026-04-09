using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using Inventor;
using MCGInventorPlugin.Utilities;

namespace MCGInventorPlugin.Services.SymbolHandler
{
    /// <summary>
    /// Render thumbnail từ DrawingSketch của SketchedSymbolDefinition.
    /// Cache kết quả vào Dictionary để tránh render lại khi scroll/filter.
    /// </summary>
    public class ThumbnailService : IThumbnailService
    {
        // ─── Hằng số log ──────────────────────────────────────────────────────
        private const string LOG_PREFIX = "[ThumbnailService]";
        private const int    PADDING    = 6;  // Pixel padding mỗi bên trong thumbnail

        // ─── Cache ─────────────────────────────────────────────────────────────
        // Key: symbol name (unique trong một library file)
        private readonly Dictionary<string, Bitmap> _cache
            = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);

        // ─── IThumbnailService ────────────────────────────────────────────────

        public Bitmap GetThumbnail(SketchedSymbolDefinition definition, int size = 80)
        {
            if (definition == null) return GdiRenderHelper.CreatePlaceholder(size);

            string key = $"{definition.Name}_{size}";

            // Trả về từ cache nếu đã có
            if (_cache.TryGetValue(key, out var cached))
                return cached;

            Debug.WriteLine($"{LOG_PREFIX} Render thumbnail: '{definition.Name}' {size}×{size}px");

            Bitmap bmp;
            try
            {
                // Lấy DrawingSketch từ definition
                var sketch = definition.Sketch;
                if (sketch == null)
                {
                    Debug.WriteLine($"{LOG_PREFIX} CẢNH BÁO: Sketch null cho '{definition.Name}'.");
                    bmp = GdiRenderHelper.CreatePlaceholder(size);
                }
                else
                {
                    bmp = GdiRenderHelper.RenderSymbol(sketch, size, PADDING);
                }
            }
            catch (Exception ex)
            {
                // Render thất bại — dùng placeholder
                Debug.WriteLine($"{LOG_PREFIX} LỖI render '{definition.Name}': {ex.Message}");
                bmp = GdiRenderHelper.CreatePlaceholder(size);
            }

            // Lưu cache
            _cache[key] = bmp;
            Debug.WriteLine($"{LOG_PREFIX} Cache: {_cache.Count} thumbnails.");

            return bmp;
        }

        public void ClearCache()
        {
            Debug.WriteLine($"{LOG_PREFIX} Xóa cache ({_cache.Count} items).");

            // Dispose tất cả bitmap trong cache
            foreach (var bmp in _cache.Values)
                bmp?.Dispose();

            _cache.Clear();
        }
    }
}
