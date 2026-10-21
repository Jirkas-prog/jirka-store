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
if (Test-Path -LiteralPath 'local\toolchain\cargo\bin\cargo.exe') {
    $env:CARGO_HOME = Join-Path $PSScriptRoot 'local\toolchain\cargo'
    $env:RUSTUP_HOME = Join-Path $PSScriptRoot 'local\toolchain\rustup'
    $env:PATH = "$env:CARGO_HOME\bin;$env:PATH"
}
cargo build --manifest-path 'launcher\Cargo.toml' -p jirka-launcher --release --locked
if ($LASTEXITCODE) { throw 'Sestavení Rust launcheru selhalo.' }
New-Item -ItemType Directory -Path 'dist\windows\catalog','dist\windows\licenses' -Force | Out-Null
Copy-Item -LiteralPath 'launcher\target\release\JirkaStore.exe' -Destination 'dist\windows\JirkaStore.exe'
Copy-Item -LiteralPath 'catalog\catalog.json' -Destination 'dist\windows\catalog\catalog.json'
dotnet publish 'src\Store.Host\Store.Host.csproj' -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o 'dist\windows\engine' --nologo
if ($LASTEXITCODE) { throw 'Sestavení instalační služby selhalo.' }
Copy-Item -LiteralPath 'launcher\LICENSE-MIT','launcher\LICENSE-APACHE','launcher\NOTICE','launcher\ATTRIBUTION.md','launcher\FORK.md' -Destination 'dist\windows\licenses'
Get-ChildItem -LiteralPath 'launcher\assets\fonts' -Filter 'OFL-*.txt' | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination 'dist\windows\licenses' }
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
