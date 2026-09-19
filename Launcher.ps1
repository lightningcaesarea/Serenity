#requires -version 5
# Serenity dev launcher. Run via Launcher.bat.

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $Root

$script:Procs = @{}   # label -> System.Diagnostics.Process

# ---------- helpers ----------

function Write-Log {
    param([string]$Msg, [string]$Level = 'info')
    $stamp = (Get-Date).ToString('HH:mm:ss')
    $line = "[$stamp] $Msg`r`n"
    $script:LogBox.AppendText($line)
    $script:LogBox.SelectionStart = $script:LogBox.TextLength
    $script:LogBox.ScrollToCaret()
}

function Test-Alive {
    param([string]$Label)
    $p = $script:Procs[$Label]
    if ($null -eq $p) { return $false }
    try { return -not $p.HasExited } catch { return $false }
}

function Get-RunningLabels {
    @($script:Procs.Keys) | Where-Object { Test-Alive $_ }
}

function Stop-Proc {
    param([string]$Label)
    $p = $script:Procs[$Label]
    if ($null -eq $p) { return }
    try {
        if (-not $p.HasExited) {
            # dotnet run spawns the real app as a child, so kill the whole tree.
            Start-Process taskkill -ArgumentList '/PID', $p.Id, '/T', '/F' `
                -NoNewWindow -Wait -ErrorAction SilentlyContinue | Out-Null
            Write-Log "Stopped $Label (pid $($p.Id))"
        }
    } catch {
        Write-Log "Could not stop $Label : $_" 'warn'
    }
    $script:Procs.Remove($Label)
}

function Stop-All {
    $running = Get-RunningLabels
    if ($running.Count -eq 0) { Write-Log 'Nothing running.'; return }
    foreach ($l in $running) { Stop-Proc $l }
}

function Start-Proc {
    param(
        [string]$Label,
        [string]$Project,
        [string[]]$ExtraArgs = @()
    )

    if (Test-Alive $Label) {
        Write-Log "$Label is already running." 'warn'
        return
    }

    $cfg = $script:CfgBox.SelectedItem
    $dotnetArgs = @('run', '--project', $Project)
    if ($cfg -ne 'Debug') { $dotnetArgs += @('--configuration', $cfg) }
    $dotnetArgs += $ExtraArgs

    try {
        # Own console window: the server needs stdin for its admin console,
        # and redirecting it would make that unusable.
        $p = Start-Process -FilePath 'dotnet' -ArgumentList $dotnetArgs `
            -WorkingDirectory $Root -PassThru
        $script:Procs[$Label] = $p
        Write-Log "Started $Label ($cfg) - pid $($p.Id)"
    } catch {
        Write-Log "Failed to start $Label : $_" 'error'
    }
}

function Invoke-Build {
    $running = Get-RunningLabels
    if ($running.Count -gt 0) {
        $ans = [System.Windows.Forms.MessageBox]::Show(
            "These are running and will lock the output DLLs, making the build fail:`n`n  $($running -join "`n  ")`n`nStop them and build?",
            'Processes running', 'YesNo', 'Warning')
        if ($ans -ne 'Yes') { Write-Log 'Build cancelled.'; return }
        Stop-All
        Start-Sleep -Milliseconds 700
    }

    $cfg = $script:CfgBox.SelectedItem
    Write-Log "Building ($cfg)..."
    $script:BuildBtn.Enabled = $false
    $script:BuildBtn.Text = 'Building...'
    [System.Windows.Forms.Application]::DoEvents()

    $args = @('build', 'SpaceStation14.slnx')
    if ($cfg -ne 'Debug') { $args += @('--configuration', $cfg) }

    $out = & dotnet @args 2>&1
    $errors = $out | Select-String -Pattern ': error ' | Select-Object -First 12

    if ($LASTEXITCODE -eq 0) {
        Write-Log 'Build succeeded.'
    } else {
        Write-Log "Build FAILED (exit $LASTEXITCODE):" 'error'
        foreach ($e in $errors) { Write-Log "   $e" 'error' }
        if (-not $errors) { Write-Log '   (no ": error " lines - see full output in a terminal)' }
    }

    $script:BuildBtn.Text = 'Build'
    $script:BuildBtn.Enabled = $true
}

# ---------- UI ----------

$form                = New-Object System.Windows.Forms.Form
$form.Text           = 'Serenity Launcher'
$form.Size           = New-Object System.Drawing.Size(620, 470)
$form.StartPosition  = 'CenterScreen'
$form.BackColor      = [System.Drawing.Color]::FromArgb(32, 32, 36)
$form.ForeColor      = [System.Drawing.Color]::Gainsboro
$form.Font           = New-Object System.Drawing.Font('Segoe UI', 9)

function New-Btn {
    param([string]$Text, [int]$X, [int]$Y, [int]$W, [int]$H = 34, [string]$Accent = '60,60,66')
    $b = New-Object System.Windows.Forms.Button
    $b.Text = $Text
    $b.Location = New-Object System.Drawing.Point($X, $Y)
    $b.Size = New-Object System.Drawing.Size($W, $H)
    $rgb = $Accent -split ','
    $b.BackColor = [System.Drawing.Color]::FromArgb([int]$rgb[0], [int]$rgb[1], [int]$rgb[2])
    $b.ForeColor = [System.Drawing.Color]::White
    $b.FlatStyle = 'Flat'
    $b.FlatAppearance.BorderSize = 0
    $form.Controls.Add($b)
    return $b
}

# config row
$cfgLabel = New-Object System.Windows.Forms.Label
$cfgLabel.Text = 'Configuration'
$cfgLabel.Location = New-Object System.Drawing.Point(14, 16)
$cfgLabel.AutoSize = $true
$form.Controls.Add($cfgLabel)

$script:CfgBox = New-Object System.Windows.Forms.ComboBox
$script:CfgBox.Location = New-Object System.Drawing.Point(100, 12)
$script:CfgBox.Size = New-Object System.Drawing.Size(120, 24)
$script:CfgBox.DropDownStyle = 'DropDownList'
$script:CfgBox.BackColor = [System.Drawing.Color]::FromArgb(45, 45, 50)
$script:CfgBox.ForeColor = [System.Drawing.Color]::Gainsboro
[void]$script:CfgBox.Items.AddRange(@('Debug', 'Tools', 'Release'))
$script:CfgBox.SelectedIndex = 0
$form.Controls.Add($script:CfgBox)

$cntLabel = New-Object System.Windows.Forms.Label
$cntLabel.Text = 'Clients'
$cntLabel.Location = New-Object System.Drawing.Point(245, 16)
$cntLabel.AutoSize = $true
$form.Controls.Add($cntLabel)

$script:CountBox = New-Object System.Windows.Forms.NumericUpDown
$script:CountBox.Location = New-Object System.Drawing.Point(300, 12)
$script:CountBox.Size = New-Object System.Drawing.Size(50, 24)
$script:CountBox.Minimum = 1
$script:CountBox.Maximum = 4
$script:CountBox.Value = 1
$script:CountBox.BackColor = [System.Drawing.Color]::FromArgb(45, 45, 50)
$script:CountBox.ForeColor = [System.Drawing.Color]::Gainsboro
$form.Controls.Add($script:CountBox)

# buttons
$script:BuildBtn = New-Btn 'Build'         14  50 120 -Accent '70,70,78'
$srvBtn          = New-Btn 'Start Server'  144 50 130 -Accent '38,110,70'
$cliBtn          = New-Btn 'Start Client'  284 50 130 -Accent '38,90,140'
$bothBtn         = New-Btn 'Server+Client' 424 50 160 -Accent '92,64,140'
$stopBtn         = New-Btn 'Stop All'      14  92 120 -Accent '140,50,50'
$logsBtn         = New-Btn 'Open Logs'     144 92 130 -Accent '70,70,78'
$dataBtn         = New-Btn 'Open Data Dir' 284 92 130 -Accent '70,70,78'
$clrBtn          = New-Btn 'Clear Log'     424 92 160 -Accent '70,70,78'

# status
$script:StatusLabel = New-Object System.Windows.Forms.Label
$script:StatusLabel.Location = New-Object System.Drawing.Point(14, 136)
$script:StatusLabel.Size = New-Object System.Drawing.Size(570, 20)
$script:StatusLabel.Text = 'Server: stopped     Client: stopped'
$form.Controls.Add($script:StatusLabel)

# log
$script:LogBox = New-Object System.Windows.Forms.TextBox
$script:LogBox.Location = New-Object System.Drawing.Point(14, 162)
$script:LogBox.Size = New-Object System.Drawing.Size(570, 255)
$script:LogBox.Multiline = $true
$script:LogBox.ScrollBars = 'Vertical'
$script:LogBox.ReadOnly = $true
$script:LogBox.BackColor = [System.Drawing.Color]::FromArgb(22, 22, 25)
$script:LogBox.ForeColor = [System.Drawing.Color]::FromArgb(200, 200, 205)
$script:LogBox.Font = New-Object System.Drawing.Font('Consolas', 9)
$script:LogBox.Anchor = 'Top,Left,Right,Bottom'
$form.Controls.Add($script:LogBox)

# ---------- wiring ----------

$script:BuildBtn.Add_Click({ Invoke-Build })

$srvBtn.Add_Click({
    Start-Proc 'Server' 'Content.Server' @('/p:EmitCompilerGeneratedFiles=true')
})

$cliBtn.Add_Click({
    $n = [int]$script:CountBox.Value
    for ($i = 1; $i -le $n; $i++) {
        $label = if ($n -eq 1) { 'Client' } else { "Client$i" }
        Start-Proc $label 'Content.Client'
        if ($i -lt $n) { Start-Sleep -Milliseconds 400 }
    }
})

$bothBtn.Add_Click({
    Start-Proc 'Server' 'Content.Server' @('/p:EmitCompilerGeneratedFiles=true')
    Write-Log 'Waiting 6s for the server to come up...'
    Start-Sleep -Seconds 6
    $n = [int]$script:CountBox.Value
    for ($i = 1; $i -le $n; $i++) {
        $label = if ($n -eq 1) { 'Client' } else { "Client$i" }
        Start-Proc $label 'Content.Client'
        if ($i -lt $n) { Start-Sleep -Milliseconds 400 }
    }
})

$stopBtn.Add_Click({ Stop-All })
$clrBtn.Add_Click({ $script:LogBox.Clear() })

$logsBtn.Add_Click({
    $p = Join-Path $Root 'bin\Content.Server\logs'
    if (Test-Path $p) { Start-Process explorer.exe $p }
    else { Write-Log "No log dir yet at $p" 'warn' }
})

$dataBtn.Add_Click({
    $p = Join-Path $Root 'bin\Content.Server\data'
    if (Test-Path $p) { Start-Process explorer.exe $p }
    else { Write-Log "No data dir yet at $p" 'warn' }
})

# status poller
$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = 1000
$timer.Add_Tick({
    $running = @(Get-RunningLabels)
    $srv = if ($running -contains 'Server') { 'running' } else { 'stopped' }
    $clients = @($running | Where-Object { $_ -like 'Client*' })
    $cli = if ($clients.Count -eq 0) { 'stopped' } else { "running x$($clients.Count)" }
    $script:StatusLabel.Text = "Server: $srv     Client: $cli"
    $script:StatusLabel.ForeColor = if ($running.Count -gt 0) {
        [System.Drawing.Color]::FromArgb(120, 200, 130)
    } else {
        [System.Drawing.Color]::Gainsboro
    }
})
$timer.Start()

$form.Add_FormClosing({
    $running = @(Get-RunningLabels)
    if ($running.Count -gt 0) {
        $ans = [System.Windows.Forms.MessageBox]::Show(
            "Stop these before closing?`n`n  $($running -join "`n  ")",
            'Still running', 'YesNoCancel', 'Question')
        if ($ans -eq 'Cancel') { $_.Cancel = $true; return }
        if ($ans -eq 'Yes') { Stop-All }
    }
    $timer.Stop()
})

Write-Log "Repo: $Root"
Write-Log 'Server and client each open their own console window (the server needs stdin for admin commands).'

[void]$form.ShowDialog()
