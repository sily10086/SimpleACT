[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ProjectPath,
    [Parameter(Mandatory=$true)][string]$UrpPackagePath,
    [string]$ShaderPath='Assets/Sprite Shaders Ultimate/Shaders/2D Lit URP SSU.shader',
    [switch]$DryRun
)
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$OutputEncoding=[Text.UTF8Encoding]::new($false)
$ssuTemporary=$null
function Get-SsuHash([byte[]]$Bytes){
    $ssuHasher=[Security.Cryptography.SHA256]::Create()
    try{return ([BitConverter]::ToString($ssuHasher.ComputeHash($Bytes))).Replace('-','').ToLowerInvariant()}finally{$ssuHasher.Dispose()}
}
function Assert-SsuRepairPath([string]$Path){
    $ssuCursor=[IO.Path]::GetFullPath($Path)
    while($ssuCursor){
        if(Test-Path -LiteralPath $ssuCursor){if(((Get-Item -LiteralPath $ssuCursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw "Reparse path is unsupported: $ssuCursor"}}
        $ssuParent=[IO.Path]::GetDirectoryName($ssuCursor.TrimEnd('\','/'));if($ssuParent -eq $ssuCursor){break};$ssuCursor=$ssuParent
    }
}
try{
    $ssuProject=(Resolve-Path -LiteralPath $ProjectPath).Path.TrimEnd('\','/')
    if(-not [IO.File]::Exists([IO.Path]::Combine($ssuProject,'ProjectSettings/ProjectVersion.txt'))){throw 'ProjectPath must be a Unity project.'}
    $ssuTarget=if([IO.Path]::IsPathRooted($ShaderPath)){[IO.Path]::GetFullPath($ShaderPath)}else{[IO.Path]::GetFullPath([IO.Path]::Combine($ssuProject,$ShaderPath))}
    $ssuAssets=[IO.Path]::Combine($ssuProject,'Assets').TrimEnd('\','/')+'\'
    if(-not $ssuTarget.StartsWith($ssuAssets,[StringComparison]::OrdinalIgnoreCase)){throw 'ShaderPath must be strictly within the target Assets.'}
    Assert-SsuRepairPath $ssuTarget
    Assert-SsuRepairPath ([IO.Path]::Combine($ssuProject,'Temp/UnityTrueCLI'))
    $ssuUrp=(Resolve-Path -LiteralPath $UrpPackagePath).Path
    $ssuPackage=[IO.File]::ReadAllText([IO.Path]::Combine($ssuUrp,'package.json'))|ConvertFrom-Json
    if($ssuPackage.name -ne 'com.unity.render-pipelines.universal' -or $ssuPackage.version -notmatch '^17\.'){throw 'This patch requires an explicitly resolved URP 17 package.'}
    $ssuLock=[IO.File]::ReadAllText([IO.Path]::Combine($ssuProject,'Packages/packages-lock.json'))|ConvertFrom-Json
    $ssuResolved=$ssuLock.dependencies.'com.unity.render-pipelines.universal'.version
    if($ssuResolved -ne $ssuPackage.version){throw 'Target project resolved URP version does not match the supplied URP 17 package.'}
    $ssuDebug=[IO.File]::ReadAllText([IO.Path]::Combine($ssuUrp,'ShaderLibrary/Debug/Debugging2D.hlsl'))
    if($ssuDebug -notmatch '#define\s+SETUP_DEBUG_DATA_2D\(inputData,\s*positionWS,\s*positionCS\)'){throw 'URP does not expose the expected three-argument 2D debug macro.'}
    $ssuBytes=[IO.File]::ReadAllBytes($ssuTarget);$ssuBefore=Get-SsuHash $ssuBytes
    # Exact private input fingerprint; an unknown vendor version must be reviewed rather than guessed.
    $ssuOriginalHash='f0f62f54a097308c77cdb4c1f23f5348936c6a1bbc3cead2a3d2453b42cf5f78'
    $ssuEncoding=[Text.UTF8Encoding]::new($false,$true)
    $ssuText=$ssuEncoding.GetString($ssuBytes)
    $ssuOld='SETUP_DEBUG_DATA_2D(inputData, positionWS);'
    $ssuNew='SETUP_DEBUG_DATA_2D(inputData, positionWS, IN.clipPos);'
    if($ssuBefore -ne $ssuOriginalHash){
        if($ssuBefore -eq '66a30356dc7f06c00da7999ea0082096798f49745b387cf457405dd5757dce5e'){
            [ordered]@{success=$true;state='already_patched';patch='ssu-urp17-debug2d-v1';path=$ssuTarget;sha256=$ssuBefore}|ConvertTo-Json -Compress
            return
        }
        throw 'Unknown SSU 2D shader fingerprint. No bytes were changed.'
    }
    if(([regex]::Matches($ssuText,[regex]::Escape($ssuOld))).Count -ne 2){throw 'Expected exactly two original 2D debug calls.'}
    if(-not [IO.File]::Exists($ssuTarget+'.meta')){throw 'Shader meta is missing; preserve its existing Unity GUID before patching.'}
    $ssuPatched=$ssuEncoding.GetBytes($ssuText.Replace($ssuOld,$ssuNew))
    $ssuPlan=[ordered]@{success=$true;state='planned';patch='ssu-urp17-debug2d-v1';path=$ssuTarget;beforeSha256=$ssuBefore;afterSha256=(Get-SsuHash $ssuPatched);urp=$ssuPackage.version;calls=2;import='Not performed; reimport and ShaderUtil/GPU validation are required.'}
    if($DryRun){$ssuPlan|ConvertTo-Json -Compress;return}
    $ssuBackupRoot=[IO.Path]::Combine($ssuProject,'Temp/UnityTrueCLI/ssu-patches')
    Assert-SsuRepairPath $ssuBackupRoot
    [IO.Directory]::CreateDirectory($ssuBackupRoot)|Out-Null
    $ssuBackup=[IO.Path]::Combine($ssuBackupRoot,[Guid]::NewGuid().ToString('N')+'.shader.original')
    $ssuTemporary=[IO.Path]::Combine($ssuBackupRoot,[Guid]::NewGuid().ToString('N')+'.shader.pending')
    [IO.File]::WriteAllBytes($ssuTemporary,$ssuPatched)
    if((Get-SsuHash ([IO.File]::ReadAllBytes($ssuTarget))) -ne $ssuBefore){throw 'Shader changed during preflight; retry after inspecting it.'}
    [IO.File]::Replace($ssuTemporary,$ssuTarget,$ssuBackup);$ssuTemporary=$null
    $ssuPlan.state='patched_pending_editor_import';$ssuPlan.backup=$ssuBackup
    $ssuPlan|ConvertTo-Json -Compress
}
catch{[ordered]@{success=$false;state='rejected';message=$_.Exception.Message}|ConvertTo-Json -Compress;exit 1}
finally{if($ssuTemporary -and [IO.File]::Exists($ssuTemporary)){Assert-SsuRepairPath $ssuTemporary;[IO.File]::Delete($ssuTemporary)}}
