<#
.SYNOPSIS
    Builds, tests and packages PassManager.

.DESCRIPTION
    Produces in .\artifacts:
      PassManager-<version>-win-x64-portable.exe  Self-contained single-file executable (no install, no .NET needed)
      PassManager-<version>-win-x64-setup.exe     Installer (Inno Setup) - per-user by default, no admin rights needed
      PassManager-<version>-win-x64.msi           Windows Installer package for managed/enterprise deployment
      SHA256SUMS.txt                              Checksums for release verification

.PARAMETER SignCertificateThumbprint
    Optional. SHA-1 thumbprint of an Authenticode code-signing certificate in the current user's store.
    When given, the executable and both installers are signed (requires signtool from the Windows SDK).

.EXAMPLE
    .\build.ps1 -Version 1.2.0
#>
[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '1.0.0',
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64',
    [switch]$SkipTests,
    [switch]$SkipInstallers,
    [string]$SignCertificateThumbprint,
    [string]$TimestampUrl = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'
$publishDir = Join-Path $artifacts "publish\$Runtime"
$appProject = Join-Path $root 'src\PassManager.App\PassManager.App.csproj'
$testProject = Join-Path $root 'tests\PassManager.Tests\PassManager.Tests.csproj'
$wixProject = Join-Path $root 'installer\wix\PassManager.Installer.wixproj'
$innoScript = Join-Path $root 'installer\inno\PassManager.iss'

function Invoke-Step([string]$Title, [scriptblock]$Command) {
    Write-Host "`n==> $Title" -ForegroundColor Cyan
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "$Title failed (exit code $LASTEXITCODE)." }
}

function Find-Tool([string[]]$Candidates) {
    foreach ($candidate in $Candidates) {
        $match = Get-Item -Path $candidate -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1
        if ($match) { return $match.FullName }
    }
    return $null
}

function Invoke-Sign([string]$Path) {
    if (-not $SignCertificateThumbprint) { return }
    $signtool = Find-Tool @("${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe")
    if (-not $signtool) { throw 'signtool.exe not found. Install the Windows 10/11 SDK.' }
    Invoke-Step "Sign $(Split-Path $Path -Leaf)" {
        & $signtool sign /sha1 $SignCertificateThumbprint /fd SHA256 /tr $TimestampUrl /td SHA256 $Path
    }
}

# ---------------------------------------------------------------- prepare
if (Test-Path $artifacts) { Remove-Item $artifacts -Recurse -Force }
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null

if (-not (Test-Path (Join-Path $root 'assets\app.ico'))) {
    & (Join-Path $root 'tools\New-AppIcon.ps1')
}

# ---------------------------------------------------------------- test
if (-not $SkipTests) {
    Invoke-Step 'Run unit tests' { dotnet test --project $testProject -c $Configuration }
}

# ---------------------------------------------------------------- publish
Invoke-Step 'Publish self-contained single-file executable' {
    dotnet publish $appProject -c $Configuration -r $Runtime --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=None -p:DebugSymbols=false `
        -p:Version=$Version `
        -o $publishDir
}

$exe = Join-Path $publishDir 'PassManager.exe'
Invoke-Sign $exe
Copy-Item $exe (Join-Path $artifacts "PassManager-$Version-$Runtime-portable.exe")

# ---------------------------------------------------------------- installers
if (-not $SkipInstallers) {
    $iscc = Find-Tool @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe")

    if ($iscc) {
        Invoke-Step 'Build setup.exe (Inno Setup)' {
            & $iscc /Q "/DAppVersion=$Version" "/DSourceDir=$publishDir" "/DOutputDir=$artifacts" $innoScript
        }
        Invoke-Sign (Join-Path $artifacts "PassManager-$Version-$Runtime-setup.exe")
    }
    else {
        Write-Warning 'Inno Setup 6 not found - skipping setup.exe. Install it from https://jrsoftware.org/isinfo.php'
    }

    Invoke-Step 'Build MSI (WiX Toolset)' {
        dotnet build $wixProject -c $Configuration "-p:Version=$Version" "-p:AppPublishDir=$publishDir" -nologo
    }
    $msi = Get-ChildItem (Join-Path $root 'installer\wix\bin') -Recurse -Filter "PassManager-$Version-*.msi" |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $msi) { throw 'The MSI was not produced.' }
    $msiTarget = Join-Path $artifacts $msi.Name
    Copy-Item $msi.FullName $msiTarget
    Invoke-Sign $msiTarget
}

# ---------------------------------------------------------------- checksums
$sums = Get-ChildItem $artifacts -File | Sort-Object Name | ForEach-Object {
    '{0}  {1}' -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name
}
$sums | Set-Content -Path (Join-Path $artifacts 'SHA256SUMS.txt') -Encoding ASCII

Write-Host "`nBuild succeeded. Artifacts:" -ForegroundColor Green
Get-ChildItem $artifacts -File | Select-Object Name, @{ Name = 'Size (MB)'; Expression = { [math]::Round($_.Length / 1MB, 1) } } | Format-Table -AutoSize
