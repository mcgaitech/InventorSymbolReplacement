using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Inventor;
using MCGInventorPlugin.Core;
using MCGInventorPlugin.Modules;

namespace MCGInventorPlugin
{
    /// <summary>
    /// Entry point duy nhất của MCGInventorPlugin.
    /// Inventor tìm class này qua COM GUID trong file .addin.
    /// Tất cả modules được đăng ký tại đây — ModuleManager quản lý lifecycle.
    /// </summary>
    [Guid("7C3D8E4F-2A1B-4C5D-9E8F-1A2B3C4D5E6F")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    public class MCGInventorPluginAddin : ApplicationAddInServer
    {
        private const string LOG_PREFIX = "[MCGInventorPlugin]";

        private Inventor.Application _app;
        private ModuleManager _moduleManager;

        public void Activate(ApplicationAddInSite addInSiteObject, bool firstTime)
        {
            Debug.WriteLine($"{LOG_PREFIX} ===== Bắt đầu kích hoạt addin =====");
            Debug.WriteLine($"{LOG_PREFIX} firstTime = {firstTime}");

            try
            {
                _app = addInSiteObject.Application;
                Debug.WriteLine($"{LOG_PREFIX} Inventor version: {_app.SoftwareVersion.DisplayVersion}");

                // Đăng ký tất cả modules
                _moduleManager = new ModuleManager();
                _moduleManager.Register(new SymbolHandlerModule());
                // Tương lai:
                // _moduleManager.Register(new PartToolsModule());
                // _moduleManager.Register(new AssemblyToolsModule());

                // Khởi tạo + tạo UI
                _moduleManager.ActivateAll(_app, firstTime);

                Debug.WriteLine($"{LOG_PREFIX} ===== Addin kích hoạt THÀNH CÔNG =====");
            }
            catch (Exception ex)
            {
                // KHÔNG re-throw — Inventor không gọi Deactivate() nếu Activate() throw
                Debug.WriteLine($"{LOG_PREFIX} LỖI NGHIÊM TRỌNG: {ex.Message}");
                Debug.WriteLine($"{LOG_PREFIX} Stack trace:\n{ex.StackTrace}");
            }
        }

        public void Deactivate()
        {
            Debug.WriteLine($"{LOG_PREFIX} ===== Bắt đầu tắt addin =====");

            try { _moduleManager?.CleanupAll(); }
            catch (Exception ex) { Debug.WriteLine($"{LOG_PREFIX} LỖI CleanupAll: {ex.Message}"); }

            _app = null;

            Debug.WriteLine($"{LOG_PREFIX} ===== Addin tắt THÀNH CÔNG =====");
        }

        public void ExecuteCommand(int commandID)
        {
            Debug.WriteLine($"{LOG_PREFIX} ExecuteCommand commandID={commandID}");
        }

        public object Automation => null;
    }
}
