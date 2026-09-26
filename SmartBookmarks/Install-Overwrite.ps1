#Requires -Version 5.1
<#
.SYNOPSIS
    Rebuild Smart Bookmarks and overwrite-install it into daily Visual Studio 2022.

.DESCRIPTION
    Same extension Id + same Version is rejected by VSIXInstaller
    ("this extension is already installed to all applicable products").
    This script does not uninstall first. If the source manifest version is
    already higher than the installed version, it rebuilds that version and
    installs over the old one. Incremental MSBuild can leave an old packed
    .vsix, so the rebuild is always a full Rebuild. Per-solution
    bookmarks.json is kept.

.PARAMETER Configuration
    MSBuild configuration. Default: Debug.

.PARAMETER BumpVersion
    Always bump the patch before rebuild, even if the packed version is already higher.
#>
[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [switch]$BumpVersion
)

$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Sln = Join-Path $Root "SmartBookmarks.sln"
$Manifest = Join-Path $Root "src\SmartBookmarks\source.extension.vsixmanifest"
$VsixCs = Join-Path $Root "src\SmartBookmarks\Vsix.cs"
$Vsix = Join-Path $Root "src\SmartBookmarks\bin\$Configuration\SmartBookmarks.vsix"
$ExtensionId = "SmartBookmarks.3263A3AD-B8A3-42F7-A0B9-224FFF2F9479"

function Get-ManifestVersion {
    param([string]$Path)
    [xml]$xml = Get-Content -LiteralPath $Path -Encoding UTF8
    $nsmgr = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
    $nsmgr.AddNamespace("v", "http://schemas.microsoft.com/developer/vsx-schema/2011")
    $identity = $xml.SelectSingleNode("//v:Identity", $nsmgr)
    if (-not $identity) {
        throw "Identity node not found in $Path"
    }
    return [string]$identity.Version
}

function Set-ManifestVersion {
    param([string]$Path, [string]$Version)
    $raw = [System.IO.File]::ReadAllText($Path)
    $updated = [regex]::Replace(
        $raw,
        '(<Identity\b[^>]*\bVersion=")([^"]+)(")',
        { param($m) $m.Groups[1].Value + $Version + $m.Groups[3].Value },
        1)
    if ($updated -eq $raw) {
        throw "Failed to rewrite Version in $Path"
    }
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($Path, $updated, $utf8NoBom)
}

function Set-VsixCsVersion {
    param([string]$Path, [string]$Version)
    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }
    $raw = [System.IO.File]::ReadAllText($Path)
    $updated = [regex]::Replace(
        $raw,
        '(public const string Version = ")([^"]+)(";)',
        { param($m) $m.Groups[1].Value + $Version + $m.Groups[3].Value },
        1)
    if ($updated -eq $raw) {
        Write-Host "Vsix.cs Version already $Version"
        return
    }
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($Path, $updated, $utf8NoBom)
}

function Get-VsixPackedVersion {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $entry = $zip.Entries | Where-Object { $_.FullName -eq "extension.vsixmanifest" } | Select-Object -First 1
        if (-not $entry) {
            return $null
        }
        $sr = New-Object System.IO.StreamReader($entry.Open())
        try {
            $text = $sr.ReadToEnd()
        }
        finally {
            $sr.Close()
        }
    }
    finally {
        $zip.Dispose()
    }
    $m = [regex]::Match($text, 'Identity\b[^>]*\bVersion="([^"]+)"')
    if (-not $m.Success) {
        return $null
    }
    return $m.Groups[1].Value
}

function Get-InstalledVersion {
    param([string]$Id)
    $root = Join-Path $env:LOCALAPPDATA "Microsoft\VisualStudio"
    if (-not (Test-Path -LiteralPath $root)) {
        return $null
    }
    $found = $null
    Get-ChildItem -LiteralPath $root -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like "17.0_*" -and $_.Name -notlike "*Exp" } |
        ForEach-Object {
            $extRoot = Join-Path $_.FullName "Extensions"
            if (-not (Test-Path -LiteralPath $extRoot)) {
                return
            }
            Get-ChildItem -LiteralPath $extRoot -Recurse -Filter "extension.vsixmanifest" -ErrorAction SilentlyContinue |
                ForEach-Object {
                    $c = Get-Content -LiteralPath $_.FullName -Raw -ErrorAction SilentlyContinue
                    if ($c -and $c.Contains($Id)) {
                        $m = [regex]::Match($c, 'Identity\b[^>]*\bVersion="([^"]+)"')
                        if ($m.Success) {
                            $found = $m.Groups[1].Value
                        }
                    }
                }
        }
    return $found
}

function ConvertTo-Version {
    param([string]$Text)
    if ([string]::IsNullOrWhiteSpace($Text)) {
        return $null
    }
    $v = $null
    if ([Version]::TryParse($Text, [ref]$v)) {
        return $v
    }
    return $null
}

function Get-NextPatchVersion {
    param([string]$Current)
    $v = ConvertTo-Version $Current
    if (-not $v) {
        throw "Cannot parse version '$Current'"
    }
    $build = if ($v.Build -lt 0) { 0 } else { $v.Build }
    $rev = if ($v.Revision -lt 0) { -1 } else { $v.Revision }
    if ($rev -ge 0) {
        return ("{0}.{1}.{2}.{3}" -f $v.Major, $v.Minor, $build, ($rev + 1))
    }
    return ("{0}.{1}.{2}" -f $v.Major, $v.Minor, ($build + 1))
}

function Test-VersionGreater {
    param([string]$Left, [string]$Right)
    $lv = ConvertTo-Version $Left
    $rv = ConvertTo-Version $Right
    if (-not $lv -or -not $rv) {
        return $false
    }
    return ($lv -gt $rv)
}

function Find-MsBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path -LiteralPath $vswhere) {
        $msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" |
            Select-Object -First 1
        if ($msbuild -and (Test-Path -LiteralPath $msbuild)) {
            return $msbuild
        }
    }
    $fallback = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
    if (Test-Path -LiteralPath $fallback) {
        return $fallback
    }
    throw "MSBuild.exe not found. Install Visual Studio 2022 with the Visual Studio extension development workload."
}

function Wait-VsixInstallerExit {
    param([int]$TimeoutSeconds = 180)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $busy = @(Get-Process -Name VSIXInstaller -ErrorAction SilentlyContinue)
        if ($busy.Count -eq 0) {
            return
        }
        Start-Sleep -Milliseconds 400
    }
    $ids = ((Get-Process -Name VSIXInstaller -ErrorAction SilentlyContinue) | ForEach-Object { $_.Id }) -join ", "
    throw "VSIXInstaller is still running after $TimeoutSeconds seconds (PID: $ids)."
}

function Get-LatestInstallerLog {
    Get-ChildItem -LiteralPath $env:TEMP -Filter "dd_VSIXInstaller_*.log" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
}

function Wait-InstallerLog {
    param([datetime]$StartedAfter, [int]$TimeoutSeconds = 60)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $log = Get-LatestInstallerLog
        if ($log -and $log.LastWriteTime -ge $StartedAfter.AddSeconds(-2) -and $log.Length -gt 0) {
            return $log
        }
        Start-Sleep -Milliseconds 300
    }
    return $null
}

function Assert-InstallerSucceeded {
    param([datetime]$StartedAfter, [string]$ExpectedVersion)
    Wait-VsixInstallerExit
    $log = Wait-InstallerLog -StartedAfter $StartedAfter
    if (-not $log) {
        throw "VSIXInstaller produced no new log under %TEMP%. Install did not run."
    }
    $text = Get-Content -LiteralPath $log.FullName -Raw
    if ($text -match "InvalidCommandLineException") {
        throw "VSIXInstaller rejected the command line. See $($log.FullName)"
    }
    if ($text -match "AlreadyInstalledException") {
        throw "VSIXInstaller refused because the same version is already installed. See $($log.FullName)"
    }
    if ($text -notmatch [regex]::Escape($ExpectedVersion)) {
        throw "Installer log does not mention packed version $ExpectedVersion. See $($log.FullName)"
    }
    Write-Host "Installer log OK: $($log.Name)"
}

function Wait-ManualClose {
    # 右键“使用 PowerShell 运行”没有 -NoExit，进程结束窗口就消失。
    # 从已有终端调用时父进程不是 explorer，输出本来就留在那个窗口里，不必再等。
    $ownWindow = $true
    try {
        $parentId = (Get-CimInstance Win32_Process -Filter "ProcessId=$PID").ParentProcessId
        $parentName = (Get-Process -Id $parentId -ErrorAction SilentlyContinue).ProcessName
        if ($parentName -and $parentName -ne "explorer") {
            $ownWindow = $false
        }
    }
    catch {
        $ownWindow = $true
    }

    if ($ownWindow) {
        Write-Host ""
        try {
            Read-Host "按回车关闭"
        }
        catch {
        }
    }
}

trap {
    Write-Host ""
    Write-Host "FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Wait-ManualClose
    exit 1
}

function Find-VsixInstaller {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path -LiteralPath $vswhere) {
        $installer = & $vswhere -latest -property productPath
        if ($installer) {
            $candidate = Join-Path (Split-Path -Parent $installer) "VSIXInstaller.exe"
            if (Test-Path -LiteralPath $candidate) {
                return $candidate
            }
        }
    }
    $fallback = "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\VSIXInstaller.exe"
    if (Test-Path -LiteralPath $fallback) {
        return $fallback
    }
    throw "VSIXInstaller.exe not found."
}

if (-not (Test-Path -LiteralPath $Sln)) {
    throw "Solution not found: $Sln"
}
if (-not (Test-Path -LiteralPath $Manifest)) {
    throw "Manifest not found: $Manifest"
}

$busy = @(Get-Process -Name devenv -ErrorAction SilentlyContinue)
if ($busy.Count -gt 0) {
    $ids = ($busy | ForEach-Object { $_.Id }) -join ", "
    throw "Visual Studio is still running (devenv.exe PID: $ids). Close all VS windows, including F5 Exp instances, then rerun."
}

$sourceVersion = Get-ManifestVersion $Manifest
$packedVersion = Get-VsixPackedVersion $Vsix
$installedVersion = Get-InstalledVersion $ExtensionId

Write-Host "Source manifest : $sourceVersion  ($Manifest)"
Write-Host "Packed .vsix    : $(if ($packedVersion) { $packedVersion } else { '(none yet)' })"
Write-Host "Installed VS2022: $(if ($installedVersion) { $installedVersion } else { '(not installed)' })"

$needBump = [bool]$BumpVersion
if (-not $needBump -and $installedVersion -and -not (Test-VersionGreater $sourceVersion $installedVersion)) {
    $needBump = $true
}

if ($needBump) {
    $baseline = $sourceVersion
    if ($installedVersion -and -not (Test-VersionGreater $baseline $installedVersion)) {
        $baseline = $installedVersion
    }
    $next = Get-NextPatchVersion $baseline
    Write-Host "Bumping Version $sourceVersion -> $next (overwrite install requires a higher version; no uninstall)."
    Set-ManifestVersion -Path $Manifest -Version $next
    $sourceVersion = $next
}
else {
    Write-Host "Source version $sourceVersion is already higher than installed $installedVersion; keeping it and forcing a Rebuild so the packed .vsix picks it up."
}

Set-VsixCsVersion -Path $VsixCs -Version $sourceVersion

$msbuild = Find-MsBuild
Write-Host "Rebuilding with $msbuild ..."
& $msbuild $Sln /t:Rebuild /p:Configuration=$Configuration /restore /nologo /v:m
if ($LASTEXITCODE -ne 0) {
    throw "MSBuild failed with exit code $LASTEXITCODE"
}

if (-not (Test-Path -LiteralPath $Vsix)) {
    throw "VSIX not produced: $Vsix"
}

$packedVersion = Get-VsixPackedVersion $Vsix
Write-Host "Packed .vsix version: $packedVersion"
if ($installedVersion -and -not (Test-VersionGreater $packedVersion $installedVersion)) {
    throw "Packed version $packedVersion is not higher than installed $installedVersion. VSIXInstaller would still refuse. Check $Manifest."
}

$installer = Find-VsixInstaller
$started = Get-Date
Write-Host "Installing $Vsix with $installer ..."
& $installer /quiet /norepair $Vsix
$installExit = $LASTEXITCODE
if ($null -ne $installExit -and $installExit -ne 0) {
    throw "VSIXInstaller failed with exit code $installExit"
}
Assert-InstallerSucceeded -StartedAfter $started -ExpectedVersion $packedVersion

$after = Get-InstalledVersion $ExtensionId
Write-Host "Installed version now: $(if ($after) { $after } else { '(unknown)' })"
if ($after -and $after -ne $packedVersion) {
    throw "Install finished but VS still reports $after, not packed $packedVersion."
}

Write-Host "Installed $packedVersion over the previous version. Reopen Visual Studio 2022; per-solution bookmarks.json is kept."
Wait-ManualClose
