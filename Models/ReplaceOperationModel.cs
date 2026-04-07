using System.Collections.Generic;
using Inventor;

namespace SymbolReplacer.Models
{
    /// <summary>
    /// Snapshot các thuộc tính của một SketchedSymbol trước khi xóa.
    /// Dùng để restore position, rotation, scale, layer và prompt text sau khi insert symbol mới.
    /// </summary>
    public class ReplaceOperationModel
    {
        /// <summary>Tên definition của symbol cũ.</summary>
        public string OldDefinitionName { get; set; }

        /// <summary>Tên definition của symbol mới sẽ thay thế.</summary>
        public string NewDefinitionName { get; set; }

        /// <summary>Vị trí insert point (2D drawing coordinates).</summary>
        public Point2d Position { get; set; }

        /// <summary>Góc xoay (radians).</summary>
        public double Rotation { get; set; }

        /// <summary>Hệ số tỷ lệ.</summary>
        public double Scale { get; set; }

        /// <summary>Layer của symbol.</summary>
        public Layer Layer { get; set; }

        /// <summary>Trạng thái static của symbol.</summary>
        public bool IsStatic { get; set; }

        /// <summary>
        /// Prompt text snapshot.
        /// Key = "tb_{index}_{textBoxContent}" hoặc "tb_{index}" nếu không có text.
        /// Value = text đã nhập vào symbol instance.
        /// </summary>
        public Dictionary<string, string> PromptTextSnapshot { get; set; }
            = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
    }
}
