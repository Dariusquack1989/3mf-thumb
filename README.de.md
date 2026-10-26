# 3mf-thumb

[English](README.md) | **Deutsch**

Vorschaubilder **und** Vorschaufenster im Windows-Explorer für `.3mf`-Dateien, auch für Projekte aus Bambu Studio, OrcaSlicer und PrusaSlicer.

Viele installieren [stl-thumb](https://github.com/unlimitedbacon/stl-thumb), weil dort 3MF-Unterstützung angegeben ist. Die aktuelle Version (0.5.0 von 2022) kann 3MF aber noch nicht. Der Windows-Installer registriert nur `.stl`, und das Programm in Version 0.5.0 kann 3MF-Dateien nicht lesen. Die 3MF-Unterstützung gibt es bisher nur im unveröffentlichten Quellcode. Dieses Projekt schließt die Lücke unter Windows. Es arbeitet neben stl-thumb, das weiterhin `.stl` übernimmt.

## Was es macht

- **Vorschaubilder** (Symbolansicht) und **Vorschaufenster** (Alt+P) für `.3mf`
- Zeigt das Vorschaubild, das der Slicer in die Datei eingebettet hat:
  1. `Metadata/plate_1.png`, die Slicer-Ansicht des Modells (Bambu Studio / Orca)
  2. `Metadata/thumbnail.png` (PrusaSlicer, Cura und andere)
  3. das Standard-Vorschaubild aus `_rels/.rels`
- Enthält die Datei kein Bild (z. B. ein 3MF-Export aus einem CAD-Programm), **rendert es die Geometrie selbst** mit einem kleinen eingebauten Renderer. Dabei werden auch Komponenten und die 3MF-Production-Extension (`p:path`) unterstützt. Diese nutzt Bambu Studio, um Objekte auf mehrere Dateien unter `3D/Objects/*.model` aufzuteilen.

## Voraussetzungen

- Windows 10 / 11, 64-Bit
- .NET Framework 4.5 oder neuer (bei jedem aktuellen Windows vorinstalliert)
- Administratorrechte für die Installation

Mehr wird nicht benötigt. Der Code lässt sich mit dem C#-Compiler bauen, der in Windows schon enthalten ist.

## Installation

Lade `3mf-thumb-<version>.zip` aus dem [neuesten Release](https://github.com/Dariusquack1989/3mf-thumb/releases/latest) herunter und entpacke die Datei. Führe dann in einer PowerShell **als Administrator** aus:

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1
```

Das Skript kopiert die DLL nach `C:\Program Files\ThreeMFThumb\<version>\`, registriert sie und schreibt ein Protokoll nach `install.log`. Am Ende sollte „OK“ stehen. Schließe danach alle Explorer-Fenster und öffne sie neu.

Werden noch alte Symbole angezeigt, leere den Thumbnail-Cache: Datenträgerbereinigung → *Miniaturansichten*.

## Aus dem Quellcode bauen

```powershell
.\build.ps1      # erzeugt ThreeMFThumb.dll
.\install.ps1    # als Administrator
```

## Deinstallation

```powershell
powershell -ExecutionPolicy Bypass -File .\uninstall.ps1   # als Administrator
```

## Einschränkungen

- Dateien, die nur in OneDrive liegen (Cloud-Platzhalter, nicht heruntergeladen), bekommen keine Vorschau. Windows lässt für solche Dateien keine Vorschau-Programme von Drittanbietern zu. Abhilfe: Rechtsklick → „Immer auf diesem Gerät behalten“.
- `.step`- / `.stp`-Dateien werden nicht unterstützt. Sie enthalten exakte CAD-Flächen statt Dreiecken. Für eine Vorschau bräuchte man einen CAD-Kern.

## Funktionsweise

| Datei | Aufgabe |
| --- | --- |
| `ThreeMFThumb.cs` | `IThumbnailProvider`: liest die Datei, sucht das eingebettete Bild und skaliert es |
| `MeshRenderer.cs` | Fallback: liest die 3MF-Modelldateien und rendert eine schattierte isometrische Ansicht |
| `PreviewHandler.cs` | `IPreviewHandler` für das Vorschaufenster des Explorers, mit eigenem UI-Thread |

Das Vorschaufenster läuft auf einem eigenen STA-Thread mit eigener Nachrichtenschleife. `prevhost.exe` kann Handler auf einem Thread aufrufen, der keine Fensternachrichten verarbeitet, und ein dort erstelltes Fenster bleibt leer.

## Danksagung

Dieses Projekt wurde von [stl-thumb](https://github.com/unlimitedbacon/stl-thumb) von [unlimitedbacon](https://github.com/unlimitedbacon) inspiriert. stl-thumb erzeugt Vorschaubilder für STL-Dateien unter Windows und Linux. 3mf-thumb enthält keinen Code aus stl-thumb. Es ist eine eigenständige C#-Umsetzung und ergänzt, was der aktuellen stl-thumb-Version unter Windows fehlt: Vorschaubilder für `.3mf` und ein Vorschaufenster. Am besten nutzt man beide zusammen: stl-thumb für `.stl`, 3mf-thumb für `.3mf`.

## Lizenz

MIT
