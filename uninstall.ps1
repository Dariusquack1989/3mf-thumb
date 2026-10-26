# Removes the 3MF thumbnail + preview pane handlers. Run elevated.
$ErrorActionPreference = 'Continue'
$thumbClsid = '{8d5dcf6c-7129-43f1-b09d-6532d963b208}'
$previewClsid = '{3a1c2f5e-9b47-4d2a-8e61-7c0f4b9d2a13}'
$thumbIid = '{e357fccd-a995-4576-b01f-234630154e96}'
$previewIid = '{8895b1c6-b41f-4c1c-a562-0d564250836f}'
$fw = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'

New-PSDrive -Name HKCR -PSProvider Registry -Root HKEY_CLASSES_ROOT -ErrorAction SilentlyContinue | Out-Null

foreach ($iid in $thumbIid, $previewIid) {
    $k = "HKCR:\.3mf\ShellEx\$iid"
    if ((Get-ItemProperty $k -ErrorAction SilentlyContinue).'(default)' -in $thumbClsid, $previewClsid) {
        Remove-Item $k -Force
    }
}

foreach ($dll in Get-ChildItem 'C:\Program Files\ThreeMFThumb' -Recurse -Filter ThreeMFThumb.dll -ErrorAction SilentlyContinue) {
    & "$fw\RegAsm.exe" /nologo /unregister $dll.FullName
}
foreach ($c in $thumbClsid, $previewClsid) {
    Remove-Item "HKCR:\CLSID\$c" -Recurse -Force -ErrorAction SilentlyContinue
    Remove-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved' -Name $c -ErrorAction SilentlyContinue
}
Remove-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\PreviewHandlers' -Name $previewClsid -ErrorAction SilentlyContinue

# Files may be locked by Explorer / prevhost until next sign-in
Remove-Item 'C:\Program Files\ThreeMFThumb' -Recurse -Force -ErrorAction SilentlyContinue
"Uninstalled. Clear the thumbnail cache (Disk Cleanup > Thumbnails) to remove cached previews."
