[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$SourcePath,
    [Parameter(Mandatory=$true)][string]$ProjectPath,
    [string]$Destination='Assets/Sprite Shaders Ultimate',
    [switch]$DryRun
)
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$OutputEncoding=[Text.UTF8Encoding]::new($false)
$ssuStage=$null
function Assert-SsuConcretePath([string]$Path){
    $ssuCursor=[IO.Path]::GetFullPath($Path)
    while($ssuCursor){
        if(Test-Path -LiteralPath $ssuCursor){if(((Get-Item -LiteralPath $ssuCursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw "Reparse path is unsupported for SSU installation/cleanup: $ssuCursor"}}
        $ssuParent=[IO.Path]::GetDirectoryName($ssuCursor.TrimEnd('\','/'));if($ssuParent -eq $ssuCursor){break};$ssuCursor=$ssuParent
    }
}
try {
    $ssuSource=(Resolve-Path -LiteralPath $SourcePath).Path.TrimEnd('\','/')
    $ssuProject=(Resolve-Path -LiteralPath $ProjectPath).Path.TrimEnd('\','/')
    if(-not [IO.File]::Exists([IO.Path]::Combine($ssuProject,'ProjectSettings/ProjectVersion.txt'))){throw 'ProjectPath must be a Unity project.'}
    $ssuAssets=[IO.Path]::GetFullPath([IO.Path]::Combine($ssuProject,'Assets')).TrimEnd('\','/')
    if([IO.Path]::IsPathRooted($Destination)){$ssuDestination=[IO.Path]::GetFullPath($Destination)}
    else{$ssuDestination=[IO.Path]::GetFullPath([IO.Path]::Combine($ssuProject,$Destination))}
    $ssuDestination=$ssuDestination.TrimEnd('\','/')
    Assert-SsuConcretePath $ssuSource
    Assert-SsuConcretePath $ssuDestination
    Assert-SsuConcretePath ([IO.Path]::Combine($ssuProject,'Temp/UnityTrueCLI'))
    if(-not $ssuDestination.StartsWith($ssuAssets+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Destination must be a new folder strictly within target Assets.'}
    if($ssuSource.Equals($ssuDestination,[StringComparison]::OrdinalIgnoreCase) -or $ssuSource.StartsWith($ssuDestination+'\',[StringComparison]::OrdinalIgnoreCase) -or $ssuDestination.StartsWith($ssuSource+'\',[StringComparison]::OrdinalIgnoreCase)){
        if(-not $ssuSource.Equals($ssuDestination,[StringComparison]::OrdinalIgnoreCase)){throw 'Source/destination cannot contain each other.'}
    }
    foreach($ssuFolder in @('Shaders','Textures','Scripts')){
        if(-not [IO.Directory]::Exists([IO.Path]::Combine($ssuSource,$ssuFolder))){throw "Source lacks SSU $ssuFolder folder."}
    }
    $ssuShader=[IO.Path]::Combine($ssuSource,'Shaders/GUI SSU.shader')
    if(-not [IO.File]::Exists($ssuShader) -or -not ([IO.File]::ReadAllText($ssuShader) -match 'Shader\s+"Sprite Shaders Ultimate/GUI SSU"')){throw 'Source is not a recognized local SSU package.'}
    if($ssuSource.Equals($ssuDestination,[StringComparison]::OrdinalIgnoreCase)){
        [ordered]@{success=$true;state='already_installed';path=$ssuDestination;import='No files copied; get_ssu_catalog must verify loaded shaders.'}|ConvertTo-Json -Compress
        return
    }
    $ssuFiles=@()
    foreach($ssuFolder in @('Shaders','Textures','Scripts')){
        $ssuFiles+=@(Get-ChildItem -LiteralPath ([IO.Path]::Combine($ssuSource,$ssuFolder)) -File -Recurse -Force)
        $ssuFolderMeta=[IO.Path]::Combine($ssuSource,$ssuFolder+'.meta')
        if(-not(Test-Path -LiteralPath $ssuFolderMeta)){throw "Source folder meta missing: $ssuFolderMeta"}
        $ssuFiles+=Get-Item -LiteralPath $ssuFolderMeta
    }
    foreach($ssuFile in $ssuFiles|Where-Object{$_.Extension -ne '.meta'}){if(-not [IO.File]::Exists($ssuFile.FullName+'.meta')){throw "Source asset meta missing: $($ssuFile.FullName)"}}
    foreach($ssuFolder in @('Shaders','Textures','Scripts')){
        foreach($ssuDirectory in Get-ChildItem -LiteralPath ([IO.Path]::Combine($ssuSource,$ssuFolder)) -Directory -Recurse -Force){if(-not [IO.File]::Exists($ssuDirectory.FullName+'.meta')){throw "Source folder meta missing: $($ssuDirectory.FullName)"}}
    }
    if(@($ssuFiles|Where-Object{($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0}).Count){throw 'SSU source contains reparse files; use a concrete local asset copy.'}
    if(@(Get-ChildItem -LiteralPath $ssuSource -Directory -Recurse -Force|Where-Object{($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0}).Count){throw 'SSU source contains reparse directories; use a concrete local asset copy.'}
    $ssuGuids=@{}
    foreach($ssuFile in $ssuFiles|Where-Object{$_.Extension -eq '.meta'}){
        $ssuGuid=[regex]::Match([IO.File]::ReadAllText($ssuFile.FullName),'(?m)^guid:\s*([0-9a-fA-F]{32})\s*$').Groups[1].Value.ToLowerInvariant()
        if(-not $ssuGuid){throw "Source meta missing GUID: $($ssuFile.FullName)"}
        if($ssuGuids.ContainsKey($ssuGuid)){throw "Duplicate source GUID: $ssuGuid"}
        $ssuGuids[$ssuGuid]=$ssuFile.FullName
    }
    # Existing target assets may contain SSU elsewhere. Never cause Unity to silently rewrite a duplicate GUID.
    $ssuCollisions=@()
    $ssuTargetRoots=@($ssuAssets,[IO.Path]::Combine($ssuProject,'Packages'))
    $ssuCache=[IO.Path]::Combine($ssuProject,'Library/PackageCache');if([IO.Directory]::Exists($ssuCache)){$ssuTargetRoots+=$ssuCache}
    # Resolve explicit file: dependencies, including transitive local package dependencies.
    $ssuManifests=[Collections.Generic.Queue[string]]::new();$ssuManifests.Enqueue([IO.Path]::Combine($ssuProject,'Packages/manifest.json'));$ssuVisited=@{}
    while($ssuManifests.Count){
        $ssuManifest=$ssuManifests.Dequeue();if($ssuVisited.ContainsKey($ssuManifest) -or -not [IO.File]::Exists($ssuManifest)){continue};$ssuVisited[$ssuManifest]=$true
        $ssuDependencies=([IO.File]::ReadAllText($ssuManifest)|ConvertFrom-Json).dependencies
        if($null -eq $ssuDependencies){continue}
        foreach($ssuDependency in $ssuDependencies.PSObject.Properties){
            $ssuValue=[string]$ssuDependency.Value;if(-not $ssuValue.StartsWith('file:')){continue}
            $ssuLocal=$ssuValue.Substring(5);if([IO.Path]::IsPathRooted($ssuLocal)){$ssuLocal=[IO.Path]::GetFullPath($ssuLocal)}else{$ssuLocal=[IO.Path]::GetFullPath([IO.Path]::Combine([IO.Path]::GetDirectoryName($ssuManifest),$ssuLocal))}
            if(-not [IO.Directory]::Exists($ssuLocal)){throw "Cannot verify local package GUIDs; directory unavailable: $ssuLocal"}
            $ssuTargetRoots+=$ssuLocal;$ssuManifests.Enqueue([IO.Path]::Combine($ssuLocal,'package.json'))
        }
    }
    $ssuTargetRoots=@($ssuTargetRoots|Select-Object -Unique)
    foreach($ssuFile in Get-ChildItem -LiteralPath $ssuTargetRoots -Filter '*.meta' -File -Recurse -Force){
        $ssuGuid=[regex]::Match([IO.File]::ReadAllText($ssuFile.FullName),'(?m)^guid:\s*([0-9a-fA-F]{32})\s*$').Groups[1].Value.ToLowerInvariant()
        if($ssuGuid -and $ssuGuids.ContainsKey($ssuGuid)){$ssuCollisions+=$ssuFile.FullName}
    }
    if(Test-Path -LiteralPath $ssuDestination){throw 'Destination already exists. Existing assets are never overwritten; use their current installation.'}
    if($ssuCollisions.Count){throw "Target project already contains matching SSU GUIDs. Use its installed SSU; no second copy was created. First conflict: $($ssuCollisions[0])"}
    $ssuPlan=[ordered]@{success=$true;state='planned';source=$ssuSource;destination=$ssuDestination;files=$ssuFiles.Count;preserveGuids=$true;includes=@('Shaders','Textures','Scripts');excludes=@('Demo','ASE');vendorBundledWithCli=$false;refresh='Not performed; import/compilation and get_ssu_catalog in the target Editor must confirm availability.'}
    if($DryRun){$ssuPlan|ConvertTo-Json -Depth 4 -Compress;return}
    $ssuStage=[IO.Path]::Combine($ssuProject,'Temp/UnityTrueCLI/ssu-stage-'+[Guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($ssuStage)|Out-Null
    foreach($ssuFile in $ssuFiles){
        $ssuRelative=$ssuFile.FullName.Substring($ssuSource.Length+1);$ssuOutput=[IO.Path]::Combine($ssuStage,$ssuRelative)
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($ssuOutput))|Out-Null
        [IO.File]::Copy($ssuFile.FullName,$ssuOutput,$false)
    }
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($ssuDestination))|Out-Null
    [IO.Directory]::Move($ssuStage,$ssuDestination);$ssuStage=$null
    $ssuPlan.state='copied_pending_editor_import';$ssuPlan|ConvertTo-Json -Depth 4 -Compress
}
catch{[ordered]@{success=$false;state='rejected';message=$_.Exception.Message}|ConvertTo-Json -Depth 3 -Compress;exit 1}
finally{
    if($ssuStage -and [IO.Directory]::Exists($ssuStage)){
        # Resolve and verify our exact stage remains inside this task's Temp/UnityTrueCLI before recursive cleanup.
        $ssuResolved=[IO.Path]::GetFullPath($ssuStage);$ssuStageRoot=[IO.Path]::GetFullPath([IO.Path]::Combine($ssuProject,'Temp/UnityTrueCLI')).TrimEnd('\','/')+'\'
        if($ssuResolved.StartsWith($ssuStageRoot,[StringComparison]::OrdinalIgnoreCase) -and [IO.Path]::GetFileName($ssuResolved).StartsWith('ssu-stage-')){Assert-SsuConcretePath $ssuResolved;Remove-Item -LiteralPath $ssuResolved -Recurse -Force}
    }
}
