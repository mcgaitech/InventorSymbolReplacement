# CONTEXT.md — SymbolReplacer

# Inventor 2023 AddIn | C# | WPF | VS Code

---

## Thông tin project

- **Tên project**: SymbolReplacer
- **Phần mềm đích**: Inventor 2023
- **Loại output**: Inventor AddIn (COM .dll + .addin)
- **GUID**: `{7C3D8E4F-2A1B-4C5D-9E8F-1A2B3C4D5E6F}`

---

## Bài toán cần giải quyết

Trong bản vẽ IDW của Inventor, người dùng cần thay thế các Sketched Symbol cũ bằng symbol mới nhưng **giữ nguyên toàn bộ thuộc tính** của symbol cũ.

**Vấn đề hiện tại**: Inventor không có công cụ replace symbol built-in. Người dùng phải xóa thủ công từng symbol cũ và chèn lại symbol mới, dẫn đến mất insert point, rotation, scale và attribute values.

**Đặc điểm symbol**: Symbol mới và cũ có **cùng attribute names** (chỉ khác hình dạng) → copy attribute 1-1, không cần mapping dialog.

**Workflow hiện tại (thủ công)**:

1. Ghi nhớ vị trí, rotation, scale, attribute values của symbol cũ
2. Xóa symbol cũ
3. Insert symbol mới tại vị trí cũ
4. Điền lại toàn bộ attribute values
5. Lặp lại cho từng symbol → tốn thời gian, dễ nhầm

**Workflow sau khi có tool**:

1. Mở palette Symbol Replacer từ ribbon Custom Tools
2. Chọn symbol mới từ danh sách thumbnail trong palette
3. Click "Replace" → click vào symbol cũ trên bản vẽ → replace ngay
4. Hoặc "Replace All" → chọn phạm vi (Current Sheet / All Sheets) → confirm → done

---

## Kiến trúc thư mục

```
SymbolReplacer/
├── CLAUDE.md
├── CONTEXT.md
├── SESSION_LOG.md
├── SymbolReplacer.csproj
├── SymbolReplacer.addin
├── SymbolReplacerAddin.cs          ← Entry point, COM [Guid], ApplicationAddInServer
│
├── Resources/
│   ├── ReplaceSymbol_32.png        ← Icon ribbon 32x32
│   └── ReplaceSymbol_16.png        ← Icon ribbon 16x16
│
├── Models/
│   ├── SymbolDefinitionModel.cs    ← Tên symbol, thumbnail bitmap, definition ref
│   ├── ReplaceOperationModel.cs    ← Snapshot properties của symbol cũ
│   └── LibraryConfigModel.cs      ← Library path, last used path
│
├── Views/
│   ├── SymbolReplacerPanel.xaml    ← WPF UserControl nhúng vào DockableWindow
│   ├── SymbolReplacerPanel.xaml.cs
│   ├── ConfirmReplaceAllDialog.xaml
│   ├── ConfirmReplaceAllDialog.xaml.cs
│   └── Styles/
│       └── SharedStyles.xaml       ← ResourceDictionary màu sắc, font chuẩn
│
├── ViewModels/
│   ├── SymbolReplacerViewModel.cs
│   └── ConfirmReplaceAllViewModel.cs
│
├── Controllers/
│   ├── RibbonController.cs         ← Tạo tab Custom Tools, button, DockableWindow
│   ├── PaletteController.cs        ← Load symbols, search, selection
│   ├── ReplaceController.cs        ← Logic replace single + all
│   └── InteractionController.cs    ← InteractionEvents pick mode
│
├── Services/
│   ├── ILibraryService.cs
│   ├── LibraryService.cs           ← Mở .idw library ẩn, đọc definitions
│   ├── IThumbnailService.cs
│   ├── ThumbnailService.cs         ← GDI+ render thumbnail + Dictionary cache
│   ├── ISymbolReplaceService.cs
│   ├── SymbolReplaceService.cs     ← Core replace logic + TransactionManager
│   ├── IConfigService.cs
│   └── ConfigService.cs            ← Đọc/ghi config.json
│
└── Helpers/
    ├── PictureDispConverter.cs     ← Bitmap → stdole.IPictureDisp cho ribbon icon
    ├── CoordinateHelper.cs         ← 2D coordinate transform cho thumbnail
    └── GdiRenderHelper.cs          ← GDI+ drawing utilities
```

---

## Cấu hình kỹ thuật

### .csproj

```xml
<PropertyGroup>
  <OutputType>Library</OutputType>
  <TargetFramework>net48</TargetFramework>
  <UseWPF>true</UseWPF>
  <PlatformTarget>x64</PlatformTarget>
  <RegisterForComInterop>true</RegisterForComInterop>

  <!--
    ĐỔI ĐƯỜNG DẪN NÀY nếu Inventor không cài ở ổ C.
    Tìm đường dẫn thực tế bằng PowerShell:
    Get-ChildItem "C:\Program Files\Autodesk" -Recurse
      -Filter "Autodesk.Inventor.Interop.dll" | Select FullName
  -->
  <InventorBinPath>C:\Program Files\Autodesk\Inventor 2023\Bin</InventorBinPath>
</PropertyGroup>

<ItemGroup>
  <Reference Include="Autodesk.Inventor.Interop">
    <HintPath>$(InventorBinPath)\Autodesk.Inventor.Interop.dll</HintPath>
    <EmbedInteropTypes>False</EmbedInteropTypes>
    <Private>False</Private>
  </Reference>
  <Reference Include="stdole">
    <HintPath>$(InventorBinPath)\stdole.dll</HintPath>
    <EmbedInteropTypes>False</EmbedInteropTypes>
    <Private>False</Private>
  </Reference>
</ItemGroup>
```

---

## Các quyết định kỹ thuật đã chốt

| #   | Quyết định                                                     | Lý do                                             |
| --- | -------------------------------------------------------------- | ------------------------------------------------- |
| 1   | UI dùng WPF thay WinForms                                      | MVVM pattern, binding, styling dễ hơn             |
| 2   | Embed WPF vào DockableWindow qua HwndSource                    | Inventor DockableWindow chỉ nhận Win32 HWND       |
| 3   | DI thủ công trong `SymbolReplacerAddin.Activate()`             | Tránh thêm dependency framework phức tạp          |
| 4   | Symbol source: Library file + Working file kết hợp             | User không cần copy/paste thủ công                |
| 5   | Library default path: `C:\server\System\2023\Inventor\Library` | Đường dẫn server chuẩn của công ty                |
| 6   | Mở library file ở chế độ ẩn (`openVisible: false`)             | Không làm phiền workflow user                     |
| 7   | Thumbnail render bằng GDI+ từ SketchedSymbolDefinition.Sketch  | Luôn sync với definition, không cần file ảnh tĩnh |
| 8   | Thumbnail size: 80×80px, cache vào Dictionary<string, Bitmap>  | Tốc độ load nhanh từ lần 2 trở đi                 |
| 9   | Single replace: 1 Transaction per instance                     | Granular undo cho từng thao tác                   |
| 10  | Replace All: 1 Transaction bao toàn bộ                         | Undo 1 lần duy nhất, sạch sẽ                      |
| 11  | Replace All có 2 scope: Current Sheet / All Sheets             | Tránh replace nhầm sang sheet khác                |
| 12  | Ribbon entry: tab "Custom Tools"                               | Nhất quán với các tool nội bộ khác của team       |

---

## UI — Layout Palette

```
┌─────────────────────────────────┐
│ LIBRARY SOURCE            [⚙]  │ ← Tên file library + nút đổi path
│ ShipSymbols.idw                 │
├─────────────────────────────────┤
│ 🔍 [Search...              ]   │ ← Filter thumbnail theo tên
├─────────────────────────────────┤
│                                 │
│  ┌────┐ ┌────┐ ┌────┐ ┌────┐   │
│  │    │ │ ✓  │ │    │ │    │   │ ← ThumbnailGrid (WPF ItemsControl)
│  │sym1│ │sym2│ │sym3│ │sym4│   │   Wrap tự động khi resize palette
│  └────┘ └────┘ └────┘ └────┘   │
│  Door_A  Door_B  Door_C  ...   │
│                                 │
├─────────────────────────────────┤
│ REPLACE                         │
│ [🔄  Replace  (Pick mode)    ]  │ ← Primary button màu accent
│ [🔄  Replace All  ▼          ]  │ ← Dropdown: Current Sheet / All Sheets
├─────────────────────────────────┤
│ 🟡 PICK MODE: Click symbol...  │ ← Status bar
└─────────────────────────────────┘
```

**Resize behavior**: ThumbnailGrid chiếm toàn bộ không gian còn lại (Fill), các section khác fixed height.

---

## UI — WPF Style Constants (SharedStyles.xaml)

```xml
<Color x:Key="BackgroundColor">#FFF0F0F0</Color>
<Color x:Key="SurfaceColor">#FFD6D6D6</Color>
<Color x:Key="AccentColor">#FF0078D4</Color>
<Color x:Key="AccentHoverColor">#FF006BBD</Color>
<Color x:Key="BorderColor">#FFB4B4B4</Color>
<Color x:Key="TextPrimaryColor">#FF3C3C3C</Color>
<Color x:Key="TextSecondaryColor">#FF646464</Color>
<Color x:Key="StatusOkColor">#FF008000</Color>
<Color x:Key="StatusPickColor">#FFC86400</Color>
<FontFamily x:Key="UIFont">Segoe UI</FontFamily>
<sys:Double x:Key="FontSizeNormal">8.5</sys:Double>
<sys:Double x:Key="FontSizeSmall">7.5</sys:Double>
```

---

## Inventor API — Objects quan trọng

```csharp
// ── Drawing document ──────────────────────────────────────────────
DrawingDocument doc = (DrawingDocument)_app.ActiveDocument;

// ── Symbol definitions trong file ────────────────────────────────
SketchedSymbolDefinitions defs = doc.SketchedSymbolDefinitions;
SketchedSymbolDefinition  def  = defs["DoorSymbol_A"];

// ── Instance của symbol trên sheet ───────────────────────────────
DrawingSketchedSymbol instance = ...; // từ selection hoặc iterate
Point2d pos      = instance.Position;   // insert point 2D
double  rotation = instance.Rotation;   // radians
double  scale    = instance.Scale;
Layer   layer    = instance.Layer;

// ── Capture attributes trước khi xóa symbol cũ ───────────────────
var attrValues = new Dictionary<string, object>();
foreach (AttributeSet attrSet in instance.AttributeSets)
    foreach (Inventor.Attribute attr in attrSet)
        attrValues[attr.Name] = attr.Value;

// ── Transaction cho Undo ──────────────────────────────────────────
Transaction t = _app.TransactionManager
    .StartTransaction((Document)doc, "Replace Symbol");
try   { /* thay đổi */ t.End(); }
catch { t.Abort(); throw; }

// ── InteractionEvents pick mode ───────────────────────────────────
InteractionEvents ie = _app.CommandManager.CreateInteractionEvents();
ie.SelectEvents.AddSelectionFilter(
    SelectionFilterEnum.kDrawingSketchedSymbolFilter);
ie.SelectEvents.OnSelect += OnSymbolSelected;
ie.OnTerminate           += OnPickModeEnded;
ie.Start();

// ── Embed WPF vào DockableWindow qua HwndSource ───────────────────
// Xem CLAUDE.md mục "Embed WPF vào DockableWindow"
```

---

## Config / Settings

**Config file**: `%AppData%\YourCompany\SymbolReplacer\config.json`

```json
{
  "libraryPath": "C:\\server\\System\\2023\\Inventor\\Library",
  "lastUsedSymbol": "",
  "windowWidth": 280,
  "windowHeight": 600
}
```

---

## Phases triển khai

### 🔧 Phase 1 — Addin Skeleton + Ribbon + DockableWindow

**Mục tiêu**: Addin load được, button hiện trên ribbon, palette mở được.
**Files đã tạo**:

- `SymbolReplacer.csproj` ← cần fix `InventorBinPath` theo máy thực tế
- `SymbolReplacer.addin`
- `SymbolReplacerAddin.cs`
- `Controllers/RibbonController.cs`
- `Views/SymbolReplacerPanel.cs` ← WinForms tạm, Phase 2 chuyển sang WPF
- `Helpers/PictureDispConverter.cs`

**Vấn đề tồn đọng**:

- Build lỗi CS0246 vì `Autodesk.Inventor.Interop.dll` không tìm thấy
- Fix: cập nhật `<InventorBinPath>` trong `.csproj` đúng với máy thực tế
- Lệnh tìm đường dẫn:
  ```powershell
  Get-ChildItem "C:\Program Files\Autodesk" -Recurse `
    -Filter "Autodesk.Inventor.Interop.dll" | Select FullName
  ```

**Test khi Phase 1 xong**:

- [ ] Build không lỗi
- [ ] Addin xuất hiện trong Inventor Add-In Manager
- [ ] Tab "Custom Tools" hiện trên Drawing ribbon
- [ ] Click button → DockableWindow mở bên phải
- [ ] Log khởi động hiện trong DebugView / VS Output

### 🔲 Phase 2 — LibraryService + ThumbnailService + WPF Palette

**Mục tiêu**: Palette hiển thị danh sách symbol với thumbnail từ library file.
**Files cần tạo**:

- `Models/SymbolDefinitionModel.cs`
- `Models/LibraryConfigModel.cs`
- `Services/ILibraryService.cs` + `LibraryService.cs`
- `Services/IThumbnailService.cs` + `ThumbnailService.cs`
- `Services/IConfigService.cs` + `ConfigService.cs`
- `Views/SymbolReplacerPanel.xaml` + `.xaml.cs` ← thay thế WinForms
- `Views/Styles/SharedStyles.xaml`
- `ViewModels/SymbolReplacerViewModel.cs`
- `Helpers/CoordinateHelper.cs`
- `Helpers/GdiRenderHelper.cs`

**Test**: Mở palette → thumbnail symbol từ library hiện đúng, search lọc được.

### 🔲 Phase 3 — InteractionController + Pick Mode

**Mục tiêu**: Click symbol cũ trên bản vẽ → log đúng thông tin.
**Files cần tạo**:

- `Controllers/InteractionController.cs`
- `Models/ReplaceOperationModel.cs`

**Test**: Click "Replace" → cursor thay đổi → click symbol cũ → log hiện Position, Rotation, Scale, Attributes đúng.

### 🔲 Phase 4 — SymbolReplaceService + ReplaceController + ConfirmDialog

**Mục tiêu**: Replace hoàn chỉnh, Undo hoạt động, Replace All với confirm.
**Files cần tạo**:

- `Services/ISymbolReplaceService.cs` + `SymbolReplaceService.cs`
- `Controllers/ReplaceController.cs`
- `Controllers/PaletteController.cs`
- `Views/ConfirmReplaceAllDialog.xaml` + `.xaml.cs`
- `ViewModels/ConfirmReplaceAllViewModel.cs`

**Test**:

- [ ] Single replace: giữ nguyên position/rotation/scale/attributes
- [ ] Ctrl+Z undo từng instance
- [ ] Replace All Current Sheet: confirm dialog đúng số lượng
- [ ] Replace All All Sheets: hiện số lượng theo từng sheet, cảnh báo rõ

---

## Vấn đề đã biết / Quirks của API

| #   | Vấn đề                                       | Nguyên nhân                                      | Giải pháp                                     |
| --- | -------------------------------------------- | ------------------------------------------------ | --------------------------------------------- |
| 1   | Build lỗi CS0246                             | HintPath sai đường dẫn DLL                       | Cập nhật `<InventorBinPath>` trong `.csproj`  |
| 2   | `$(AppData)` không hợp lệ trong `.addin` XML | `.addin` là XML thuần, không expand MSBuild vars | Dùng đường dẫn tương đối `SymbolReplacer.dll` |
| 3   | `EmbedInteropTypes` phải là `False`          | Inventor API resolve interfaces lúc runtime      | Đã fix trong `.csproj`                        |
