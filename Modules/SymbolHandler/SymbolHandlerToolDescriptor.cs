using System.Drawing;
using System.Reflection;
using UserControl = System.Windows.Controls.UserControl;
using Inventor;
using MCG.Inventor.Ribbon;
using MCGInventorPlugin.Views.SymbolHandler;

namespace MCGInventorPlugin.Modules.SymbolHandler
{
    /// <summary>
    /// Tool descriptor cho Symbol Handler — hiện trên panel Drawing của tab "MCG TOOLS".
    /// Có palette (SymbolHandlerPanel) → DockablePanel descriptor gắn liền với class này.
    /// </summary>
    internal class SymbolHandlerToolDescriptor : IToolDescriptor
    {
        internal const string BUTTON_ID  = "id.Button.SymbolHandler";
        internal const string DOCKWIN_ID = "SymbolHandler.DockableWindow";

        private readonly SymbolHandlerPanelDescriptor _panelDescriptor = new SymbolHandlerPanelDescriptor();

        public SymbolHandlerPanel CreatedPanel => _panelDescriptor.CreatedPanel;

        // ─── IToolDescriptor ──────────────────────────────────────────────────
        public string Id          => BUTTON_ID;
        public string DisplayName => "Symbol\nHandler";
        public string Tooltip     => "Symbol Handler";
        public string Description => "Insert, replace and manage drawing symbols while preserving position and attributes";

        public Bitmap Icon16 => LoadIcon("ReplaceSymbol_16.png");
        public Bitmap Icon32 => LoadIcon("ReplaceSymbol_32.png");

        public PanelLocation Panel    => PanelLocation.Drawing;
        public RibbonContext Contexts => RibbonContext.Drawing;

        public ButtonDisplayEnum ButtonDisplay => ButtonDisplayEnum.kAlwaysDisplayText;
        public CommandTypesEnum  CommandType   => CommandTypesEnum.kNonShapeEditCmdType;
        public bool              UseLargeIcon  => true;

        public void OnExecute(NameValueMap context) { /* MCGRibbonManager auto-toggles palette */ }

        public IDockablePanelDescriptor DockablePanel => _panelDescriptor;

        // ─── Private: load icon từ assembly hiện tại ──────────────────────────

        private static Bitmap LoadIcon(string fileName)
        {
            // Resource naming dùng RootNamespace (MCGInventorPlugin) — KHÔNG dùng AssemblyName.
            // Sau khi rename assembly thành MCG_InventorSymbolHandler, asm.GetName().Name không còn
            // khớp với RootNamespace trong .csproj nữa, nên phải lấy namespace từ type chính xác.
            // Resource ID thực tế: "MCGInventorPlugin.Resources.SymbolHandler.ReplaceSymbol_16.png"
            var asm = Assembly.GetExecutingAssembly();
            string rootNs = typeof(SymbolHandlerToolDescriptor).Namespace.Split('.')[0];
            string resourceName = $"{rootNs}.Resources.SymbolHandler.{fileName}";
            return PictureDispConverter.LoadBitmapFromResource(asm, resourceName);
        }
    }

    /// <summary>
    /// Descriptor cho DockableWindow của Symbol Handler.
    /// </summary>
    internal class SymbolHandlerPanelDescriptor : IDockablePanelDescriptor
    {
        private SymbolHandlerPanel _panel;

        public SymbolHandlerPanel CreatedPanel => _panel;

        public string Id                            => SymbolHandlerToolDescriptor.DOCKWIN_ID;
        public string Title                         => "Symbol Handler";
        public DockingStateEnum DefaultDockingState => DockingStateEnum.kDockRight;
        public int MinWidth                         => 220;
        public int MinHeight                        => 400;

        public UserControl CreateContent()
        {
            _panel = new SymbolHandlerPanel();
            return _panel;
        }

        public void OnContentEmbedded(UserControl content)
        {
            // Module wire controllers trong OnUIReady() — không cần action ở đây.
        }
    }
}
