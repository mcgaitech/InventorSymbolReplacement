using MCGInventorPlugin.Models.SymbolHandler;

namespace MCGInventorPlugin.Services.SymbolHandler
{
    /// <summary>
    /// Interface cho service đọc/ghi config.json.
    /// Config lưu tại: %AppData%\SymbolHandler\config.json
    /// </summary>
    public interface IConfigService
    {
        LibraryConfigModel Load();
        void Save(LibraryConfigModel config);
    }
}
