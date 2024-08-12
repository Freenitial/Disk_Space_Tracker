<# :
    @echo off & chcp 65001 >nul & cd /d "%~dp0" & Title Disk Space Tracker

    ::========= SETTINGS =========
    set "Powershell_WindowStyle=Hidden"  :: Normal, Hidden, Minimized, Maximized
    set "Show_Loading=true"              :: Show cmd while preparing powershell
    set "Ensure_Local_Running=true"      :: If not launched from disk 'C', Re-Write in %temp% then execute
        set "Show_Writing_Lines=false"   :: Show lines writing in %temp% while preparing powershell
        set "Debug_Writting_Lines=false" :: Pause between each line writing (press a key to see next line)
    ::============================
 
    if "%Show_Writing_Lines%"=="true" set "Show_Loading=true"
    if "%Debug_Writting_Lines%"=="true" set "Show_Loading=true" && set "Show_Writing_Lines=true"
    if "%Show_Loading%"=="false" (
        if not DEFINED IS_MINIMIZED set IS_MINIMIZED=1 && start "" /min "%~dpnx0" %* && exit
        ) else (if "%Show_Writing_Lines%"=="false" if "%Powershell_WindowStyle%"=="Hidden" mode con: cols=55 lines=3)
    echo. & echo  Loading...
;   if "%Ensure_Local_Running%"=="true" if "%~d0" NEQ "C:" ((
;       for /f "eol=; usebackq delims=" %%k in ("%~f0") do (
;           setlocal enabledelayedexpansion & set "line=%%k" & echo(!line!
;           if "%Show_Writing_Lines%"=="true" echo(!line! 1>&2
;           if "%Debug_Writting_Lines%"=="true" pause 1>&2 >nul
;           endlocal
;       )) > "%temp%\%~nx0" & start "" cmd.exe /c "%temp%\%~nx0" %* & exit)

    cls & echo. & echo  Launching PowerShell...
    powershell /nologo /noprofile /executionpolicy bypass /windowstyle %Powershell_WindowStyle% /command ^
        "&{[ScriptBlock]::Create((gc """%~f0""" -Raw)).Invoke(@(&{$args}%*))}"

    if "%~dp0" NEQ "%temp%\" (exit) else ((goto) 2>nul & del "%~f0")
#>



Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type -TypeDefinition 'using System.Runtime.InteropServices; public class Dpi {[DllImport("user32")] public static extern bool SetProcessDPIAware();}'
[Dpi]::SetProcessDPIAware() | Out-Null; [System.Windows.Forms.Application]::EnableVisualStyles()

$loadingForm = New-Object System.Windows.Forms.Form; $loadingForm.Text = "Loading interface..."
$loadingForm.Size = New-Object System.Drawing.Size(300,100); $loadingForm.StartPosition = "CenterScreen"
$loadingForm.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::FixedDialog; $loadingForm.ControlBox = $false
$launch_progressBar = New-Object System.Windows.Forms.ProgressBar; $launch_progressBar.Location = New-Object System.Drawing.Point(10,30)
$launch_progressBar.Size = New-Object System.Drawing.Size(260,20); $launch_progressBar.Style = "Continuous"
$loadingLabel = New-Object System.Windows.Forms.Label; $loadingLabel.Text = "Loading interface..."
$loadingLabel.Location = New-Object System.Drawing.Point(10,10); $loadingLabel.Size = New-Object System.Drawing.Size(280,20)
$loadingForm.Controls.Add($launch_progressBar); $loadingForm.Controls.Add($loadingLabel)
$loadingForm.Show(); $loadingForm.Refresh()

$launch_progressBar.Value = 10
$loadingLabel.Text = "Loading interface..."



$script:timer = $null; $script:isRecording = $false; $script:initialFreeSpace = 0
$script:maxDiffAdded = $script:maxDiffDeleted = $script:currentDiffBytes = $script:currentDiffMB = $script:currentDiffGB = 0
$script:elapsedTime = [TimeSpan]::Zero; $script:lastTickTime = $null
$script:isDragging = $false
$script:offset = New-Object System.Drawing.Point
$script:selectedDrive = "C:"
$script:readCounter = $null
$script:writeCounter = $null
$script:End_Trigger = $false
$darkBackground = [System.Drawing.Color]::FromArgb(30, 30, 30)
$darkForeground = [System.Drawing.Color]::FromArgb(240, 240, 240)
$accentColor = [System.Drawing.Color]::FromArgb(0, 120, 215)
$buttonColor = [System.Drawing.Color]::FromArgb(60, 60, 60)

$form = New-Object System.Windows.Forms.Form
$form.Text = "Disk Space Tracker"; $form.Size = New-Object System.Drawing.Size(535,275)
$form.StartPosition = "CenterScreen"; $form.BackColor = $darkBackground; $form.ForeColor = $darkForeground
$form.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::None
$titleBar = New-Object System.Windows.Forms.Panel
$titleBar.Dock = [System.Windows.Forms.DockStyle]::Top
$titleBar.Height = 30; $titleBar.BackColor = $accentColor
$titleLabel = New-Object System.Windows.Forms.Label
$titleLabel.Text = "Disk Space Tracker"; $titleLabel.ForeColor = $darkForeground
$titleLabel.Font = [System.Drawing.Font]::new("Arial", 12, [System.Drawing.FontStyle]::Bold)
$titleLabel.AutoSize = $true; $titleLabel.Location = [System.Drawing.Point]::new(10, 5)
$titleBar.Controls.Add($titleLabel)

function Add-TitleBarButton($text, $size, $location, $onClick) {
    $button = New-Object System.Windows.Forms.Button
    $button.Text = $text; $button.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
    $button.FlatAppearance.BorderSize = 0; $button.BackColor = $accentColor
    $button.ForeColor = $darkForeground; $button.Font = [System.Drawing.Font]::new("Arial", 10, [System.Drawing.FontStyle]::Bold)
    $button.Size = [System.Drawing.Size]::new($size[0], $size[1])
    $button.Location = [System.Drawing.Point]::new($location[0], $location[1])
    $button.Add_Click($onClick)
    $titleBar.Controls.Add($button)
}
Add-TitleBarButton "-" @(30, 30) @(470, 0) { $form.WindowState = [System.Windows.Forms.FormWindowState]::Minimized }
Add-TitleBarButton "X" @(40, 30) @(500, 0) { $form.Close() }

$launch_progressBar.Value = 30
$loadingLabel.Text = "Loading interface..."

$titleBar.Add_MouseDown({
    $script:isDragging = $true
    $script:offset = $form.PointToScreen($_.Location) - $form.Location
})
$titleBar.Add_MouseMove({
    if ($script:isDragging) {
        $form.Location = $titleBar.PointToScreen($_.Location) - $script:offset
    }
})
$titleBar.Add_MouseUp({ $script:isDragging = $false })
$form.Controls.Add($titleBar)

$launch_progressBar.Value = 50
$loadingLabel.Text = "Loading interface..."

function Add-Control ($type, $props) {
    $control = New-Object $type
    $props.GetEnumerator() | ForEach-Object { $control.$($_.Key) = $_.Value }
    if ($control -is [System.Windows.Forms.Button]) {
        $control.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
        $control.BackColor = $buttonColor; $control.ForeColor = $darkForeground
        $control.FlatAppearance.BorderColor = $accentColor
        $control.FlatAppearance.MouseOverBackColor = $accentColor
    } elseif ($control -is [System.Windows.Forms.TextBox]) {
        $control.BorderStyle = [System.Windows.Forms.BorderStyle]::FixedSingle
        $control.BackColor = $darkBackground; $control.ForeColor = $darkForeground
    }
    [void] $form.Controls.Add($control)
    return $control
}

$recordButton = Add-Control "System.Windows.Forms.Button" @{
    Location = New-Object System.Drawing.Point(10,40); Size = New-Object System.Drawing.Size(58,23); Text = "Record"}
$ExecButton = Add-Control "System.Windows.Forms.Button" @{
    Location = New-Object System.Drawing.Point(67,40); Size = New-Object System.Drawing.Size(58,23); Text = "Exec"}
$genReportButton = Add-Control "System.Windows.Forms.Button" @{
    Location = New-Object System.Drawing.Point(134,40); Size = New-Object System.Drawing.Size(58,23); Text = "Report"}
$resetButton = Add-Control "System.Windows.Forms.Button" @{
    Location = New-Object System.Drawing.Point(191,40); Size = New-Object System.Drawing.Size(58,23); Text = "Reset"}
$readLabel = Add-Control "System.Windows.Forms.Label" @{
    Location = New-Object System.Drawing.Point(259,110); Size = New-Object System.Drawing.Size(83,14); Text = "R: 0 MBs"; ForeColor = $darkForeground; Font = New-Object System.Drawing.Font("Cascadia Code",7)}
$writeLabel = Add-Control "System.Windows.Forms.Label" @{
    Location = New-Object System.Drawing.Point(259,126); Size = New-Object System.Drawing.Size(83,14); Text = "W: 0 MBs"; ForeColor = $darkForeground; Font = New-Object System.Drawing.Font("Cascadia Code",7)}
$includeUnitCheckBox = Add-Control "System.Windows.Forms.CheckBox" @{
    Location = New-Object System.Drawing.Point(261,40); Size = New-Object System.Drawing.Size(83,23); Text = "Copy unit"; Checked = $true; ForeColor = $darkForeground}
$timerLabel = Add-Control "System.Windows.Forms.Label" @{
    Location = New-Object System.Drawing.Point(259,80); Size = New-Object System.Drawing.Size(80,23); Text = "T: 00:00:00"; Font = New-Object System.Drawing.Font("Consolas",7)}

$labels = @(
    @{Text="Initial free space:"; Y=80}, @{Text="Current free space:"; Y=120},
    @{Text="Current difference:"; Y=160}, @{Text="Added max diff:"; Y=200},
    @{Text="Deleted max diff:"; Y=240}
)
$textBoxes = @()
foreach ($label in $labels) {
    [void] (Add-Control "System.Windows.Forms.Label" @{Location=New-Object System.Drawing.Point(10,$label.Y); Size=New-Object System.Drawing.Size(119,23); Text=$label.Text})
    $textBoxes += Add-Control "System.Windows.Forms.TextBox" @{Location=New-Object System.Drawing.Point(129,$label.Y); Size=New-Object System.Drawing.Size(121,23); ReadOnly=$true}
}
$copyButtons = @("Bytes", "MB", "GB")
foreach ($i in 2..4) {
    foreach ($j in 0..2) {
        [void] (Add-Control "System.Windows.Forms.Button" @{
            Location = New-Object System.Drawing.Point((260 + $j*88), ($labels[$i].Y - 3))
            Size = New-Object System.Drawing.Size(88, 26)
            Text = "Copy $($copyButtons[$j])"
            Tag = @{Index=$i; Unit=$copyButtons[$j]}
        })
    }
}

$launch_progressBar.Value = 70
$loadingLabel.Text = "Loading interface..."

function Get-AvailableDrives {
    $drives = Get-WmiObject Win32_LogicalDisk | Where-Object { $_.DriveType -eq 3 }  # 3 représente les disques locaux
    return $drives | ForEach-Object { 
        $label = if ($_.VolumeName) { $_.VolumeName } else { "Local Disk" }
        "$($_.DeviceID) ($label)"
    } | Sort-Object
}

$driveComboBox = Add-Control "System.Windows.Forms.ComboBox" @{
    Location = New-Object System.Drawing.Point(350, 40)
    Size = New-Object System.Drawing.Size(175, 23)
    DropDownStyle = [System.Windows.Forms.ComboBoxStyle]::DropDownList
    BackColor = $darkBackground
    ForeColor = $darkForeground
    FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
}

# Appliquer un style personnalisé pour le contrôle ComboBox
$driveComboBox.DrawMode = [System.Windows.Forms.DrawMode]::OwnerDrawFixed
$driveComboBox.Add_DrawItem({
    param($unused, $e)
    $e.DrawBackground()
    if ($e.Index -ge 0) {
        $brush = New-Object System.Drawing.SolidBrush($darkForeground)
        $e.Graphics.DrawString($driveComboBox.Items[$e.Index], $e.Font, $brush, $e.Bounds.Left, $e.Bounds.Top)
    }
    $e.DrawFocusRectangle()
})

$driveComboBox.Items.AddRange((Get-AvailableDrives))
$cDrive = $driveComboBox.Items | Where-Object { $_.StartsWith("C:") } | Select-Object -First 1
if ($cDrive) {
    $driveComboBox.SelectedItem = $cDrive
} else {
    $driveComboBox.SelectedIndex = 0  # Sélectionne le premier élément si le lecteur C: n'est pas trouvé
}
$driveComboBox.Add_SelectedIndexChanged({
    $script:selectedDrive = $this.SelectedItem.Substring(0, 2)  # Prend seulement la lettre du lecteur et le ":"
})


function Browse-Process {
    param (
        [System.Windows.Forms.TextBox]$associatedTextBox
    )
    $processForm = [System.Windows.Forms.Form]::new()
    $processForm.Text = "Select a Process"
    $processForm.Size = [System.Drawing.Size]::new(550, 550)
    $processForm.StartPosition = "CenterScreen"
    $processForm.FormBorderStyle = "FixedDialog"
    $processForm.BackColor = [System.Drawing.ColorTranslator]::FromHtml("#2E2E2E")
    $processForm.ForeColor = [System.Drawing.ColorTranslator]::FromHtml("#FFFFFF")
    $addControl = { param($control) $processForm.Controls.Add($control) }
    $searchLabel = [System.Windows.Forms.Label]::new()
    $searchLabel.Text = "Search :"
    $searchLabel.Size = [System.Drawing.Size]::new(60, 30)
    $searchLabel.Location = [System.Drawing.Point]::new(10, 10)
    $searchLabel.Font = [System.Drawing.Font]::new("Segoe UI", 10)
    &$addControl $searchLabel
    $searchBox = [System.Windows.Forms.TextBox]::new()
    $searchBox.Size = [System.Drawing.Size]::new(300, 30)
    $searchBox.Location = [System.Drawing.Point]::new(75, 10)
    $searchBox.Font = [System.Drawing.Font]::new("Segoe UI", 10)
    $searchBox.ForeColor = [System.Drawing.ColorTranslator]::FromHtml("#FFFFFF")
    $searchBox.BackColor = [System.Drawing.ColorTranslator]::FromHtml("#3A3A3A")
    &$addControl $searchBox
    $refreshButton = [System.Windows.Forms.Button]::new()
    $refreshButton.Text = "Refresh"
    $refreshButton.Size = [System.Drawing.Size]::new(100, 30)
    $refreshButton.Location = [System.Drawing.Point]::new(385, 10)
    $refreshButton.Font = [System.Drawing.Font]::new("Segoe UI", 10)
    $refreshButton.BackColor = [System.Drawing.ColorTranslator]::FromHtml("#5A5A5A")
    $refreshButton.ForeColor = [System.Drawing.ColorTranslator]::FromHtml("#FFFFFF")
    $refreshButton.FlatStyle = "Flat"
    &$addControl $refreshButton
    $processList = [System.Windows.Forms.ListBox]::new()
    $processList.Location = [System.Drawing.Point]::new(10, 50)
    $processList.Size = [System.Drawing.Size]::new(520, 410)
    $processList.Font = [System.Drawing.Font]::new("Segoe UI", 10)
    $processList.BackColor = [System.Drawing.ColorTranslator]::FromHtml("#3A3A3A")
    $processList.ForeColor = [System.Drawing.ColorTranslator]::FromHtml("#FFFFFF")
    $processList.SelectionMode = "One"
    &$addControl $processList
    $okButton = [System.Windows.Forms.Button]::new()
    $okButton.Text = "OK"
    $okButton.Size = [System.Drawing.Size]::new(510, 40)
    $okButton.Location = [System.Drawing.Point]::new(12, 460)
    $okButton.Font = [System.Drawing.Font]::new("Segoe UI", 10)
    $okButton.BackColor = [System.Drawing.ColorTranslator]::FromHtml("#5A5A5A")
    $okButton.ForeColor = [System.Drawing.ColorTranslator]::FromHtml("#FFFFFF")
    $okButton.FlatStyle = "Flat"
    &$addControl $okButton
    $populateList = {
        $processList.Items.Clear()
        Get-Process | Group-Object -Property ProcessName | Sort-Object Name | ForEach-Object {
            $name = "$($_.Name).exe"
            $name += if ($_.Count -gt 1) { " (x$($_.Count))" } else { "" }
            $processList.Items.Add($name)
        }
    }
    &$populateList
    $refreshButton.Add_Click({ &$populateList })
    $searchBox.Add_TextChanged({
        $processList.Items.Clear()
        $searchText = "*$($searchBox.Text)*"
        $processes = Get-Process | Where-Object { $_.Name -like $searchText } | Group-Object -Property ProcessName | Sort-Object Name
        $processes | ForEach-Object {
            $name = "$($_.Name).exe"
            $name += if ($_.Count -gt 1) { " (x$($_.Count))" } else { "" }
            $processList.Items.Add($name)
        }
    })
    $onSelectProcess = {
        $selectedProcess = $processList.SelectedItem.Split(" ")[0]
        $associatedTextBox.Text = $selectedProcess
        $processForm.Close()
    }
    $okButton.Add_Click($onSelectProcess)
    $processList.Add_DoubleClick($onSelectProcess)
    $processForm.ShowDialog()
}




##############################################################################################################################################
##############################################################################################################################################

function Show-ExecInterface {
    $execForm = New-Object System.Windows.Forms.Form
    $execForm.Text = "Exec Interface"
    $execForm.Size = New-Object System.Drawing.Size(535,245)
    $execForm.StartPosition = "Manual"
    $execForm.Location = $form.PointToScreen([System.Drawing.Point]::new(0, 30))
    $execForm.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::None
    $execForm.BackColor = $darkBackground
    $execForm.Opacity = 0.95
    $contentPanel = New-Object System.Windows.Forms.Panel
    $contentPanel.Dock = [System.Windows.Forms.DockStyle]::Fill
    $contentPanel.Padding = New-Object System.Windows.Forms.Padding(0, 0, 0, 30)
    $execForm.Controls.Add($contentPanel)
    $backButton = New-Object System.Windows.Forms.Button
    $backButton.Text = "Back"
    $backButton.Size = New-Object System.Drawing.Size(80, 20)
    $backButton.Location = New-Object System.Drawing.Point(10, 10)
    $backButton.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
    $backButton.BackColor = $buttonColor
    $backButton.ForeColor = $darkForeground
    $backButton.Add_Click({ $execForm.Close() })
    $contentPanel.Controls.Add($backButton)
    $waitLabel = New-Object System.Windows.Forms.Label
    $waitLabel.Text = "NOT IMPLEMENTED Conditions to Freeze:"
    $waitLabel.Font = [System.Drawing.Font]::new("Arial", 10, [System.Drawing.FontStyle]::Bold)
    $waitLabel.Location = New-Object System.Drawing.Point(147, 11)
    $waitLabel.Size = New-Object System.Drawing.Size(180, 20)
    $waitLabel.ForeColor = $darkForeground
    $contentPanel.Controls.Add($waitLabel)
    $checkBoxes = @(
        "Exec and wait end", "File exist", "File not exist", "File not used", "File used",
        "Process exist", "Process not exist"
    )
    $yPos = 40
    foreach ($text in $checkBoxes) {
        $checkBox = New-Object System.Windows.Forms.CheckBox
        $checkBox.Text = $text
        $checkBox.Location = New-Object System.Drawing.Point(10, $yPos)
        $checkBox.Size = New-Object System.Drawing.Size(137, 20)
        $checkBox.ForeColor = $darkForeground
        $checkBox.Add_CheckedChanged({ Update-Conditions })
        $contentPanel.Controls.Add($checkBox)
        $textBox = New-Object System.Windows.Forms.TextBox
        $textBox.Location = New-Object System.Drawing.Point(150, ($yPos - 1))
        $textBox.Size = New-Object System.Drawing.Size(181, 20)
        $textBox.BackColor = $darkBackground
        $textBox.ForeColor = $darkForeground
        $textBox.Name = "TextBox_$($checkBoxes.IndexOf($text))"
        $contentPanel.Controls.Add($textBox)
        $browseButton = New-Object System.Windows.Forms.Button
        $browseButton.Text = "Browse"
        $browseButton.Location = New-Object System.Drawing.Point(330, ($yPos - 1))
        $browseButton.Size = New-Object System.Drawing.Size(59, 20)
        $browseButton.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
        $browseButton.BackColor = $buttonColor
        $browseButton.ForeColor = $darkForeground
        # Utiliser une closure pour capturer la référence correcte du TextBox
        $scriptBlock = {
            param($associatedTextBox, $labelText)
            # Vérification du texte du label pour déterminer si on doit ouvrir la boîte de dialogue des processus ou des fichiers
            if ($labelText.ToLower().Contains("process")) {
                Browse-Process -associatedTextBox $associatedTextBox
            } else {
                Browse-File -associatedTextBox $associatedTextBox
            }
        }.GetNewClosure()
        # Attach the click event with the correct TextBox and label text
        $browseButton.Add_Click($ExecutionContext.InvokeCommand.NewScriptBlock("& {$scriptBlock} `$this.Parent.Controls['$($textBox.Name)'] `'$($checkBox.Text)'"))
        $contentPanel.Controls.Add($browseButton)
        $yPos += 20
    }

    #==================================================================
    function Create-Control($type, $text, $x, $y, $width, $height) {
        $control = New-Object $type
        $control.Text = $text
        $control.Location = New-Object System.Drawing.Point($x, $y)
        $control.Size = New-Object System.Drawing.Size($width, $height)
        $control.ForeColor = $darkForeground
        $contentPanel.Controls.Add($control)
        return $control
    }
    function Set-CommonProperties($control) {
        $control.BackColor = $darkBackground
        $control.ForeColor = $darkForeground
    }
    $CheckboxlineLabel = Create-Control "System.Windows.Forms.CheckBox" "Analyze text file" 10 $yPos 135 20
    $CheckboxlineLabel.ForeColor = [System.Drawing.ColorTranslator]::FromHtml("#ebdebf")
    $textBox_Line_Browse = Create-Control "System.Windows.Forms.TextBox" "" 150 ($yPos - 1) 181 20
    Set-CommonProperties $textBox_Line_Browse
    $textBox_Line_Browse.Name = "TextBox_Line_Browse"
    $browseButton_Line = Create-Control "System.Windows.Forms.Button" "Browse" 330 ($yPos - 1) 59 20
    $browseButton_Line.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
    $browseButton_Line.BackColor = $buttonColor
    $browseButton_Line.Add_Click({ Browse-File -associatedTextBox $textBox_Line_Browse })
    $yPos += 21
    $ContentlineLabel = Create-Control "System.Windows.Forms.Label" "Content to search :" 10 $yPos 135 20
    $ContentlineLabel.ForeColor = [System.Drawing.ColorTranslator]::FromHtml("#ebdebf")
    $textBox_Line_Content = Create-Control "System.Windows.Forms.TextBox" "" 150 ($yPos - 3) 239 20
    Set-CommonProperties $textBox_Line_Content
    $yPos += 20
    $line_number_Label = Create-Control "System.Windows.Forms.Label" "Line n°" 10 ($yPos + 2) 47 20
    $line_number_Label.ForeColor = [System.Drawing.ColorTranslator]::FromHtml("#ebdebf")
    $textBox_Line = Create-Control "System.Windows.Forms.TextBox" "" 57 ($yPos - 1) 26 20
    Set-CommonProperties $textBox_Line
    $radioButtons = @(
        @("Equal", "=", 100, 32),
        @("Contains", "✶", 141, 34),
        @("NOT_Equal", "$([char]33)=", 186, 40),
        @("NOT_Contains", "$([char]33)✶", 232, 44),
        @("StartsWith", "Start", 283, 54),
        @("EndsWith", "End", 345, 60)
    )

    foreach ($rb in $radioButtons) {
        ${"radioButton_$($rb[0])"} = Create-Control "System.Windows.Forms.RadioButton" $rb[1] ([int]$rb[2]) $yPos ([int]$rb[3]) 20
    }
    #==================================================================

    $checkBox_Report = New-Object System.Windows.Forms.CheckBox
    $checkBox_Report.Text = "Gen Report"
    $checkBox_Report.Location = New-Object System.Drawing.Point(420, 100)
    $checkBox_Report.Size = New-Object System.Drawing.Size(100, 20)
    $checkBox_Report.ForeColor = $darkForeground
    $checkBox_Report.Add_CheckedChanged({ Update-Conditions })
    $contentPanel.Controls.Add($checkBox_Report)
    $startExecButton = New-Object System.Windows.Forms.Button
    $startExecButton.Text = "Record"
    $startExecButton.Location = New-Object System.Drawing.Point(420, 125)
    $startExecButton.Size = New-Object System.Drawing.Size(90, 30)
    $startExecButton.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
    $startExecButton.BackColor = $buttonColor
    $startExecButton.ForeColor = $darkForeground
    $startExecButton.Add_Click({ $execForm.Close() })
    $contentPanel.Controls.Add($startExecButton)
    $execForm.Add_Shown({ $execForm.Activate() })
    $execForm.ShowDialog($form)
}

function Browse-File {
    param (
        [System.Windows.Forms.TextBox]$associatedTextBox
    )
    $dialog = New-Object System.Windows.Forms.OpenFileDialog
    $dialog.InitialDirectory = [Environment]::GetFolderPath('Desktop')
    $dialog.Filter = "All Files (*.*)|*.*"
    $dialog.Multiselect = $false
    $dialog.Title = "Select a File or Folder"
    $dialog.ValidateNames = $false
    $dialog.CheckFileExists = $false
    $dialog.CheckPathExists = $true
    $dialog.FileName = "This Folder"
    if ($dialog.ShowDialog() -eq 'OK') {
        $selectedPath = $dialog.FileName
        if ($selectedPath.EndsWith("\This Folder")) {
            $selectedPath = [System.IO.Path]::GetDirectoryName($selectedPath)
        }
        if ([System.IO.Directory]::Exists($selectedPath)) {
            # Un dossier a été sélectionné
            $associatedTextBox.Text = $selectedPath
        } elseif ([System.IO.File]::Exists($selectedPath)) {
            # Un fichier a été sélectionné
            $associatedTextBox.Text = $selectedPath
        } else {
            # Le chemin sélectionné n'existe pas (probablement un nouveau fichier)
            $associatedTextBox.Text = $selectedPath
        }
    }
}

function Update-Conditions {
    # Fonction à implémenter pour vérifier les conditions
    $script:End_Trigger = $false
    # Logique pour vérifier toutes les conditions cochées
    # Si toutes les conditions sont remplies, définir $script:End_Trigger = $true
    Write-Host "Conditions update not implemented yet"
}

# Ajouter une fonction pour vérifier périodiquement les conditions
function Start-ConditionCheck {
    while (-not $script:End_Trigger) {
        Update-Conditions
        Start-Sleep -Seconds 1
    }
    Write-Host "All conditions met!"
}

$ExecButton.Add_Click({
    Show-ExecInterface
})

##############################################################################################################################################
##############################################################################################################################################



function Initialize-DiskSpeedCounters {
    try {
        $script:readCounter = New-Object System.Diagnostics.PerformanceCounter("LogicalDisk", "Disk Read Bytes/sec", "$($script:selectedDrive.Substring(0,1)):", $true)
        $script:writeCounter = New-Object System.Diagnostics.PerformanceCounter("LogicalDisk", "Disk Write Bytes/sec", "$($script:selectedDrive.Substring(0,1)):", $true)
    }
    catch {
        Write-Host "Error while initializing speed disk counter : $_"
        $script:readCounter = $script:writeCounter = $null
    }
}


$units = @("B", "KB", "MB", "GB")
$CreatePanel = { param($x, $y, $w, $h) Add-Control "System.Windows.Forms.Panel" `
    @{Location = [Drawing.Point]::new($x, $y); Size = [Drawing.Size]::new($w, $h); BackColor = [Drawing.Color]::Black} }
$CreateTextBox = { param($x, $y) Add-Control "System.Windows.Forms.TextBox" `
    @{Location = [Drawing.Point]::new($x, $y); Size = [Drawing.Size]::new(100, 23)} }
$CreateComboBox = { param($x, $y) $cb = Add-Control "System.Windows.Forms.ComboBox" `
    @{Location = [Drawing.Point]::new($x, $y); Size = [Drawing.Size]::new(47, 23); 
        DropDownStyle = [System.Windows.Forms.ComboBoxStyle]::DropDownList}; $cb.Items.AddRange($units); $cb }
$conversionLabel = Add-Control "System.Windows.Forms.Label" `
    @{Location = [Drawing.Point]::new(355, 80); Size = [Drawing.Size]::new(100, 10); 
        Text = "Unit Conversion"; TextAlign = [Drawing.ContentAlignment]::MiddleCenter}
$conversionTextBox1 = &$CreateTextBox 360 97
$conversionTextBox2 = &$CreateTextBox 360 117
$conversionComboBox1 = &$CreateComboBox 465 96
$conversionComboBox2 = &$CreateComboBox 465 116
$conversionComboBox1.SelectedIndex = 2  # MB default
$conversionComboBox2.SelectedIndex = 3  # GB default
&$CreatePanel 349 75 175 1
&$CreatePanel 349 144 176 1
&$CreatePanel 349 75 1 69
&$CreatePanel 524 75 1 69
function Convert-Units {
    param([string]$value, [string]$fromUnit, [string]$toUnit)
    $value = $value.Replace(',', '.').Replace(' ', '')
    $expr = [ScriptBlock]::Create("($value)")
    $numericValue = & $expr
    $bytesValue = switch ($fromUnit) { "B" { $numericValue } "KB" { $numericValue * 1KB } "MB" { $numericValue * 1MB } "GB" { $numericValue * 1GB } }
    switch ($toUnit) { "B" { $bytesValue } "KB" { $bytesValue / 1KB } "MB" { $bytesValue / 1MB } "GB" { $bytesValue / 1GB } }
}
function Update-Conversion {
    param($srcTextBox, $srcComboBox, $tgtTextBox, $tgtComboBox)
    try {
        $result = Convert-Units -value $srcTextBox.Text -fromUnit $srcComboBox.SelectedItem -toUnit $tgtComboBox.SelectedItem
        $tgtTextBox.Text = [math]::Round($result, 6).ToString()
    } catch {
        $tgtTextBox.Clear()
    }
}
$conversionTextBox1.Add_GotFocus({ $script:activeTextBox = $conversionTextBox1 })
$conversionTextBox2.Add_GotFocus({ $script:activeTextBox = $conversionTextBox2 })
$HandleTextChange = {
    if ($script:activeTextBox -eq $conversionTextBox1) { 
    Update-Conversion $conversionTextBox1 $conversionComboBox1 $conversionTextBox2 $conversionComboBox2 
    } else { 
    Update-Conversion $conversionTextBox2 $conversionComboBox2 $conversionTextBox1 $conversionComboBox1 }
}
$conversionTextBox1.Add_TextChanged($HandleTextChange)
$conversionTextBox2.Add_TextChanged($HandleTextChange)
$conversionComboBox1.Add_SelectedIndexChanged($HandleTextChange)
$conversionComboBox2.Add_SelectedIndexChanged($HandleTextChange)



function Get-FreeSpace { return [math]::Round((Get-PSDrive $script:selectedDrive.Substring(0,1)).Free/1MB, 2) }



function Format-ElapsedTime ([TimeSpan]$ts) { return "T: {0:00}:{1:00}:{2:00}" -f $ts.Hours, $ts.Minutes, $ts.Seconds }

function Update-Display {
    $currentFreeSpace = Get-FreeSpace
    $difference = [math]::Round($script:initialFreeSpace - $currentFreeSpace, 2)
    $textBoxes[1].Text = if ($currentFreeSpace -gt 1000) { "$([math]::Round($currentFreeSpace / 1024, 2)) GB" } else { "$currentFreeSpace MB" }
    $script:currentDiffGB = [math]::Round($difference / 1024, 2)
    $script:currentDiffMB = $difference
    $script:currentDiffBytes = [math]::Round($difference * 1MB / 1000) * 1000
    $textBoxes[2].Text = if ([Math]::Abs($difference) -gt 1000) { "$script:currentDiffGB GB" } else { "$script:currentDiffMB MB" }
    if ($difference -gt $script:maxDiffAdded) {
        $script:maxDiffAdded = $difference
        $textBoxes[3].Text = if ($script:maxDiffAdded -gt 1000) { "$([math]::Round($script:maxDiffAdded / 1024, 2)) GB" } else { "$script:maxDiffAdded MB" }
    }
    if ($difference -lt $script:maxDiffDeleted) {
        $script:maxDiffDeleted = $difference
        $textBoxes[4].Text = if ([Math]::Abs($script:maxDiffDeleted) -gt 1000) { "$([math]::Round($script:maxDiffDeleted / 1024, 2)) GB" } else { "$script:maxDiffDeleted MB" }
    }

    if ($script:readCounter -and $script:writeCounter) {
        $readSpeed = [math]::Round($script:readCounter.NextValue() / 1MB, 0)
        $writeSpeed = [math]::Round($script:writeCounter.NextValue() / 1MB, 0)
        $readLabel.Text = "R: $readSpeed MBs"
        $writeLabel.Text = "W: $writeSpeed MBs"
    }
}

$resetButton.Add_Click({
    if ($script:timer) { $script:timer.Stop(); $script:timer.Dispose(); $script:timer = $null }
    $script:isRecording = $false
    $recordButton.Text = "Record"
    $script:initialFreeSpace = Get-FreeSpace
    $textBoxes[1].Text = if ($script:initialFreeSpace -gt 1000) { "$([math]::Round($script:initialFreeSpace / 1024, 2)) GB" } else { "$script:initialFreeSpace MB" }
    $textBoxes[0].Text = $textBoxes[2].Text = $textBoxes[3].Text = $textBoxes[4].Text = $null
    $script:maxDiffAdded = $script:maxDiffDeleted = $script:currentDiffBytes = $script:currentDiffMB = $script:currentDiffGB = 0
    $script:elapsedTime = [TimeSpan]::Zero
    $timerLabel.Text = "00:00:00"
    $driveComboBox.Enabled = $true
})

$recordButton.Add_Click({
    if ($script:isRecording) {
        $script:timer.Stop()
        $recordButton.Text = "Resume"
        $script:isRecording = $false
    } elseif ($recordButton.Text -eq "Resume") {
        $script:lastTickTime = [DateTime]::Now
        $script:timer.Start()
        $recordButton.Text = "Freeze"
        $script:isRecording = $true
    } else {
        $script:initialFreeSpace = Get-FreeSpace
        $textBoxes[0].Text = if ($script:initialFreeSpace -gt 1000) { "$([math]::Round($script:initialFreeSpace / 1024, 2)) GB" } else { "$script:initialFreeSpace MB" }
        $textBoxes[2].Text = "0 MB"
        Initialize-DiskSpeedCounters
        $script:elapsedTime = [TimeSpan]::Zero
        $script:lastTickTime = [DateTime]::Now
        $script:timer = New-Object System.Windows.Forms.Timer
        $script:timer.Interval = 1000
        $script:timer.Add_Tick({
            $now = [DateTime]::Now
            $script:elapsedTime += $now - $script:lastTickTime
            $script:lastTickTime = $now
            $timerLabel.Text = Format-ElapsedTime $script:elapsedTime
            Update-Display
        })
        $script:timer.Start()
        $recordButton.Text = "Freeze"
        $script:isRecording = $true
        $driveComboBox.Enabled = $false
    }
})

$genReportButton.Add_Click({
    # Logique pour générer le rapport (à implémenter plus tard)
    [System.Windows.Forms.MessageBox]::Show("Report generation not implemented yet.", "Info", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information)
})

$form.Controls | Where-Object { $_ -is [System.Windows.Forms.Button] -and $_.Tag } | ForEach-Object {
    $_.Add_Click({
        $unit = $this.Tag.Unit
        $index = $this.Tag.Index
        $value = switch($index) {
            2 { switch($unit) { "Bytes" { $script:currentDiffBytes }; "MB" { $script:currentDiffMB }; "GB" { $script:currentDiffGB } } }
            3 { switch($unit) { "Bytes" { [math]::Round($script:maxDiffAdded * 1MB / 1000) * 1000 }; "MB" { $script:maxDiffAdded }; "GB" { [math]::Round($script:maxDiffAdded / 1024, 2) } } }
            4 { switch($unit) { "Bytes" { [math]::Round([math]::Abs($script:maxDiffDeleted * 1MB / 1000)) * 1000 }; "MB" { [math]::Abs($script:maxDiffDeleted) }; "GB" { [math]::Round([math]::Abs($script:maxDiffDeleted) / 1024, 2) } } }
        }
        $textToCopy = if ($includeUnitCheckBox.Checked) { "$value $unit" } else { "$value" }
        [System.Windows.Forms.Clipboard]::SetText($textToCopy)
    })
}
[math]::Round($difference * 1MB / 1000) * 1000
$script:initialFreeSpace = Get-FreeSpace
$textBoxes[1].Text = if ($script:initialFreeSpace -gt 1000) { "$([math]::Round($script:initialFreeSpace / 1024, 2)) GB" } else { "$script:initialFreeSpace MB" }


$form.Add_FormClosing({
    if ($script:timer) {
        $script:timer.Stop()
        $script:timer.Dispose()
    }
    if ($script:readCounter) { $script:readCounter.Dispose() }
    if ($script:writeCounter) { $script:writeCounter.Dispose() }
})

$form.Add_Load({
    # This event fires when the form is about to be shown
    $launch_progressBar.Value = 90
    $loadingLabel.Text = "Finalizing..."
})



$form.Add_Shown({ 
    $driveComboBox.Enabled = ($textBoxes[0].Text -eq "")
    $launch_progressBar.Value = 100
    $loadingLabel.Text = "Complete"
    $loadingForm.Close()
    [System.Windows.Forms.Application]::EnableVisualStyles();
    $form.Activate()
})


[System.Windows.Forms.Application]::Run($form)
