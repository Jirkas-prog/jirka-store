param(
    [Parameter(Mandatory=$true)][string]$AppId,
    [Parameter(Mandatory=$true)][string]$SourceDirectory,
    [Parameter(Mandatory=$true)][string]$EntryPoint,
    [Parameter(Mandatory=$true)][string]$Version,
    [string]$DownloadUrl = ''
)
$ErrorActionPreference = 'Stop'
if ($AppId -notmatch '^[a-z][a-z0-9-]{0,63}$' -or $Version -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') { throw 'Neplatné ID nebo verze.' }
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sourceRoot = (Resolve-Path -LiteralPath $SourceDirectory).Path
$exe = [IO.Path]::GetFullPath((Join-Path $sourceRoot $EntryPoint))
if (!$exe.StartsWith($sourceRoot.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase) -or !(Test-Path -LiteralPath $exe -PathType Leaf) -or [IO.Path]::GetExtension($exe) -ne '.exe') { throw 'EXE musí existovat uvnitř zdrojové složky.' }
$catalogPath = Join-Path $projectRoot 'catalog\catalog.json'
$existingLocal = Join-Path $projectRoot 'local\catalog.local.json'
$inputCatalog = if (!$DownloadUrl -and (Test-Path -LiteralPath $existingLocal)) { $existingLocal } else { $catalogPath }
$catalog = Get-Content -LiteralPath $inputCatalog -Raw | ConvertFrom-Json
$app = @($catalog.apps | Where-Object id -eq $AppId)
if ($app.Count -ne 1) { throw 'Nejdřív přidejte právě jednu položku se zadaným ID do catalog/catalog.json.' }
$packageDirectory = Join-Path $projectRoot 'packages'
if ($packageDirectory.StartsWith($sourceRoot.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Zdrojová složka nesmí obsahovat výstupní složku balíčků.' }
if ($DownloadUrl) {
    $uri = [Uri]$DownloadUrl
    if (!$uri.IsAbsoluteUri -or $uri.Scheme -ne 'https' -or $uri.UserInfo) { throw 'DownloadUrl musí být HTTPS bez přihlašovacích údajů.' }
}
New-Item -ItemType Directory -Path $packageDirectory -Force | Out-Null
$zip = Join-Path $packageDirectory "$AppId-$Version.zip"
if (Test-Path -LiteralPath $zip) { throw "Balíček již existuje: $zip. Verze jsou neměnné; použijte novou verzi." }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($sourceRoot,$zip,[IO.Compression.CompressionLevel]::Optimal,$false)
$digest = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
if ($DownloadUrl) {
    $url = $DownloadUrl; $outputCatalog = $catalogPath
} else {
    $url = "packages/$AppId-$Version.zip"
    $outputCatalog = Join-Path $projectRoot 'local\catalog.local.json'
    New-Item -ItemType Directory -Path (Split-Path $outputCatalog) -Force | Out-Null
    $localPackages = Join-Path $projectRoot 'local\packages'
    New-Item -ItemType Directory -Path $localPackages -Force | Out-Null
    Copy-Item -LiteralPath $zip -Destination $localPackages
}
$fields = @{kind='portable-zip'; version=$Version; url=$url; sha256=$digest; entryPoint=$EntryPoint.Replace('\','/')}
foreach ($key in $fields.Keys) { $app[0] | Add-Member -NotePropertyName $key -NotePropertyValue $fields[$key] -Force }
$json = $catalog | ConvertTo-Json -Depth 15
[IO.File]::WriteAllText($outputCatalog,$json,[Text.UTF8Encoding]::new($false))
Write-Output "Balíček: $zip"
Write-Output "Katalog: $outputCatalog"
Write-Output "SHA256: $digest"
