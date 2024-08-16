<# :
    @echo off & chcp 65001 >nul & cd /d "%~dp0" & Title Disk Space Tracker

    ::========= SETTINGS =========
    set "Powershell_WindowStyle=Hidden"  :: Normal, Hidden, Minimized, Maximized
    set "Keep_Open=false"                :: Keep showing PowerShell console when script exit or crash
    set "Show_Loading=true"              :: Show cmd while preparing powershell
    set "Ensure_Local_Running=true"      :: If not launched from disk 'C', Re-Write in %temp% then execute
        set "Show_Writing_Lines=false"   :: Show lines writing in %temp% while preparing powershell
        set "Debug_Writting_Lines=false" :: Pause between each line writing (press a key to see next line)
    ::============================
    
    if "%Keep_Open%"=="true" (set "environment=k") else (set "environment=c")
    if "%Show_Writing_Lines%"=="true" set "Show_Loading=true"
    if "%Debug_Writting_Lines%"=="true" set "Show_Loading=true" && set "Show_Writing_Lines=true"
    if "%Show_Loading%"=="false" (
        if not DEFINED IS_MINIMIZED set IS_MINIMIZED=1 && start "" /min "%~f0" %* && exit
        ) else (if "%Show_Writing_Lines%"=="false" if "%Powershell_WindowStyle%"=="Hidden" mode con: cols=55 lines=3)
    echo. & echo  Loading...
;   if "%Ensure_Local_Running%"=="true" if "%~d0" NEQ "C:" ((
;       for /f "eol=; usebackq delims=" %%k in ("%~f0") do (
;           setlocal enabledelayedexpansion & set "line=%%k" & echo(!line!
;           if "%Show_Writing_Lines%"=="true" echo(!line! 1>&2
;           if "%Debug_Writting_Lines%"=="true" pause 1>&2 >nul
;           endlocal
;       )) > "%temp%\%~nx0" & start "" cmd.exe /%environment% "%temp%\%~nx0" %* & exit)

    cls & echo. & echo  Launching PowerShell...
    powershell /nologo /noprofile /executionpolicy bypass /windowstyle %Powershell_WindowStyle% /command ^
        "&{[ScriptBlock]::Create((gc """%~f0""" -Raw)).Invoke(@(&{$args}%*))}"

    if "%~dp0" NEQ "%temp%\" (exit) else ((goto) 2>nul & del "%~f0")
#>




#======================= PRE-LOAD =======================

Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type -TypeDefinition 'using System.Runtime.InteropServices; public class Dpi {[DllImport("user32")] public static extern bool SetProcessDPIAware();}'
[Dpi]::SetProcessDPIAware() | Out-Null; [System.Windows.Forms.Application]::EnableVisualStyles()




#==================== POPUP LOADING ====================

$loadingForm = New-Object System.Windows.Forms.Form; $loadingForm.Text = "Loading interface..."
$loadingForm.Size = New-Object System.Drawing.Size(300,100); $loadingForm.StartPosition = "CenterScreen"
$loadingForm.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::FixedDialog; $loadingForm.ControlBox = $false
$launch_progressBar = New-Object System.Windows.Forms.ProgressBar; $launch_progressBar.Location = New-Object System.Drawing.Point(10,30)
$launch_progressBar.Size = New-Object System.Drawing.Size(260,20); $launch_progressBar.Style = "Continuous"
$loadingLabel = New-Object System.Windows.Forms.Label; $loadingLabel.Text = "Loading interface..."
$loadingLabel.Location = New-Object System.Drawing.Point(10,10); $loadingLabel.Size = New-Object System.Drawing.Size(280,20)
$loadingForm.Controls.Add($launch_progressBar); $loadingForm.Controls.Add($loadingLabel)
$loadingForm.Show(); $loadingForm.Refresh()





#===================== GLOBAL VARS =====================

$launch_progressBar.Value = 10
$loadingLabel.Text = "Loading Global Vars..."



$script:timer = $null; $script:isStarting = $false; $script:initialFreeSpace = 0
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







#===================== MAIN WINDOW =====================

$launch_progressBar.Value = 15
$loadingLabel.Text = "Loading Main Window..."



$form = New-Object System.Windows.Forms.Form
$form.Text = "Disk Space Tracker"; $form.Size = New-Object System.Drawing.Size(535,275)
$form.StartPosition = "CenterScreen"; $form.BackColor = $darkBackground; $form.ForeColor = $darkForeground
$form.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::None





#==================== GLOBAL FUNCTIONS ====================

$launch_progressBar.Value = 30
$loadingLabel.Text = "Loading Functions..."



function gen {
    param(
        [Parameter(Mandatory=$true)] $container,
        [Parameter(Mandatory=$true)] [string]$type,
        [Parameter(Mandatory=$true)] [AllowEmptyString()] [string]$text,
        [Parameter(Mandatory=$true)] [int]$x, [int]$y, [int]$width, [int]$height,
        [Parameter(ValueFromRemainingArguments=$true)] $additionalProps
    )
    $control = New-Object "System.Windows.Forms.$type" -Property @{
        Text = $text; Location = New-Object System.Drawing.Point($x, $y)
        Size = New-Object System.Drawing.Size($width, $height)
        BackColor = $darkBackground; ForeColor = $darkForeground
    }
    switch ($control.GetType().Name) {
        "Button" {
            $control.FlatStyle = "Flat"; $control.BackColor = $buttonColor
            if ($container -eq $mainPanel) {
                $control.FlatAppearance.BorderColor = $accentColor
                $control.FlatAppearance.MouseOverBackColor = $accentColor
            }
        }
        "TextBox" { $control.BorderStyle = "FixedSingle" }
    }
    if ($additionalProps) {
        $additionalProps | ForEach-Object {
            $propName, $propValue = $_ -split '=', 2
            $propName = $propName.Trim(); $propValue = $propValue.Trim()
            if ($propValue -match '^New-Object' -or $propValue -match '^\@\{') {
                $control.$propName = Invoke-Expression $propValue }
            elseif ($propValue -match '^\[.*\]::') {
                $control.$propName = Invoke-Expression $propValue }
            elseif ($propName -eq "Font") {
                $fontParts= $propValue -split ',\s*'
                $fontName = $fontParts[0].Trim('"')
                $fontSize = [float]($fontParts[1].Trim())
                $fontStyle= if ($fontParts.Count -gt 2) { [System.Drawing.FontStyle]($fontParts[2].Trim() -replace '\s', '') } 
                            else { [System.Drawing.FontStyle]::Regular }
                $control.Font = New-Object System.Drawing.Font($fontName, $fontSize, $fontStyle) }
            elseif ($propName -eq "Padding") {
                $padValues = $propValue -split '\s+'
                $control.Padding = New-Object System.Windows.Forms.Padding($padValues[0], $padValues[1], $padValues[2], $padValues[3]) }
            elseif ($propName -in @("ForeColor", "BackColor", "BorderColor", "HoverColor")) {
                $colorValues = $propValue -split '\s+'
                $color = if ($colorValues.Count -eq 1) { [System.Drawing.ColorTranslator]::FromHtml($colorValues[0]) }
                         else { [System.Drawing.Color]::FromArgb($colorValues[0], $colorValues[1], $colorValues[2]) }
                switch ($propName) {
                    "ForeColor" { $control.ForeColor = $color }
                    "BackColor" { $control.BackColor = $color }
                    "BorderColor" { $control.FlatAppearance.BorderColor = $color }
                    "HoverColor" { $control.FlatAppearance.MouseOverBackColor = $color } } }
            elseif ($propName -eq "Dock") {
                $control.Dock = [System.Windows.Forms.DockStyle]::$propValue }
            elseif ($propName -eq "BorderStyle") {
                $control.BorderStyle = $propValue }
            else { $control.$propName = switch ($propValue) {
                        '$true' { $true }; '$false' { $false }
                        { $_ -match '^\d+$' } { [int]$_ }; default { $_ } } 
            }
        }
    }
    $container.Controls.Add($control); $control
}



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



function Browse-File {
    param ( [System.Windows.Forms.TextBox]$associatedTextBox )
    try {
        $dialog = New-Object System.Windows.Forms.OpenFileDialog; $dialog.InitialDirectory = [Environment]::GetFolderPath('Desktop')
        $dialog.Filter = "All Files (*.*)|*.*"; $dialog.Multiselect = $false; $dialog.Title = "Select a File or Folder"
        $dialog.ValidateNames = $false; $dialog.CheckFileExists = $false; $dialog.CheckPathExists = $true
        $dialog.FileName = "Current Folder"
        if ($dialog.ShowDialog() -eq 'OK') {
            $selectedPath = $dialog.FileName
            if ($selectedPath.EndsWith("\Current Folder")) { $selectedPath = [System.IO.Path]::GetDirectoryName($selectedPath) }
            if ($selectedPath.EndsWith(".lnk")) {
                $shell = New-Object -ComObject WScript.Shell
                $shortcut = $shell.CreateShortcut($selectedPath)
                $selectedPath = $shortcut.TargetPath
            }
            if ([System.IO.Directory]::Exists($selectedPath) -or [System.IO.File]::Exists($selectedPath)) {
                $associatedTextBox.Text = $selectedPath
            } else {
                $associatedTextBox.Text = $selectedPath
            }
        }
    } catch {
        $associatedTextBox.Text = "ERROR"
    }
}



function Browse-Process {
    param (
        [System.Windows.Forms.TextBox]$associatedTextBox
    )
    $processForm = New-Object System.Windows.Forms.Form
    $processForm.Text = "Select a Process"
    $processForm.Size = New-Object System.Drawing.Size(550, 550)
    $processForm.StartPosition = "CenterScreen"; $processForm.FormBorderStyle = "FixedDialog"
    $searchLabel = gen $processForm "Label" "Search :" 10 10 60 30 'Font=New-Object System.Drawing.Font("Segoe UI", 10)'
    $searchBox = gen $processForm "TextBox" "" 75 10 300 30 'Font=New-Object System.Drawing.Font("Segoe UI", 10)'
    $refreshButton = gen $processForm "Button" "Refresh" 385 10 100 30 'Font=New-Object System.Drawing.Font("Segoe UI", 10)'
    $processList = gen $processForm "ListBox" "" 10 50 520 410 'Font=New-Object System.Drawing.Font("Segoe UI", 10)' 'SelectionMode=One'
    $okButton = gen $processForm "Button" "OK" 12 460 510 40 'Font=New-Object System.Drawing.Font("Segoe UI", 10)'
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



function Get-AvailableDrives {
    $drives = Get-WmiObject Win32_LogicalDisk | Where-Object { $_.DriveType -eq 3 }  # 3 représente les disques locaux
    return $drives | ForEach-Object { 
        $label = if ($_.VolumeName) { $_.VolumeName } else { "Local Disk" }
        "$($_.DeviceID) ($label)"
    } | Sort-Object
}



function Get-FreeSpace { return [math]::Round((Get-PSDrive $script:selectedDrive.Substring(0,1)).Free/1MB, 2) }



function Format-ElapsedTime ([TimeSpan]$ts) { return "{0:00}:{1:00}:{2:00}" -f $ts.Hours, $ts.Minutes, $ts.Seconds }



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





#======================= TITLEBAR =======================

$launch_progressBar.Value = 20
$loadingLabel.Text = "Loading TitleBar..."



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
$titleBar = New-Object System.Windows.Forms.Panel
$titleBar.Dock = [System.Windows.Forms.DockStyle]::Top
$titleBar.Height = 30; $titleBar.BackColor = $accentColor
$titleLabel = New-Object System.Windows.Forms.Label
$titleLabel.Text = "Disk Space Tracker"; $titleLabel.ForeColor = $darkForeground
$titleLabel.Font = [System.Drawing.Font]::new("Arial", 12, [System.Drawing.FontStyle]::Bold)
$titleLabel.AutoSize = $true; $titleLabel.Location = [System.Drawing.Point]::new(10, 5)
$titleBar.Controls.Add($titleLabel)
Add-TitleBarButton "-" @(30, 30) @(470, 0) { $form.WindowState = [System.Windows.Forms.FormWindowState]::Minimized }
Add-TitleBarButton "X" @(40, 30) @(500, 0) { $form.Close() }
$titleBar.Add_MouseDown({ $script:isDragging = $true; $script:offset = $form.PointToScreen($_.Location) - $form.Location })
$titleBar.Add_MouseMove({ if ($script:isDragging) { $form.Location = $titleBar.PointToScreen($_.Location) - $script:offset } })
$titleBar.Add_MouseUp({ $script:isDragging = $false })
$form.Controls.Add($titleBar)





#====================== INIT PANELS ======================

$launch_progressBar.Value = 25
$loadingLabel.Text = "Loading Main Panels..."



$mainpanel = New-Object System.Windows.Forms.Panel
$mainpanel.Dock = [System.Windows.Forms.DockStyle]::Fill
$form.Controls.Add($mainpanel)

$AutoPanel = New-Object System.Windows.Forms.Panel
$AutoPanel.Dock = [System.Windows.Forms.DockStyle]::Fill
$AutoPanel.Visible = $false
$form.Controls.Add($AutoPanel)











#==================== [INTERFACE] MAIN PANEL ====================

$launch_progressBar.Value = 50
$loadingLabel.Text = "Loading Main Panel..."



$StartButton = gen $mainpanel "Button" "Start" 10 40 58 23
$AutoButton = gen $mainpanel "Button" "Auto" 67 40 58 23
$genReportButton = gen $mainpanel "Button" "Report" 134 40 58 23
$resetButton = gen $mainpanel "Button" "Reset" 191 40 58 23
$readLabel = gen $mainpanel "Label" "R: 0 MBs" 259 110 83 14 'Font=Consolas,7'
$writeLabel = gen $mainpanel "Label" "W: 0 MBs" 259 126 83 14 'Font=Consolas,7'
$includeUnitCheckBox = gen $mainpanel "CheckBox" "Copy unit" 261 40 83 23 "Checked=$null"
$timerLabel = gen $mainpanel "Label" "00:00:00" 259 80 80 23 'Font=Consolas,9'


$labels = @(
    @{Text="Initial free space:"; Y=80}, @{Text="Current free space:"; Y=120},
    @{Text="Current difference:"; Y=160}, @{Text="Added max diff:"; Y=200},
    @{Text="Deleted max diff:"; Y=240}
)
$textBoxes = @()
foreach ($label in $labels) {
    [void] (gen $mainpanel "Label" $label.Text 10 $label.Y 119 23)
    $textBoxes += gen $mainpanel "TextBox" "" 129 $label.Y 121 23 "ReadOnly=$true"
}
$copyButtons = @("Bytes", "MB", "GB")
foreach ($i in 2..4) {
    foreach ($j in 0..2) {
        $copyButton = gen $mainpanel "Button" "Copy $($copyButtons[$j])" (260 + $j*88) ($labels[$i].Y - 3) 88 26 'Tag=@{Index=$i; Unit=$copyButtons[$j]}'
        $copyButton.Add_Click({
            $tag = $this.Tag; $unit = $tag.Unit; $index = $tag.Index
            $value = switch($index) {
                2 { switch($unit) { "Bytes" { $script:currentDiffBytes }; "MB" { $script:currentDiffMB }; "GB" { $script:currentDiffGB } } }
                3 { switch($unit) { "Bytes" { [math]::Round($script:maxDiffAdded * 1MB / 1000) * 1000 }; "MB" { $script:maxDiffAdded }; "GB" { [math]::Round($script:maxDiffAdded / 1024, 2) } } }
                4 { switch($unit) { "Bytes" { [math]::Round([math]::Abs($script:maxDiffDeleted * 1MB / 1000)) * 1000 }; "MB" { [math]::Abs($script:maxDiffDeleted) }; "GB" { [math]::Round([math]::Abs($script:maxDiffDeleted) / 1024, 2) } } }
            }
            $textToCopy = if ($includeUnitCheckBox.Checked) { "$value $unit" } else { "$value" }
            [System.Windows.Forms.Clipboard]::SetText($textToCopy)
        })
    }
}


$driveComboBox = gen $mainpanel "ComboBox" "" 350 40 175 23 'DropDownStyle=DropDownList' 'FlatStyle=Flat' 'DrawMode=OwnerDrawFixed'
$driveComboBox.Add_DrawItem({
    param($unused, $e); $e.DrawBackground()
    if ($e.Index -ge 0) 
          { $brush = New-Object System.Drawing.SolidBrush($darkForeground)
            $e.Graphics.DrawString($driveComboBox.Items[$e.Index], $e.Font, $brush, $e.Bounds.Left, $e.Bounds.Top) }
    $e.DrawFocusRectangle()
})
$driveComboBox.Items.AddRange((Get-AvailableDrives))
$cDrive = $driveComboBox.Items | Where-Object { $_.StartsWith("C:") } | Select-Object -First 1
if ($cDrive) 
        { $driveComboBox.SelectedItem = $cDrive } 
else    { $driveComboBox.SelectedIndex = 0  } # Sélectionne le premier élément si le lecteur C: n'est pas trouvé
$driveComboBox.Add_SelectedIndexChanged({ $script:selectedDrive = $this.SelectedItem.Substring(0, 2)  }) # Prend seulement la lettre du lecteur et le ":"





#==================== [INTERFACE] AUTO PANEL ====================

$launch_progressBar.Value = 60
$loadingLabel.Text = "Loading Auto Panel..."



function Show-AutoInterface {
    $mainpanel.Visible = $false
    $AutoPanel.Visible = $true

    # Si le panel Auto est vide, ajoutez les contrôles
    if ($AutoPanel.Controls.Count -eq 0) {
        $backButton = gen $AutoPanel "Button" ($([char]0x2B9C) +"    Back") 10 40 90 25
        $backButton.Add_Click({
            $AutoPanel.Visible = $false
            $mainpanel.Visible = $true
        })

        # Ajout des onglets
        $tabControl = gen $AutoPanel "TabControl" "" 9 70 517 196

        $yPos = 10



        $waitLabel = gen $AutoPanel "Label" "NOT IMPLEMENTED" 359 40 180 20 'Font=Arial,10,Bold' "ForeColor=red"
        $checkBoxes = @("File exist", "File used", "Process exist", "Reg key exist")

        $tabPages = @("Freeze", "Start", "Reset")
        foreach ($tabName in $tabPages) {



            $tabPage = New-Object System.Windows.Forms.TabPage
            $tabPage.Text = $tabName
            $tabPage.BackColor = $darkBackground
            $tabPage.ForeColor = $darkForeground
            
            # Créer un panel pour chaque onglet
            $tabPanel = New-Object System.Windows.Forms.Panel
            $tabPanel.Dock = [System.Windows.Forms.DockStyle]::Fill
            $tabPage.Controls.Add($tabPanel)

            $xPos = 105; $yPos = 0

            $All_or_Any_groupBox = gen $tabPanel "GroupBox" "" 0 10 90 85
            $All_or_Any_groupBox_Label = gen $All_or_Any_groupBox "Label" "Logic :" 5 $yPos 80 19 'Font=Arial,8,Bold'
            $radioButton_All_Selected = gen $All_or_Any_groupBox "RadioButton" "All (and)" 5 ($yPos + 21) 80 20
            $radioButton_Any_Selected = gen $All_or_Any_groupBox "RadioButton" "Any (or)" 5 ($yPos + 40) 80 20

            $EnableAuto_Checkbox = gen $tabPanel "Button" "Enable" 0 137 90 30 'Font=Arial,9,Bold'
            $Report_Checkbox = gen $tabPanel "CheckBox" "Gen Report" 10 208 90 20
            $Report_Checkbox.Add_CheckedChanged({ Update-Conditions })

            # Contenu de l'onglet
            $ExeclineLabel = gen $tabPanel "CheckBox" "Execute, then wait closing" $xPos $yPos 185 20
            $ExecLinePath = gen $tabPanel "TextBox" "" ($xPos + 190) $yPos 156 20
            $ExecbrowseButton = gen $tabPanel "Button" "Browse" ($xPos + 345) $yPos 59 22
            $ExecbrowseButton.Add_Click({ Browse-File -associatedTextBox $ExecLinePath })
            $yPos += 20

            foreach ($text in $checkBoxes) {
                $checkBox = gen $tabPanel "CheckBox" $text $xPos $yPos 108 20
                $checkBox.Add_CheckedChanged({ Update-Conditions })
                $groupBox_YesNo = gen $tabPanel "GroupBox" "" ($xPos + 108) ($yPos-5) 80 22
                $radioButton_Yes = gen $tabPanel "RadioButton" $([char]0x2713) 0 4 40 20
                $radioButton_No = gen $tabPanel "RadioButton" $([char]0x2717) 40 4 40 20
                $groupBox_YesNo.Controls.AddRange(@($radioButton_Yes, $radioButton_No))
                $textBox = gen $tabPanel "TextBox" "" ($xPos + 190) ($yPos - 1) 156 20
                $textBox.Name = "TextBox_$($checkBoxes.IndexOf($text))_$tabName"
                $browseButton = gen $tabPanel "Button" "Browse" ($xPos + 345) ($yPos - 1) 59 22
                $scriptBlock = {
                    param($associatedTextBox, $labelText)
                    if ($labelText.ToLower().Contains("process")) 
                            { Browse-Process -associatedTextBox $associatedTextBox }
                    else    { Browse-File -associatedTextBox $associatedTextBox }
                }.GetNewClosure()
                $browseButton.Add_Click($ExecutionContext.InvokeCommand.NewScriptBlock("& {$scriptBlock} `$this.Parent.Controls['$($textBox.Name)'] `'$($checkBox.Text)'"))
                $yPos += 20
            }

            # Analyze Text File section
            $CheckboxlineLabel = gen $tabPanel "CheckBox" "Analyze text file" $xPos $yPos 121 20 "ForeColor=#ebdebf"
            $textBox_Line_Browse = gen $tabPanel "TextBox" "" ($xPos + 190) ($yPos - 1) 156 20 "Name=TextBox_Line_Browse"
            $browseButton_Line = gen $tabPanel "Button" "Browse" ($xPos + 345) ($yPos - 1) 59 22
            $browseButton_Line.Add_Click({ Browse-File -associatedTextBox $textBox_Line_Browse })
            $yPos += 20; $xPos += 17

            $ContentlineLabel = gen $tabPanel "Label" "Content to search :" ($xPos) $yPos 122 17 "ForeColor=#ebdebf"
            $textBox_Line_Content = gen $tabPanel "TextBox" "" ($xPos + 173) ($yPos - 3) 214 20
            $yPos += 19

            $line_number_Label = gen $tabPanel "Label" "At line n$([char]176)" $xPos ($yPos + 2) 60 20 "ForeColor=#ebdebf"
            $textBox_Line = gen $tabPanel "TextBox" "" ($xPos + 60) ($yPos - 1) 30 20
            $xPos += 25

            $radioButtons = @(
                @("Equal", "=", ($xPos + 83), 32),
                @("Contains", "$([char]33)=", ($xPos + 123), 40),
                @("NOT_Equal", "$([char]10038)", ($xPos + 168), 34),
                @("NOT_Contains", "$([char]33)$([char]10038)", ($xPos + 212), 44),
                @("StartsWith", "Start", ($xPos + 260), 54),
                @("EndsWith", "End", ($xPos + 317), 60)
            )
            foreach ($rb in $radioButtons) {
                ${"radioButton_$($rb[0])"} = gen $tabPanel "RadioButton" $rb[1] ([int]$rb[2]) $yPos ([int]$rb[3]) 20
                $tabPanel.Controls.Add(${"radioButton_$($rb[0])"})
            }

            $tabControl.TabPages.Add($tabPage)
        }

        $AutoPanel.Controls.Add($tabControl)
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
    while (-not $script:End_Trigger) { Update-Conditions; Start-Sleep -Seconds 1 }
    Write-Host "All conditions met!"
}

$AutoButton.Add_Click({ Show-AutoInterface })





#==================== [INTERFACE] CONVERTER ====================

$launch_progressBar.Value = 70
$loadingLabel.Text = "Loading Unit Converter..."



$units = @("B", "KB", "MB", "GB")
$CreatePanel = { param($x, $y, $w, $h); gen $mainpanel "Panel" "" $x $y $w $h "backcolor=Black" }
$CreateTextBox = { param($x, $y); gen $mainpanel "TextBox" "" $x $y 100 23 }
$CreateComboBox = { 
    param($x, $y) 
    $cb = gen $mainpanel "ComboBox" "" $x $y 47 23 'DropDownStyle=[System.Windows.Forms.ComboBoxStyle]::DropDownList'
    $cb.Items.AddRange($units);  $cb
}
$conversionLabel = gen $mainpanel "Label" "Unit Conversion" 355 80 100 10 'TextAlign=[System.Drawing.ContentAlignment]::MiddleCenter'
$conversionTextBox1 = &$CreateTextBox 360 97; $conversionTextBox2 = &$CreateTextBox 360 117
$conversionComboBox1 = &$CreateComboBox 465 96; $conversionComboBox2 = &$CreateComboBox 465 116
$conversionComboBox1.SelectedIndex = 2; $conversionComboBox2.SelectedIndex = 3 # MB default, GB default
&$CreatePanel 349 75 175 1; &$CreatePanel 349 144 176 1; &$CreatePanel 349 75 1 69; &$CreatePanel 524 75 1 69

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





#======================= EVENTS =======================

$launch_progressBar.Value = 80
$loadingLabel.Text = "Loading events..."



$resetButton.Add_Click({
    if ($script:timer) { $script:timer.Stop(); $script:timer.Dispose(); $script:timer = $null }
    $script:isStarting = $false
    $StartButton.Text = "Start"
    $script:initialFreeSpace = Get-FreeSpace
    $textBoxes[1].Text = if ($script:initialFreeSpace -gt 1000) { "$([math]::Round($script:initialFreeSpace / 1024, 2)) GB" } else { "$script:initialFreeSpace MB" }
    $textBoxes[0].Text = $textBoxes[2].Text = $textBoxes[3].Text = $textBoxes[4].Text = $null
    $script:maxDiffAdded = $script:maxDiffDeleted = $script:currentDiffBytes = $script:currentDiffMB = $script:currentDiffGB = 0
    $script:elapsedTime = [TimeSpan]::Zero
    $timerLabel.Text = "00:00:00"
    $driveComboBox.Enabled = $true
})



$StartButton.Add_Click({
    if ($script:isStarting) {
        $script:timer.Stop()
        $StartButton.Text = "Resume"
        $script:isStarting = $false
    } elseif ($StartButton.Text -eq "Resume") {
        $script:lastTickTime = [DateTime]::Now
        $script:timer.Start()
        $StartButton.Text = "Freeze"
        $script:isStarting = $true
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
        $StartButton.Text = "Freeze"
        $script:isStarting = $true
        $driveComboBox.Enabled = $false
    }
})



$genReportButton.Add_Click({
    # Logique pour générer le rapport (à implémenter plus tard)
    [System.Windows.Forms.MessageBox]::Show("Report generation not implemented yet.", "Info", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information)
})



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
    $form.Activate()
})





#==================== FIRST ACTIONS ====================

[math]::Round($difference * 1MB / 1000) * 1000
$script:initialFreeSpace = Get-FreeSpace
$textBoxes[1].Text = if ($script:initialFreeSpace -gt 1000) { "$([math]::Round($script:initialFreeSpace / 1024, 2)) GB" } else { "$script:initialFreeSpace MB" }



[System.Windows.Forms.Application]::Run($form)
