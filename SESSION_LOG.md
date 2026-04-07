# SESSION_LOG.md — Tiến độ theo session
# Claude Code tự cập nhật file này. Không sửa thủ công phần log.

---

## ⚡ Trạng thái hiện tại — Đọc đây trước

```
Phase    : Phase 5 — UI Embedding + Panel Features
Bước     : 5 UI improvements implemented, build PASS
Trạng thái: [ ] Cần test toàn bộ 5 features mới trong Inventor
```

### Làm ngay tiếp theo
> Claude Code đọc mục này và bắt đầu từ đây, không hỏi lại.

```
Test 5 features mới (rebuild với quyền Admin trước):

1. Load library via file path:
   - Gõ/paste đường dẫn file .idw vào TextBox "LIBRARY SOURCE" → nhấn Enter
   - Hoặc click ⚙ → chọn file qua dialog
   - Kiểm tra symbols hiện trong palette

2. Grid / List view toggle:
   - Click ☰ → chuyển sang list view (thumbnail nhỏ 40×40, tên symbol bên phải)
   - Click ⊞ → chuyển lại grid view

3. Insert bằng right-click:
   - Click chọn 1 symbol trong palette → right-click → "Insert into Drawing"
   - Kiểm tra insert mode hoạt động

4. Replace All dropdown:
   - Chọn symbol trong palette → click "Replace All ▾"
   - Phải hiện menu: "Current Sheet Only" / "All Sheets"

5. Scan Sheet:
   - Mở bản vẽ có symbol
   - Click "Scan Sheet" → symbols không có trong palette bị highlight (selected)
   - Click "Clear Highlight" → bỏ selection
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
| `SymbolReplacerAddin.cs` | 🔧 Bug | Thiếu DI: InteractionController, SymbolReplaceService, ReplaceController |
| `Resources/ReplaceSymbol_32.png` | ✅ Done | |
| `Resources/ReplaceSymbol_16.png` | ✅ Done | |
| `Models/SymbolDefinitionModel.cs` | ✅ Done | |
| `Models/LibraryConfigModel.cs` | ✅ Done | |
| `Models/ReplaceOperationModel.cs` | ❌ Chưa tạo | Phase 3 yêu cầu |
| `Views/SymbolReplacerPanel.cs` | 🔧 Bug | Thiếu 3 events: ReplaceRequested, ReplaceAllCurrentSheetRequested, ReplaceAllAllSheetsRequested |
| `Views/ThumbnailGridControl.cs` | ✅ Done | WinForms thay cho XAML |
| `Controllers/RibbonController.cs` | ✅ Done | |
| `Controllers/PaletteController.cs` | ✅ Done | |
| `Controllers/ReplaceController.cs` | ✅ Done | Replace Single PASS |
| `Controllers/InteractionController.cs` | ✅ Done | Phase 3 complete |
| `Services/ILibraryService.cs` | ✅ Done | |
| `Services/LibraryService.cs` | ✅ Done | Support cả folder và single .idw |
| `Services/IThumbnailService.cs` | ✅ Done | |
| `Services/ThumbnailService.cs` | ✅ Done | |
| `Services/ISymbolReplaceService.cs` | ✅ Done | |
| `Services/SymbolReplaceService.cs` | ✅ Done | Phase 4 logic done |
| `Services/IConfigService.cs` | ✅ Done | |
| `Services/ConfigService.cs` | ✅ Done | |
| `Helpers/PictureDispConverter.cs` | ✅ Done | |
| `Helpers/CoordinateHelper.cs` | ✅ Done | |
| `Helpers/GdiRenderHelper.cs` | ✅ Done | |
| `Views/ConfirmReplaceAllDialog` | ❌ Chưa tạo | Phase 4 — REPLACED bởi MessageBox.Show() trong ReplaceController |
| `ViewModels/` | ❌ Không cần | Dùng WinForms thay WPF/MVVM |
| `deploy.bat` | ✅ Done | Copy dll+addin vào %AppData% |

**Ký hiệu**: ✅ Done — 🔧 Có bug — ❌ Chưa làm — ⏳ Đang làm

---

## 🏗️ Quyết định kiến trúc đã thay đổi so với CONTEXT.md

| Thay đổi | Lý do |
|----------|-------|
| WinForms thay WPF XAML | DockableWindow nhận HWND trực tiếp, WinForms embed đơn giản hơn HwndSource |
| Không có ViewModels/ | WinForms không cần MVVM |
| ConfirmReplaceAllDialog → MessageBox | Đơn giản hơn, đủ dùng, không cần dialog riêng |
| ThumbnailGridControl.cs (WinForms Panel custom) | Thay cho WPF ItemsControl thumbnail grid |

---

## 🗒️ Session Log Chi Tiết
<!-- Claude Code thêm vào ĐẦU mục này (mới nhất lên trên) -->
<!-- Giữ lại tối đa 10 session gần nhất -->

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
