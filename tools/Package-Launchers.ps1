$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Set-Location -LiteralPath $projectRoot
$releaseDirectory = Join-Path $projectRoot 'dist\release'
New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null
$archivePath = Join-Path $releaseDirectory 'JirkaStore-Windows-1.0.0.zip'
if (Test-Path -LiteralPath $archivePath) { throw 'Vydání už je zabalené. Publikované balíčky jsou neměnné.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::Open($archivePath,[IO.Compression.ZipArchiveMode]::Create)
try {
    # Explicit allowlist prevents local project packages and signing material entering a public release.
    [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,(Join-Path $projectRoot 'dist\windows\JirkaStore.exe'),'JirkaStore.exe') | Out-Null
    [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,(Join-Path $projectRoot 'README.md'),'README.md') | Out-Null
    [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,(Join-Path $projectRoot 'catalog\catalog.json'),'catalog/catalog.json') | Out-Null
} finally { $zip.Dispose() }
Copy-Item -LiteralPath 'dist\android\JirkaStore.apk' -Destination $releaseDirectory
$lines = foreach ($name in @('JirkaStore-Windows-1.0.0.zip','JirkaStore.apk')) {
    $hash = (Get-FileHash -LiteralPath (Join-Path $releaseDirectory $name) -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $name"
}
[IO.File]::WriteAllLines((Join-Path $releaseDirectory 'SHA256SUMS.txt'),$lines,[Text.UTF8Encoding]::new($false))
Get-ChildItem -LiteralPath $releaseDirectory -File | Select-Object Name,Length
