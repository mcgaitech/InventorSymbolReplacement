using SymbolReplacer.Models;

namespace SymbolReplacer.Services
{
    /// <summary>
    /// Interface cho service đọc/ghi config.json.
    /// Config lưu tại: %AppData%\SymbolReplacer\config.json
    /// </summary>
    public interface IConfigService
    {
        LibraryConfigModel Load();
        void Save(LibraryConfigModel config);
    }
}
