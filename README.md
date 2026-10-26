# 3mf-thumb

**English** | [Deutsch](README.de.md)

Windows Explorer thumbnails **and** preview pane for `.3mf` files, including Bambu Studio / OrcaSlicer / PrusaSlicer projects.

Many people install [stl-thumb](https://github.com/unlimitedbacon/stl-thumb) because it says it supports 3MF. But the latest release (0.5.0, 2022) does not: the Windows installer only registers `.stl`, and the 0.5.0 binary cannot read 3MF files. 3MF support exists only in unreleased source code. This project fills that gap for Windows. It works next to stl-thumb, which keeps handling `.stl`.

## What it does

- **Thumbnails** (icon view) and **preview pane** (Alt+P) for `.3mf`
- Uses the preview image the slicer embedded in the file:
  1. `Metadata/plate_1.png`, the slicer render of the model (Bambu Studio / Orca)
  2. `Metadata/thumbnail.png` (PrusaSlicer, Cura and others)
  3. the standard OPC thumbnail relationship from `_rels/.rels`
- If the file has no embedded image (for example a 3MF exported from CAD), it **renders the geometry itself** with a small built-in z-buffer renderer. This supports components and the 3MF production extension (`p:path`), which Bambu Studio uses to split objects into `3D/Objects/*.model`.

## Requirements

- Windows 10 / 11, 64-bit
- .NET Framework 4.5 or newer (preinstalled on every current Windows)
- Administrator rights for installation

Nothing else is needed. The code compiles with the C# compiler that ships with Windows.

## Install

Download `3mf-thumb-<version>.zip` from the [latest release](https://github.com/Dariusquack1989/3mf-thumb/releases/latest), extract it, then run in an **elevated** PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1
```

The script copies the DLL to `C:\Program Files\ThreeMFThumb\<version>\`, registers it, and writes a log to `install.log`. Close and reopen your Explorer windows afterwards.

If old icons are still cached, clear the thumbnail cache: Disk Cleanup → *Thumbnails*.

## Build from source

```powershell
.\build.ps1      # produces ThreeMFThumb.dll
.\install.ps1    # elevated
```

## Uninstall

```powershell
powershell -ExecutionPolicy Bypass -File .\uninstall.ps1   # elevated
```

## Limitations

- Files that exist only in OneDrive (cloud placeholders that are not downloaded) get no preview. Windows does not run third-party preview handlers for them. Choose "Always keep on this device" to fix that.
- `.step` / `.stp` files are not supported. They contain exact CAD surfaces rather than triangles, so rendering them would need a CAD kernel.

## How it works

| File | Purpose |
| --- | --- |
| `ThreeMFThumb.cs` | `IThumbnailProvider`: reads the file stream, finds the embedded image, scales it |
| `MeshRenderer.cs` | Fallback: parses the 3MF model files and renders an isometric, shaded view |
| `PreviewHandler.cs` | `IPreviewHandler` for the Explorer preview pane, with its own UI thread |

The preview handler runs its window on a dedicated STA thread with its own message loop. `prevhost.exe` can call handlers on a thread that never pumps messages, and a window created there stays blank.

## Acknowledgements

This project was inspired by [stl-thumb](https://github.com/unlimitedbacon/stl-thumb) by [unlimitedbacon](https://github.com/unlimitedbacon), which provides thumbnails for STL files on Windows and Linux. 3mf-thumb shares no code with stl-thumb. It is an independent C# implementation that adds what the current stl-thumb release lacks on Windows: `.3mf` thumbnails and a preview pane handler. Use both together: stl-thumb for `.stl`, 3mf-thumb for `.3mf`.

## License

MIT
