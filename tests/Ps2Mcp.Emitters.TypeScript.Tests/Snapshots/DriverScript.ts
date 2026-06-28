// Snapshot artifact: auto-generated test fixture. Not runtime source.
const invokePowerShellCommandScript = `
$ErrorActionPreference = 'Stop'
$modulePath = $env:PS2MCP_MODULE_PATH
$profilePath = $env:PS2MCP_PROFILE_PATH
$secureParameterNamesJson = $env:PS2MCP_SECURE_PARAMETER_NAMES
$sourceCommand = $env:PS2MCP_SOURCE_COMMAND
$serializationDepth = [int]$env:PS2MCP_SERIALIZATION_DEPTH
function Write-StructuredError {
    param(
        [string]$category,
        [string]$message,
        [string]$details
    )

    $payload = [ordered]@{
        category = $category
        message = $message
        sourceCommand = $sourceCommand
    }

    if (-not [string]::IsNullOrWhiteSpace($details)) {
        $payload.details = $details
    }

    [Console]::Error.WriteLine(($payload | ConvertTo-Json -Compress))
    exit 1
}

function Convert-SecureParameterValue {
    param(
        [string]$parameterName,
        $secureValue
    )

    if ($null -eq $secureValue) {
        return $null
    }

    if ($secureValue -is [string]) {
        return ConvertTo-SecureString -String $secureValue -AsPlainText -Force
    }

    if ($secureValue -is [System.Collections.IEnumerable] -and $secureValue -isnot [string]) {
        return @($secureValue | ForEach-Object {
            if ($_ -isnot [string]) {
                throw "Secure parameter '$parameterName' must be a string or array of strings."
            }

            ConvertTo-SecureString -String $_ -AsPlainText -Force
        })
    }

    throw "Secure parameter '$parameterName' must be a string or array of strings."
}

try {
    try {
        $argumentsJson = [Console]::In.ReadToEnd()
        $arguments = if ([string]::IsNullOrWhiteSpace($argumentsJson)) { @{} } else { ConvertFrom-Json -InputObject $argumentsJson -AsHashtable }
        if ($arguments -isnot [System.Collections.IDictionary]) {
            throw "Tool arguments must deserialize to an object."
        }

        $secureParameterNames = if ([string]::IsNullOrWhiteSpace($secureParameterNamesJson)) { @() } else { @(ConvertFrom-Json -InputObject $secureParameterNamesJson) }
    }
    catch {
        Write-StructuredError 'invalid input' 'Failed to parse tool arguments.' $_.Exception.Message
    }

    if (-not [string]::IsNullOrWhiteSpace($profilePath)) {
        if (-not (Test-Path -LiteralPath $profilePath -PathType Leaf)) {
            Write-StructuredError 'bootstrap profile failure' "Bootstrap profile file not found: $profilePath" $null
        }

        try {
            . $profilePath
        }
        catch {
            Write-StructuredError 'bootstrap profile failure' 'Bootstrap profile failed.' $_.Exception.Message
        }
    }

    try {
        Import-Module -Force $modulePath
    }
    catch {
        Write-StructuredError 'module load failure' 'Failed to import bundled module.' $_.Exception.Message
    }

    try {
        foreach ($secureParameterName in $secureParameterNames) {
            if ($arguments.Contains($secureParameterName)) {
                $arguments[$secureParameterName] = Convert-SecureParameterValue -parameterName $secureParameterName -secureValue $arguments[$secureParameterName]
            }
        }
    }
    catch {
        Write-StructuredError 'invalid input' 'Failed to bind secure parameter values.' $_.Exception.Message
    }

    try {
        $result = & $sourceCommand @arguments
    }
    catch {
        Write-StructuredError 'command execution failure' 'PowerShell command failed.' $_.Exception.Message
    }

    try {
        $result | ConvertTo-Json -Depth $serializationDepth -Compress
    }
    catch {
        Write-StructuredError 'serialization failure' 'Failed to serialize PowerShell output.' $_.Exception.Message
    }
}
catch {
    Write-StructuredError 'runtime internal error' 'Unexpected runtime failure.' $_.Exception.Message
}
`;
