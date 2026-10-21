param([switch]$IncludeAndroid)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Set-Location -LiteralPath $projectRoot
$version = ([xml](Get-Content 'src\Store.Host\Store.Host.csproj')).Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Neplatná verze Windows projektu.' }
$releaseDirectory = Join-Path $projectRoot "dist\releases\$version"
New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null
$archiveName = "JirkaStore-Windows-$version.zip"
$archivePath = Join-Path $releaseDirectory $archiveName
if (Test-Path -LiteralPath $archivePath) { throw 'Vydání už je zabalené. Publikované balíčky jsou neměnné.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::Open($archivePath,[IO.Compression.ZipArchiveMode]::Create)
try {
    # Explicit allowlist prevents local project packages and signing material entering a public release.
    [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,(Join-Path $projectRoot 'dist\windows\JirkaStore.exe'),'JirkaStore.exe') | Out-Null
    [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,(Join-Path $projectRoot 'dist\windows\engine\JirkaStore.Host.exe'),'engine/JirkaStore.Host.exe') | Out-Null
    [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,(Join-Path $projectRoot 'README.md'),'README.md') | Out-Null
    [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,(Join-Path $projectRoot 'catalog\catalog.json'),'catalog/catalog.json') | Out-Null
    Get-ChildItem -LiteralPath 'dist\windows\licenses' -File | ForEach-Object {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$_.FullName,('licenses/' + $_.Name)) | Out-Null
    }
} finally { $zip.Dispose() }
$names = @($archiveName)
if ($IncludeAndroid) { Copy-Item -LiteralPath 'dist\android\JirkaStore.apk' -Destination $releaseDirectory; $names += 'JirkaStore.apk' }
$lines = foreach ($name in $names) {
    $hash = (Get-FileHash -LiteralPath (Join-Path $releaseDirectory $name) -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $name"
}
[IO.File]::WriteAllLines((Join-Path $releaseDirectory 'SHA256SUMS.txt'),$lines,[Text.UTF8Encoding]::new($false))
Get-ChildItem -LiteralPath $releaseDirectory -File | Select-Object Name,Length
