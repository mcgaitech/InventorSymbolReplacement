# SESSION_LOG.md — Tiến độ theo session
# Claude Code tự cập nhật file này. Không sửa thủ công phần log.

---

## ⚡ Trạng thái hiện tại — Đọc đây trước

```
Phase    : Phase 6 — DONE (trừ snap point)
Bước     : Insert/Replace symbol hoàn chỉnh
Trạng thái: [x] Replace leader restore — OK
             [x] PromptStrings string[] — OK
             [x] Position reset sau AddLeader — OK
             [x] Insert attached vào edge (3D model) — OK
             [x] Insert attached vào sketch line — OK
             [x] Insert floating (click vùng trống) — OK
             [x] Leader arrowhead filled solid — OK
             [x] Keyboard input cho Scale/Rotate TextBox — OK
             [x] Numeric-only filter — OK
             [ ] Snap point (endpoint, midpoint, intersection) — CHƯA
```

### Làm ngay tiếp theo
> Claude Code đọc mục này và bắt đầu từ đây, không hỏi lại.

```
BUG CÒN LẠI: Snap point khi insert symbol

Hiện trạng:
  - Insert mode dùng SelectEvents (entity highlight) + MouseEvents (floating fallback)
  - SelectEvents pick được edge, sketch line → attached OK
  - MouseEvents pick vùng trống → floating OK
  - KHÔNG snap được endpoint, midpoint, intersection

Vấn đề:
  - SelectEvents pick ENTITY (curve/line), không pick POINT
  - modelPosition từ OnSelect đã snap, nhưng user muốn visual snap indicator
  - Intersection (giao 2 line) không phải entity → SelectEvents không trigger

Hướng giải quyết đã thử:
  - MouseEvents only (full snap) → không pick được edge (InteractionEvents cần SelectEvents)
  - SelectEvents + MouseEvents dual mode → pick edge OK, nhưng không snap point

Hướng tiếp theo:
  - Tìm cách enable Inventor's snap system trong SelectEvents context
  - Hoặc dùng InteractionEvents.SnapEvents (nếu tồn tại)
  - Hoặc probe runtime để tìm snap-related API

Dev cycle: tự động đóng/mở Inventor (không hỏi user — xem memory feedback_auto_deploy.md)
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
| `Services/SymbolReplaceService.cs` | ✅ Done | Replace leader OK; Insert attach OK; `AttachLeaderToGeometry()` + `FindNearestDrawingCurve()` + `TryAddLeader()`; `BuildPromptStrings()` → `string[]` |
| `Services/IConfigService.cs` | ✅ Done | |
| `Services/ConfigService.cs` | ✅ Done | |
| `Helpers/PictureDispConverter.cs` | ✅ Done | |
| `Helpers/CoordinateHelper.cs` | ✅ Done | |
| `Helpers/GdiRenderHelper.cs` | ✅ Done | |
| `probe/ProbeInvApi.cs` | ✅ Done | Reflection probe — khám phá Inventor API members (không build cùng main project) |
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
### Session: 2026-04-09 (run 9) — UI polish + Bug fixes + Font size

**Ngày**: 2026-04-09

**Tất cả tính năng OK. Các fix trong session:**

| # | Fix | File |
|---|-----|------|
| 1 | Inventor crash khi đóng: `doc.Close()` trên COM object đã release | `Services/LibraryService.cs` — kiểm tra `doc.FullFileName` trước Close |
| 2 | Inventor crash khi đóng: PaletteController.Cleanup không có try/catch | `Controllers/PaletteController.cs` — wrap từng bước cleanup |
| 3 | Default List view thay vì Grid | `Views/SymbolReplacerPanel.xaml` — `IsChecked` đổi sang `rbListView` |
| 4 | Mất symbol khi đổi Grid↔List: `SetItems` tạo list mới, restore reference cũ stale | `Views/SymbolReplacerPanel.xaml.cs` — raise `ViewModeChanged` → PaletteController `ApplyFilter()` |
| 5 | Mất symbol khi xóa search + đổi view: `SetSearchPlaceholder` set flag SAU Text | `Views/SymbolReplacerPanel.xaml.cs` — set `_searchIsPlaceholder = true` TRƯỚC `txtSearch.Text` |
| 6 | Font size tăng 10% (general) + 15% (buttons) + giữ nguyên file path | `Views/SymbolReplacerPanel.xaml` |

**Font size changes:**
| Loại | Trước | Sau |
|------|-------|-----|
| Root FontSize | 11 | 12 |
| General text | 9-10 | 10-11 |
| Button text | 10 | 12 |
| Button height | 22-28 | 26-32 |
| File path | 10 | 10 (giữ nguyên) |

---
### Session: 2026-04-09 (run 8) — Insert attach DONE + Keyboard fix + Leader style

**Ngày**: 2026-04-09

**Kết quả test user — TẤT CẢ OK trừ snap point:**
- ✅ Insert attached vào edge (3D model) — leader + di chuyển theo view
- ✅ Insert attached vào sketch line — DrawingSketch → FindNearestDrawingCurve resolve
- ✅ Insert floating (click vùng trống) — không leader
- ✅ Leader arrowhead: filled solid đen (`kFilledArrowheadType`)
- ✅ Keyboard input cho Scale/Rotate TextBox — `WM_GETDLGCODE` + `WH_GETMESSAGE` hook
- ✅ Numeric-only filter cho Scale/Rotate
- ✅ Replace leader restore + position reset
- ✅ PromptStrings string[]
- ❌ Snap point (endpoint, midpoint, intersection) — chưa giải quyết

**Files đã sửa:**
| File | Thay đổi |
|------|---------|
| `Controllers/InteractionController.cs` | Dual mode (SelectEvents + MouseEvents); OnInsertGeometrySelected gửi null geometry (service tự resolve); OnInsertMouseClickWithSnap cho floating; InsertPickEventArgs class; 7 SelectEvents filter |
| `Services/SymbolReplaceService.cs` | `AttachLeaderToGeometry()` resolve DrawingCurveSegment→Parent, DrawingSketch→FindNearestDrawingCurve, retry AddLeader; `TryAddLeader()` với leader properties (LeaderVisible, LeaderClipping, ArrowheadType); `FindNearestDrawingCurve()` với distance threshold 0.5cm; `InsertSymbol()` auto-resolve geometry khi null |
| `Services/ISymbolReplaceService.cs` | `InsertSymbol` thêm `object attachGeometry = null` |
| `Controllers/ReplaceController.cs` | `OnInsertPointPicked` nhận `InsertPickEventArgs` |
| `Views/SymbolReplacerPanel.xaml.cs` | Keyboard fix: `WM_GETDLGCODE → DLGC_WANTALLKEYS` + `WH_GETMESSAGE` hook redirect; numeric-only filter (`PreviewTextInput` + paste handler) |
| `resart.bat` | `TEST_FILE_DEFAULT` cập nhật sang CAS-0033254.idw |

**Inventor API facts xác nhận qua probe (session này):**
| Fact | Chi tiết |
|------|---------|
| `DrawingCurveSegment` | AddLeader FAIL — cần `.Parent` (DrawingCurve) |
| `DrawingCurve` | AddLeader OK với Point2d hoặc Missing hoặc Double(0.5) intent |
| `DrawingCurve + Int32(0)` | AddLeader FAIL |
| `SketchLine` (trực tiếp) | AddLeader OK với mọi intent |
| `kDrawingSketchObject (117443328)` | SelectEvents trả về DrawingSketch container khi pick sketch line |
| `DrawingSketch` | AddLeader FAIL — cần resolve ra DrawingCurve qua FindNearestDrawingCurve |
| `SketchLine.StartSketchPoint.Geometry` | Tọa độ sketch space (model units) ≠ sheet coordinates |
| `DrawingCurveSegment.StartPoint` | Tọa độ sheet coordinates — dùng cho distance matching |
| Grip display (2 green vs 4 yellow + 1 blue) | Internal Inventor UI state — KHÔNG fix được qua API |
| `kBlankArrowheadType` | Mũi tên rỗng (outline △), KHÔNG phải "không mũi tên" |
| `kFilledArrowheadType` | Mũi tên filled solid ▶ — giống dimension arrows |
| WPF TextBox trong COM host | Inventor intercept keyboard tại message pump; Fix: WM_GETDLGCODE + WH_GETMESSAGE hook |

---
### Session: 2026-04-09 (run 7) — Dev cycle setup + Insert attach debug prep

**Ngày**: 2026-04-09

**Files đã tạo/sửa:**
| File | Thay đổi |
|------|---------|
| `CLAUDE.md` | User cập nhật: thêm Section 2 (Dev cycle), Section 9 (Session management), cấu trúc lại toàn bộ |
| `deploy.bat` | User tạo mới: build + verify DLL, guard Inventor đang mở → exit 1 |
| `resart.bat` | User tạo mới: đóng Inventor → mở lại với file test; Claude cập nhật `TEST_FILE_DEFAULT` |
| `probe/ProbeInvApi.cs` | Claude viết probe mới: test Insert+Attach flow (CreateGeometryIntent + AddLeader) trên Inventor đang chạy |
| `CLAUDE_CODE_PROMPT_GUIDE.md` | User tạo mới: hướng dẫn prompt cho Claude Code |

**Trạng thái:**
- Phase 6 — BUG OPEN
- ĐÃ OK: Replace leader restore, PromptStrings string[], Position reset sau AddLeader
- CHƯA OK: Insert symbol attach vào đối tượng

**Pending issues:**
1. Insert symbol không attach được vào geometry khi pick — cần chạy probe live để xác định bước nào fail
2. `resart.bat` đặt tên sai (thiếu chữ 't') — CLAUDE.md reference `restart.bat`
3. `TEST_FILE_DEFAULT` đã cập nhật sang `CAS-0033254.idw`

**Bước tiếp theo:**
1. Chạy `probe\bin\Debug\net48\ProbeInvApi.exe live` với Inventor đang mở
2. Đọc output → xác định CreateGeometryIntent hoặc AddLeader fail ở đâu
3. Sửa `Services/SymbolReplaceService.cs` → method `AttachLeaderToGeometry()`
4. Áp dụng dev cycle: `deploy.bat` → `resart.bat` → chờ user test

**Inventor API notes (session này):**
- `SketchedSymbol._AttachedEntity` setter: luôn throw "Value does not fall within expected range" — KHÔNG DÙNG
- `Leader.AddLeader([GeometryIntent])` (1 item): E_FAIL — PHẢI có Point2d trước GeometryIntent
- `Leader.AddLeader([Point2d, GeometryIntent])` (2 items): THÀNH CÔNG — đây là cách duy nhất
- `leaf.AttachedEntity` setter: hoạt động SAU KHI `HasRootNode=True`
- `leaf.Position` setter: hoạt động SAU KHI `HasRootNode=True`
- Sau `Add()`, `HasRootNode=False` — phải gọi `AddLeader` để tạo leader
- `AddLeader` dời ROOT (symbol body) về Point2d position → phải reset `newSym.Position` sau
- SelectionFilterEnum: KHÔNG có `kDrawingPointFilter` — dùng `kSketchPointFilter` + `kWorkPointFilter`
- PromptStrings: phải là `string[]` (SAFEARRAY of BSTR), KHÔNG phải `NameValueMap` (VT_DISPATCH bị reject)

---
### Session: 2026-04-08 (run 6) — Insert symbol attach + Position reset

**Kết quả test user:**
- ✅ Replace symbol: leader restore THÀNH CÔNG (position + leader đúng vị trí)
- ✅ PromptStrings string[] fix: không còn E_INVALIDARG
- ✅ Position reset sau AddLeader: insertion point + leader tip đúng vị trí
- ❌ Insert symbol: chưa attach được vào đối tượng khi pick

**Thay đổi đã implement (build PASS, chưa verify hoạt động):**

1. **InteractionController.cs — Insert mode dùng SelectEvents thay MouseEvents**
   - `EnterInsertMode()`: SelectEvents + 7 filter (curve, centerline, centermark, symbol, view, sketch point, work point)
   - Handler mới `OnInsertGeometrySelected()`: extract entity + position
   - Class `InsertPickEventArgs`: chứa `Position` (Point2d) + `PickedGeometry` (object)
   - Xóa `OnInsertMouseClick()` (MouseEvents cũ)

2. **ReplaceController.cs — Truyền geometry xuống service**
   - `OnInsertPointPicked()` nhận `InsertPickEventArgs` thay `Point2d`
   - Truyền `args.PickedGeometry` xuống `InsertSymbol()`

3. **ISymbolReplaceService.cs + SymbolReplaceService.cs — Leader cho Insert**
   - `InsertSymbol()` thêm optional `object attachGeometry`
   - Method mới `AttachLeaderToGeometry()` với CreateGeometryIntent fallback chain:
     - Try 1: `CreateGeometryIntent(geo, Point2d)` — snap gần nhất
     - Try 2: `CreateGeometryIntent(geo, Type.Missing)` — Inventor tự chọn
     - Fail: symbol floating + log cảnh báo
   - Sau AddLeader: `newSym.Position = position` (reset insertion point)

4. **SymbolReplaceService.cs — Position reset sau replace**
   - Thêm `newSym.Position = CreatePoint2d(posX, posY)` sau `RestoreLeader()` trong `ReplaceOne()`
   - Fix: insertion point không còn nhảy sang vị trí leader tip

**Trạng thái**: Insert attach CHƯA VERIFY — cần debug log từ DebugView khi test.

---
### Session: 2026-04-08 (run 5) — Fix PromptStrings + Fix Leader restore

**Hai bugs đã fix trong `Services/SymbolReplaceService.cs`:**

#### Bug 1: E_INVALIDARG khi Insert/Replace symbol có prompt fields
- **Root cause**: `BuildPromptStrings()` trả về `NameValueMap` (VT_DISPATCH). Inventor COM expect `string[]` (SAFEARRAY of BSTR, VT_ARRAY|VT_BSTR) → reject với E_INVALIDARG
- **Fix**: Đổi `BuildPromptStrings()` trả về `string[]` thay `NameValueMap`
- **Xác nhận qua probe**: `SketchedSymbols.Add()` với `string[2]` → THÀNH CÔNG

#### Bug 2: Symbol mới floating sau replace (không bám vào đối tượng, không có leader)
- **Root cause xác nhận qua probe live** (3 lần chạy với Inventor đang mở):
  1. Tất cả 39/40 symbol: `_AttachedEntity = null`, `Leader.HasRootNode = True`
  2. Cơ chế attach thực sự: `Leader.AllLeafNodes[i].AttachedEntity` (GeometryIntent trên leaf node)
  3. `symbol._AttachedEntity` setter → FAIL "Value does not fall within the expected range" (code cũ silent fail)
  4. `AddLeader([GeometryIntent])` → E_FAIL
  5. **`AddLeader([Point2d, GeometryIntent])`** → **THÀNH CÔNG** (công thức đúng)
  6. Sau `Add()` mới: `HasRootNode=False` → phải gọi `AddLeader` để tạo leader

- **Fix**: Thêm `SnapshotLeader()` + `RestoreLeader()` + 2 struct `LeaderLeafSnapshot`/`LeaderSnapshot`
  - Snapshot: `leaf.Position.X/Y` (doubles) + `leaf.AttachedEntity.Geometry/Intent` (raw COM refs)
  - Restore: `sheet.CreateGeometryIntent(geo, intent)` → `AddLeader([Point2d, freshIntent])`
  - Xóa code cũ: `_AttachedEntity` snapshot/restore (hoàn toàn không work)

**Inventor API facts xác nhận qua probe (CRITICAL — đọc kỹ trước khi sửa sau này):**

| Fact | Chi tiết |
|------|---------|
| `symbol._AttachedEntity` | Read: null cho hầu hết symbols. SET: ném exception |
| `Leader.HasRootNode` | False sau `Add()`. True khi symbol được đặt bởi user UI |
| `Leader.AddLeader([GeometryIntent])` | E_FAIL — thiếu Point2d |
| `Leader.AddLeader([Point2d, GeometryIntent])` | **THÀNH CÔNG** — đây là cách duy nhất |
| `leaf.AttachedEntity` setter | Hoạt động sau khi HasRootNode=True |
| `leaf.Position` setter | Hoạt động sau khi HasRootNode=True |
| Symbol #40 exception | `_AttachedEntity` có giá trị, `HasRootNode=False` — loại đặc biệt (callout?) |

**Build**: PASS (0 errors). Chưa test trong Inventor (cần restart Inventor để deploy DLL mới).

---
### Session: 2026-04-08 (run 4) — Phân tích E_INVALIDARG PromptStrings

**Vấn đề**: `InsertSymbol` và `ReplaceOne` ném E_INVALIDARG khi symbol có prompt fields.

**Log xác nhận**:
```
TB[0] promptUID='1' value='DECK'
TB[1] promptUID='2' value='ABL'
BuildPromptStrings: 2 prompt entries.
LỖI InsertSymbol: The parameter is incorrect. (E_INVALIDARG)
```

**Root cause xác định**:
`BuildPromptStrings()` trả về `NameValueMap` (COM object, VT_DISPATCH).
Inventor COM expect `PromptStrings` là `string[]` (SAFEARRAY of BSTR, VT_ARRAY|VT_BSTR).
Reflection probe xác nhận param type là `System.Object optional=True` — tức là COM Variant.
Inventor type-checks Variant tag runtime → VT_DISPATCH bị reject.

VBA convention: `Dim p(1) As String: p(0)="DECK": p(1)="ABL": Add def, pt, 0, 1, p`

**Kế hoạch fix (đã đề xuất, chờ xác nhận)**:
| Option | Mô tả | Ưu tiên |
|--------|-------|---------|
| A | Đổi `BuildPromptStrings()` → trả về `string[]` thay `NameValueMap` | 1st |
| B | Insert với `Type.Missing`, rồi `SetPromptResultText()` sau | 2nd fallback |
| C | Thử `null` thay `Type.Missing` | 3rd fallback |

**Trạng thái**: Chờ user confirm Option A để implement.

---
### Session: 2026-04-08 (run 3) — Replace floating bug FIXED

**Root cause thực sự** (xác nhận qua reflection probe vào Autodesk.Inventor.Interop.dll):

`SketchedSymbol._AttachedEntity` (kiểu `GeometryIntent`) — property read/write kiểm soát việc symbol có "bám" vào entity nào không. Khi user đặt symbol lên view trong UI, Inventor set `_AttachedEntity` trỏ tới entity. Khi entity di chuyển, symbol di chuyển theo.

Code cũ snapshot `Position/Rotation/Scale/Layer` nhưng KHÔNG snapshot `_AttachedEntity` và `Static` → symbol mới luôn floating.

**Inventor API facts đã xác nhận qua probe** (quan trọng — đọc kỹ trước khi sửa sau này):

| Fact | Chi tiết |
|------|---------|
| `DrawingView.SketchedSymbols` | **KHÔNG TỒN TẠI** trong Inventor 2023 API |
| `SketchedSymbol.Parent` | Luôn trả về `Sheet` (không bao giờ là `DrawingView`) |
| `SketchedSymbol._AttachedEntity` | Kiểu `GeometryIntent` — đây là cơ chế attach vào view/entity |
| `SketchedSymbol.Static` | `true` = cố định tại sheet coords; `false` = tham gia annotation update |
| `DrawingView` có | `Sketches` (sketch collection), `Include3DAnnotations` — không có SketchedSymbols |
| Insert symbol | Luôn dùng `Sheet.SketchedSymbols.Add()` — không có cách nào khác |
| View attachment | Hoàn toàn qua `_AttachedEntity` property trên symbol, không qua collection khác nhau |

**Fix thực hiện trong `SymbolReplaceService.ReplaceOne()`**:

```
Thứ tự restore SAU khi insert newSym:
  1. newSym.Static = isStatic           (phải TRƯỚC _AttachedEntity)
  2. newSym._AttachedEntity = oldEntity (key fix — symbol bám lại vào view/entity)
  3. newSym.Layer = layer
  4. RestorePromptText(newSym, snapshot)
```

**Files đã sửa**:
| File | Thay đổi |
|------|---------|
| `Services/SymbolReplaceService.cs` | Snapshot + restore `_AttachedEntity` + `Static`; xóa `FindAttachedView()` sai; đơn giản hóa `GetSheetFromSymbol()` |
| `SymbolReplacer.csproj` | Exclude `probe/` subfolder khỏi build |
| `probe/ProbeInvApi.cs` | Script dùng reflection để khám phá Inventor API (giữ để tham khảo) |

**Build**: PASS (0 errors)

---
### Session: 2026-04-08 (run 2) — Tasks 1-3 DONE

**Tasks completed (build PASS)**:

| # | Task | File | Status |
|---|------|------|--------|
| 1 | Fix view toggle mất symbols | SymbolReplacerPanel.xaml.cs `ViewMode_Changed` — save/restore ItemsSource | ✅ Done |
| 2 | Properties panel resizable (GridSplitter) | SymbolReplacerPanel.xaml — Row 3 splitter, Properties → Row 4 `Height=180 MinHeight=60` | ✅ Done |
| 3 | Local source tab (SOURCE: File / Local) | Panel.xaml + xaml.cs + ILibraryService + LibraryService + PaletteController | ✅ Done |

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
