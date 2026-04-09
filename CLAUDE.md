# CLAUDE.md — CAD Development Team

# Autodesk Inventor 2023 + AutoCAD 2023 | C# | WPF | VS Code

> File này được Claude Code tự động đọc mỗi khi mở project.
> Không cần nhắc lại. Luôn áp dụng toàn bộ nội dung bên dưới.

---

## 1. Khởi động session

### Thứ tự đọc bắt buộc

1. File này (`CLAUDE.md`) — rules và patterns cố định
2. `CONTEXT.md` — kiến trúc chi tiết của project hiện tại
3. `SESSION_LOG.md` — trạng thái mới nhất, bước tiếp theo

### Báo cáo sau khi đọc xong

Sau khi đọc đủ 3 file, báo cáo ngắn theo mẫu:

```
Phase hiện tại : [tên phase]
File cần làm   : [tên file + method cụ thể]
Tồn đọng       : [nếu có, liệt kê ngắn]
```

Sau đó hỏi: **"Tiếp tục hay có thay đổi?"** rồi chờ xác nhận.

### Khi nào tự động đọc 3 file

- User mở session mới chưa nói gì
- User nhắn: "bắt đầu" / "tiếp tục" / "hôm nay làm gì" / "status"

Không bao giờ tự ý bắt đầu làm việc trước khi user xác nhận.

---

## 2. Dev cycle — Quy trình tự động hóa

> Đây là quy trình cốt lõi. Tuân thủ tuyệt đối, không bỏ bước, không tự ý sáng tạo thêm.

### Hai lệnh cốt lõi

```
deploy.bat                          ← Build + verify DLL
restart.bat "đường\dẫn\file.idw"   ← Đóng Inventor, mở lại với file test
```

File test mặc định nếu không truyền tham số:

```
C:\TestFiles\TestDrawing.idw
```

→ Cập nhật đường dẫn thực tế tại biến `TEST_FILE_DEFAULT` trong `restart.bat`.

---

### Quy trình — theo đúng thứ tự này

```
┌─ BƯỚC 1: Viết / sửa code ──────────────────────────────────────┐
│  Áp dụng checklist section 7 trước khi sang bước 2.            │
│  Nếu sửa nhiều file, liệt kê rõ file nào đã thay đổi.         │
└────────────────────────────────────────────────────────────────┘
         │
         ▼
┌─ BƯỚC 2: Chạy deploy.bat ──────────────────────────────────────┐
│  Exit 0 → sang bước 3                                          │
│  Exit 1 → ĐỌC LỖI BUILD, sửa code, chạy lại deploy.bat        │
│           KHÔNG chạy restart khi deploy chưa thành công        │
└────────────────────────────────────────────────────────────────┘
         │
         ▼
┌─ BƯỚC 3: Chạy restart.bat ─────────────────────────────────────┐
│  Exit 0 → sang bước 4                                          │
│  Exit 1 → báo lỗi rõ cho user, không tự xử lý                 │
└────────────────────────────────────────────────────────────────┘
         │
         ▼
┌─ BƯỚC 4: Thông báo cho user ───────────────────────────────────┐
│  Dùng đúng mẫu sau, không thêm bớt:                           │
│                                                                 │
│  "✓ Build thành công. Inventor đang mở file [tên file].        │
│   Đợi 20–30 giây để load xong rồi test nhé.                   │
│   Kiểm tra ribbon 'Custom Tools' có hiện chưa?                 │
│   Cho tôi biết kết quả."                                       │
└────────────────────────────────────────────────────────────────┘
         │
         ▼
┌─ BƯỚC 5: DỪNG — Chờ user phản hồi ────────────────────────────┐
│  Không làm gì thêm. Không đề xuất bước tiếp. Chỉ chờ.        │
└────────────────────────────────────────────────────────────────┘
         │
         ├── User báo LỖI / không như mong đợi
         │         ▼
         │   ┌─ BƯỚC 6A ──────────────────────────────────────────┐
         │   │  Phân tích log / mô tả lỗi.                       │
         │   │  Sửa code.                                         │
         │   │  Hỏi: "Tôi đã sửa [mô tả ngắn gọn].              │
         │   │         Deploy và restart lại không?"              │
         │   │  Chờ user xác nhận → quay lại bước 2.             │
         │   └────────────────────────────────────────────────────┘
         │
         └── User xác nhận OK / implement
                   ▼
             ┌─ BƯỚC 6B ──────────────────────────────────────────┐
             │  Cập nhật SESSION_LOG.md.                          │
             │  Hỏi: "Tiếp tục task tiếp theo không?"            │
             │  KHÔNG tự động chạy thêm lệnh nào.                │
             └────────────────────────────────────────────────────┘

```

---

### Quy tắc cứng — vi phạm là sai

```
✗  KHÔNG chạy restart.bat khi deploy.bat chưa exit 0
✗  KHÔNG chạy deploy.bat khi Inventor đang mở
   (deploy.bat tự guard, nhưng Claude Code phải biết trước)
✗  KHÔNG lặp lại cycle khi chưa có xác nhận của user
✗  KHÔNG mở Inventor lần thứ 2 nếu lần 1 đã chạy thành công
✗  KHÔNG bỏ qua bước 5 dù code có vẻ đúng
✗  KHÔNG tự suy đoán kết quả test — chỉ user mới biết
```

---

## 3. Quy tắc code — BẮT BUỘC

### Ngôn ngữ

| Loại nội dung                             | Ngôn ngữ       |
| ----------------------------------------- | -------------- |
| Tên class, method, variable, property     | **English**    |
| UI text (button, label, tooltip, message) | **English**    |
| XML doc comment (`/// <summary>`)         | **Tiếng Việt** |
| Inline comment (`//`)                     | **Tiếng Việt** |
| Log message                               | **Tiếng Việt** |
| Error message hiển thị cho user           | **Tiếng Việt** |

---

### Logging — bắt buộc mọi class

```csharp
// Khai báo prefix ở đầu mỗi class để dễ filter log
private const string LOG_PREFIX = "[TênClass]";

// Tool xem log: DebugView (Sysinternals) hoặc VS Output window
// Dùng: System.Diagnostics.Debug.WriteLine

public void DoSomething()
{
    Debug.WriteLine($"{LOG_PREFIX} Bắt đầu [tên hành động]...");
    try
    {
        // ... code ...
        Debug.WriteLine($"{LOG_PREFIX} [Tên hành động] THÀNH CÔNG.");
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"{LOG_PREFIX} LỖI: {ex.Message}");
        Debug.WriteLine($"{LOG_PREFIX} Stack trace:\n{ex.StackTrace}");
        throw;
    }
}
```

---

### Kiến trúc — MVC + SOLID

- **Model** — data objects thuần túy, không chứa logic
- **View** — WPF XAML + code-behind tối thiểu (chỉ UI logic)
- **Controller** — điều phối giữa View và Service
- **Service** — business logic, luôn có Interface tương ứng
- **DI** — Dependency Injection thủ công trong entry point (`Addin.Activate`)
- **SRP** — mỗi class chỉ có 1 trách nhiệm duy nhất
- **OCP** — mở rộng qua interface, không sửa class đã ổn định
- **DIP** — phụ thuộc vào abstraction (interface), không phụ thuộc implementation

---

### Error handling

```csharp
// Tầng Service   : luôn log + throw để Controller xử lý
// Tầng Controller: log + chuyển thành thông báo user-friendly
// Tầng View      : hiển thị thông báo, không crash
// KHÔNG BAO GIỜ swallow exception ở tầng Service / Controller
```

---

## 4. Môi trường kỹ thuật

### Chung

```
IDE          : Visual Studio Code
Build        : dotnet build -c Debug (chạy với quyền Administrator)
Version ctrl : Git
```

### Inventor 2023 Add-in

```
Runtime      : .NET Framework 4.8, x64
UI           : WPF (Window Presentation Foundation)
API DLL      : C:\Program Files\Autodesk\Inventor 2023\Bin\Autodesk.Inventor.Interop.dll
               C:\Program Files\Autodesk\Inventor 2023\Bin\stdole.dll
Output       : COM AddIn (.dll + .addin file)
EmbedInterop : False (bắt buộc cho Inventor interop)
Addin folder : %APPDATA%\Autodesk\Inventor 2023\Addins\
```

### AutoCAD 2023 Plugin

```
Runtime      : .NET Framework 4.8, x64
UI           : WPF (Window Presentation Foundation)
API DLL      : C:\Program Files\Autodesk\AutoCAD 2023\acmgd.dll
               C:\Program Files\Autodesk\AutoCAD 2023\acdbmgd.dll
               C:\Program Files\Autodesk\AutoCAD 2023\accoremgd.dll
Output       : AutoCAD Plugin (.dll + .bundle folder)
Bundle folder: %APPDATA%\Autodesk\ApplicationPlugins\
```

---

## 5. Inventor API — Patterns chuẩn

### Lấy active document

```csharp
// Luôn kiểm tra type trước khi cast
if (_app.ActiveDocument is DrawingDocument drawDoc)
{
    // làm việc với drawing document
}
else if (_app.ActiveDocument is AssemblyDocument asmDoc)
{
    // làm việc với assembly document
}
else
{
    Debug.WriteLine($"{LOG_PREFIX} CẢNH BÁO: Document type không được hỗ trợ.");
}
```

### Mở file ẩn (không hiện UI)

```csharp
// Kiểm tra file đã mở chưa để tránh mở 2 lần
Document FindOrOpen(string filePath)
{
    foreach (Document doc in _app.Documents)
    {
        if (string.Equals(doc.FullFileName, filePath,
            StringComparison.OrdinalIgnoreCase))
        {
            Debug.WriteLine($"{LOG_PREFIX} File đã mở sẵn, tái sử dụng: {filePath}");
            return doc;
        }
    }
    Debug.WriteLine($"{LOG_PREFIX} Mở file ẩn: {filePath}");
    return _app.Documents.Open(filePath, openVisible: false);
}
```

### Transaction (Undo support)

```csharp
// Bắt buộc wrap mọi thay đổi document vào Transaction
Transaction transaction = _app.TransactionManager
    .StartTransaction((Document)_doc, "Tên hành động hiển thị trong Undo history");
try
{
    // ... thực hiện thay đổi ...
    transaction.End();
    Debug.WriteLine($"{LOG_PREFIX} Transaction kết thúc THÀNH CÔNG.");
}
catch (Exception ex)
{
    Debug.WriteLine($"{LOG_PREFIX} Transaction ABORT: {ex.Message}");
    transaction.Abort();
    throw;
}
```

### InteractionEvents (pick mode)

```csharp
InteractionEvents _interEvents;

void EnterPickMode()
{
    _interEvents = _app.CommandManager.CreateInteractionEvents();
    var selectEvents = _interEvents.SelectEvents;
    selectEvents.AddSelectionFilter(
        SelectionFilterEnum.kDrawingSketchedSymbolFilter);
    selectEvents.OnSelect    += OnObjectSelected;
    _interEvents.OnTerminate += OnPickModeEnded;
    _interEvents.Start();
    Debug.WriteLine($"{LOG_PREFIX} Pick mode đã bắt đầu.");
}

void ExitPickMode()
{
    if (_interEvents != null)
    {
        _interEvents.Stop();
        _interEvents = null;
        Debug.WriteLine($"{LOG_PREFIX} Pick mode đã kết thúc.");
    }
}
```

### Embed WPF vào DockableWindow

```csharp
[DllImport("user32.dll")]
static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

void EmbedWpfControl(DockableWindow dockWin, UserControl control)
{
    var parameters = new HwndSourceParameters("WpfHost")
    {
        ParentWindow = new IntPtr(dockWin.HWND),
        WindowStyle  = 0x40000000 | 0x10000000, // WS_CHILD | WS_VISIBLE
        Width        = dockWin.Width,
        Height       = dockWin.Height,
        PositionX    = 0,
        PositionY    = 0
    };
    var hwndSource = new HwndSource(parameters);
    hwndSource.RootVisual = control;
    Debug.WriteLine($"{LOG_PREFIX} WPF control đã embed vào DockableWindow.");
}
```

---

## 6. AutoCAD API — Patterns chuẩn

### Command registration

```csharp
[CommandMethod("TENLENHCUATOI", CommandFlags.Modal)]
public void MyCommand()
{
    var doc = Application.DocumentManager.MdiActiveDocument;
    var db  = doc.Database;
    var ed  = doc.Editor;

    Debug.WriteLine($"{LOG_PREFIX} Bắt đầu lệnh MyCommand...");

    using (var tr = db.TransactionManager.StartTransaction())
    {
        try
        {
            // ... thao tác với entities ...
            tr.Commit();
            Debug.WriteLine($"{LOG_PREFIX} Lệnh hoàn thành THÀNH CÔNG.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"{LOG_PREFIX} LỖI: {ex.Message}");
            ed.WriteMessage($"\nLỗi: {ex.Message}");
        }
    }
}
```

### Embed WPF Palette vào AutoCAD

```csharp
PaletteSet _paletteSet;

void ShowPalette(UserControl wpfControl)
{
    if (_paletteSet == null)
    {
        _paletteSet = new PaletteSet("Tên Palette",
            new Guid("YOUR-GUID-HERE"));
        _paletteSet.Size            = new System.Drawing.Size(280, 600);
        _paletteSet.DockEnabled     = DockSides.Left | DockSides.Right;
        _paletteSet.RecalculateSize = true;
        _paletteSet.AddVisual("Tab Name", wpfControl);
        Debug.WriteLine($"{LOG_PREFIX} PaletteSet đã được tạo.");
    }
    _paletteSet.Visible = true;
}
```

### Selection filter

```csharp
var filterList = new TypedValue[]
{
    new TypedValue((int)DxfCode.Start, "INSERT"),
};
var filter = new SelectionFilter(filterList);
var result = doc.Editor.GetSelection(filter);

if (result.Status == PromptStatus.OK)
{
    Debug.WriteLine($"{LOG_PREFIX} User chọn {result.Value.Count} đối tượng.");
}
```

---

## 7. WPF UI — Patterns chuẩn

### Style chuẩn (Inventor/AutoCAD dark theme)

```xml
<ResourceDictionary>
    <Color x:Key="BackgroundColor">#FF1E1E1E</Color>
    <Color x:Key="SurfaceColor">#FF2D2D2D</Color>
    <Color x:Key="AccentColor">#FF0078D4</Color>
    <Color x:Key="AccentHoverColor">#FF006BBD</Color>
    <Color x:Key="BorderColor">#FF404040</Color>
    <Color x:Key="TextPrimaryColor">#FFEEEEEE</Color>
    <Color x:Key="TextSecondaryColor">#FF999999</Color>

    <FontFamily x:Key="UIFont">Segoe UI</FontFamily>
    <sys:Double x:Key="FontSizeNormal">12</sys:Double>
    <sys:Double x:Key="FontSizeSmall">10</sys:Double>
    <sys:Double x:Key="FontSizeHeader">11</sys:Double>
</ResourceDictionary>
```

### MVVM pattern

```csharp
public class MyViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    private string _statusText = "Ready";
    /// <summary>Trạng thái hiển thị trên status bar</summary>
    public string StatusText
    {
        get => _statusText;
        set
        {
            _statusText = value;
            PropertyChanged?.Invoke(this,
                new PropertyChangedEventArgs(nameof(StatusText)));
        }
    }
}
```

### Thread safety — cập nhật UI từ background thread

```csharp
// Đồng bộ
Application.Current.Dispatcher.Invoke(() =>
{
    StatusText = "Đang xử lý...";
});

// Bất đồng bộ
await Application.Current.Dispatcher.InvokeAsync(() =>
{
    StatusText = "Hoàn thành.";
});
```

---

## 8. Checklist trước khi viết code

```
[ ] Đọc SESSION_LOG.md — phase và bước tiếp theo là gì?
[ ] File cần tạo/sửa đã tồn tại chưa? (không tạo lại nếu đã có)
[ ] Có Interface chưa nếu tạo Service mới?
[ ] Có LOG_PREFIX ở đầu class chưa?
[ ] Có log đầu/cuối method quan trọng chưa?
[ ] Có Transaction nếu thay đổi Inventor / AutoCAD document chưa?
[ ] Comment tiếng Việt đầy đủ chưa?
[ ] Có kiểm tra null trước khi dùng COM objects chưa?
```

---

## 9. Quản lý session và context

### Cuối mỗi task — bắt buộc

Sau khi hoàn thành BẤT KỲ task nào (viết code, fix lỗi, hoàn thành phase),
LUÔN tự động cập nhật `SESSION_LOG.md` mà không cần chờ user nhắc.

Dấu hiệu "task xong":

- Vừa tạo / sửa xong 1 file trở lên
- Vừa fix xong 1 lỗi
- Vừa hoàn thành 1 phase
- User vừa confirm "OK" / "done" / "implement"

### Khi sắp hết context

Trước khi context đầy, thực hiện theo thứ tự:

1. Cập nhật `SESSION_LOG.md`:
   - Phase hiện tại
   - Files đã tạo / sửa trong session này
   - Bước tiếp theo cụ thể (file nào, method nào)
   - Quirk của API đã phát hiện (nếu có)
2. Thông báo cho user: "Context sắp đầy. Đã lưu trạng thái vào SESSION_LOG.md. Mở session mới và nhắn 'tiếp tục' để tiếp."

### Nội dung SESSION_LOG.md tối thiểu

```markdown
## Trạng thái: [ngày]

Phase : [tên phase]
Hoàn thành: [liệt kê]
Đang làm : [file + method]
Tiếp theo : [file + method + mô tả cụ thể]
Quirks : [nếu có]
```
