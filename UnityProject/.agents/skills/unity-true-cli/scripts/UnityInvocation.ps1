# Shared by single-command, task discovery and finite recipes. Windows PowerShell 5.1.
. (Join-Path $PSScriptRoot 'UnityContext.ps1')

function Write-UnityTiming {
    param([string]$Path,[string]$Phase,[double]$ElapsedMs,[string]$Command,[string]$State,[int]$ExitCode=0,[int]$OutputBytes=0)
    if (-not $Path) { return }
    $Row=[ordered]@{schemaVersion=1;timestampUtc=[DateTime]::UtcNow.ToString('o');phase=$Phase;elapsedMs=$ElapsedMs;command=$Command;state=$State;exitCode=$ExitCode;outputBytes=$OutputBytes}
    try {
        $Absolute=[IO.Path]::GetFullPath($Path)
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Absolute))|Out-Null
        [IO.File]::AppendAllText($Absolute,(($Row|ConvertTo-Json -Compress)+[Environment]::NewLine),[Text.UTF8Encoding]::new($false))
    }
    catch {[Console]::Error.WriteLine('UNITY_TIMING_WRITE_FAILED: timing metadata unavailable; original command result is retained')}
}

function Initialize-UnityConsumer {
    param([string]$ProjectPath,[string]$EditorPath,[switch]$DiscoverOnly,[switch]$RetryFailedInstall,[switch]$UpgradePipeline,[string]$TracePath)
    $Clock=[Diagnostics.Stopwatch]::StartNew()
    $Context=Get-UnityToolContext -ProjectPath $ProjectPath -EditorPath $EditorPath
    $Context['IdentityTracePath']=$TracePath
    Write-UnityTiming $TracePath 'context_validation' $Clock.Elapsed.TotalMilliseconds '' 'confirmed'
    if (-not $ProjectPath) { throw '-ProjectPath is required' }
    $Clock.Restart()
    if ($DiscoverOnly) {
        if (-not (Test-UnityPipelineReady -ProjectPath $Context.ProjectPath)) { throw 'DiscoverOnly requires an already ready Pipeline; no installation or Editor launch was started' }
    } else {
        . (Join-Path $PSScriptRoot 'PipelineInstallation.ps1')
        Install-UnityPipeline -Context $Context -KeepEditorOpen -RetryFailedInstall:$RetryFailedInstall -UpgradePipeline:$UpgradePipeline | Out-Null
    }
    Write-UnityTiming $TracePath 'prepare_service' $Clock.Elapsed.TotalMilliseconds '' 'confirmed'
    return $Context
}

function Add-UnityTargetArguments {
    param([hashtable]$Context,[string[]]$ArgumentList)
    $Invocation=Get-UnityInvocation -ArgumentList $ArgumentList
    if ($Invocation.Help -or $Invocation.Verb -notin @('command','list','job','mcp')) { throw 'Shared task calls require a scoped command/list/job/mcp invocation' }
    if (@($ArgumentList|Where-Object {$_ -match '^--(project-path|runtime|runtime-path|caller|skill)(=|$)'}).Count) { throw 'Task arguments must not override target or caller/skill; use the wrapper project' }
    $Result=@($ArgumentList)
    $Result[$Invocation.Index]=$Invocation.Verb
    $Result=@($Result|Select-Object -First ($Invocation.Index+1))+@('--project-path',$Context.ProjectPath)+@($Result|Select-Object -Skip ($Invocation.Index+1))
    # job target options belong to the subcommand; Commander accepts inherited project option here only for command/list.
    if ($Invocation.Verb -eq 'job') {
        $Result=@($ArgumentList)+@('--project-path',$Context.ProjectPath)
    }
    if ($Invocation.Verb -eq 'command') { $Result+=@('--caller','plugin','--skill','unity-true-cli') }
    return ,$Result
}

function Confirm-UnityCompatibleService {
    param([hashtable]$Context)
    $Expected = (Get-Content -LiteralPath (Join-Path $Context.SkillRoot 'assets/com.unity.pipeline/package.json') -Raw -Encoding UTF8 | ConvertFrom-Json).version
    $Arguments = Add-UnityTargetArguments -Context $Context -ArgumentList @('command','get_cli_extensions','--timeout','90','--json')
    $Reply = Invoke-UnityNative -Context $Context -ArgumentList $Arguments -TimeoutSeconds 100 -CommandName 'verify_compatible_service'
    if ($Reply.timedOut -or $Reply.exitCode -ne 0) { throw "Current Pipeline does not provide this bundle's extension entry. Skill installed; project is not ready for this release. Inspect the existing package before explicitly upgrading; the Editor was left running." }
    $Envelope = $Reply.stdout | ConvertFrom-Json
    if ((Get-UnityEnvelopeOutcome 'get_cli_extensions' $Envelope) -ne 'confirmed') { throw 'Current Pipeline did not confirm compatible capabilities; project preparation is incomplete.' }
    $Packages = @($Envelope.data.result.packages | Where-Object { $_.name -eq 'com.unity.pipeline' })
    if ($Packages.Count -ne 1 -or $Packages[0].version -cne $Expected) { throw "Loaded Pipeline version differs from required $Expected. Inspect the existing package and explicitly upgrade it; the Editor was left running." }
    $Arguments = Add-UnityTargetArguments -Context $Context -ArgumentList @('command','package_list','--scope','installed','--timeout','90','--json')
    $Reply = Invoke-UnityNative -Context $Context -ArgumentList $Arguments -TimeoutSeconds 100 -CommandName 'verify_loaded_package'
    if ($Reply.timedOut -or $Reply.exitCode -ne 0) { throw 'Could not verify the loaded Pipeline package source; project preparation is incomplete.' }
    $Envelope = $Reply.stdout | ConvertFrom-Json
    $Loaded = @($Envelope.data.result.packages | Where-Object { $_.name -eq 'com.unity.pipeline' })
    if ($Envelope.success -ne $true -or $Envelope.data.result.success -ne $true -or $Loaded.Count -ne 1 -or $Loaded[0].version -cne $Expected -or -not $Loaded[0].resolvedPath) { throw 'Loaded Pipeline package identity is missing or mismatched; project preparation is incomplete.' }
    return [ordered]@{version=$Loaded[0].version;resolvedPath=$Loaded[0].resolvedPath;source=$Loaded[0].source;capabilitiesVerified=$true}
}

function Invoke-UnityNative {
    param([hashtable]$Context,[string[]]$ArgumentList,[double]$TimeoutSeconds=120,[string]$TracePath,[string]$CommandName,[switch]$Streaming)
    if (($TimeoutSeconds -le 0 -and -not ($Streaming -and $TimeoutSeconds -eq 0)) -or $TimeoutSeconds -gt 86400) { throw 'TimeoutSeconds must be in (0,86400]; streaming compatibility calls may use 0' }
    $StartInfo=[Diagnostics.ProcessStartInfo]::new()
    $StartInfo.FileName=$Context.CliPath
    $StartInfo.UseShellExecute=$false
    $StartInfo.CreateNoWindow=-not $Streaming
    $StartInfo.RedirectStandardOutput=-not $Streaming
    $StartInfo.RedirectStandardError=-not $Streaming
    if (-not $Streaming) {
        $StartInfo.StandardOutputEncoding=[Text.UTF8Encoding]::new($false)
        $StartInfo.StandardErrorEncoding=[Text.UTF8Encoding]::new($false)
    }
    $StartInfo.Arguments=(@($ArgumentList|ForEach-Object {ConvertTo-UnityProcessArgument $_}) -join ' ')
    foreach($Name in @('UNITY_NO_UPDATE_CHECK','UNITY_NO_CONSENT_PROMPT','UNITY_NO_CRASH_REPORT')) {$StartInfo.EnvironmentVariables[$Name]='1'}
    $Clock=[Diagnostics.Stopwatch]::StartNew()
    $Process=[Diagnostics.Process]::Start($StartInfo)
    if ($Streaming) {
        # Preserve interactive/stdin/stdout consumers (notably MCP); never buffer a stdio server to EOF.
        if ($TimeoutSeconds -eq 0) {$Process.WaitForExit();$Finished=$true}
        else {$Finished=$Process.WaitForExit([int][Math]::Ceiling($TimeoutSeconds*1000))}
        if (-not $Finished) {try {$Process.Kill()} catch [InvalidOperationException] {}; $Process.WaitForExit()}
        $Result=[pscustomobject]@{exitCode=$Process.ExitCode;timedOut=(-not $Finished);pid=$Process.Id;wallMs=$Clock.Elapsed.TotalMilliseconds;stdout='';stderr=''}
        $Process.Dispose()
        Write-UnityTiming $TracePath 'native_cli' $Result.wallMs $CommandName $(if($Finished){'returned'}else{'result_unknown'}) $Result.exitCode -1
        return $Result
    }
    $OutTask=$Process.StandardOutput.ReadToEndAsync()
    $ErrTask=$Process.StandardError.ReadToEndAsync()
    $TimedOut=-not $Process.WaitForExit([int][Math]::Ceiling($TimeoutSeconds*1000))
    if ($TimedOut) {
        # Only the exact client created above. Its Editor command may continue; never resend automatically.
        try {$Process.Kill()} catch [InvalidOperationException] {}
        $Process.WaitForExit()
    }
    $Stdout=$OutTask.GetAwaiter().GetResult();$Stderr=$ErrTask.GetAwaiter().GetResult()
    $Result=[pscustomobject]@{exitCode=$Process.ExitCode;timedOut=$TimedOut;pid=$Process.Id;wallMs=$Clock.Elapsed.TotalMilliseconds;stdout=$Stdout;stderr=$Stderr}
    $Process.Dispose()
    Write-UnityTiming $TracePath 'native_cli' $Result.wallMs $CommandName $(if($TimedOut){'result_unknown'}elseif($Result.exitCode -eq 0){'returned'}else{'failed'}) $Result.exitCode ([Text.Encoding]::UTF8.GetByteCount($Stdout))
    return $Result
}

function Get-UnityProperty {
    param($Value,[string]$Name,$Default=$null)
    if ($null -eq $Value) { return $Default }
    $Property=$Value.PSObject.Properties[$Name]
    if ($null -eq $Property) { return $Default }
    return $Property.Value
}

function Get-UnityOutcome {
    param([string]$Command,$Value)
    if ($null -eq $Value) { return 'result_unknown' }
    if ($Command -in @('eval','eval_file')) {
        $Success=Get-UnityProperty $Value 'success'
        if ($Success -eq $false) {return 'failed'}
        if ($Success -ne $true) {return 'result_unknown'}
    }
    if ($Command -eq 'batch') {
        if ((Get-UnityProperty $Value 'reverted' $false) -or (Get-UnityProperty $Value 'valid') -eq $false -or (Get-UnityProperty $Value 'error')) {return 'failed'}
        $Rows=@(Get-UnityProperty $Value 'results' @())
        if (-not $Rows.Count) {return 'result_unknown'}
        foreach($Row in $Rows) {
            if ((Get-UnityProperty $Row 'success') -ne $true -or (Get-UnityProperty $Row 'skipped' $false)) {return 'partial'}
        }
    }
    if ($Command -in @('wait_for','wait_status')) {
        if (Get-UnityProperty $Value 'onMetError') {return 'failed'}
        if ((Get-UnityProperty $Value 'met') -eq $true) {return 'confirmed'}
        $State=Get-UnityProperty $Value 'state'
        if ($State -in @('waiting','pending','running')) {return 'in_flight'}
        if ($State -in @('timedOut','failed','error')) {return 'failed'}
        return 'result_unknown'
    }
    return 'confirmed'
}

function Get-UnityEnvelopeOutcome {
    param([string]$Command,$Envelope)
    if ((Get-UnityProperty $Envelope 'success') -eq $false) { return 'failed' }
    if ((Get-UnityProperty $Envelope 'success') -ne $true) { return 'result_unknown' }
    $Data=Get-UnityProperty $Envelope 'data'
    if (-not $Data) { return 'result_unknown' }
    return Get-UnityOutcome -Command $Command -Value (Get-UnityProperty $Data 'result')
}

function Get-UnityServiceIdentity {
    param([hashtable]$Context)
    $Path=Join-Path $Context.ProjectPath 'Library/Pipeline/.unity-pipeline-port'
    $Descriptor=Get-Content -LiteralPath $Path -Raw -Encoding UTF8|ConvertFrom-Json
    if ([IO.Path]::GetFullPath($Descriptor.projectPath).TrimEnd('\','/') -ine $Context.ProjectPath.TrimEnd('\','/')) {throw 'Service belongs to another project'}
    $IdentityClock=[Diagnostics.Stopwatch]::StartNew()
    $Cached=$Context['ValidatedEditorLife']
    if(-not $Cached) {
        # Once per prepared consumer: inspect the real command line, not a descriptor assertion.
        $Editor=Get-UnityProjectProcess -ProjectPath $Context.ProjectPath
        if (-not $Editor -or $Editor.ProcessId -ne $Descriptor.pid -or $Editor.ExecutablePath -ine $Context.EditorPath) {throw 'Service process does not match the exact project Editor'}
    } elseif($Cached.pid -ne $Descriptor.pid){throw 'Service Editor PID changed; prepare a new consumer'}
    $Process=[Diagnostics.Process]::GetProcessById([int]$Descriptor.pid)
    try {
        $Created=$Process.StartTime.ToUniversalTime()
        $Executable=$Process.MainModule.FileName
        # WMI creation dates have microsecond precision; bind its command-line observation to this life.
        if(-not $Cached -and [Math]::Abs(($Created-$Editor.CreationDate.ToUniversalTime()).Ticks) -gt 10){throw 'Editor changed between command-line and lifetime observations'}
        if($Process.HasExited -or $Executable -ine $Context.EditorPath){throw 'Exact Editor is no longer running'}
        if($Cached -and ($Cached.startTicks -ne $Created.Ticks -or $Cached.executable -ine $Executable)){throw 'Editor process lifetime changed; stale PID is not reusable'}
        if(-not $Cached){$Context['ValidatedEditorLife']=@{pid=[int]$Descriptor.pid;startTicks=$Created.Ticks;executable=$Executable}}
    } finally {$Process.Dispose()}
    if (-not $Descriptor.startedAt) {throw 'Service descriptor has no lifecycle identity'}
    $Material=(@($Context.ProjectPath,$Context.EditorPath,$Descriptor.pid,$Created.ToString('o'),$Descriptor.port,$Descriptor.startedAt,$Descriptor.evalToken) -join '|')
    $Hasher=[Security.Cryptography.SHA256]::Create()
    try {$Fingerprint=[BitConverter]::ToString($Hasher.ComputeHash([Text.Encoding]::UTF8.GetBytes($Material))).Replace('-','').ToLowerInvariant()} finally {$Hasher.Dispose()}
    Write-UnityTiming $Context['IdentityTracePath'] 'service_identity' $IdentityClock.Elapsed.TotalMilliseconds '' 'confirmed'
    return [pscustomobject]@{fingerprint=$Fingerprint;readAtUtc=[DateTime]::UtcNow.ToString('o');pid=$Descriptor.pid;createdAt=$Created.ToString('o');serviceStartedAt=$Descriptor.startedAt;project=$Context.ProjectPath;editor=$Context.EditorPath}
}

function Write-UnityJsonFile {
    param([string]$Path,$Value)
    $Temporary=$Path+'.'+[Guid]::NewGuid().ToString('N')+'.tmp'
    $Backup=$Temporary+'.previous'
    try {
        [IO.File]::WriteAllText($Temporary,(ConvertTo-Json -InputObject $Value -Depth 64),[Text.UTF8Encoding]::new($false))
        # PS 5.1 coerces a null String argument to ""; use an owned backup path for File.Replace.
        if([IO.File]::Exists($Path)){[IO.File]::Replace($Temporary,$Path,$Backup)}else{[IO.File]::Move($Temporary,$Path)}
    }
    finally {if([IO.File]::Exists($Temporary)){[IO.File]::Delete($Temporary)};if([IO.File]::Exists($Backup)){[IO.File]::Delete($Backup)}}
}
