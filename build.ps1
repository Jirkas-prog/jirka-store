param(
    [switch]$Android,
    [string]$AndroidSdk = '',
    [string]$JavaSdk = '',
    [switch]$UseLocalCatalog
)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
dotnet run --project 'tests\Store.Checks' -c Release -- 'catalog\catalog.json'
if ($LASTEXITCODE) { throw 'Testy selhaly.' }
dotnet publish 'src\Store.Windows\Store.Windows.csproj' -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o 'dist\windows' --nologo
if ($LASTEXITCODE) { throw 'Sestavení Windows selhalo.' }
if ($UseLocalCatalog) {
    $localCatalog = Join-Path $PSScriptRoot 'local\catalog.local.json'
    if (!(Test-Path -LiteralPath $localCatalog)) { throw 'Nejdřív vytvořte lokální balíček pomocí tools/Package-Windows.ps1.' }
    Copy-Item -LiteralPath $localCatalog -Destination 'dist\windows\catalog\catalog.json'
    New-Item -ItemType Directory -Path 'dist\windows\catalog\packages' -Force | Out-Null
    Get-ChildItem -LiteralPath 'local\packages' -File | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination 'dist\windows\catalog\packages' }
}
Copy-Item -LiteralPath 'README.md' -Destination 'dist\windows\README.md'
if ($Android) {
    if (!$AndroidSdk) { $AndroidSdk = ${env:ANDROID_HOME} }
    if (!$AndroidSdk -and (Test-Path -LiteralPath 'C:\Program Files (x86)\Android\android-sdk')) { $AndroidSdk = 'C:\Program Files (x86)\Android\android-sdk' }
    if (!$JavaSdk) { $JavaSdk = ${env:JAVA_HOME} }
    if (!$JavaSdk -and (Test-Path -LiteralPath 'C:\Program Files (x86)\Android\openjdk\jdk-17.0.8.101-hotspot')) { $JavaSdk = 'C:\Program Files (x86)\Android\openjdk\jdk-17.0.8.101-hotspot' }
    if (!$AndroidSdk -or !$JavaSdk) { throw 'Zadejte -AndroidSdk a -JavaSdk (JDK 17).' }
    dotnet build 'src\Store.Android\Store.Android.csproj' -c Release "-p:AndroidSdkDirectory=$AndroidSdk" "-p:JavaSdkDirectory=$JavaSdk" --nologo
    if ($LASTEXITCODE) { throw 'Sestavení Androidu selhalo.' }
    & (Join-Path $PSScriptRoot 'tools\Sign-Android.ps1') -AndroidSdk $AndroidSdk -JavaSdk $JavaSdk
}
Write-Output 'Hotovo: dist/windows/JirkaStore.exe'
