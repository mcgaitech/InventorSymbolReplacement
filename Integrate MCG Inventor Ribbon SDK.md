# Tích hợp MCG Inventor Ribbon SDK

Tôi vừa copy folder `Core/` (chứa shared SDK của team MCG) vào project này.
Mục tiêu: addin của project này hiện button cùng tab "MCG TOOLS" với các addin
MCG khác (Symbol Handler, PickData, CreateDummyDetail...).

## Bước 1 — Đọc tài liệu (BẮT BUỘC, làm trước)

Đọc kỹ **`Core/MCGRibbonReadme.md`** từ đầu đến cuối. File này là single source
of truth cho cách dùng SDK — gồm:

- Cấu trúc 7 file SDK trong `Core/` + namespace `MCG.Inventor.Ribbon`
- Pattern share tab "MCG TOOLS" giữa nhiều addin (Tab/Panel ID cố định)
- Template `IToolDescriptor` + `IDockablePanelDescriptor`
- **Pattern JIT-safe entry point** — `Activate` chỉ chứa try/catch +
  `ActivateInternal` với `[MethodImpl(MethodImplOptions.NoInlining)]`
- **`Build(bool firstTime)` gating** — RibbonTab/Panel chỉ tạo khi `firstTime=true`
- Quy ước ID + checklist tránh xung đột GUID
  (đặc biệt phần HKCU registry stale từ RegAsm cũ)
- Layout deploy subfolder `%APPDATA%\Autodesk\Inventor 2023\Addins\<AddinName>\`
- Mẫu `.addin` file đúng format (`<SoftwareVersionGreaterThan>26</...>`,
  relative path, KHÔNG dùng `<SupportedSoftwareVersionGreaterThan>` sai tag)
- Pattern file logger với quy ước "log tồn tại = lỗi"

## Bước 2 — Audit project hiện tại

Sau khi đọc xong Readme, báo cáo:

1. Project hiện có class implement `ApplicationAddInServer` chưa?
   Nếu có: class name, namespace, GUID, đã có pattern JIT-safe chưa.
2. File `.addin` hiện tại (nếu có): nội dung XML, ClassId/ClientId, version tag.
3. Có file nào trong project tham chiếu namespace `MCG.Inventor.Ribbon` chưa?
4. Project đã build ra DLL nào? Có file rác từ rename trước đây không?
5. Trong `C:\CustomTools\Inventor\` và `%APPDATA%\Autodesk\Inventor 2023\Addins\`
   có addin MCG nào đang dùng GUID giống GUID dự định cho project này không?

## Bước 3 — Đề xuất kế hoạch

Dựa trên Readme + audit, viết kế hoạch ngắn gọn:

- File mới cần tạo (entry point, ToolDescriptor, Module, .addin)
- File cần sửa (.csproj reference, existing class)
- GUID mới sinh ra (PowerShell: `[guid]::NewGuid()`)
- Tools sẽ register: tên, `PanelLocation`, `RibbonContext`, có palette WPF hay không
- Layout deploy đề xuất

**DỪNG ở đây — chờ tôi confirm kế hoạch trước khi code.**

## Bước 4 — Sau khi tôi OK kế hoạch

Implement theo đúng pattern trong Readme. Yêu cầu:

- Mọi file mới phải khớp 1-1 với template trong Readme (không tự sáng tạo)
- Entry point bắt buộc dùng pattern JIT-safe
  `[MethodImpl(MethodImplOptions.NoInlining)]`
- `MCGRibbonManager.Build(firstTime)` luôn truyền `firstTime` từ `Activate`
- Resource ID load icon dùng `typeof(...).Namespace.Split('.')[0]`,
  KHÔNG dùng `asm.GetName().Name`
- Tool button ID prefix bằng tên project ngắn để tránh đụng các addin khác
- Sau build, deploy DLL + .addin vào subfolder Addins theo layout trong Readme
- Xoá HKCU registry entry stale (nếu có) khi đổi GUID hoặc rename class:
  `reg delete "HKCU\SOFTWARE\Classes\CLSID\{old-guid}" /f`

## Quy tắc cứng

- Tab ID `id.Tab.MCGTools` và Panel ID `id.Panel.MCGTools.{Model|Drawing|Utility}`
  — KHÔNG đổi (đây là khoá để các addin share cùng tab).
- Mỗi addin một GUID duy nhất toàn hệ thống — KHÔNG copy GUID từ project khác.
- Pattern JIT-safe `ActivateInternal` + `NoInlining` là BẮT BUỘC, không bỏ qua.

Hãy bắt đầu từ Bước 1.
