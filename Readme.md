# MCGInventorPlugin — Autodesk Inventor 2023 Add-in

Multi-module plugin for Autodesk Inventor 2023. Extensible architecture with module registration system.

## Modules

### Symbol Handler (Drawing environment)
Insert, replace and manage Sketched Symbols in Drawing documents (.idw) while preserving position, rotation, scale, leader attachment and prompt text.

- **Insert Symbol** — pick geometry (edge, sketch line) to attach with leader, or click empty area for floating
- **Replace Symbol** — single or batch replace across current sheet / all sheets, preserving leader + position
- **Symbol Library** — browse from file (.idw folder) or active document (Local source)
- **Grid / List View** — toggle display mode with search filter
- **Continuous Insert** — double-click or right-click to enter insert mode, insert multiple times until ESC
- **Scan Sheet** — highlight symbols not present in the current library palette

## Requirements

- Autodesk Inventor 2023
- .NET Framework 4.8, x64
- Visual Studio Code (build via `dotnet build`)

## Project Structure

```
MCGInventorPlugin/
├── Core/                       IModule interface + ModuleManager
├── Modules/                    Module registration (SymbolHandlerModule)
├── Controllers/SymbolHandler/  Flow coordination per module
├── Services/SymbolHandler/     Business logic per module (with interfaces)
├── Views/SymbolHandler/        WPF UI per module
├── Models/SymbolHandler/       Data objects per module
├── Utilities/                  Shared helpers (all modules)
├── Resources/                  Shared icons
├── Tools/                      Build & deploy scripts
└── probe/                      Runtime Inventor API reflection tool
```

## Build & Deploy

```bash
# Build + copy DLL to Inventor Addins folder
Tools\deploy.bat

# Close Inventor + reopen with test file
Tools\resart.bat
```

## Architecture

- **Module system**: `IModule` interface → `ModuleManager` handles lifecycle → `MCGInventorPluginAddin` delegates
- **MVC + SOLID** per module: Model (data) → View (WPF) → Controller (orchestration) → Service (logic + interfaces)
- **Adding a new module**: Create `Modules/NewModule.cs` implementing `IModule`, register in `MCGInventorPluginAddin.Activate()`, add Controllers/Services/Views/Models subfolders

## Known Limitations

- Symbol grip display (2 green dots vs 4 yellow + 1 blue) differs from native Inventor — API limitation
- Snap point visual indicators (endpoint/midpoint/intersection) not available through InteractionEvents API
