[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$packageRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$dataRoot = Join-Path $env:LOCALAPPDATA "PowerToysRunPluginManager"
$appTarget = Join-Path $dataRoot "App"
$transactionRoot = Join-Path $dataRoot "Transactions"
$stagingRoot = Join-Path $dataRoot "Staging"
$backupRoot = Join-Path $dataRoot "Backups"
$pluginTarget = Join-Path $env:LOCALAPPDATA "Microsoft\PowerToys\PowerToys Run\Plugins\PluginManager"
$transactionId = [guid]::NewGuid()
$transactionName = $transactionId.ToString("N")
$pluginStage = Join-Path $stagingRoot "$transactionName\PluginManager"

if (Get-Process "PowerToysRun.PluginManager" -ErrorAction SilentlyContinue) {
    throw "Close PowerToys Run Plugin Manager before updating it."
}

New-Item -ItemType Directory -Force -Path $appTarget, $transactionRoot, $pluginStage | Out-Null
Copy-Item -Path (Join-Path $packageRoot "App\*") -Destination $appTarget -Recurse -Force
Copy-Item -Path (Join-Path $packageRoot "Bootstrap\*") -Destination $pluginStage -Recurse -Force

$powerToysPath = Get-Process "PowerToys" -ErrorAction SilentlyContinue |
    Select-Object -First 1 -ExpandProperty Path
$kind = if (Test-Path $pluginTarget) { "update" } else { "install" }
$plan = @{
    id = $transactionId
    createdAt = [DateTimeOffset]::UtcNow.ToString("O")
    powerToysExecutablePath = $powerToysPath
    restartPowerToys = $true
    operations = @(
        @{
            kind = $kind
            pluginId = "9A68C4D267E24E88A20DF0E199C50E74"
            pluginName = "Plugin Manager"
            sourceDirectory = $pluginStage
            targetDirectory = $pluginTarget
            backupDirectory = Join-Path $backupRoot "$transactionName\PluginManager"
        }
    )
}

$planPath = Join-Path $transactionRoot "$transactionName.json"
$plan | ConvertTo-Json -Depth 6 | Set-Content -Path $planPath -Encoding UTF8
$updater = Join-Path $appTarget "PowerToysRun.PluginManager.Updater.exe"
$process = Start-Process -FilePath $updater -ArgumentList @("`"$planPath`"") -PassThru
$process.WaitForExit()
$exitCode = $process.ExitCode
$process.Dispose()
if ($exitCode -ne 0) {
    $resultPath = "$planPath.result.json"
    $detail = if (Test-Path $resultPath) {
        (Get-Content $resultPath -Raw | ConvertFrom-Json).error
    } else {
        "The updater rejected the transaction before producing a result file."
    }

    throw "The plugin transaction failed with exit code ${exitCode}: $detail"
}

Write-Host "Installed PowerToys Run Plugin Manager."
Write-Host "Open PowerToys Run and type: plugins"
