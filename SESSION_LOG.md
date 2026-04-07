# SESSION_LOG.md — Tiến độ theo session
# Claude Code tự cập nhật file này. Không sửa thủ công phần log.

---

## ⚡ Trạng thái hiện tại — Đọc đây trước

```
Phase    : Phase 6 — DONE. Build PASS.
Bước     : Replace bug fixed (floating symbol), test cần xác nhận trong Inventor
Trạng thái: [ ] User test replace với symbol đang attach vào DrawingView
```

### Làm ngay tiếp theo
> Claude Code đọc mục này và bắt đầu từ đây, không hỏi lại.

```
Chờ user test replace trong Inventor:
  1. Đặt symbol lên bản vẽ sao cho nó bám vào DrawingView (kéo vào trong view)
  2. Dùng Replace → chọn symbol mới → click old symbol
  3. Kiểm tra: kéo DrawingView → symbol mới có di chuyển theo không?

Nếu _AttachedEntity không đủ (symbol vẫn floating sau khi restore):
  → Thử thêm: set newSym.Static = false SAU KHI gán _AttachedEntity
  → Hoặc kiểm tra Transformation matrix thay vì Position
```

---

## 📋 Phase Checklist

- [x] Phase 1 — Addin Skeleton + Ribbon + DockableWindow
- [x] Phase 2 — LibraryService + ThumbnailService + WinForms Palette
- [x] Phase 3 — InteractionController + Pick Mode
- [x] Phase 4 — SymbolReplaceService + ReplaceController (Replace Single PASS)

---

## 📁 Trạng thái từng file

| File | Trạng thái | Ghi chú |
|------|-----------|---------|
| `CLAUDE.md` | ✅ Done | Rules cố định |
| `CONTEXT.md` | ✅ Done | Kiến trúc project |
| `SESSION_LOG.md` | ✅ Done | File này |
| `SymbolReplacer.csproj` | ✅ Done | net48, x64, WPF + WinForms |
| `SymbolReplacer.addin` | ✅ Done | COM GUID đúng |
| `SymbolReplacerAddin.cs` | ✅ Done | DI đầy đủ, đã xóa OnActivateDocument retry |
| `Resources/ReplaceSymbol_32.png` | ✅ Done | |
| `Resources/ReplaceSymbol_16.png` | ✅ Done | |
| `Models/SymbolDefinitionModel.cs` | ✅ Done | |
| `Models/LibraryConfigModel.cs` | ✅ Done | |
| `Models/ReplaceOperationModel.cs` | ❌ Không cần | Không dùng trong kiến trúc hiện tại |
| `Views/SymbolReplacerPanel.xaml` | ✅ Done | WPF; GridSplitter resizable properties; Local/File source tab |
| `Views/SymbolReplacerPanel.xaml.cs` | ✅ Done | Lazy ContextMenu; ViewMode save/restore ItemsSource; SourceMode_Changed |
| `Views/WpfSymbolGrid.cs` | ✅ Done | Wrapper ListBox, ConvertBitmapToSource |
| `Views/SymbolReplacerPanel.cs` | 🗑️ Đã xóa | WinForms cũ — thay bằng WPF XAML |
| `Views/ThumbnailGridControl.cs` | 🗑️ Đã xóa | WinForms cũ — thay bằng WpfSymbolGrid |
| `Controllers/RibbonController.cs` | ✅ Done | AssemblyResolve BAML fix; HwndSource embed; DockWindowSizer |
| `Controllers/PaletteController.cs` | ✅ Done | Local source; Scan/Highlight; Search filter |
| `Controllers/ReplaceController.cs` | ✅ Done | Replace Single/All; Insert mode |
| `Controllers/InteractionController.cs` | ✅ Done | Pick mode; Insert mode; ESC cancel |
| `Services/ILibraryService.cs` | ✅ Done | Thêm LoadLocalDefinitions() |
| `Services/LibraryService.cs` | ✅ Done | Folder + single .idw + Local doc |
| `Services/IThumbnailService.cs` | ✅ Done | |
| `Services/ThumbnailService.cs` | ✅ Done | GDI render + cache |
| `Services/ISymbolReplaceService.cs` | ✅ Done | |
| `Services/SymbolReplaceService.cs` | 🔧 Bug | Replace tạo floating symbol — view attachment không được preserve |
| `Services/IConfigService.cs` | ✅ Done | |
| `Services/ConfigService.cs` | ✅ Done | |
| `Helpers/PictureDispConverter.cs` | ✅ Done | |
| `Helpers/CoordinateHelper.cs` | ✅ Done | |
| `Helpers/GdiRenderHelper.cs` | ✅ Done | |
| `deploy.bat` | ✅ Done | Copy dll+addin vào %AppData% |

**Ký hiệu**: ✅ Done — 🔧 Có bug — ❌ Chưa làm — ⏳ Đang làm

---

## 🏗️ Quyết định kiến trúc đã thay đổi so với CONTEXT.md

| Thay đổi | Lý do |
|----------|-------|
| WPF XAML (HwndSource) thay WinForms | BAML AssemblyResolve fix cho phép WPF hoạt động trong COM host |
| WpfSymbolGrid.cs thay ThumbnailGridControl | Wrapper ListBox WPF, giữ nguyên interface |
| ConfirmReplaceAllDialog → MessageBox | Đơn giản hơn, đủ dùng |
| Không có ViewModels/ | Controller trực tiếp update View qua public methods |
| OnActivateDocument retry đã xóa | Không cần mở/đóng Inventor để load ribbon |

---

## 🗒️ Session Log Chi Tiết
<!-- Claude Code thêm vào ĐẦU mục này (mới nhất lên trên) -->
<!-- Giữ lại tối đa 10 session gần nhất -->

---
### Session: 2026-04-08 (run 2) — Tasks 1-3 DONE + Replace bug analysis

**Tasks completed (build PASS)**:

| # | Task | File | Status |
|---|------|------|--------|
| 1 | Fix view toggle mất symbols | SymbolReplacerPanel.xaml.cs `ViewMode_Changed` | ✅ Done |
| 2 | Properties panel resizable (GridSplitter) | SymbolReplacerPanel.xaml Row 3 splitter | ✅ Done |
| 3 | Local source tab | Panel.xaml, Panel.xaml.cs, ILibraryService, LibraryService, PaletteController | ✅ Done |

**Bug mới phát hiện: Replace tạo floating symbol**

Root cause nằm ở `SymbolReplaceService.ReplaceOne()`:

```
Inventor object model — 2 cách đặt SketchedSymbol:

[Sheet level — floating]
  Sheet
  └─ SketchedSymbols.Add() → symbol.Parent = Sheet
     Symbol di chuyển độc lập với DrawingView

[View level — attached]
  Sheet
  └─ DrawingViews[i]
     └─ SketchedSymbols.Add() → symbol.Parent = DrawingView
        Symbol di chuyển CÙNG DrawingView khi di chuyển view
```

Code hiện tại luôn dùng `sheet.SketchedSymbols.Add()` → tạo floating.
Cần detect xem old symbol có parent là DrawingView không, và insert vào đúng collection.

**Inventor API quirk đã xác nhận**:
- `SketchedSymbol.Parent` trả về `Sheet` hoặc `DrawingView` tùy cách đặt
- `Sheet.SketchedSymbols` chứa TẤT CẢ symbols trên sheet kể cả view-attached
- `DrawingView.SketchedSymbols` chỉ chứa symbols attach vào view đó
- Position trong cả 2 trường hợp đều là sheet coordinates (paper space)

---
### Session: 2026-04-08 (run 1) — Test results + 4 pending tasks

**Test results từ user (Inventor thực tế)**:

| # | Feature | Kết quả |
|---|---------|---------|
| 1 | Palette hiển thị đầy đủ | ✅ PASS |
| 2 | Grid/List view toggle | ✅ PASS (nhưng có bug — xem bên dưới) |
| 3 | Load library link (⚙ dialog) | ✅ PASS |
| 4 | Scan Sheet + highlight | ✅ PASS |
| 5 | View toggle bug | ❌ Sau khi load symbols, đổi Grid↔List → symbols biến mất |
| 6 | Properties panel resize | ❌ Chưa có — user muốn kéo height |
| 7 | Local symbols | ❌ Chưa có — user muốn xem symbols từ active drawing |

**Fixes trong session này**:
- BAML connectionId lỗi: `ContextMenuInsert_Click` trong Style `<Setter><ContextMenu>` → đã di chuyển ContextMenu ra khỏi XAML style, tạo lazy trong code-behind qua `PreviewMouseRightButtonDown`
- Removed `OnActivateDocument` retry mechanism (user yêu cầu không cần mở/đóng Inventor)
- Added `AppDomain.CurrentDomain.AssemblyResolve` hook quanh `new SymbolReplacerPanel()` để giải quyết BAML assembly resolution trong COM host

**Files đã sửa trong session này**:
| File | Thay đổi |
|------|---------|
| `Views/SymbolReplacerPanel.xaml` | Xóa `EventSetter` + Xóa `<ContextMenu>` khỏi ItemContainerStyle |
| `Views/SymbolReplacerPanel.xaml.cs` | Lazy ContextMenu trong `ListSymbols_PreviewMouseRightButtonDown` |
| `Controllers/RibbonController.cs` | `AssemblyResolve` hook + `OnBamlAssemblyResolve` method |
| `SymbolReplacerAddin.cs` | Xóa `_appEvents`, `OnActivateDocument`, `_ribbonUICreated` retry |

**Build**: PASS

---
### Session: 2026-04-07 (run 4) — 5 UI improvements

**Features implemented**:

| # | Feature | Implementation |
|---|---------|---------------|
| 1 | Insert via right-click | ContextMenu trên ListBoxItem, `PreviewMouseRightButtonDown` để select trước khi menu mở, `InsertRequested` event giữ nguyên |
| 2 | Grid / List view toggle | 2 RadioButton ⊞/☰ trong search bar; code-behind swap `ItemTemplate` + `ItemsPanel` + `HorizontalContentAlignment`; `ListItemTemplate` DataTemplate (40×40 thumb + tên) |
| 3 | Load by file path | `txtLibraryPath` đổi từ TextBlock → TextBox (editable, transparent); Enter → `LibraryPathChangeRequested`; ⚙ dialog điền vào TextBox; `SetLibraryPath()` hiển thị full path |
| 4 | Replace All dropdown | Single `btnReplaceAll "Replace All ▾"` + `Button.ContextMenu` → "Current Sheet Only" / "All Sheets"; click opens `PlacementMode.Bottom` |
| 5 | Scan + highlight | `btnScanSheet` + `btnClearHighlight`; `PaletteController.ScanAndHighlight()` dùng `DrawingDocument.SelectSet.Select()` (multi-select tích lũy); `ClearHighlight()` gọi `SelectSet.Clear()` |

**API quirk**: `Application.HighlightSets` không tồn tại cho Drawing context → dùng `DrawingDocument.SelectSet.Select()` thay thế.

**Files đã sửa**:
| File | Thay đổi |
|------|---------|
| `Views/SymbolReplacerPanel.xaml` | Full rewrite — 4 Resources (2 DataTemplates, 2 ItemsPanelTemplates, 1 Style), Row 0 TextBox, Row 1 toggle, Row 2 ContextMenu, Row 4 Replace All + Scan section |
| `Views/SymbolReplacerPanel.xaml.cs` | Full rewrite — 8 new handlers, 2 new events, 3 new public methods |
| `Controllers/PaletteController.cs` | Add `_app` param, ScanAndHighlight, ClearHighlight, 2 new event handlers |
| `SymbolReplacerAddin.cs` | Pass `_app` to PaletteController |

**Build**: PASS (0 errors)

---
### Session: 2026-04-07 (run 3) — Fix WPF empty palette (3 root causes)

**Vấn đề**: Palette hiện nhưng không có nội dung (form trống).

**Root causes đã tìm ra và fix**:

| # | Root cause | Hậu quả | Fix |
|---|-----------|---------|-----|
| 1 | `System.Windows.Application.Current == null` trong COM host | WPF controls tạo được nhưng không render (ResourceDictionary/Theme không hoạt động) | Tạo `new Application { ShutdownMode = OnExplicitShutdown }` ở đầu `EmbedWpfPanel()` |
| 2 | `DockableWindow.HWND = 0` ngay cả trong `OnShow(kAfter)` (đã biết từ trước) | `EmbedWpfPanel()` return early, HwndSource không bao giờ được tạo | Thêm retry timer `System.Windows.Forms.Timer` 200ms — fire trên UI thread, gọi lại `EmbedWpfPanel()` |
| 3 | Không gọi `UpdateLayout()` sau `HwndSource.RootVisual = _panel` | WPF không tự layout lần đầu trong non-WPF message loop | Gọi `_panel.UpdateLayout()` sau khi gán RootVisual |

**Bug thêm đã fix**: `Color` ambiguous (System.Drawing.Color vs Inventor.Color) trong `CreatePlaceholderIcon` → dùng fully qualified `System.Drawing.Color`.

**Files đã sửa**:
| File | Thay đổi |
|------|---------|
| `Controllers/RibbonController.cs` | 3 fixes trong `EmbedWpfPanel()` + `ScheduleEmbedRetry()` + `StopEmbedRetry()` mới + `_embedRetryTimer` field + cleanup trong `Cleanup()` + resize dùng `GetClientRect` thay `dockableWindow.Width/Height` + fix Color ambiguity |

**API quirks mới**:
- WPF trong COM addin PHẢI có `Application.Current != null` để render — tạo thủ công nếu null
- `DockableWindow.HWND` có thể = 0 ngay cả trong `OnShow(kAfter)` → cần retry timer

**Build**: PASS (0 errors, 3 warnings pre-existing)

---
### Session: 2026-04-07 (run 2) — Fix Inventor crash on startup

**Vấn đề**:
| Root Cause | Hậu quả | Fix |
|-----------|---------|-----|
| `SplitContainer.SplitterDistance = 260` trong object initializer khi UserControl.Height = 0 → `ArgumentOutOfRangeException` | Constructor SymbolReplacerPanel throw → Activate() catch + `throw;` → Inventor nhận exception → không gọi Deactivate() → COM leak → "37 objects not released" → TerminateProcess → Inventor crash | Xóa SplitterDistance khỏi initializer; set trong `OnLayout` override sau khi có kích thước thực |
| `Activate()` có `throw;` trong outer catch | Inventor crash (TerminateProcess) thay vì graceful disable | Bỏ `throw;` — log lỗi và return, Inventor disable addin nhưng không crash |
| `deploy.bat` dùng `dotnet` không có full path | "No .NET SDKs were found" khi bat file chạy | Sửa thành `"C:\Program Files\dotnet\dotnet.exe"` |

**Files đã sửa**: `Views/SymbolReplacerPanel.cs`, `SymbolReplacerAddin.cs`, `deploy.bat`

---
### Session: 2026-04-07 (run 1) — Fix palette empty + Properties panel redesign

**Phase đã làm**: Phase 5 — UI Embedding + Panel Features

**Vấn đề đã sửa**:

| Vấn đề | Root cause | Fix |
|--------|-----------|-----|
| Palette empty (nội dung trống) | UserControl tạo không có WinForms parent → WS_POPUP style thay vì WS_CHILD. SetParent() tạo "owned window" thay vì child → không clip/paint trong DockableWindow | `SetWindowLong`: thêm `WS_CHILD\|WS_VISIBLE`, xóa `WS_POPUP` TRƯỚC khi SetParent |
| Palette empty (kích thước 0) | `_dockableWindow.Width/Height` = 0 khi DockableWindow mới show | Dùng Win32 `GetClientRect(hwndParent)` thay cho Inventor API size |
| Panel không hiện sau embed | Không có ShowWindow call sau khi reparent | Thêm `ShowWindow(hwndPanel, SW_SHOW)` sau SetParent |
| Addin crash on activate | `Application.EnableVisualStyles()` nội bộ gọi `SetCompatibleTextRenderingDefault` → throws vì Inventor đã tạo Win32 windows | Xóa hoàn toàn cả 2 calls |
| Duplicate addin conflict | Stale copy trong `Addins/InventorShipDesign/` cùng GUID | Xóa file duplicate |
| COM class not in registry | `Register-Addin.reg` chưa được import | Import reg + RegAsm thành công |
| Drawing ribbon not found at startup | `Ribbons["Drawing"]` fail khi chưa mở file .idw | Subscribe `ApplicationEvents.OnActivateDocument` → retry khi DrawingDocument được mở |

**Files đã sửa**:
| File | Thay đổi |
|------|---------|
| `Controllers/RibbonController.cs` | Thêm GetWindowLong/SetWindowLong/ShowWindow/GetClientRect Win32 imports; EmbedWinFormsPanel 3 fixes |
| `Views/SymbolReplacerPanel.cs` | Đổi `Form` → `UserControl`; Properties panel: Scale/Rotate textbox, 4 checkboxes, PictureBox preview |
| `SymbolReplacerAddin.cs` | Xóa WinForms Application init calls; thêm OnActivateDocument retry logic |
| `Services/ISymbolReplaceService.cs` | InsertSymbol thêm rotation + scale params |
| `Services/SymbolReplaceService.cs` | InsertSymbol implementation với rotation + scale |
| `Controllers/ReplaceController.cs` | OnInsertPointPicked đọc scale/rotation từ panel |

**API quirks phát hiện**:
- `Application.EnableVisualStyles()` trong .NET 4.8 gọi nội bộ `SetCompatibleTextRenderingDefault` → KHÔNG được gọi trong addin context
- `DockableWindow.HWND` có thể = 0 ngay cả trong `OnShow kAfter` → phải retry
- `_dockableWindow.Width/Height` = 0 khi mới show → dùng Win32 `GetClientRect` thay thế
- `UserControl` không có WinForms parent → tạo top-level window với WS_POPUP → phải force WS_CHILD bằng SetWindowLong

**Bước tiếp theo**:
```
1. Test: Mở Inventor → mở .idw → click Symbol Replacer → panel có hiện nội dung?
2. Nếu panel hiện: test Insert, Replace, Properties preview
3. Nếu vẫn trống: xem DebugView tìm dòng "Embed WinForms panel THÀNH CÔNG"
```
---

---
### Session: 2026-04-06 — Fix replace execution + test PASS

**Phase đã làm**: Phase 3 hoàn thành, Phase 4 Replace Single PASS

**Files đã sửa**:
| File | Thay đổi |
|------|---------|
| `Views/SymbolReplacerPanel.cs` | Thêm 3 events + wire button handlers |
| `SymbolReplacerAddin.cs` | DI wiring: InteractionController, SymbolReplaceService, ReplaceController |
| `Models/ReplaceOperationModel.cs` | Tạo mới — snapshot model |
| `SymbolReplacer.csproj` | Fix InventorPublicAssembliesPath, RegisterForComInterop condition |
| `Services/LibraryService.cs` | Fix Path/Directory ambiguity với Inventor.Path |
| `Services/SymbolReplaceService.cs` | Fix stale Point2d + cross-doc definition + (_Document) cast |

**Phát hiện API quirks**:
- `Autodesk.Inventor.Interop.dll` nằm trong `Bin\Public Assemblies\`, không phải `Bin\` (Inventor 2023)
- `old.Position` là COM reference — bị stale sau `old.Delete()` → phải extract X/Y dưới dạng double trước khi delete, rồi dùng `TransientGeometry.CreatePoint2d()` để tạo lại
- `TransactionManager.StartTransaction()` nhận `_Document` interface, không nhận `DrawingDocument` trực tiếp → cast `(_Document)doc`
- `System.IO.Path` bị ambiguous với `Inventor.Path` → dùng alias `using SystemPath = System.IO.Path`
- `RegisterForComInterop` không tương thích với dotnet CLI (MSBuildRuntimeType=Core) → cần condition `'$(MSBuildRuntimeType)'=='Full'`

**Test kết quả (Replace Single)**:
- ✅ Pick mode active
- ✅ Symbol được pick đúng
- ✅ Delete + Insert thành công
- ✅ Position/rotation/scale preserved
- ⚠️ `SnapshotPromptText: 0 entries` — symbol test không có prompt text fields (cần test lại với symbol có text)

**Bước tiếp theo**:
```
1. Test Replace All Current Sheet + All Sheets
2. Test Undo
3. Test với symbol có prompt text fields
```
---
### Session: 2026-04-06 — Audit

**Phase đã làm**: Audit toàn bộ project

**Trạng thái phát hiện**:
- Phase 1 + 2 HOÀN THÀNH
- Phase 3 gần xong: InteractionController ✅, ReplaceController ✅
- 3 vấn đề chặn compile/runtime:

| Vấn đề | File | Chi tiết |
|--------|------|---------|
| Missing events | `Views/SymbolReplacerPanel.cs` | ReplaceController subscribe 3 events không tồn tại trên panel |
| Missing DI wiring | `SymbolReplacerAddin.cs` | InteractionController, SymbolReplaceService, ReplaceController chưa được tạo trong Activate() |
| Missing model | `Models/ReplaceOperationModel.cs` | File chưa tồn tại |

**Phát hiện API quirks**:
- WinForms embed vào DockableWindow hoạt động tốt hơn WPF HwndSource — đã confirmed qua implementation
- `SketchedSymbol.GetResultText(TextBox)` và `SetPromptResultText(TextBox, string)` là API chính xác để đọc/ghi prompt text (không dùng AttributeSets)

**Quyết định kỹ thuật mới**:
- WinForms thay WPF cho toàn bộ UI — đã triển khai từ session trước
- ConfirmReplaceAllDialog → dùng MessageBox.Show() trong ReplaceController — đơn giản hơn

**Bước tiếp theo** (session sau bắt đầu từ đây):
```
1. Views/SymbolReplacerPanel.cs:
   - Thêm events: ReplaceRequested, ReplaceAllCurrentSheetRequested, ReplaceAllAllSheetsRequested
   - Sửa OnReplaceClick → raise ReplaceRequested
   - Sửa OnReplaceAllCurrentSheetClick → raise ReplaceAllCurrentSheetRequested
   - Sửa OnReplaceAllAllSheetsClick → raise ReplaceAllAllSheetsRequested

2. SymbolReplacerAddin.cs → Activate():
   - Thêm fields: _interactionController, _symbolReplaceService, _replaceController
   - Trong Activate(): new InteractionController(_app), new SymbolReplaceService(_app),
     new ReplaceController(_app, _symbolReplaceService, _interactionController, _paletteController)
   - Sau SetPanel của PaletteController: _replaceController.SetPanel(_ribbonController.Panel)

3. Tạo Models/ReplaceOperationModel.cs (snapshot: position, rotation, scale, layer, attributes)

4. Build + test
```
---
