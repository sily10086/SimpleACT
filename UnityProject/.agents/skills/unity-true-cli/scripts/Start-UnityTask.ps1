[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ProjectPath,
    [string]$EditorPath,
    [string]$Task='general',
    [string]$Query,
    [ValidatePattern('^[a-zA-Z][a-zA-Z0-9_]*$')][string]$Command,
    [switch]$PrepareOnly,
    [switch]$DiscoverOnly,
    [string]$TracePath,
    [ValidateRange(1,600)][int]$TimeoutSeconds=60
)
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$OutputEncoding=[Text.UTF8Encoding]::new($false)
$EntryClock=[Diagnostics.Stopwatch]::StartNew()
$EntryState='failed'
try {
    Add-Type -AssemblyName System.Net.Http
    . (Join-Path $PSScriptRoot 'UnityInvocation.ps1')
    $Profiles=Get-Content -LiteralPath (Join-Path $PSScriptRoot '../references/workflow-profiles.json') -Raw -Encoding UTF8|ConvertFrom-Json
    $Profile=$null
    if ($Task -ne 'general') {
        $Found=@($Profiles.profiles|Where-Object {$_.aliases -icontains $Task})
        if ($Found.Count -ne 1) {throw 'Unknown task. Use general/camera/ui/tmp/input/rendering/ssu/observe, or an alias in workflow-profiles.json'}
        $Profile=$Found[0]
    }
    $Context=Initialize-UnityConsumer -ProjectPath $ProjectPath -EditorPath $EditorPath -DiscoverOnly:$DiscoverOnly -TracePath $TracePath
    $Identity=Get-UnityServiceIdentity -Context $Context
    $CompatibleService=Confirm-UnityCompatibleService -Context $Context
    $Public=[ordered]@{projectPath=$Context.ProjectPath;editorVersion=$Context.Version;cliPath=$Context.CliPath;bundleVersion=$Context.Manifest.bundle_version;serviceIdentity=$Identity.fingerprint;identityReadAtUtc=$Identity.readAtUtc;preparationMode=$(if($DiscoverOnly){'existing_ready_only'}else{'prepare_may_install_and_launch'})}
    $Answer=[ordered]@{success=$true;schemaVersion=1;task=$(if($Profile){$Profile.name}else{'general'});context=$Public}
    $Public['pipeline']=$CompatibleService
    if (-not $PrepareOnly) {
        if ($Command) {
            $Arguments=Add-UnityTargetArguments -Context $Context -ArgumentList @('command','--query',$Command,'--detail','full','--limit','200','--json')
            $Native=Invoke-UnityNative -Context $Context -ArgumentList $Arguments -TimeoutSeconds $TimeoutSeconds -TracePath $TracePath -CommandName 'discover_schema'
            if($Native.timedOut){throw 'Schema query timed out; no write command was submitted'}
            $Envelope=$Native.stdout|ConvertFrom-Json
            if($Native.exitCode -ne 0 -or (Get-UnityProperty $Envelope 'success') -ne $true){throw 'Current service did not return command schema'}
            $Data=Get-UnityProperty $Envelope 'data'
            $Commands=@((Get-UnityProperty $Data 'commands' @())|Where-Object {$_.name -ceq $Command})
            if($Commands.Count -ne 1){throw 'Exact command is not registered on the current service; no fallback eval was run'}
            $Answer['commands']=$Commands
        } elseif(-not $Profile -and $Query) {
            $Arguments=Add-UnityTargetArguments -Context $Context -ArgumentList @('command','--query',$Query,'--detail','compact','--limit','100','--json')
            $Native=Invoke-UnityNative -Context $Context -ArgumentList $Arguments -TimeoutSeconds $TimeoutSeconds -TracePath $TracePath -CommandName 'discover_commands'
            if($Native.timedOut){throw 'Command search timed out; no write command was submitted'}
            $Envelope=$Native.stdout|ConvertFrom-Json
            if($Native.exitCode -ne 0 -or (Get-UnityProperty $Envelope 'success') -ne $true){throw 'Current service did not return command search'}
            $Answer['commands']=@($Envelope.data.commands)
            $Answer['registeredCount']=$Envelope.data.count
            $Answer['totalMatches']=$Envelope.data.total
            $Answer['schemaHint']='Use -Command <exact_name> for a selected schema. This search includes core commands, not only extensions.'
        } else {
            $Arguments=Add-UnityTargetArguments -Context $Context -ArgumentList @('command','get_cli_extensions','--json')
            $Native=Invoke-UnityNative -Context $Context -ArgumentList $Arguments -TimeoutSeconds $TimeoutSeconds -TracePath $TracePath -CommandName 'get_cli_extensions'
            if($Native.timedOut){throw 'Capability query timed out; no write command was submitted'}
            $Envelope=$Native.stdout|ConvertFrom-Json
            if($Native.exitCode -ne 0 -or (Get-UnityEnvelopeOutcome 'get_cli_extensions' $Envelope) -ne 'confirmed'){throw 'Current service did not return extension capabilities'}
            $Capabilities=$Envelope.data.result
            $Answer['packages']=@($Capabilities.packages)
            $Commands=@($Capabilities.commands)
            if($Profile){
                $Commands=@($Commands|Where-Object {
                    $Candidate=$_
                    @($Profile.tags|Where-Object {$Prefix=$_; @($Candidate.tags|Where-Object {$_.StartsWith($Prefix,[StringComparison]::OrdinalIgnoreCase)}).Count -gt 0}).Count -gt 0
                })
                $Answer['reference']=Join-Path $Context.SkillRoot $Profile.reference
                $Recipe=Get-UnityProperty $Profile 'recipe'
                if($Recipe){$Answer['recipe']=Join-Path $Context.SkillRoot $Recipe}
            }
            if($Query){
                $Terms=@($Query)
                if($Profile){$Synonyms=$Profile.queries.PSObject.Properties[$Query];if($Synonyms){$Terms=@($Synonyms.Value)}}
                $Commands=@($Commands|Where-Object {$Name=$_.name; @($Terms|Where-Object {$Name.IndexOf($_,[StringComparison]::OrdinalIgnoreCase) -ge 0}).Count -gt 0})
            }
            if($Profile -or $Query){$Answer['commands']=@($Commands|ForEach-Object {$_.name});$Answer['registeredCount']=$Commands.Count}
            else {
                $Answer['tasks']=@($Profiles.profiles|ForEach-Object {@{task=$_.name;aliases=$_.aliases;reference=$_.reference}})
                $Answer['extensionCommandCount']=$Commands.Count
            }
            $Answer['schemaHint']='Use -Command <exact_name> for one current schema. Names above are live registrations, not blanket feature validation.'
            $Answer['workflowHint']='Use Invoke-UnityRecipe.ps1 for finite steps; batch and wait/job schemas come from the same live command query.'
        }
        if((Get-UnityServiceIdentity -Context $Context).fingerprint -cne $Identity.fingerprint){throw 'Service lifecycle changed during discovery; discard this schema snapshot and prepare again'}
    }
    $EntryState='confirmed'
    $Answer['elapsedMs']=$EntryClock.Elapsed.TotalMilliseconds
    [Console]::Out.WriteLine(($Answer|ConvertTo-Json -Depth 64 -Compress))
} catch {
    [Console]::Out.WriteLine((@{success=$false;errors=@(@{code='UNITY_TASK_ERROR';message=$_.Exception.Message})}|ConvertTo-Json -Depth 6 -Compress))
    exit 1
} finally {
    if(Get-Command Write-UnityTiming -ErrorAction SilentlyContinue){Write-UnityTiming $TracePath 'entry_total' $EntryClock.Elapsed.TotalMilliseconds 'Start-UnityTask' $EntryState}
}
