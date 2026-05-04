using System;
using System.Collections.Generic;
using System.Diagnostics;
using Inventor;
using MCG.Inventor.Ribbon;

namespace MCGInventorPlugin.Infrastructure
{
    /// <summary>
    /// Quản lý đăng ký, khởi tạo và cleanup tất cả modules của addin này.
    /// Gọi từ MCGInventorPluginAddin.Activate() và Deactivate().
    ///
    /// Flow:
    ///   1. Register(module) cho từng module
    ///   2. ActivateAll() — gọi module.Activate(), thu thập IToolDescriptor, build ribbon
    ///   3. module.OnUIReady() — wire controllers với palette content
    /// </summary>
    public class ModuleManager
    {
        private const string LOG_PREFIX = "[ModuleManager]";
        private readonly List<IModule> _modules = new List<IModule>();
        private MCGRibbonManager _ribbonManager;

        public void Register(IModule module)
        {
            if (module == null) return;
            _modules.Add(module);
            Debug.WriteLine($"{LOG_PREFIX} Đăng ký module: {module.Name}");
        }

        public void ActivateAll(Application app, string addinGuid, bool firstTime)
        {
            Debug.WriteLine($"{LOG_PREFIX} ActivateAll: {_modules.Count} modules (firstTime={firstTime}).");

            foreach (var module in _modules)
            {
                try
                {
                    module.Activate(app, firstTime);
                    Debug.WriteLine($"{LOG_PREFIX} Module '{module.Name}' activated.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"{LOG_PREFIX} LỖI activate module '{module.Name}': {ex.Message}");
                }
            }

            // MCG_FIX (TASK 2): RibbonManager phải chạy MỌI Activate (kể cả
            // firstTime=false) để re-attach OnExecute event handlers — Inventor
            // không persist handler giữa các session. Bên trong Build, việc
            // tạo Tab/Panel mới đã được gate bởi firstTime.
            _ribbonManager = new MCGRibbonManager(app, addinGuid);
            foreach (var module in _modules)
            {
                try
                {
                    foreach (var tool in module.GetTools())
                        _ribbonManager.RegisterTool(tool);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"{LOG_PREFIX} LỖI GetTools '{module.Name}': {ex.Message}");
                }
            }
            _ribbonManager.Build(firstTime);

            foreach (var module in _modules)
            {
                try { module.OnUIReady(); }
                catch (Exception ex) { Debug.WriteLine($"{LOG_PREFIX} LỖI OnUIReady '{module.Name}': {ex.Message}"); }
            }
        }

        public void CleanupAll()
        {
            Debug.WriteLine($"{LOG_PREFIX} CleanupAll: {_modules.Count} modules.");

            try { _ribbonManager?.Cleanup(); }
            catch (Exception ex) { Debug.WriteLine($"{LOG_PREFIX} LỖI ribbon cleanup: {ex.Message}"); }
            _ribbonManager = null;

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
