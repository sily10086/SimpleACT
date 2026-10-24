[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ProjectPath,
    [string]$EditorPath,
    [Parameter(Mandatory=$true)][string]$RecipePath,
    [Parameter(Mandatory=$true)][string]$StateDirectory,
    [string]$InputsPath,
    [switch]$Resume,
    [switch]$ValidateOnly,
    [switch]$DiscoverOnly,
    [string]$TracePath,
    [ValidateRange(1,3600)][int]$TimeoutSeconds=120
)
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$OutputEncoding=[Text.UTF8Encoding]::new($false)
$Clock=[Diagnostics.Stopwatch]::StartNew();$RunState='failed';$Lock=$null;$State=$null;$StateFile=$null
try {
    Add-Type -AssemblyName System.Net.Http
    . (Join-Path $PSScriptRoot 'UnityInvocation.ps1')

    function Get-ValueAtPath($Value,[string]$Path) {
        $Current=$Value
        foreach($Part in $Path.Split('.')) {
            if($Part -match '^(\w+)\[(\d+)\]$') {$Current=Get-UnityProperty $Current $Matches[1];$Index=[int]$Matches[2];if($null -eq $Current -or @($Current).Count -le $Index){throw 'Assertion array path is missing'};$Current=@($Current)[$Index]}
            else {if($null -eq $Current -or $null -eq $Current.PSObject.Properties[$Part]){throw 'Assertion property path is missing'};$Current=Get-UnityProperty $Current $Part}
        }
        return $Current
    }
    function Assert-RecipeResult($Value,$Assertions) {
        if($null -eq $Assertions){return}
        foreach($Assertion in @($Assertions)) {
            $Actual=Get-ValueAtPath $Value $Assertion.path
            $Expected=Get-UnityProperty $Assertion 'value'
            switch($Assertion.op) {
                'equals' {if(($Actual|ConvertTo-Json -Compress -Depth 32) -cne ($Expected|ConvertTo-Json -Compress -Depth 32)){throw 'Current result differs from asserted value'}}
                'contains' {if(($Actual|ConvertTo-Json -Compress -Depth 32).IndexOf([string]$Expected,[StringComparison]::Ordinal) -lt 0){throw 'Current result does not contain asserted value'}}
                'exists' {if($null -eq $Actual){throw 'Current result has no asserted value'}}
                default {throw 'Unsupported assertion op; use equals/contains/exists'}
            }
        }
    }
    function Parameters-ToArguments($Parameters) {
        $Result=[Collections.Generic.List[string]]::new()
        if($null -eq $Parameters){return $Result.ToArray()}
        foreach($Property in $Parameters.PSObject.Properties) {
            if($Property.Name -notmatch '^[A-Za-z][A-Za-z0-9_]*$' -or $Property.Name -in @('project_path','runtime','runtime_path','caller','skill','detach','json','result_only')){throw 'Recipe parameter conflicts with controlled invocation options'}
            $Result.Add('--'+$Property.Name)
            $Value=$Property.Value
            if($null -eq $Value){$Result.Add('null')}
            elseif($Value -is [bool]){$Result.Add($Value.ToString().ToLowerInvariant())}
            elseif($Value -is [string]){$Result.Add($Value)}
            elseif($Value -is [ValueType]){$Result.Add([Convert]::ToString($Value,[Globalization.CultureInfo]::InvariantCulture))}
            else {$Result.Add((ConvertTo-Json -InputObject $Value -Depth 64 -Compress))}
        }
        return $Result.ToArray()
    }
    function Read-NativeEnvelope($Native) {
        if($Native.timedOut){return [pscustomobject]@{state='result_unknown';envelope=$null}}
        try {$Envelope=$Native.stdout|ConvertFrom-Json} catch {return [pscustomobject]@{state='result_unknown';envelope=$null}}
        $Outcome=if($Native.exitCode -ne 0){'failed'}else{'returned'}
        return [pscustomobject]@{state=$Outcome;envelope=$Envelope}
    }
    function Save-State {Write-UnityJsonFile -Path $StateFile -Value $State}
    function Confirm-ReturnedResult($Step,$Row) {
        $Outcome=Get-UnityOutcome $Step.command $Row.result
        if($Outcome -ne 'confirmed'){$Row.state=$Outcome;Save-State;throw 'Command returned without confirming the requested business outcome'}
        $Row.state='awaiting_assertion';Save-State
        try {Assert-RecipeResult $Row.result (Get-UnityProperty $Step 'assert' @())} catch {$Row.state='partial';Save-State;throw}
        $Row.state='confirmed';$Row.endedAtUtc=[DateTime]::UtcNow.ToString('o');Save-State
    }
    function Receive-OriginalWait($Step,$Row) {
        $WaitClock=[Diagnostics.Stopwatch]::StartNew()
        while($WaitClock.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
            Assert-Identity
            $Remaining=$TimeoutSeconds-$WaitClock.Elapsed.TotalSeconds
            if($Remaining -le 0){break}
            $Arguments=Add-UnityTargetArguments $Context @('command','wait_status','--wait_id',$Row.waitId,'--json')
            $Native=Invoke-UnityNative -Context $Context -ArgumentList $Arguments -TimeoutSeconds ([Math]::Min(5,$Remaining)) -TracePath $TracePath -CommandName 'wait_status'
            $Parsed=Read-NativeEnvelope $Native
            if($Parsed.state -ne 'returned' -or (Get-UnityProperty $Parsed.envelope 'success') -ne $true){$Row.state='result_unknown';Save-State;throw 'Original wait is unavailable; no wait or business action was replayed'}
            $Row.result=Get-UnityProperty (Get-UnityProperty $Parsed.envelope 'data') 'result'
            $Outcome=Get-UnityOutcome 'wait_status' $Row.result
            if($Outcome -eq 'confirmed'){Confirm-ReturnedResult $Step $Row;return}
            if($Outcome -ne 'in_flight'){$Row.state=$Outcome;Save-State;throw 'Original wait did not confirm its condition and requested follow-up'}
            $Row.state='in_flight';Save-State
            $Remaining=$TimeoutSeconds-$WaitClock.Elapsed.TotalSeconds
            if($Remaining -gt 0){Start-Sleep -Milliseconds ([int][Math]::Min(500,$Remaining*1000))}
        }
        $Row.state='in_flight';Save-State;throw 'Original async wait remains pending; resume queries the same waitId without replay'
    }
    function Assert-Identity {
        $Now=Get-UnityServiceIdentity -Context $Context
        if($Now.fingerprint -cne $Identity.fingerprint){throw 'Service lifecycle changed; previous schema/handles invalid. No step was replayed'}
        if((Get-UnityFileHash -Path (Join-Path $Context.SkillRoot 'bundle-manifest.json')) -cne $State.toolHash){throw 'Tool bundle changed during recipe; no further steps submitted'}
        if((Get-UnityFileHash -Path (Join-Path $Context.PackagePath 'package.json')) -cne $State.packageManifestHash){throw 'Project package identity changed during recipe; prepare again'}
    }
    function Execute-Step($Step,$Row,[bool]$Recover) {
        Assert-Identity
        if($Recover -and $Row.state -eq 'awaiting_assertion'){Confirm-ReturnedResult $Step $Row;return}
        if($Recover -and (Get-UnityProperty $Row 'waitId')){Receive-OriginalWait $Step $Row;return}
        $Existing=Get-UnityProperty $Row 'jobId'
        if($Recover -and -not $Existing){throw 'Unknown write has no recorded job; inspect its original consumer manually, do not replay'}
        if(-not $Recover) {
            $Row.state='started';$Row.startedAtUtc=[DateTime]::UtcNow.ToString('o');Save-State
            $Arguments=@('command',$Step.command)+@(Parameters-ToArguments (Get-UnityProperty $Step 'parameters'))
            if(Get-UnityProperty $Step 'detach' $false){$Arguments+=@('--detach')}
            $Arguments+=@('--timeout',[string]$TimeoutSeconds,'--json')
            $Native=Invoke-UnityNative -Context $Context -ArgumentList (Add-UnityTargetArguments $Context $Arguments) -TimeoutSeconds ($TimeoutSeconds+5) -TracePath $TracePath -CommandName $Step.command
            [IO.File]::WriteAllText((Join-Path $StateDirectory ($Step.id+'.stdout.json')),$Native.stdout,[Text.UTF8Encoding]::new($false))
            [IO.File]::WriteAllText((Join-Path $StateDirectory ($Step.id+'.stderr.txt')),$Native.stderr,[Text.UTF8Encoding]::new($false))
            $Row.wallMs=$Native.wallMs;$Row.clientPid=$Native.pid;$Row.exitCode=$Native.exitCode
            $Parsed=Read-NativeEnvelope $Native
            if($Parsed.state -ne 'returned'){$Row.state=$Parsed.state;Save-State;throw 'CLI did not return a confirmed envelope; inspect saved result before any retry'}
            $Envelope=$Parsed.envelope
            if(Get-UnityProperty $Step 'detach' $false) {
                $Data=Get-UnityProperty $Envelope 'data'
                $Job=Get-UnityProperty $Data 'jobId'
                if((Get-UnityProperty $Envelope 'success') -ne $true -or -not $Job){$Row.state='result_unknown';Save-State;throw 'No actual job identity returned; no replay is allowed'}
                $Row.jobId=$Job;$Row.state='in_flight';Save-State
            } else {
                $Row.result=Get-UnityProperty (Get-UnityProperty $Envelope 'data') 'result'
                $Outcome=Get-UnityEnvelopeOutcome $Step.command $Envelope
                if($Step.command -eq 'wait_for' -and $Outcome -eq 'in_flight') {
                    $Row.state='in_flight'
                    $Row.waitId=Get-UnityProperty $Row.result 'waitId'
                    if(-not $Row.waitId){$Row.state='result_unknown';Save-State;throw 'Async wait returned no identity'}
                    Save-State;Receive-OriginalWait $Step $Row;return
                }
                if($Outcome -ne 'confirmed'){$Row.state=$Outcome;Save-State;throw 'Command result did not confirm requested outcome; inspect inner result'}
                Confirm-ReturnedResult $Step $Row
                return
            }
        }
        Assert-Identity
        $Arguments=Add-UnityTargetArguments $Context @('job','wait',$Row.jobId,'--timeout',[string]$TimeoutSeconds,'--poll-interval','500','--json')
        $Native=Invoke-UnityNative -Context $Context -ArgumentList $Arguments -TimeoutSeconds ($TimeoutSeconds+5) -TracePath $TracePath -CommandName 'job_wait'
        [IO.File]::WriteAllText((Join-Path $StateDirectory ($Step.id+'.job.stdout.json')),$Native.stdout,[Text.UTF8Encoding]::new($false))
        $Parsed=Read-NativeEnvelope $Native
        if($Parsed.state -ne 'returned'){$Row.state='result_unknown';Save-State;throw 'Job result unavailable; original job retained, no command replayed'}
        $Envelope=$Parsed.envelope;$Data=Get-UnityProperty $Envelope 'data';$Terminal=Get-UnityProperty $Data 'state'
        $Row.jobState=$Terminal;$Row.result=Get-UnityProperty $Data 'result'
        if((Get-UnityProperty $Envelope 'success') -ne $true -or $Terminal -ne 'completed'){$Row.state='result_unknown';Save-State;throw 'Job not confirmed completed; canceled/not_found do not prove no effects'}
        $Outcome=Get-UnityOutcome $Step.command $Row.result
        if($Step.command -eq 'wait_for' -and $Outcome -eq 'in_flight') {
            $Row.state='in_flight'
            $Row.waitId=Get-UnityProperty $Row.result 'waitId'
            if(-not $Row.waitId){$Row.state='result_unknown';Save-State;throw 'Completed job returned a pending wait without its original identity'}
            Save-State;Receive-OriginalWait $Step $Row;return
        }
        if($Outcome -ne 'confirmed'){$Row.state=$Outcome;Save-State;throw 'Completed job contains a failed/unknown business result'}
        Confirm-ReturnedResult $Step $Row
    }

    $RecipePath=(Resolve-Path -LiteralPath $RecipePath).Path
    $Recipe=Get-Content -LiteralPath $RecipePath -Raw -Encoding UTF8|ConvertFrom-Json
    $InputFileHash=''
    $RecipeInputs=Get-UnityProperty $Recipe 'inputs' ([pscustomobject]@{})
    if($InputsPath){$InputsPath=(Resolve-Path -LiteralPath $InputsPath).Path;$Overrides=Get-Content -LiteralPath $InputsPath -Raw -Encoding UTF8|ConvertFrom-Json;$InputFileHash=Get-UnityFileHash $InputsPath;foreach($Property in $Overrides.PSObject.Properties){$RecipeInputs|Add-Member -NotePropertyName $Property.Name -NotePropertyValue $Property.Value -Force}}
    function Expand-RecipeValue($Value) {
        # JSON integer/bool values wrapped by a PS pipeline can also satisfy PSCustomObject in 5.1.
        if($null -eq $Value -or $Value -is [ValueType]){return $Value}
        if($Value -is [string]) {
            if($Value -match '^\{\{([A-Za-z][A-Za-z0-9_]*)\}\}$'){
                $Property=$RecipeInputs.PSObject.Properties[$Matches[1]]
                if(-not $Property){throw 'Recipe input is missing'}
                return ,$Property.Value
            }
            $Expanded=$Value
            foreach($Match in [regex]::Matches($Value,'\{\{([A-Za-z][A-Za-z0-9_]*)\}\}')){
                $Property=$RecipeInputs.PSObject.Properties[$Match.Groups[1].Value]
                if(-not $Property -or $Property.Value -isnot [string]){throw 'Embedded recipe input must be a string'}
                $Expanded=$Expanded.Replace($Match.Value,$Property.Value)
            }
            return $Expanded
        }
        if($Value -is [System.Array]){return ,@($Value|ForEach-Object {Expand-RecipeValue $_})}
        if($Value -is [pscustomobject]){$Map=[ordered]@{};foreach($Property in $Value.PSObject.Properties){$Map[$Property.Name]=Expand-RecipeValue $Property.Value};return [pscustomobject]$Map}
        return $Value
    }
    $Recipe=Expand-RecipeValue $Recipe
    if((Get-UnityProperty $Recipe 'schemaVersion') -ne 1){throw 'Recipe schemaVersion must be 1'}
    $Steps=@(Get-UnityProperty $Recipe 'steps' @())
    $Verification=@(Get-UnityProperty $Recipe 'verify' @())
    $Preflight=@(Get-UnityProperty $Recipe 'preflight' @())
    if(-not $Steps.Count -or -not $Verification.Count){throw 'Recipe requires finite steps and current read-only verification'}
    $Ids=@{}
    foreach($Step in @($Steps)+@($Verification)+@($Preflight)) {
        if($Step.id -notmatch '^[A-Za-z][A-Za-z0-9_-]*$' -or $Ids.ContainsKey($Step.id)){throw 'Step ids must be unique safe filenames'}
        $Ids[$Step.id]=$true
        if($Step.command -notmatch '^[A-Za-z][A-Za-z0-9_]*$'){throw 'Invalid registered command name'}
        $null=Parameters-ToArguments (Get-UnityProperty $Step 'parameters')
    }
    $ReadCommands=@('inspect_cinemachine','inspect_cinemachine_pipeline','inspect_cinemachine_component','inspect_camera_blend','get_cinemachine_catalog','inspect_ui_components','inspect_ui_point','inspect_ui_text','inspect_tmp_text','get_ui_snapshot','explain_ui_layout','find_ui_overlaps','get_component_properties','get_scene_hierarchy','find_assets','inspect_player_input','inspect_input_actions','read_input_controls','get_input_system_status','inspect_urp','inspect_render_camera','inspect_urp_volume_profile','inspect_shader_global_profile','inspect_ssu_material','inspect_timeline_director','editor_status')
    $ReadCommands+=@('find_gameobjects','inspect_ui_shape','wait_status')
    foreach($Step in @($Verification)+@($Preflight)) {
        if($Step.command -eq 'batch') {
            $Operations=@(Get-UnityProperty (Get-UnityProperty $Step 'parameters') 'operations' @())
            if(-not $Operations.Count){throw 'Read-only verification batch must contain inspectors'}
            foreach($Operation in $Operations){if($Operation.command -notin $ReadCommands){throw 'Verification batch contains a command outside the read-only inspector allowlist'}}
        } elseif($Step.command -notin $ReadCommands){throw 'Verification/preflight command must be a supported current read-only inspector; custom code is not verification'}
    }
    $StateDirectory=[IO.Path]::GetFullPath($StateDirectory)
    if($Resume){if(-not(Test-Path -LiteralPath (Join-Path $StateDirectory 'state.json'))){throw 'Resume requires original state.json'}}
    elseif(Test-Path -LiteralPath $StateDirectory){throw 'StateDirectory already exists; use original -Resume or choose a fresh run'}
    [IO.Directory]::CreateDirectory($StateDirectory)|Out-Null
    $Lock=[IO.File]::Open((Join-Path $StateDirectory '.writer.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    if(-not $TracePath){$TracePath=Join-Path $StateDirectory 'timing.jsonl'}
    $Context=Initialize-UnityConsumer -ProjectPath $ProjectPath -EditorPath $EditorPath -DiscoverOnly:$DiscoverOnly -TracePath $TracePath
    $Identity=Get-UnityServiceIdentity $Context
    $RecipeHash=Get-UnityFileHash $RecipePath
    $HashAlgorithm=[Security.Cryptography.SHA256]::Create()
    try {$InputHash=[BitConverter]::ToString($HashAlgorithm.ComputeHash([Text.Encoding]::UTF8.GetBytes($RecipeHash+'|'+$InputFileHash))).Replace('-','').ToLowerInvariant()}finally{$HashAlgorithm.Dispose()}
    $ToolHash=Get-UnityFileHash (Join-Path $Context.SkillRoot 'bundle-manifest.json')
    $StateFile=Join-Path $StateDirectory 'state.json'
    if($Resume) {
        $State=Get-Content -LiteralPath $StateFile -Raw -Encoding UTF8|ConvertFrom-Json
        if($State.inputHash -cne $InputHash -or $State.toolHash -cne $ToolHash -or $State.service.fingerprint -cne $Identity.fingerprint){throw 'Recipe/tool/service identity differs from original run; no step replayed'}
    } else {
        $Rows=@($Steps|ForEach-Object {[pscustomobject]@{id=$_.id;command=$_.command;state='validated';jobId=$null;waitId=$null;jobState=$null;startedAtUtc=$null;endedAtUtc=$null;wallMs=$null;clientPid=$null;exitCode=$null;result=$null}})
        $PublicIdentity=[pscustomobject]@{fingerprint=$Identity.fingerprint;readAtUtc=$Identity.readAtUtc;project=$Context.ProjectPath;editor=$Context.EditorPath}
        $State=[pscustomobject]@{schemaVersion=1;runId=[Guid]::NewGuid().ToString('N');state='validated';inputHash=$InputHash;toolHash=$ToolHash;packageManifestHash=(Get-UnityFileHash (Join-Path $Context.PackagePath 'package.json'));service=$PublicIdentity;steps=$Rows;verification=@();error=$null;elapsedMs=0}
        Save-State
    }
    # Guards run before any first submission. Resume of confirmed writes uses current verification instead.
    if(@($State.steps|Where-Object {$_.state -ne 'validated'}).Count -eq 0) {
        foreach($Step in $Preflight) {
            Assert-Identity
            $Arguments=Add-UnityTargetArguments $Context (@('command',$Step.command)+@(Parameters-ToArguments (Get-UnityProperty $Step 'parameters'))+@('--json'))
            $Native=Invoke-UnityNative -Context $Context -ArgumentList $Arguments -TimeoutSeconds $TimeoutSeconds -TracePath $TracePath -CommandName $Step.command
            $Parsed=Read-NativeEnvelope $Native
            if($Parsed.state -ne 'returned' -or (Get-UnityEnvelopeOutcome $Step.command $Parsed.envelope) -ne 'confirmed'){throw 'Preflight read did not confirm its result; no recipe write was submitted'}
            Assert-RecipeResult $Parsed.envelope.data.result (Get-UnityProperty $Step 'assert' @())
        }
    }
    if($ValidateOnly) {
        $Batches=@($Steps|Where-Object {$_.command -eq 'batch'})
        foreach($Step in $Batches) {
            $Parameters=$Step.parameters|ConvertTo-Json -Depth 64|ConvertFrom-Json
            $Parameters|Add-Member -NotePropertyName dry_run -NotePropertyValue $true -Force
            $DryStep=[pscustomobject]@{id=$Step.id;command='batch';parameters=$Parameters;detach=$false;assert=@()}
            $DryRow=[pscustomobject]@{state='validated';jobId=$null;waitId=$null;startedAtUtc=$null;endedAtUtc=$null;wallMs=$null;clientPid=$null;exitCode=$null;result=$null}
            Execute-Step $DryStep $DryRow $false
        }
        $RunState='validated'
        [Console]::Out.WriteLine((@{success=$true;state='validated';validationScope='structure_and_native_batch_dry_run';liveWritesExecuted=$false;dryRunBatches=$Batches.Count;stateDirectory=$StateDirectory}|ConvertTo-Json -Compress))
        return
    }
    for($Index=0;$Index -lt $Steps.Count;$Index++) {
        $Step=$Steps[$Index];$Row=$State.steps[$Index]
        if($Row.state -eq 'confirmed'){continue}
        if($Row.state -in @('failed','partial','stale')){throw 'Previously failed/partial step needs explicit inspection; resume does not replay it'}
        $Recover=$Row.state -in @('started','in_flight','result_unknown','awaiting_assertion')
        Execute-Step $Step $Row $Recover
    }
    $Checks=@()
    foreach($Step in $Verification) {
        Assert-Identity
        $Arguments=Add-UnityTargetArguments $Context (@('command',$Step.command)+@(Parameters-ToArguments (Get-UnityProperty $Step 'parameters'))+@('--json'))
        $Native=Invoke-UnityNative -Context $Context -ArgumentList $Arguments -TimeoutSeconds $TimeoutSeconds -TracePath $TracePath -CommandName $Step.command
        [IO.File]::WriteAllText((Join-Path $StateDirectory ($Step.id+'.verify.stdout.json')),$Native.stdout,[Text.UTF8Encoding]::new($false))
        $Parsed=Read-NativeEnvelope $Native
        if($Parsed.state -ne 'returned' -or (Get-UnityEnvelopeOutcome $Step.command $Parsed.envelope) -ne 'confirmed'){$State.state='stale';Save-State;throw 'Current verification failed; historical completion does not prove current objects exist'}
        $Value=$Parsed.envelope.data.result
        try {Assert-RecipeResult $Value (Get-UnityProperty $Step 'assert' @())} catch {$State.state='stale';Save-State;throw}
        $Checks+=@{id=$Step.id;command=$Step.command;state='confirmed';result=$Value}
    }
    $State.verification=$Checks;$State.state='confirmed';$State.error=$null;$State.elapsedMs=$Clock.Elapsed.TotalMilliseconds;Save-State;$RunState='confirmed'
    [Console]::Out.WriteLine((@{success=$true;state=$State.state;runId=$State.runId;stateDirectory=$StateDirectory;elapsedMs=$State.elapsedMs;steps=@($State.steps|ForEach-Object {@{id=$_.id;command=$_.command;state=$_.state}});verification=$Checks}|ConvertTo-Json -Depth 64 -Compress))
} catch {
    $OriginalError=$_
    if($State -and $StateFile){if($State.state -ne 'stale'){$State.state='needs_inspection'};$State.error=$OriginalError.Exception.Message;$State.elapsedMs=$Clock.Elapsed.TotalMilliseconds;try{Save-State}catch{[Console]::Error.WriteLine('UNITY_STATE_WRITE_FAILED: original failure retained; inspect current effects before retry')}}
    [Console]::Out.WriteLine((@{success=$false;state=$(if($State){$State.state}else{'failed'});stateDirectory=$StateDirectory;errors=@(@{code='UNITY_RECIPE_ERROR';message=$OriginalError.Exception.Message})}|ConvertTo-Json -Depth 6 -Compress))
    exit 1
} finally {
    if($Lock){$Lock.Dispose()}
    if(Get-Command Write-UnityTiming -ErrorAction SilentlyContinue){Write-UnityTiming $TracePath 'entry_total' $Clock.Elapsed.TotalMilliseconds 'Invoke-UnityRecipe' $RunState}
}
