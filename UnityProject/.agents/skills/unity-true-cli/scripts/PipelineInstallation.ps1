function Assert-UnityBootstrapRequestIdentity {
    param([string]$Path, [object]$Expected)
    $Existing = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($Field in @('RequestId','PackagePath','ResultPath','ExitWhenReady','ExpiresAt')) {
        if ($Existing.$Field -cne $Expected.$Field) { throw "Bootstrap request belongs to another operation or changed: $Path" }
    }
}

function Publish-UnityBootstrapRequest {
    param([string]$Path, [object]$Request, [string]$Body)
    if (Test-Path -LiteralPath $Request.ResultPath) {
        $ExistingReceipt = Get-Content -LiteralPath $Request.ResultPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($ExistingReceipt.RequestId -cne $Request.RequestId) { throw 'Bootstrap result identity mismatch' }
        if ($ExistingReceipt.Success -eq $true) { return }
        # A failed receipt must retain its original mailbox for the explicit failed-install retry.
    }
    if (Test-Path -LiteralPath $Path) { Assert-UnityBootstrapRequestIdentity -Path $Path -Expected $Request; return }
    [IO.Directory]::CreateDirectory((Split-Path $Path -Parent)) | Out-Null
    $Temporary = $Path + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    try {
        [IO.File]::WriteAllText($Temporary, $Body, [Text.UTF8Encoding]::new($false))
        try { [IO.File]::Move($Temporary, $Path) }
        catch [IO.IOException] {
            if (-not (Test-Path -LiteralPath $Path)) { throw }
            Assert-UnityBootstrapRequestIdentity -Path $Path -Expected $Request
        }
        # A receipt may arrive between the preflight and publish. Leave it intact and remove only
        # this exact completed request, so a successful operation cannot leave a stale mailbox.
        if ((Test-Path -LiteralPath $Request.ResultPath) -and (Test-Path -LiteralPath $Path)) {
            $Receipt = Get-Content -LiteralPath $Request.ResultPath -Raw -Encoding UTF8 | ConvertFrom-Json
            if ($Receipt.RequestId -ceq $Request.RequestId -and $Receipt.Success -eq $true) {
                Assert-UnityBootstrapRequestIdentity -Path $Path -Expected $Request
                [IO.File]::Delete($Path)
            }
        }
    }
    finally { if ([IO.File]::Exists($Temporary)) { [IO.File]::Delete($Temporary) } }
}

function Install-UnityPipeline {
    param(
        [Parameter(Mandatory = $true)][hashtable]$Context,
        [switch]$KeepEditorOpen,
        [switch]$RetryFailedInstall,
        [switch]$UpgradePipeline,
        [int]$TimeoutSeconds = 300
    )
    $Hasher = [Security.Cryptography.SHA256]::Create()
    try { $Key = [BitConverter]::ToString($Hasher.ComputeHash([Text.Encoding]::UTF8.GetBytes($Context.ProjectPath.ToLowerInvariant().TrimEnd('\','/')))).Replace('-','') }
    finally { $Hasher.Dispose() }
    $Mutex = [Threading.Mutex]::new($false, ('Local\UnityTrueCli-' + $Key))
    $Acquired = $false
    try {
        try { $Acquired = $Mutex.WaitOne([TimeSpan]::FromSeconds($TimeoutSeconds)) }
        catch [Threading.AbandonedMutexException] { $Acquired = $true }
        if (-not $Acquired) { throw 'Another CLI installation is active for this project; inspect its original result' }
        Install-UnityPipelineCore -Context $Context -KeepEditorOpen:$KeepEditorOpen -RetryFailedInstall:$RetryFailedInstall -UpgradePipeline:$UpgradePipeline -TimeoutSeconds $TimeoutSeconds
    }
    finally { if ($Acquired) { $Mutex.ReleaseMutex() }; $Mutex.Dispose() }
}

function Install-UnityPipelineCore {
    param(
        [Parameter(Mandatory = $true)][hashtable]$Context,
        [switch]$KeepEditorOpen,
        [switch]$RetryFailedInstall,
        [switch]$UpgradePipeline,
        [int]$TimeoutSeconds = 300
    )

    foreach ($Relative in @('Tools/UnityTrueCLI','Assets/Editor/UnityTrueCli','Temp/UnityTrueCLI','Packages')) {
        Assert-UnityWritePath -Root $Context.ProjectPath -Path (Join-Path $Context.ProjectPath $Relative)
    }

    $SourceVersion = (Get-Content -LiteralPath (Join-Path $Context.PackageSource 'package.json') -Raw -Encoding UTF8 | ConvertFrom-Json).version
    $InstalledManifest = Join-Path $Context.PackagePath 'package.json'
    $VersionDiffers = (Test-Path -LiteralPath $InstalledManifest) -and ((Get-Content -LiteralPath $InstalledManifest -Raw -Encoding UTF8 | ConvertFrom-Json).version -ne $SourceVersion)
    if ($VersionDiffers) {
        if (-not $UpgradePipeline) { throw "Project Pipeline differs from bundle $SourceVersion; use -UpgradePipeline with the Editor closed to preserve a backup and install the new commands" }
        if (Get-UnityProjectProcess -ProjectPath $Context.ProjectPath) { throw 'Close the target Editor after saving your work before -UpgradePipeline; this command does not close it automatically' }
        $UpgradeId = [Guid]::NewGuid().ToString('N')
        $BackupPath = $Context.PackagePath + '.backup-' + $UpgradeId
        $StagingPath = $Context.PackagePath + '.stage-' + $UpgradeId
        Copy-Item -LiteralPath $Context.PackageSource -Destination $StagingPath -Recurse
        Move-Item -LiteralPath $Context.PackagePath -Destination $BackupPath
        try { Move-Item -LiteralPath $StagingPath -Destination $Context.PackagePath }
        catch { Move-Item -LiteralPath $BackupPath -Destination $Context.PackagePath; throw }
        [Console]::Error.WriteLine("Previous Pipeline preserved at $BackupPath")
    }
    if (Test-UnityPipelineReady -ProjectPath $Context.ProjectPath) {
        [ordered]@{ success = $true; state = 'ready'; project = $Context.ProjectPath; installed = $false } | ConvertTo-Json -Compress
        return
    }

    $RunningEditor = Get-UnityProjectProcess -ProjectPath $Context.ProjectPath
    if ($RunningEditor -and $RunningEditor.ExecutablePath -ne $Context.EditorPath) { throw 'Running Editor differs from the requested exact Editor' }
    if (Test-Path -LiteralPath $Context.PackagePath) {
        foreach ($SourceFile in Get-ChildItem -LiteralPath $Context.PackageSource -File -Recurse -Force) {
            $RelativePath = $SourceFile.FullName.Substring($Context.PackageSource.Length + 1)
            $InstalledFile = Join-Path $Context.PackagePath $RelativePath
            if (-not (Test-Path -LiteralPath $InstalledFile)) { throw "Installed package file missing: $InstalledFile" }
            if ($SourceFile.Extension -eq '.meta') {
                $SourceGuid = [regex]::Match((Get-Content -LiteralPath $SourceFile.FullName -Raw -Encoding UTF8), '(?m)^guid:\s*(\S+)').Groups[1].Value
                $InstalledGuid = [regex]::Match((Get-Content -LiteralPath $InstalledFile -Raw -Encoding UTF8), '(?m)^guid:\s*(\S+)').Groups[1].Value
                if ($SourceGuid -ne $InstalledGuid) { throw "Installed package GUID differs: $InstalledFile" }
                continue
            }
            if ((Get-UnityFileHash -Path $InstalledFile) -ne (Get-UnityFileHash -Path $SourceFile.FullName)) {
                throw "Installed package differs from this bundle: $InstalledFile"
            }
        }
    }
    else {
        [IO.Directory]::CreateDirectory((Split-Path $Context.PackagePath -Parent)) | Out-Null
        $StagePath = $Context.PackagePath + '.stage-' + [Guid]::NewGuid().ToString('N')
        Copy-Item -LiteralPath $Context.PackageSource -Destination $StagePath -Recurse
        Move-Item -LiteralPath $StagePath -Destination $Context.PackagePath
    }
    $TaskDirectory = Join-Path $Context.ProjectPath 'Temp/UnityTrueCLI'
    [IO.Directory]::CreateDirectory($TaskDirectory) | Out-Null
    $RequestPath = Join-Path $TaskDirectory 'bootstrap-request.json'
    if (Test-Path -LiteralPath $RequestPath) {
        $Previous = Get-Content -LiteralPath $RequestPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if (-not $RetryFailedInstall -or -not (Test-Path -LiteralPath $Previous.ResultPath)) {
            throw "Bootstrap request already exists: $($Previous.RequestId); inspect its result before starting another installation"
        }
        $PreviousResult = Get-Content -LiteralPath $Previous.ResultPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($PreviousResult.RequestId -ne $Previous.RequestId -or $PreviousResult.Success) { throw 'Only an identity-bound failed installation can be retried' }
        Move-Item -LiteralPath $RequestPath -Destination (Join-Path $TaskDirectory "bootstrap-failed-$($Previous.RequestId).request.json")
    }
    $RequestId = [Guid]::NewGuid().ToString('N')
    $ResultPath = Join-Path $TaskDirectory "bootstrap-result-$RequestId.json"
    $BootstrapDirectory = Join-Path $Context.ProjectPath 'Assets/Editor/UnityTrueCli'
    [IO.Directory]::CreateDirectory($BootstrapDirectory) | Out-Null
    $BootstrapPath = Join-Path $BootstrapDirectory 'PackageBootstrap.cs'
    $TemplatePath = Join-Path $PSScriptRoot 'PackageBootstrap.cs.txt'
    if (Test-Path -LiteralPath $BootstrapPath) {
        if ((Get-UnityFileHash -Path $BootstrapPath) -ne (Get-UnityFileHash -Path $TemplatePath)) {
            # Only the exact earlier tool-generated bootstrap may migrate automatically. Preserve
            # it outside Assets and keep its .meta; unknown user edits still require inspection.
            if ((Get-UnityFileHash -Path $BootstrapPath) -ne '033f402cacb1afdf395cb7268c2cfc2f51d22e3cce008cc4dc134abcb2bce603') {
                throw "Existing bootstrap source differs: $BootstrapPath"
            }
            $BootstrapBackupRoot = Join-Path $Context.ProjectPath 'Tools/UnityTrueCLI/bootstrap-backups'
            [IO.Directory]::CreateDirectory($BootstrapBackupRoot) | Out-Null
            [IO.File]::Copy($BootstrapPath, (Join-Path $BootstrapBackupRoot ('PackageBootstrap-' + [Guid]::NewGuid().ToString('N') + '.cs.txt')), $false)
        }
    }
    $Request = [ordered]@{
        RequestId = $RequestId
        PackagePath = $Context.PackagePath
        ResultPath = $ResultPath
        ExitWhenReady = -not $RunningEditor -and -not $KeepEditorOpen
        ExpiresAt = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds).ToString('o')
    }
    $RequestBody = $Request | ConvertTo-Json
    Publish-UnityBootstrapRequest -Path $RequestPath -Request $Request -Body $RequestBody
    Copy-Item -LiteralPath $TemplatePath -Destination $BootstrapPath
    $StartedProcess = $null
    if (-not $RunningEditor) {
        $LogPath = Join-Path $TaskDirectory "bootstrap-$RequestId.log"
        $EditorArgumentList = @('-batchmode', '-nographics', '-projectPath', $Context.ProjectPath, '-logFile', $LogPath)
        $QuotedArgumentList = @($EditorArgumentList | ForEach-Object { ConvertTo-UnityProcessArgument $_ })
        $StartedProcess = Start-Process -FilePath $Context.EditorPath -ArgumentList ($QuotedArgumentList -join ' ') -PassThru -WindowStyle Hidden
    }
    $Deadline = [DateTime]::Parse($Request.ExpiresAt).ToUniversalTime()
    while (-not (Test-Path -LiteralPath $ResultPath)) {
        if ($StartedProcess -and $StartedProcess.HasExited) { throw "Unity exited before bootstrap completed; inspect $LogPath" }
        if ([DateTime]::UtcNow -ge $Deadline) { throw "Bootstrap outcome unknown; request $RequestId; inspect $ResultPath and $RequestPath before retrying" }
        # The Editor or a local cleanup may remove Temp while it starts. Recover communication
        # with the same immutable identity/deadline, never a second installation request.
        Publish-UnityBootstrapRequest -Path $RequestPath -Request $Request -Body $RequestBody
        Start-Sleep -Milliseconds 250
    }
    $Result = Get-Content -LiteralPath $ResultPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($Result.RequestId -ne $RequestId) { throw 'Bootstrap result identity mismatch' }
    if (-not $Result.Success) {
        Publish-UnityBootstrapRequest -Path $RequestPath -Request $Request -Body $RequestBody
        throw "Pipeline bootstrap failed: $($Result.Error)"
    }
    if ($KeepEditorOpen -and -not (Test-UnityPipelineReady -ProjectPath $Context.ProjectPath)) { throw 'Bootstrap completed but Pipeline is not reachable yet; inspect its original result' }
    if ($StartedProcess -and -not $KeepEditorOpen) {
        if (-not $StartedProcess.WaitForExit(30000)) { throw "Unity reported installation success but did not exit; PID $($StartedProcess.Id)" }
        if ($StartedProcess.ExitCode -ne 0) { throw "Unity exited with $($StartedProcess.ExitCode) after installation" }
    }
    [ordered]@{ success = $true; state = 'installed'; project = $Context.ProjectPath; version = $Context.Version; request_id = $RequestId; result_path = $ResultPath; package = $Context.PackagePath } | ConvertTo-Json -Compress
}
