namespace MCGInventorPlugin.Models.SymbolHandler
{
    /// <summary>
    /// Model lưu cấu hình library path.
    /// Được serialize/deserialize bởi ConfigService thành config.json.
    /// </summary>
    public class LibraryConfigModel
    {
        /// <summary>Đường dẫn đến file .idw chứa symbol library</summary>
        public string LibraryPath { get; set; } = @"C:\MacGregor_CAS_WF\System\2023\Inventor\Library";
    }
}
