# Tesira Software Help Downloader

$BaseUrl = "https://tesira-software-help.biamp.com"

$DateStamp = Get-Date -Format "yyyy.MM.dd"

$RootFolder = ".\biamp tesira ttp $DateStamp"
$OutputRoot = Join-Path $RootFolder "TesiraDocs"

New-Item -ItemType Directory -Force -Path $RootFolder | Out-Null
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

Write-Host ""
Write-Host "=================================="
Write-Host "Tesira TTP Help Downloader"
Write-Host "=================================="
Write-Host "Output Folder:"
Write-Host $RootFolder
Write-Host ""

# ----------------------------------------------------------------------
# Download search_topics.js
# ----------------------------------------------------------------------

$SearchTopicsFile = Join-Path $RootFolder "search_topics.js"

Write-Host "Downloading search_topics.js..."

Invoke-WebRequest `
    "$BaseUrl/whxdata/search_topics.js" `
    -OutFile $SearchTopicsFile

$content = Get-Content `
    $SearchTopicsFile `
    -Raw `
    -Encoding UTF8

# ----------------------------------------------------------------------
# Extract topic URLs
# ----------------------------------------------------------------------

Write-Host "Extracting topic URLs..."

$urls = [regex]::Matches(
    $content,
    '\\"relUrl\\":\\"([^"]+?\.htm)\\"'
) |
ForEach-Object {
    $_.Groups[1].Value
} |
Sort-Object -Unique

Write-Host "Found $($urls.Count) total topics."

# ----------------------------------------------------------------------
# Filter to System Control docs
# ----------------------------------------------------------------------

$filtered = $urls |
Where-Object {
    $_ -match 'System_Control/(Attribute_Tables|Tesira_Text_Protocol)'
}

Write-Host "Filtered to $($filtered.Count) System Control topics."
Write-Host ""

# ----------------------------------------------------------------------
# Download topics
# ----------------------------------------------------------------------

$Success = 0
$Failed = @()

foreach($Topic in $filtered)
{
    $Url = [System.Uri]::new(
        $BaseUrl.TrimEnd('/') + '/' + $Topic
    ).AbsoluteUri

    $OutFile = Join-Path `
        $OutputRoot `
        ($Topic -replace '/','\')

    New-Item `
        -ItemType Directory `
        -Force `
        -Path (Split-Path $OutFile) | Out-Null

    try
    {
        Invoke-WebRequest `
            -Uri $Url `
            -OutFile $OutFile `
            -ErrorAction Stop

        $Success++

        Write-Host "[OK] $Topic"
    }
    catch
    {
        Write-Warning "[FAIL] $Topic"

        $Failed += $Topic
    }
}

# ----------------------------------------------------------------------
# Save failed URLs
# ----------------------------------------------------------------------

if($Failed.Count -gt 0)
{
    $FailedFile = Join-Path `
        $RootFolder `
        "failed_topics.txt"

    $Failed |
        Set-Content `
        $FailedFile `
        -Encoding UTF8
}

# ----------------------------------------------------------------------
# Create README
# ----------------------------------------------------------------------

$Readme = @"
# Biamp Tesira TTP Reference

Source:
https://tesira-software-help.biamp.com

Generated:
$(Get-Date)

Important Areas

- System_Control/Tesira_Text_Protocol
- System_Control/Attribute_Tables

Key Telephony Objects

- VoIP_Control_Status_Block
- VoIP_Call_State_Commands
- TI_Control_Status_Block
- TC_Call_State_Commands
- Dialer_Block
- HD-1_Block
- Device
- Session

Guidance for AI Tools

- Prefer Attribute Tables over general TTP documentation.
- Use current Software Help definitions.
- Use Service and Attribute tables as authoritative.
- Use current documentation before legacy TTP v4.2 references.
"@

$ReadmeFile = Join-Path `
    $RootFolder `
    "README.md"

$Readme |
    Set-Content `
    $ReadmeFile `
    -Encoding UTF8

# ----------------------------------------------------------------------
# Summary
# ----------------------------------------------------------------------

Write-Host ""
Write-Host "=================================="
Write-Host "Download Complete"
Write-Host "=================================="
Write-Host "Folder:      $RootFolder"
Write-Host "Downloaded:  $Success"
Write-Host "Failed:      $($Failed.Count)"

if($Failed.Count -gt 0)
{
    Write-Host ""
    Write-Host "Failed topics saved to:"
    Write-Host $FailedFile
}

Write-Host ""
Write-Host "Open this folder in VS Code:"
Write-Host $RootFolder
Write-Host ""