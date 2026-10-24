[CmdletBinding()]
param(
    [string]$ProjectPath,
    [string]$EditorPath,
    [switch]$InstallOnly,
    [switch]$RetryFailedInstall,
    [switch]$UpgradePipeline,
    [string]$TracePath,
    [Parameter(ValueFromRemainingArguments = $true)][string[]]$UnityArgs
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$OutputEncoding = [Text.UTF8Encoding]::new($false)
$UnityArgs = @($UnityArgs | Where-Object { $null -ne $_ })
try {
Add-Type -AssemblyName System.Net.Http
. (Join-Path $PSScriptRoot 'UnityInvocation.ps1')
$ValidationClock=[Diagnostics.Stopwatch]::StartNew()
$Context = Get-UnityToolContext -ProjectPath $ProjectPath -EditorPath $EditorPath
Write-UnityTiming $TracePath 'context_validation' $ValidationClock.Elapsed.TotalMilliseconds '' 'confirmed'
$Invocation = Get-UnityInvocation -ArgumentList $UnityArgs
if (-not $Invocation.Help -and $Invocation.Verb -in @('open', 'close', 'run', 'build', 'test', 'recompile')) {
    throw 'Use scoped Pipeline commands through command for Editor operations; this wrapper does not forward native Editor launch or build shortcuts'
}
$UsesEditor = $InstallOnly -or (-not $Invocation.Help -and $Invocation.Verb -in @('command', 'list', 'job', 'mcp'))
if ($UsesEditor -and -not $ProjectPath) { throw '-ProjectPath is required for Editor operations' }
if (-not $Invocation.Help -and $Invocation.Verb -in @('self-update', 'self-uninstall')) { throw 'Bundled CLI is pinned; rebuild the bundle to update it' }
if (-not $Invocation.Help -and $Invocation.Verb -eq 'pipeline' -and $UnityArgs.Count -gt ($Invocation.Index + 1) -and $UnityArgs[$Invocation.Index + 1] -in @('install', 'upgrade')) { throw 'Use -InstallOnly to install the bundled compatible Pipeline' }
if ($UsesEditor) { . (Join-Path $PSScriptRoot 'PipelineInstallation.ps1') }
if ($InstallOnly) {
    Install-UnityPipeline -Context $Context -RetryFailedInstall:$RetryFailedInstall -UpgradePipeline:$UpgradePipeline
    return
}
if ($UsesEditor) {
    if ($UnityArgs -contains '--project-path' -or $UnityArgs -contains '--runtime' -or $UnityArgs -contains '--runtime-path' -or @($UnityArgs | Where-Object { $_ -match '^--(project-path|runtime|runtime-path)=' }).Count -gt 0) {
        throw 'Select the Editor with wrapper -ProjectPath; conflicting native target selectors are not accepted'
    }
    Install-UnityPipeline -Context $Context -KeepEditorOpen -RetryFailedInstall:$RetryFailedInstall -UpgradePipeline:$UpgradePipeline | Out-Null
    $UnityArgs[$Invocation.Index] = $Invocation.Verb
    $UnityArgs = @($UnityArgs | Select-Object -First ($Invocation.Index + 1)) + @('--project-path', $Context.ProjectPath) + @($UnityArgs | Select-Object -Skip ($Invocation.Index + 1))
    if ($Invocation.Verb -eq 'command') { $UnityArgs += @('--caller', 'plugin', '--skill', 'unity-true-cli') }
}
if (-not $UnityArgs) { $UnityArgs = @('--help') }
$NativeResult=Invoke-UnityNative -Context $Context -ArgumentList $UnityArgs -Streaming -TimeoutSeconds 0 -TracePath $TracePath -CommandName $Invocation.Verb
exit $NativeResult.exitCode
}
catch {
    $FormatIndex = [Array]::IndexOf($UnityArgs, '--format')
    $WantsJson = $InstallOnly -or $UnityArgs -contains '--json' -or $UnityArgs -contains '--format=json' -or ($FormatIndex -ge 0 -and $UnityArgs.Count -gt ($FormatIndex + 1) -and $UnityArgs[$FormatIndex + 1] -eq 'json')
    if ($WantsJson) {
        [Console]::Out.WriteLine(([ordered]@{success = $false; command = 'unity-true-cli'; errors = @(@{code = 'UNITY_TRUE_CLI_ERROR'; message = $_.Exception.Message})} | ConvertTo-Json -Depth 4 -Compress))
    }
    else { [Console]::Error.WriteLine($_.Exception.Message) }
    exit 1
}
