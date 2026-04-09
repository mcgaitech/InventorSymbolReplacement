using System;
using System.Drawing;
using System.Windows.Forms;

namespace MCGInventorPlugin.Utilities
{
    /// <summary>
    /// Helper chuyển đổi System.Drawing.Image sang stdole.IPictureDisp
    /// để truyền vào Inventor API khi tạo ButtonDefinition icon.
    ///
    /// Kỹ thuật: kế thừa AxHost để truy cập protected static method
    /// GetIPictureDispFromPicture() của Windows Forms.
    /// </summary>
    internal class PictureDispConverter : AxHost
    {
        // ─── Constructor private — class chỉ dùng static method ─────────────
        private PictureDispConverter() : base(string.Empty) { }

        /// <summary>
        /// Chuyển đổi System.Drawing.Image sang stdole.IPictureDisp
        /// </summary>
        /// <param name="image">Ảnh cần chuyển đổi (thường là Bitmap 16x16 hoặc 32x32)</param>
        /// <returns>IPictureDisp dùng được với Inventor API, null nếu image null</returns>
        public static stdole.IPictureDisp ToIPictureDisp(Image image)
        {
            if (image == null)
            {
                System.Diagnostics.Debug.WriteLine("[PictureDispConverter] CẢNH BÁO: image là null, trả về null.");
                return null;
            }

            try
            {
                // Dùng protected method của AxHost để convert
                var result = (stdole.IPictureDisp)GetIPictureDispFromPicture(image);
                System.Diagnostics.Debug.WriteLine("[PictureDispConverter] Chuyển đổi IPictureDisp thành công.");
                return result;
            }
            catch (Exception ex)
            {
                // Lỗi khi chuyển đổi IPictureDisp
                System.Diagnostics.Debug.WriteLine($"[PictureDispConverter] LỖI chuyển đổi: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Load Bitmap từ embedded resource trong assembly hiện tại.
        /// Resource name format: "SymbolHandler.Resources.{fileName}"
        /// </summary>
        /// <param name="resourceFileName">Tên file, ví dụ: "ReplaceSymbol_32.png"</param>
        /// <returns>Bitmap đã load, null nếu không tìm thấy</returns>
        public static Bitmap LoadFromResource(string resourceFileName)
        {
            // Thử load từ embedded resource
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            string resourceName = $"MCGInventorPlugin.Resources.SymbolHandler.{resourceFileName}";

            System.Diagnostics.Debug.WriteLine($"[PictureDispConverter] Đang load resource: {resourceName}");

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    // Không tìm thấy resource — liệt kê các resource có sẵn để debug
                    var available = assembly.GetManifestResourceNames();
                    System.Diagnostics.Debug.WriteLine($"[PictureDispConverter] CẢNH BÁO: Không tìm thấy '{resourceName}'");
                    System.Diagnostics.Debug.WriteLine($"[PictureDispConverter] Resources có sẵn: {string.Join(", ", available)}");
                    return null;
                }

                // Load thành công từ stream
                var bmp = new Bitmap(stream);
                System.Diagnostics.Debug.WriteLine($"[PictureDispConverter] Load resource thành công: {resourceFileName} ({bmp.Width}x{bmp.Height})");
                return bmp;
            }
        }
    }
}
