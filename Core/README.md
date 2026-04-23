# Core/ — MCG Inventor Ribbon SDK (portable)

Folder này là **portable SDK** chứa toàn bộ logic tạo tab "MCG TOOLS" + panels (Model/Drawing/Utility) + dockable window. Copy cả folder này vào project Inventor addin mới để tool của addin đó hiện lên cùng tab "MCG TOOLS" dùng chung với các addin MCG khác.

Namespace: `MCG.Inventor.Ribbon` — không tham chiếu addin cụ thể nào.

## Nguyên tắc share tab giữa nhiều addin

Inventor cho phép nhiều addin cùng add vào 1 ribbon tab khi dùng chung `InternalName`. MCGRibbonManager dùng pattern `try { tabs[id] } catch { tabs.Add(...) }` → addin load trước tạo tab, addin load sau tái sử dụng và chỉ thêm button của mình vào.

Mapping panel ↔ ribbon (cố định, tất cả addin phải tuân theo):

| Panel | Xuất hiện trong ribbon |
|-------|------------------------|
| Model | Part + Assembly |
| Drawing | Drawing |
| Utility | Part + Assembly + Drawing |

## Copy vào project mới — 5 bước

### 1. Copy folder `Core/` nguyên vẹn

Paste vào project mới. Giữ tên folder `Core` và namespace `MCG.Inventor.Ribbon`.

### 2. Project requirements

- TargetFramework: `net48`
- `<UseWindowsForms>true</UseWindowsForms>` (cho Timer, NativeWindow)
- `<UseWPF>true</UseWPF>` (cho HwndSource, UserControl)
- Reference: `Autodesk.Inventor.Interop.dll`, `stdole.dll`

### 3. Implement `IToolDescriptor` cho mỗi tool

```csharp
using System.Drawing;
using System.Reflection;
using Inventor;
using MCG.Inventor.Ribbon;

internal class MyToolDescriptor : IToolDescriptor
{
    public string Id          => "id.Button.MyTool";
    public string DisplayName => "My\nTool";
    public string Tooltip     => "My Tool";
    public string Description => "Làm việc XYZ";

    public Bitmap Icon16 => LoadIcon("MyTool_16.png");
    public Bitmap Icon32 => LoadIcon("MyTool_32.png");

    public PanelLocation Panel    => PanelLocation.Utility;
    public RibbonContext Contexts => RibbonContext.Part | RibbonContext.Assembly;

    public ButtonDisplayEnum ButtonDisplay => ButtonDisplayEnum.kAlwaysDisplayText;
    public CommandTypesEnum  CommandType   => CommandTypesEnum.kNonShapeEditCmdType;
    public bool              UseLargeIcon  => true;

    public void OnExecute(NameValueMap context)
    {
        // Logic khi click button (nếu không dùng palette)
    }

    public IDockablePanelDescriptor DockablePanel => null;  // hoặc provide nếu có palette

    private static Bitmap LoadIcon(string fileName)
    {
        var asm = Assembly.GetExecutingAssembly();
        string res = $"{asm.GetName().Name}.Resources.MyTool.{fileName}";
        return PictureDispConverter.LoadBitmapFromResource(asm, res);
    }
}
```

### 4. Đăng ký tool trong addin entry point

```csharp
[Guid("YOUR-OWN-ADDIN-GUID-HERE")]
[ClassInterface(ClassInterfaceType.None)]
[ComVisible(true)]
public class MyAddin : ApplicationAddInServer
{
    private const string ADDIN_GUID = "{YOUR-OWN-ADDIN-GUID-HERE}";
    private MCGRibbonManager _ribbon;

    public void Activate(ApplicationAddInSite site, bool firstTime)
    {
        _ribbon = new MCGRibbonManager(site.Application, ADDIN_GUID);
        _ribbon.RegisterTool(new MyToolDescriptor());
        _ribbon.RegisterTool(new AnotherToolDescriptor());
        _ribbon.Build();
    }

    public void Deactivate() => _ribbon?.Cleanup();
    public void ExecuteCommand(int id) { }
    public object Automation => null;
}
```

### 5. Nếu tool có palette — implement `IDockablePanelDescriptor`

```csharp
internal class MyPanelDescriptor : IDockablePanelDescriptor
{
    private MyWpfPanel _panel;

    public string Id                            => "MyTool.DockableWindow";
    public string Title                         => "My Tool";
    public DockingStateEnum DefaultDockingState => DockingStateEnum.kDockRight;
    public int MinWidth  => 220;
    public int MinHeight => 400;

    public UserControl CreateContent()
    {
        _panel = new MyWpfPanel();
        return _panel;
    }

    public void OnContentEmbedded(UserControl content)
    {
        // Wire controllers với WPF content tại đây nếu cần
    }
}
```

## API reference

- `MCGRibbonManager(app, addinGuid)` — mỗi addin 1 instance, dùng GUID riêng
- `RegisterTool(IToolDescriptor)` — gọi cho mỗi tool, trước Build()
- `Build()` — gọi 1 lần sau khi đã Register hết tool
- `Cleanup()` — gọi từ addin Deactivate()

- `PictureDispConverter.ToIPictureDisp(Bitmap)` — convert sang stdole.IPictureDisp
- `PictureDispConverter.LoadBitmapFromResource(Assembly, fullResourceName)` — helper load embedded resource

## ID naming convention (phải tuân thủ để share tab đúng)

- Tab: `id.Tab.MCGTools` (hard-coded trong MCGRibbonManager — KHÔNG đổi)
- Panels: `id.Panel.MCGTools.{Model|Drawing|Utility}` (hard-coded)
- Tool button Id: tự chọn, nên prefix bằng addin tên ngắn (ví dụ `id.Button.MySymbolTool`)
- DockableWindow Id: tự chọn, phải unique global (ví dụ `MyAddin.ToolName.DockableWindow`)

## Khi update SDK

SDK update theo nguyên tắc backwards-compatible:
- Chỉ ADD method/property mới vào interface
- KHÔNG remove hoặc thay signature
- Khi cần breaking change: bump namespace (vd `MCG.Inventor.Ribbon.V2`) và giữ cả 2

Khi bạn fix bug trong Core/, hãy sync bản mới sang mọi addin đang dùng để tránh code drift.
