# CLAUDE.md — CAD Development Team

# Autodesk Inventor 2023 + AutoCAD 2023 | C# | WPF | VS Code

> File này được Claude Code tự động đọc mỗi khi mở project.
> Không cần nhắc lại. Luôn áp dụng toàn bộ nội dung bên dưới.

---

## 1. Đọc ngay khi bắt đầu session

Thứ tự đọc bắt buộc:

1. File này (`CLAUDE.md`) — rules và patterns cố định
2. `CONTEXT.md` — kiến trúc chi tiết của project hiện tại
3. `SESSION_LOG.md` — trạng thái mới nhất, bước tiếp theo

Sau khi đọc xong, báo cáo ngắn:

- Đang ở Phase nào
- File nào cần làm tiếp theo
- Có vấn đề tồn đọng nào không

Khi user mở session mới và chưa nói gì,
hoặc nhắn "bắt đầu" / "tiếp tục" / "hôm nay làm gì":

→ Tự động đọc CLAUDE.md + CONTEXT.md + SESSION_LOG.md
→ Báo cáo ngắn: đang ở đâu, làm gì tiếp
→ Hỏi: "Tiếp tục hay có thay đổi?"
→ Chờ xác nhận rồi mới làm

## 2. Quy tắc code — BẮT BUỘC

### Ngôn ngữ

| Loại nội dung                             | Ngôn ngữ       |
| ----------------------------------------- | -------------- |
| Tên class, method, variable, property     | **English**    |
| UI text (button, label, tooltip, message) | **English**    |
| XML doc comment (`/// <summary>`)         | **Tiếng Việt** |
| Inline comment (`//`)                     | **Tiếng Việt** |
| Log message                               | **Tiếng Việt** |
| Error message hiển thị cho user           | **Tiếng Việt** |

### Logging — bắt buộc mọi class

```csharp
// Khai báo prefix ở đầu mỗi class để dễ filter log
private const string LOG_PREFIX = "[TênClass]";

// Tool: System.Diagnostics.Debug.WriteLine
// Xem log qua: DebugView (Sysinternals) hoặc VS Output window

// Pattern chuẩn — áp dụng cho mọi method quan trọng
public void DoSomething()
{
    // Bắt đầu thực hiện [tên hành động]
    Debug.WriteLine($"{LOG_PREFIX} Bắt đầu [tên hành động]...");
    try
    {
        // ... code ...
        // Hoàn thành thành công
        Debug.WriteLine($"{LOG_PREFIX} [Tên hành động] THÀNH CÔNG.");
    }
    catch (Exception ex)
    {
        // Lỗi khi thực hiện [tên hành động]
        Debug.WriteLine($"{LOG_PREFIX} LỖI: {ex.Message}");
        Debug.WriteLine($"{LOG_PREFIX} Stack trace:\n{ex.StackTrace}");
        throw;
    }
}
```

### Kiến trúc — MVC + SOLID

- **M**odel: data objects thuần túy, không chứa logic
- **V**iew: WPF XAML + code-behind tối thiểu (chỉ UI logic)
- **C**ontroller: điều phối giữa View và Service
- **Service**: business logic, luôn có Interface tương ứng
- **DI**: Dependency Injection thủ công trong entry point (Addin.Activate)
- **SRP**: mỗi class chỉ có 1 trách nhiệm duy nhất
- **OCP**: mở rộng qua interface, không sửa class đã ổn định
- **DIP**: phụ thuộc vào abstraction (interface), không phụ thuộc implementation

### Error handling

```csharp
// Tầng Service: luôn log + throw để Controller xử lý
// Tầng Controller: log + chuyển thành thông báo user-friendly
// Tầng View: hiển thị thông báo, không crash
// KHÔNG BAO GIỜ swallow exception ở tầng Service/Controller
```

---

## 3. Môi trường kỹ thuật

### Chung

```
IDE          : Visual Studio Code
Build        : dotnet build -c Debug (chạy với quyền Administrator)
Version ctrl : Git
```

### Inventor 2023 AddIn

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

## 4. Inventor API — Patterns chuẩn

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
    // Tìm trong danh sách document đang mở
    foreach (Document doc in _app.Documents)
    {
        if (string.Equals(doc.FullFileName, filePath,
            StringComparison.OrdinalIgnoreCase))
        {
            Debug.WriteLine($"{LOG_PREFIX} File đã mở sẵn, tái sử dụng: {filePath}");
            return doc;
        }
    }
    // Mở mới ở chế độ ẩn
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
    // Lỗi — hủy transaction để rollback
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
    // Bắt đầu pick mode với filter chỉ cho phép chọn symbol
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
    // Thoát pick mode và dọn dẹp events
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
// Inventor DockableWindow chỉ nhận Win32 HWND
// Dùng HwndSource để host WPF UserControl
[DllImport("user32.dll")]
static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

void EmbedWpfControl(DockableWindow dockWin, UserControl control)
{
    var parameters = new HwndSourceParameters("WpfHost")
    {
        ParentWindow   = new IntPtr(dockWin.HWND),
        WindowStyle    = 0x40000000 | 0x10000000, // WS_CHILD | WS_VISIBLE
        Width          = dockWin.Width,
        Height         = dockWin.Height,
        PositionX      = 0,
        PositionY      = 0
    };
    var hwndSource = new HwndSource(parameters);
    hwndSource.RootVisual = control;
    Debug.WriteLine($"{LOG_PREFIX} WPF control đã embed vào DockableWindow.");
}
```

---

## 5. AutoCAD API — Patterns chuẩn

### Command registration

```csharp
[CommandMethod("TENLENHCUATOI", CommandFlags.Modal)]
public void MyCommand()
{
    // Lấy document và database hiện tại
    var doc = Application.DocumentManager.MdiActiveDocument;
    var db  = doc.Database;
    var ed  = doc.Editor;

    Debug.WriteLine($"{LOG_PREFIX} Bắt đầu lệnh MyCommand...");

    // Luôn dùng Transaction khi đọc/ghi database
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
            // Lỗi — transaction tự động rollback khi dispose
            Debug.WriteLine($"{LOG_PREFIX} LỖI: {ex.Message}");
            ed.WriteMessage($"\nLỗi: {ex.Message}");
        }
    }
}
```

### Embed WPF Palette vào AutoCAD

```csharp
// AutoCAD hỗ trợ PaletteSet — container cho WPF controls
PaletteSet _paletteSet;

void ShowPalette(UserControl wpfControl)
{
    if (_paletteSet == null)
    {
        // Tạo PaletteSet mới với GUID cố định
        _paletteSet = new PaletteSet("Tên Palette",
            new Guid("YOUR-GUID-HERE"));
        _paletteSet.Size             = new System.Drawing.Size(280, 600);
        _paletteSet.DockEnabled      = DockSides.Left | DockSides.Right;
        _paletteSet.RecalculateSize  = true;

        // Thêm WPF control vào palette
        _paletteSet.AddVisual("Tab Name", wpfControl);
        Debug.WriteLine($"{LOG_PREFIX} PaletteSet đã được tạo.");
    }
    _paletteSet.Visible = true;
}
```

### Selection filter

```csharp
// Lọc đối tượng trước khi user pick
var filterList = new TypedValue[]
{
    new TypedValue((int)DxfCode.Start, "INSERT"),  // Block reference
};
var filter = new SelectionFilter(filterList);
var result = doc.Editor.GetSelection(filter);

if (result.Status == PromptStatus.OK)
{
    // Xử lý các đối tượng được chọn
    Debug.WriteLine($"{LOG_PREFIX} User chọn {result.Value.Count} đối tượng.");
}
```

---

## 6. WPF UI — Patterns chuẩn

### Style chuẩn (Inventor/AutoCAD dark theme)

```xml
<!-- ResourceDictionary dùng chung cho toàn team -->
<ResourceDictionary>
    <!-- Màu sắc chuẩn -->
    <Color x:Key="BackgroundColor">#FF1E1E1E</Color>
    <Color x:Key="SurfaceColor">#FF2D2D2D</Color>
    <Color x:Key="AccentColor">#FF0078D4</Color>
    <Color x:Key="AccentHoverColor">#FF006BBD</Color>
    <Color x:Key="BorderColor">#FF404040</Color>
    <Color x:Key="TextPrimaryColor">#FFEEEEEE</Color>
    <Color x:Key="TextSecondaryColor">#FF999999</Color>

    <!-- Font chuẩn -->
    <FontFamily x:Key="UIFont">Segoe UI</FontFamily>
    <sys:Double x:Key="FontSizeNormal">12</sys:Double>
    <sys:Double x:Key="FontSizeSmall">10</sys:Double>
    <sys:Double x:Key="FontSizeHeader">11</sys:Double>
</ResourceDictionary>
```

### MVVM pattern cho WPF

```csharp
// ViewModel luôn implement INotifyPropertyChanged
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
            // Thông báo UI cập nhật binding
            PropertyChanged?.Invoke(this,
                new PropertyChangedEventArgs(nameof(StatusText)));
        }
    }
}
```

### Thread safety (cập nhật UI từ background thread)

```csharp
// Luôn dispatch về UI thread khi cập nhật từ background
Application.Current.Dispatcher.Invoke(() =>
{
    StatusText = "Đang xử lý...";
    // ... cập nhật UI properties ...
});

// Hoặc dùng async/await
await Application.Current.Dispatcher.InvokeAsync(() =>
{
    StatusText = "Hoàn thành.";
});
```

---

## 7. Checklist trước khi viết code

```
[ ] Đọc SESSION_LOG.md — phase và bước tiếp theo là gì?
[ ] File cần tạo/sửa đã tồn tại chưa? (không tạo lại nếu đã có)
[ ] Có Interface chưa nếu tạo Service mới?
[ ] Có LOG_PREFIX ở đầu class chưa?
[ ] Có log đầu/cuối method quan trọng chưa?
[ ] Có Transaction nếu thay đổi Inventor/AutoCAD document chưa?
[ ] Comment tiếng Việt đầy đủ chưa?
[ ] Có kiểm tra null trước khi dùng COM objects chưa?
```

---

## 8. Khi sắp hết context limit

Tự động chạy skill `/update-session` hoặc thực hiện:

1. Cập nhật SESSION_LOG.md với trạng thái hiện tại
2. Liệt kê files đã tạo/sửa trong session này
3. Ghi rõ bước tiếp theo cụ thể (file nào, method nào)
4. Ghi lại mọi quirk của API đã phát hiện

## Quy tắc bắt buộc cuối mỗi task

Sau khi hoàn thành BẤT KỲ task nào
(viết xong code, fix xong lỗi, hoàn thành phase),
LUÔN LUÔN tự động chạy /update-session
mà không cần chờ user nhắc.

Dấu hiệu "task xong":

- Vừa tạo/sửa xong 1 file trở lên
- Vừa fix xong 1 lỗi
- Vừa hoàn thành 1 phase
- User vừa confirm "OK" hoặc "done"
