param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$ServiceName = "EIMSNextAsync",
    [string]$DisplayName = "EIMSNext Async Service",
    [string]$PublishDir = "D:\Services\EIMSNext\AsyncHost",
    [switch]$SelfContained,
    [switch]$SkipPublish,
    [switch]$SkipStart
)

$ErrorActionPreference = "Stop"

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Invoke-Sc {
    param([string]$Arguments)

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = "sc.exe"
    $psi.Arguments = $Arguments
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false

    $process = [System.Diagnostics.Process]::Start($psi)
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()

    if ($stdout) { Write-Host $stdout.TrimEnd() }
    if ($stderr) { Write-Host $stderr.TrimEnd() }

    return $process.ExitCode
}

if (-not (Test-IsAdministrator)) {
    throw "Run this script as Administrator to install or start the Windows Service."
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectFile = Join-Path $scriptRoot "EIMSNext.Async.Host.csproj"

if (-not $SkipPublish) {
    New-Item -ItemType Directory -Path $PublishDir -Force | Out-Null

    $publishArgs = @(
        "publish",
        $projectFile,
        "-c", $Configuration,
        "-r", $Runtime,
        "--output", $PublishDir
    )

    if ($SelfContained) {
        $publishArgs += "--self-contained"
        $publishArgs += "true"
    }
    else {
        $publishArgs += "--self-contained"
        $publishArgs += "false"
    }

    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed."
    }
}

$exePath = Join-Path $PublishDir "EIMSNext.Async.Host.exe"
if (-not (Test-Path $exePath)) {
    throw "Published executable was not found: $exePath"
}

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($service) {
    if ($service.Status -ne [System.ServiceProcess.ServiceControllerStatus]::Stopped) {
        Stop-Service -Name $ServiceName -Force
        $service.WaitForStatus([System.ServiceProcess.ServiceControllerStatus]::Stopped, [TimeSpan]::FromSeconds(30))
    }

    $deleteExitCode = Invoke-Sc -Arguments ('delete "{0}"' -f $ServiceName)
    if ($deleteExitCode -ne 0) {
        throw "Failed to delete existing service: $ServiceName"
    }

    Start-Sleep -Seconds 2
}

$createExitCode = Invoke-Sc -Arguments ('create "{0}" binPath= "{1}" start= auto DisplayName= "{2}"' -f $ServiceName, $exePath, $DisplayName)
if ($createExitCode -ne 0) {
    throw "Failed to create service: $ServiceName"
}

Invoke-Sc -Arguments ('description "{0}" "{1}"' -f $ServiceName, 'EIMSNext async task host running as Windows Service.') | Out-Null

if (-not $SkipStart) {
    Start-Service -Name $ServiceName
    $service = Get-Service -Name $ServiceName
    $service.WaitForStatus([System.ServiceProcess.ServiceControllerStatus]::Running, [TimeSpan]::FromSeconds(30))
}

$service = Get-Service -Name $ServiceName
Write-Host ""
Write-Host "ServiceName : $($service.Name)"
Write-Host "Status      : $($service.Status)"
Write-Host "PublishDir   : $PublishDir"
Write-Host "Executable   : $exePath"
Write-Host "LogsDir      : $(Join-Path $PublishDir 'Logs')"
