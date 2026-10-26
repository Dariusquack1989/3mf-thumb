# Installs the 3MF thumbnail + preview pane handlers machine-wide. Run elevated.
$ErrorActionPreference = 'Stop'
$log = Join-Path $PSScriptRoot 'install.log'
Start-Transcript -Path $log -Force | Out-Null
try {
    $version = '1.2'
    $thumbClsid = '{8d5dcf6c-7129-43f1-b09d-6532d963b208}'
    $previewClsid = '{3a1c2f5e-9b47-4d2a-8e61-7c0f4b9d2a13}'
    $thumbIid = '{e357fccd-a995-4576-b01f-234630154e96}'
    $previewIid = '{8895b1c6-b41f-4c1c-a562-0d564250836f}'
    $prevhostAppId = '{6d2b5079-2f0b-48dd-ab7f-97cec514d30b}'
    # Versioned folder: an older DLL may still be locked by a running surrogate process.
    $dest = "C:\Program Files\ThreeMFThumb\$version"
    $fw = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'

    New-Item -ItemType Directory -Force $dest | Out-Null
    Copy-Item (Join-Path $PSScriptRoot 'ThreeMFThumb.dll') $dest -Force

    & "$fw\RegAsm.exe" /nologo /codebase "$dest\ThreeMFThumb.dll"
    if ($LASTEXITCODE -ne 0) { throw "RegAsm failed: $LASTEXITCODE" }

    New-PSDrive -Name HKCR -PSProvider Registry -Root HKEY_CLASSES_ROOT -ErrorAction SilentlyContinue | Out-Null
    $approved = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved'

    # Thumbnails
    New-Item -Force "HKCR:\.3mf\ShellEx\$thumbIid" | Out-Null
    Set-ItemProperty "HKCR:\.3mf\ShellEx\$thumbIid" -Name '(default)' -Value $thumbClsid
    Set-ItemProperty "HKCR:\CLSID\$thumbClsid" -Name 'DisableProcessIsolation' -Value 0 -Type DWord
    Set-ItemProperty $approved -Name $thumbClsid -Value '3MF Thumbnail Provider'

    # Preview pane
    New-Item -Force "HKCR:\.3mf\ShellEx\$previewIid" | Out-Null
    Set-ItemProperty "HKCR:\.3mf\ShellEx\$previewIid" -Name '(default)' -Value $previewClsid
    Set-ItemProperty "HKCR:\CLSID\$previewClsid" -Name '(default)' -Value '3MF Preview Handler'
    Set-ItemProperty "HKCR:\CLSID\$previewClsid" -Name 'DisplayName' -Value '3MF Preview Handler'
    Set-ItemProperty "HKCR:\CLSID\$previewClsid" -Name 'AppID' -Value $prevhostAppId
    Set-ItemProperty "HKCR:\CLSID\$previewClsid\InprocServer32" -Name 'ThreadingModel' -Value 'Apartment'
    Set-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\PreviewHandlers' -Name $previewClsid -Value '3MF Preview Handler'
    Set-ItemProperty $approved -Name $previewClsid -Value '3MF Preview Handler'

    # Remove older versions if they are no longer locked
    Get-ChildItem 'C:\Program Files\ThreeMFThumb' | Where-Object Name -ne $version |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    "OK"
} catch {
    "ERROR: $_"
} finally {
    Stop-Transcript | Out-Null
}
