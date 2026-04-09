using System;
using System.Collections.Generic;
using System.Diagnostics;
using Inventor;

namespace MCGInventorPlugin.Core
{
    /// <summary>
    /// Quản lý đăng ký, khởi tạo và cleanup tất cả modules.
    /// Gọi từ MCGInventorPluginAddin.Activate() và Deactivate().
    /// </summary>
    public class ModuleManager
    {
        private const string LOG_PREFIX = "[ModuleManager]";
        private readonly List<IModule> _modules = new List<IModule>();

        /// <summary>Đăng ký module mới. Gọi trước ActivateAll().</summary>
        public void Register(IModule module)
        {
            if (module == null) return;
            _modules.Add(module);
            Debug.WriteLine($"{LOG_PREFIX} Đăng ký module: {module.Name} ({module.Environment})");
        }

        /// <summary>Khởi tạo + tạo UI cho tất cả modules đã đăng ký.</summary>
        public void ActivateAll(Application app, bool firstTime)
        {
            Debug.WriteLine($"{LOG_PREFIX} ActivateAll: {_modules.Count} modules.");
            foreach (var module in _modules)
            {
                try
                {
                    module.Activate(app, firstTime);
                    if (firstTime)
                        module.CreateUI(app);
                    Debug.WriteLine($"{LOG_PREFIX} Module '{module.Name}' activated.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"{LOG_PREFIX} LỖI activate module '{module.Name}': {ex.Message}");
                }
            }
        }

        /// <summary>Cleanup tất cả modules theo thứ tự ngược (LIFO).</summary>
        public void CleanupAll()
        {
            Debug.WriteLine($"{LOG_PREFIX} CleanupAll: {_modules.Count} modules.");
            for (int i = _modules.Count - 1; i >= 0; i--)
            {
                try
                {
                    _modules[i].Cleanup();
                    Debug.WriteLine($"{LOG_PREFIX} Module '{_modules[i].Name}' cleaned up.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"{LOG_PREFIX} LỖI cleanup module '{_modules[i].Name}': {ex.Message}");
                }
            }
            _modules.Clear();
        }
    }
}
