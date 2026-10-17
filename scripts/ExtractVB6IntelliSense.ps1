$txtPath = 'C:\Programming\IDE VB6 x64\LIBRARY VB6\COM_TypeLib_VB_{fcfb3d2e-a0fa-1068-a738-08002b3371b5}.txt'
$jsonPath = 'C:\Programming\IDE VB6 x64\src\VB7x64.IDE\Resources\VB6IntelliSense.json'

$interfaceMembers = @{ }
$coclassMap = @{ }

$currentInterface = $null
$collect = $false

Get-Content -Path $txtPath -Encoding UTF8 | ForEach-Object {
    $line = $_.Trim()
    if ($line -match '^coclass\s+(\w+)') {
        $coclass = $Matches[1]
        $coclassMap[$coclass] = $null
    }
    elseif ($line -match '^\[default\]\s+interface\s+(\w+);') {
        $iface = $Matches[1]
        if ($coclass) { $coclassMap[$coclass] = $iface }
    }
    elseif ($line -match '^interface\s+(\w+)\s*:\s*IUnknown') {
        $currentInterface = $Matches[1]
        $interfaceMembers[$currentInterface] = @()
        $collect = $true
    }
    elseif ($collect -and $line -match '^\};') {
        $collect = $false
        $currentInterface = $null
    }
    elseif ($collect -and $line -match '^HRESULT\s+(\w+)\s*\(') {
        $member = $Matches[1]
        $interfaceMembers[$currentInterface] += $member
    }
}

# Build final object mapping coclass to members via its interface
$result = @{}
foreach ($coclass in $coclassMap.Keys) {
    $iface = $coclassMap[$coclass]
    if ($iface -and $interfaceMembers.ContainsKey($iface)) {
        $result[$coclass] = @{ interface = $iface; members = $interfaceMembers[$iface] }
    }
}

# Write JSON pretty
$result | ConvertTo-Json -Depth 5 | Set-Content -Path $jsonPath -Encoding UTF8

Write-Host "IntelliSense JSON generated at $jsonPath"
