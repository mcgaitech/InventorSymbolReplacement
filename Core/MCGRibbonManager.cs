using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using Inventor;

namespace MCG.Inventor.Ribbon
{
    /// <summary>
    /// Quản lý ribbon "MCG TOOLS" — dùng chung giữa các addin của MCG team.
    /// Mỗi addin tạo instance riêng, đăng ký tool của mình, rồi gọi Build().
    ///
    /// Tab "MCG TOOLS" và panels (Model/Drawing/Utility) có cùng InternalName
    /// trên mọi addin → addin load sau tái sử dụng tab/panel đã có, chỉ add
    /// button của mình vào. Inventor hỗ trợ multi-addin share tab qua
    /// InternalName matching.
    ///
    /// Mapping panel ↔ ribbon:
    ///   Model   → Part + Assembly
    ///   Drawing → Drawing
    ///   Utility → Part + Assembly + Drawing
    /// Panel trống (không tool nào) sẽ không tạo.
    /// </summary>
    public class MCGRibbonManager
    {
        private const string LOG_PREFIX  = "[MCGRibbonManager]";
        private const string TAB_ID      = "id.Tab.MCGTools";
        private const string TAB_DISPLAY = "MCG TOOLS";

        private readonly global::Inventor.Application _app;
        private readonly string _addinGuid;
        private readonly List<IToolDescriptor> _tools = new List<IToolDescriptor>();
        private readonly Dictionary<string, ButtonDefinition> _buttonDefs = new Dictionary<string, ButtonDefinition>();
        private readonly Dictionary<string, DockableWindowHost> _dockHosts = new Dictionary<string, DockableWindowHost>();
        private readonly Dictionary<string, ButtonDefinitionSink_OnExecuteEventHandler> _executeHandlers =
            new Dictionary<string, ButtonDefinitionSink_OnExecuteEventHandler>();

        /// <summary>
        /// Tạo manager với addin GUID của project gọi. Mỗi addin dùng GUID riêng.
        /// </summary>
        public MCGRibbonManager(global::Inventor.Application app, string addinGuid)
        {
            _app       = app ?? throw new ArgumentNullException(nameof(app));
            _addinGuid = addinGuid;
        }

        /// <summary>Đăng ký 1 tool. Gọi trước Build().</summary>
        public void RegisterTool(IToolDescriptor tool)
        {
            if (tool == null) return;
            _tools.Add(tool);
            Debug.WriteLine($"{LOG_PREFIX} Đăng ký tool: {tool.Id} (Panel={tool.Panel}, Contexts={tool.Contexts})");
        }

        /// <summary>
        /// Build toàn bộ ribbon UI cho tất cả tool đã đăng ký.
        /// Gọi 1 lần sau khi tất cả RegisterTool() đã xong.
        /// </summary>
        public void Build()
        {
            Debug.WriteLine($"{LOG_PREFIX} ===== Build ribbon cho {_tools.Count} tools =====");

            // Bước 1: ButtonDefinitions (shared across ribbons/panels)
            foreach (var tool in _tools)
            {
                try { CreateButtonDefinition(tool); }
                catch (Exception ex) { Debug.WriteLine($"{LOG_PREFIX} LỖI CreateButtonDefinition {tool.Id}: {ex.Message}"); }
            }

            // Bước 2: build tabs/panels/buttons trước (nếu palette lỗi vẫn thấy button)
            var ribbons = new[] { RibbonContext.Part, RibbonContext.Assembly, RibbonContext.Drawing };
            var panels  = new[] { PanelLocation.Model, PanelLocation.Drawing, PanelLocation.Utility };

            foreach (var ribbonCtx in ribbons)
            {
                foreach (var panelLoc in panels)
                {
                    if (!IsPanelValidForRibbon(panelLoc, ribbonCtx)) continue;

                    var matching = _tools
                        .Where(t => t.Panel == panelLoc && t.Contexts.HasFlag(ribbonCtx))
                        .ToList();
                    if (matching.Count == 0) continue;

                    try { AddToolsToRibbon(ribbonCtx, panelLoc, matching); }
                    catch (Exception ex) { Debug.WriteLine($"{LOG_PREFIX} LỖI AddToolsToRibbon ribbon={ribbonCtx} panel={panelLoc}: {ex.Message}"); }
                }
            }

            // Bước 3: DockableWindowHosts (sau ribbon, tránh cascade failure)
            foreach (var tool in _tools)
            {
                if (tool.DockablePanel == null) continue;
                try
                {
                    var host = new DockableWindowHost(_app, tool.DockablePanel, _addinGuid, tool.Contexts);
                    if (_buttonDefs.TryGetValue(tool.Id, out var btn))
                        host.LinkButton(btn);
                    host.Create();
                    _dockHosts[tool.Id] = host;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"{LOG_PREFIX} LỖI DockableWindowHost {tool.Id}: {ex.Message}");
                    Debug.WriteLine($"{LOG_PREFIX} Stack:\n{ex.StackTrace}");
                }
            }

            Debug.WriteLine($"{LOG_PREFIX} ===== Build ribbon HOÀN TẤT =====");
        }

        /// <summary>Cleanup — gọi từ addin Deactivate().</summary>
        public void Cleanup()
        {
            Debug.WriteLine($"{LOG_PREFIX} Cleanup {_dockHosts.Count} dockable hosts + {_buttonDefs.Count} buttons.");

            foreach (var host in _dockHosts.Values)
            {
                try { host.Cleanup(); }
                catch (Exception ex) { Debug.WriteLine($"{LOG_PREFIX} LỖI host cleanup: {ex.Message}"); }
            }
            _dockHosts.Clear();

            foreach (var kvp in _buttonDefs)
            {
                try
                {
                    if (_executeHandlers.TryGetValue(kvp.Key, out var handler))
                        kvp.Value.OnExecute -= handler;
                }
                catch (Exception ex) { Debug.WriteLine($"{LOG_PREFIX} LỖI detach button {kvp.Key}: {ex.Message}"); }
            }
            _buttonDefs.Clear();
            _executeHandlers.Clear();

            Debug.WriteLine($"{LOG_PREFIX} Cleanup THÀNH CÔNG.");
        }

        // ─── Private: ribbon/panel setup ──────────────────────────────────────

        private static bool IsPanelValidForRibbon(PanelLocation panel, RibbonContext ribbon)
        {
            switch (panel)
            {
                case PanelLocation.Model:
                    return ribbon == RibbonContext.Part || ribbon == RibbonContext.Assembly;
                case PanelLocation.Drawing:
                    return ribbon == RibbonContext.Drawing;
                case PanelLocation.Utility:
                    return ribbon == RibbonContext.Part
                        || ribbon == RibbonContext.Assembly
                        || ribbon == RibbonContext.Drawing;
                default:
                    return false;
            }
        }

        private static string RibbonNameFromContext(RibbonContext ctx)
        {
            switch (ctx)
            {
                case RibbonContext.Part:     return "Part";
                case RibbonContext.Assembly: return "Assembly";
                case RibbonContext.Drawing:  return "Drawing";
                default: throw new ArgumentException($"Unsupported ribbon context: {ctx}");
            }
        }

        private static string PanelDisplayName(PanelLocation loc)
        {
            switch (loc)
            {
                case PanelLocation.Model:   return "Model";
                case PanelLocation.Drawing: return "Drawing";
                case PanelLocation.Utility: return "Utility";
                default: return loc.ToString();
            }
        }

        private static string PanelId(PanelLocation loc) => $"id.Panel.MCGTools.{loc}";

        private void AddToolsToRibbon(RibbonContext ribbonCtx, PanelLocation panelLoc, List<IToolDescriptor> tools)
        {
            string ribbonName = RibbonNameFromContext(ribbonCtx);

            global::Inventor.Ribbon ribbon;
            try { ribbon = _app.UserInterfaceManager.Ribbons[ribbonName]; }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI lấy ribbon '{ribbonName}': {ex.Message}");
                return;
            }

            var tab = GetOrCreateTab(ribbon);
            if (tab == null) return;

            var panel = GetOrCreatePanel(tab, panelLoc);
            if (panel == null) return;

            foreach (var tool in tools)
                AddButtonToPanel(panel, tool);

            Debug.WriteLine($"{LOG_PREFIX} Ribbon '{ribbonName}' / Panel '{PanelDisplayName(panelLoc)}': {tools.Count} button(s).");
        }

        private RibbonTab GetOrCreateTab(global::Inventor.Ribbon ribbon)
        {
            try { return ribbon.RibbonTabs[TAB_ID]; }
            catch { }

            try { return ribbon.RibbonTabs.Add(TAB_DISPLAY, TAB_ID, _addinGuid); }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI tạo tab: {ex.Message}");
                return null;
            }
        }

        private RibbonPanel GetOrCreatePanel(RibbonTab tab, PanelLocation loc)
        {
            string panelId = PanelId(loc);
            string display = PanelDisplayName(loc);

            try { return tab.RibbonPanels[panelId]; }
            catch { }

            try { return tab.RibbonPanels.Add(display, panelId, _addinGuid); }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI tạo panel '{display}': {ex.Message}");
                return null;
            }
        }

        private void AddButtonToPanel(RibbonPanel panel, IToolDescriptor tool)
        {
            if (!_buttonDefs.TryGetValue(tool.Id, out var buttonDef)) return;

            try
            {
                var _ = panel.CommandControls[tool.Id];
                // Đã có rồi → skip
            }
            catch
            {
                try { panel.CommandControls.AddButton(buttonDef, UseLargeIcon: tool.UseLargeIcon); }
                catch (Exception ex) { Debug.WriteLine($"{LOG_PREFIX} LỖI add button {tool.Id}: {ex.Message}"); }
            }
        }

        // ─── Private: ButtonDefinition ────────────────────────────────────────

        private void CreateButtonDefinition(IToolDescriptor tool)
        {
            if (_buttonDefs.ContainsKey(tool.Id)) return;

            ButtonDefinition buttonDef = null;

            try
            {
                buttonDef = (ButtonDefinition)_app.CommandManager.ControlDefinitions[tool.Id];
                Debug.WriteLine($"{LOG_PREFIX} ButtonDefinition đã tồn tại, tái sử dụng: {tool.Id}");
            }
            catch { }

            if (buttonDef == null)
            {
                Bitmap icon32Bmp = tool.Icon32;
                Bitmap icon16Bmp = tool.Icon16;

                bool ownsBitmaps = false;
                if (icon32Bmp == null)
                {
                    icon32Bmp = CreatePlaceholderIcon(32);
                    icon16Bmp = CreatePlaceholderIcon(16);
                    ownsBitmaps = true;
                    Debug.WriteLine($"{LOG_PREFIX} Dùng placeholder icon cho {tool.Id}.");
                }

                var icon32 = PictureDispConverter.ToIPictureDisp(icon32Bmp);
                var icon16 = PictureDispConverter.ToIPictureDisp(icon16Bmp);

                try
                {
                    buttonDef = _app.CommandManager.ControlDefinitions.AddButtonDefinition(
                        DisplayName:     tool.DisplayName,
                        InternalName:    tool.Id,
                        Classification:  tool.CommandType,
                        ClientId:        _addinGuid,
                        DescriptionText: tool.Description,
                        ToolTipText:     tool.Tooltip,
                        StandardIcon:    icon16,
                        LargeIcon:       icon32,
                        ButtonDisplay:   tool.ButtonDisplay
                    );
                    Debug.WriteLine($"{LOG_PREFIX} Tạo ButtonDefinition: {tool.Id}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"{LOG_PREFIX} LỖI tạo ButtonDefinition {tool.Id}: {ex.Message}");
                    return;
                }
                finally
                {
                    if (ownsBitmaps)
                    {
                        icon32Bmp?.Dispose();
                        icon16Bmp?.Dispose();
                    }
                    // Nếu bitmap từ tool: để tool quản lý (tránh dispose resource của addin)
                }
            }

            // Wire OnExecute — palette thì toggle, không palette thì gọi tool.OnExecute
            ButtonDefinitionSink_OnExecuteEventHandler handler = (NameValueMap ctx) =>
            {
                try
                {
                    if (_dockHosts.TryGetValue(tool.Id, out var host))
                        host.Toggle();
                    else
                        tool.OnExecute(ctx);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"{LOG_PREFIX} LỖI OnExecute {tool.Id}: {ex.Message}");
                }
            };

            buttonDef.OnExecute += handler;
            _buttonDefs[tool.Id]      = buttonDef;
            _executeHandlers[tool.Id] = handler;
        }

        // ─── Private: placeholder icon ────────────────────────────────────────

        private static Bitmap CreatePlaceholderIcon(int size)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.FromArgb(0, 120, 212));
                float fontSize = size * 0.3f;
                using (var font = new System.Drawing.Font("Segoe UI", fontSize, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel))
                using (var brush = new System.Drawing.SolidBrush(System.Drawing.Color.White))
                {
                    var sf = new System.Drawing.StringFormat
                    {
                        Alignment     = System.Drawing.StringAlignment.Center,
                        LineAlignment = System.Drawing.StringAlignment.Center
                    };
                    g.DrawString("?", font, brush, new System.Drawing.RectangleF(0, 0, size, size), sf);
                }
            }
            return bmp;
        }
    }
}
