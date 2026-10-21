param([Parameter(Mandatory=$true)][string]$AndroidSdk,[Parameter(Mandatory=$true)][string]$JavaSdk)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$signing = Join-Path $projectRoot 'local\signing'
New-Item -ItemType Directory -Path $signing -Force | Out-Null
$keystore = Join-Path $signing 'jirka-store.keystore'
$password = Join-Path $signing 'password.txt'
if (!(Test-Path -LiteralPath $keystore)) {
    if (Test-Path -LiteralPath $password) { throw 'Existuje heslo, ale chybí klíč. Obnovte podpisový klíč ze zálohy; nevytvářejte novou identitu pro existující instalace.' }
    $bytes = New-Object byte[] 32
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create(); $rng.GetBytes($bytes); $rng.Dispose()
    [IO.File]::WriteAllText($password,[Convert]::ToBase64String($bytes),[Text.UTF8Encoding]::new($false))
    & (Join-Path $JavaSdk 'bin\keytool.exe') -genkeypair -keystore $keystore -alias 'jirka-store' -keyalg RSA -keysize 3072 -validity 10000 -dname 'CN=Jirka Store' -storepass:file $password -keypass:file $password
    if ($LASTEXITCODE) { throw 'Vytvoření podpisového klíče selhalo.' }
}
if (!(Test-Path -LiteralPath $password)) { throw 'Chybí heslo k podpisovému klíči.' }
$buildTools = Get-ChildItem -LiteralPath (Join-Path $AndroidSdk 'build-tools') -Directory | Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'apksigner.bat') } | Sort-Object Name -Descending | Select-Object -First 1
if (!$buildTools) { throw 'Chybí Android build-tools s apksigner.' }
$output = Join-Path $projectRoot 'dist\android'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$oldJava = $env:JAVA_HOME
try {
    $env:JAVA_HOME = $JavaSdk
    & (Join-Path $buildTools.FullName 'apksigner.bat') sign --ks $keystore --ks-key-alias 'jirka-store' --ks-pass "file:$password" --out (Join-Path $output 'JirkaStore.apk') (Join-Path $projectRoot 'src\Store.Android\bin\Release\net8.0-android\cz.jirkas.store.apk')
    if ($LASTEXITCODE) { throw 'Podepsání APK selhalo.' }
    & (Join-Path $buildTools.FullName 'apksigner.bat') verify --print-certs (Join-Path $output 'JirkaStore.apk')
    if ($LASTEXITCODE) { throw 'Ověření podpisu APK selhalo.' }
} finally { $env:JAVA_HOME = $oldJava }
Write-Output 'APK: dist/android/JirkaStore.apk. Zálohujte local/signing mimo Git; další aktualizace musí použít stejný klíč.'
