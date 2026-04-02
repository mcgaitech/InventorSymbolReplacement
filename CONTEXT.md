# Symbol Replacer — Project Context for Claude Code

## Mục tiêu dự án

AddIn cho Autodesk Inventor 2023 (IDW drawing environment).
Thay thế (replace) các Sketched Symbol cũ bằng symbol mới, **giữ nguyên**:

- Insert point (Position 2D)
- Rotation, Scale
- Layer
- Attribute values (tên attribute giống nhau giữa symbol cũ và mới)

---

## Môi trường kỹ thuật

- **Inventor 2023** — API: `Autodesk.Inventor.Interop.dll`
- **C# / .NET Framework 4.8**, WinForms
- **IDE**: Visual Studio Code
- **Build**: `dotnet build -c Debug` (chạy với quyền Administrator)
- **Output**: COM AddIn `.dll` + `.addin` file

---

## Kiến trúc: MVC + SOLID

```
SymbolReplacer/
├── SymbolReplacer.csproj
├── SymbolReplacer.addin
├── SymbolReplacerAddin.cs          ← Entry point, COM [Guid], ApplicationAddInServer
├── Resources/
│   ├── ReplaceSymbol_32.png
│   └── ReplaceSymbol_16.png
├── Models/
│   ├── SymbolDefinitionModel.cs    ← Tên, thumbnail bitmap, definition reference
│   ├── ReplaceOperationModel.cs    ← Snapshot properties của symbol cũ
│   └── LibraryConfigModel.cs      ← Library path config
├── Views/
│   ├── SymbolReplacerPanel.cs      ← DockableWindow UserControl (WinForms)
│   ├── ThumbnailGridControl.cs     ← Custom control: grid ảnh symbol
│   └── ConfirmReplaceAllDialog.cs  ← Dialog xác nhận Replace All
├── Controllers/
│   ├── RibbonController.cs         ← Ribbon button + DockableWindow management
│   ├── PaletteController.cs        ← Load symbols, search, selection
│   ├── ReplaceController.cs        ← Logic replace single + all
│   └── InteractionController.cs    ← InteractionEvents pick mode
├── Services/
│   ├── ILibraryService.cs / LibraryService.cs         ← Mở .idw library, đọc definitions
│   ├── IThumbnailService.cs / ThumbnailService.cs     ← GDI+ render thumbnail + cache
│   ├── ISymbolReplaceService.cs / SymbolReplaceService.cs ← Core replace + Transaction
│   └── IConfigService.cs / ConfigService.cs           ← Đọc/ghi config.json
└── Helpers/
    ├── PictureDispConverter.cs     ← Convert Bitmap → stdole.IPictureDisp cho Inventor API
    ├── CoordinateHelper.cs         ← 2D coordinate transform cho thumbnail render
    └── GdiRenderHelper.cs          ← GDI+ drawing utilities
```

---

## Quy tắc code (bắt buộc tuân theo)

| Rule               | Chi tiết                                                                     |
| ------------------ | ---------------------------------------------------------------------------- |
| **Ngôn ngữ code**  | English (tên class, method, variable, UI text)                               |
| **Comment & Log**  | Tiếng Việt                                                                   |
| **Log prefix**     | Mỗi class có `private const string LOG_PREFIX = "[ClassName]"`               |
| **Log tool**       | `System.Diagnostics.Debug.WriteLine(...)` — xem qua DebugView hoặc VS Output |
| **Log bắt buộc**   | Đầu/cuối mỗi method quan trọng, mỗi catch block, mọi thay đổi state          |
| **SOLID**          | Interface cho mọi Service, DI manual trong `SymbolReplacerAddin.Activate()`  |
| **Error handling** | try/catch với log chi tiết, không swallow exception ở tầng quan trọng        |

---

## Triển khai theo Phase (test từng bước)

### ✅ Phase 1 — HOÀN THÀNH

Addin skeleton + Ribbon + DockableWindow rỗng.
**Files đã có**: `SymbolReplacerAddin.cs`, `RibbonController.cs`, `SymbolReplacerPanel.cs`, `PictureDispConverter.cs`, `.csproj`, `.addin`
**Test**: Build thành công, button hiện trên Drawing ribbon, DockableWindow mở được.

### 🔲 Phase 2 — TIẾP THEO

LibraryService + ThumbnailService + ThumbnailGridControl.
**Mục tiêu**: Panel hiển thị danh sách symbol với thumbnail từ library file.

### 🔲 Phase 3

InteractionController + pick mode.
**Mục tiêu**: Click vào symbol cũ trên bản vẽ → log đúng thông tin.

### 🔲 Phase 4

SymbolReplaceService + ReplaceController + ConfirmReplaceAllDialog.
**Mục tiêu**: Replace hoàn chỉnh, Undo hoạt động.

---

## Các quyết định kỹ thuật quan trọng

### Symbol Library

- **Default path**: `C:\MacGregor_CAS_WF\System\2023\Inventor\Library`
- **Config**: lưu vào `config.json` trong `%AppData%\SymbolReplacer\`
- **Mở library**: `inventorApp.Documents.Open(path, openVisible: false)` — không hiển thị lên UI
- **Check trùng**: nếu file đang mở sẵn → reuse instance, không mở lại
- **Palette có nút ⚙**: user browse đổi library path khác

### Symbol replace logic

- Symbol mới và cũ có **cùng attribute names** → copy 1-1, không cần mapping dialog
- Khi replace: capture properties → `TransactionManager.StartTransaction()` → delete old → insert new → `EndTransaction()`
- **Single replace**: 1 transaction per instance (user chủ động pick từng cái)
- **Replace All**: 1 transaction bao toàn bộ (undo 1 lần)

### Replace All — 2 scope

```
[Replace All ▼]
  ├── Current Sheet...  → Confirm dialog → replace all instances trên sheet hiện tại
  └── All Sheets...     → Confirm dialog → replace all instances trên toàn document
```

Confirm dialog hiện: số lượng instance theo từng sheet, cảnh báo kiểm tra bản vẽ.

### Thumbnail rendering

- **Phương pháp**: GDI+ render từ `SketchedSymbolDefinition.Sketch` geometry
- **Size**: 80×80px với label tên bên dưới
- **Cache**: `Dictionary<string, Bitmap>` — render 1 lần, reuse
- **Fallback**: nếu geometry phức tạp → placeholder icon

### UI — Inventor style

```csharp
ColorBackground  = Color.FromArgb(240, 240, 240)  // #F0F0F0
ColorSectionBg   = Color.FromArgb(214, 214, 214)  // Section headers
ColorAccent      = Color.FromArgb(0, 120, 212)    // #0078D4 — buttons, selection
ColorBorder      = Color.FromArgb(180, 180, 180)
Font             = "Segoe UI" 8.5pt
```

Layout dùng `TableLayoutPanel` — responsive khi resize DockableWindow.

### Addin registration

- **GUID**: `{7C3D8E4F-2A1B-4C5D-9E8F-1A2B3C4D5E6F}` (phải khớp trong cả 3 nơi: `[Guid]` attribute, `.addin` ClassId, `.addin` ClientId)
- **Ribbon**: tab "Custom Tools" → panel "Symbol Tools" → button "Symbol Replacer"
- **DockableWindow**: dock bên phải, minimum size 220×400

---

## Inventor API — Các object quan trọng cần biết

```csharp
// Lấy drawing document
DrawingDocument doc = (DrawingDocument)_app.ActiveDocument;

// Sketched symbol definitions trong file
SketchedSymbolDefinitions defs = doc.SketchedSymbolDefinitions;

// Instance của symbol trên sheet
DrawingSketchedSymbol instance = ...; // lấy qua selection hoặc iterate
Point2d pos = instance.Position;
double rotation = instance.Rotation;
double scale = instance.Scale;

// Attributes
AttributeSets attrSets = instance.AttributeSets;

// Transaction cho Undo
TransactionManager tm = _app.TransactionManager;
Transaction t = tm.StartTransaction(doc, "Replace Symbol");
// ... thực hiện thay đổi ...
t.End();

// InteractionEvents cho pick mode
InteractionEvents ie = _app.CommandManager.CreateInteractionEvents();
SelectEvents se = ie.SelectEvents;
se.AddSelectionFilter(SelectionFilterEnum.kDrawingSketchedSymbolFilter);
se.OnSelect += OnSymbolSelected;
ie.Start();
```

---

## Vấn đề đã biết / Cần chú ý

1. **DockableWindow HWND**: phải dùng Win32 `SetParent()` để embed WinForms vào DockableWindow — không có API managed trực tiếp.
2. **COM reload**: khi addin reload (`firstTime=false`), ButtonDefinition và DockableWindow đã tồn tại → dùng `RewireEvents()` thay vì tạo lại.
3. **Library file đang mở**: kiểm tra `_app.Documents` trước khi `Documents.Open()` để tránh mở 2 lần.
4. **Undo history**: Replace All tạo 1 transaction lớn → 1 Undo step. Single replace mỗi instance = 1 Undo step.
5. **EmbedInteropTypes = False**: bắt buộc cho Inventor interop DLL.

---

## 🔄 Session Handoff Log

<!-- Claude Code tự cập nhật mục này khi gần hết limit -->
<!-- Đây là "bàn giao ca" — session mới đọc đây để biết tiếp tục từ đâu -->

### Session mới nhất

- **Ngày**: _(Claude Code điền)_
- **Phase đang làm**: _(ví dụ: Phase 2 — LibraryService)_
- **Trạng thái**: _(In progress / Completed)_

#### Files đã tạo/sửa trong session này

| File         | Trạng thái                        | Ghi chú        |
| ------------ | --------------------------------- | -------------- |
| _(tên file)_ | ✅ Done / 🔧 Partial / ❌ Has bug | _(mô tả ngắn)_ |

#### Vấn đề phát sinh & cách giải quyết

- _(ghi lại nếu có quirk của Inventor API, workaround đã dùng, v.v.)_

#### Bước tiếp theo CỤ THỂ (next session bắt đầu từ đây)

```
1. Mở file: _(tên file)_
2. Implement: _(method/class cụ thể)_
3. Lý do: _(context ngắn)_
```

#### Quyết định kỹ thuật mới (nếu có thay đổi so với CONTEXT.md ban đầu)

- _(ghi lại nếu đã thay đổi hướng tiếp cận)_

---

## 📋 Prompt mẫu cho session mới

Khi mở Claude Code lần tiếp, paste prompt này:

```
Read CONTEXT.md carefully, especially the "Session Handoff Log" section at the bottom.
Then continue exactly from the "Bước tiếp theo" listed there.
Follow all coding rules: English code, Vietnamese comments and logs.
Do not re-implement anything marked as ✅ Done.
```
