[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [string]$EditorPath,
    [switch]$KeepEditorOpen,
    [switch]$RetryFailedInstall,
    [switch]$UpgradePipeline,
    [int]$TimeoutSeconds = 300
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$OutputEncoding = [Text.UTF8Encoding]::new($false)
Add-Type -AssemblyName System.Net.Http
. (Join-Path $PSScriptRoot 'UnityContext.ps1')
$Context = Get-UnityToolContext -ProjectPath $ProjectPath -EditorPath $EditorPath
. (Join-Path $PSScriptRoot 'PipelineInstallation.ps1')
Install-UnityPipeline -Context $Context -KeepEditorOpen:$KeepEditorOpen -RetryFailedInstall:$RetryFailedInstall -UpgradePipeline:$UpgradePipeline -TimeoutSeconds $TimeoutSeconds
