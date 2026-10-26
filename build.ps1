# Builds ThreeMFThumb.dll with the C# compiler that ships with Windows (.NET Framework 4.x).
$ErrorActionPreference = 'Stop'
$fw = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
Push-Location $PSScriptRoot
try {
    & "$fw\csc.exe" /nologo /target:library /optimize /out:ThreeMFThumb.dll `
        /r:"$fw\System.IO.Compression.dll" /r:System.Drawing.dll /r:System.Windows.Forms.dll `
        /r:System.Xml.Linq.dll /r:System.Xml.dll /r:System.Core.dll `
        ThreeMFThumb.cs MeshRenderer.cs PreviewHandler.cs
    if ($LASTEXITCODE -ne 0) { throw "csc failed: $LASTEXITCODE" }
    "Built $PSScriptRoot\ThreeMFThumb.dll"
} finally {
    Pop-Location
}
