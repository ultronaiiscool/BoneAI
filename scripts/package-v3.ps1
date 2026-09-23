param(
    [Parameter(Mandatory = $true)][string]$NativeLibraryPath,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [string]$Version = '3.0.1'
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$dll = Join-Path $repo 'bin/Release/net6.0/BoneAI.dll'
if (-not (Test-Path -LiteralPath $dll -PathType Leaf)) { throw 'Build BoneAI.dll in Release mode first.' }
$native = (Resolve-Path -LiteralPath $NativeLibraryPath).Path
if ([IO.Path]::GetFileName($native) -ne 'libcodex_app_server.so') { throw 'Expected libcodex_app_server.so.' }
$sourceDirectory = Split-Path -Parent $native
$licenseDirectory = Join-Path $sourceDirectory 'licenses'
foreach ($required in @('LICENSE', 'NOTICE', 'THIRD_PARTY_LICENSES.md')) {
    if (-not (Test-Path -LiteralPath (Join-Path $licenseDirectory $required) -PathType Leaf)) {
        throw "Native build license file missing: $required"
    }
}

$target = Join-Path $OutputDirectory "BoneAI-v$Version-Quest"
$mods = Join-Path $target 'Mods'
$userLibs = Join-Path $target 'UserLibs'
if (Test-Path -LiteralPath $target) { throw "Package directory already exists: $target" }
New-Item -ItemType Directory -Path $mods, $userLibs -Force | Out-Null
Copy-Item -LiteralPath $dll -Destination (Join-Path $mods 'BoneAI.dll')
Copy-Item -LiteralPath $native -Destination (Join-Path $userLibs 'libcodex_app_server.so')
$hash = (Get-FileHash -LiteralPath (Join-Path $userLibs 'libcodex_app_server.so') -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $userLibs 'libcodex_app_server.so.sha256'), "$hash  libcodex_app_server.so`n")
foreach ($name in @('README.md', 'QUEST-README.md', 'INSTALL-FIRST.md', 'CHANGELOG.md', 'RELEASE-v3.0.1.md', 'LICENSE', 'manifest.json', 'icon.png')) {
    Copy-Item -LiteralPath (Join-Path $repo $name) -Destination (Join-Path $target $name)
}
Copy-Item -LiteralPath (Join-Path $repo 'native/README.md') -Destination (Join-Path $target 'NATIVE-SOURCE.md')
Copy-Item -LiteralPath (Join-Path $repo 'docs/TESTING-v3.0.1.md') -Destination (Join-Path $target 'TESTING-v3.0.1.md')
Copy-Item -LiteralPath (Join-Path $repo 'native/codex-android-source-v3.zip') -Destination (Join-Path $target 'codex-android-source-v3.zip')

foreach ($name in @('NOTICE', 'THIRD_PARTY_LICENSES.txt', 'THIRD_PARTY_LICENSES.md', 'LICENSE')) {
    $source = Join-Path $licenseDirectory $name
    if (Test-Path -LiteralPath $source -PathType Leaf) {
        Copy-Item -LiteralPath $source -Destination (Join-Path $target "NATIVE-$name")
    }
}

$zip = Join-Path $OutputDirectory "BoneAI-v$Version-Quest.zip"
if (Test-Path -LiteralPath $zip) { throw "Package archive already exists: $zip" }
Compress-Archive -Path (Join-Path $target '*') -DestinationPath $zip -CompressionLevel Optimal
Get-FileHash -LiteralPath $zip -Algorithm SHA256 | ForEach-Object {
    [IO.File]::WriteAllText("$zip.sha256", "$($_.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($zip))`n")
}
Write-Output $zip
