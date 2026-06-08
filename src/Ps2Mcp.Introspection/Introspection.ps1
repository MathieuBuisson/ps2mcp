# Introspection.ps1 - Binary module metadata extraction.
# Invoked as `pwsh -NoProfile -NonInteractive -File Introspection.ps1 -ModulePath <path>`.
# Emits a single JSON object to stdout describing every command the module
# exports. The C# BinaryModuleIntrospector deserializes that object via the
# source-generated BinaryIntrospectionJsonSerializerContext.
#
# JSON line endings are LF on every platform; ConvertTo-Json on PowerShell 7
# emits a trailing newline that we strip in the C# layer. Errors (module path
# not found, Import-Module failure) are written to stderr; the C# layer maps
# them to BinaryModuleIntrospectionException and surfaces the captured stderr
# to the user.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $ModulePath
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $ModulePath)) {
    [Console]::Error.WriteLine("Module path not found: $ModulePath")
    exit 2
}

try {
    Import-Module -Name $ModulePath -Force -ErrorAction Stop
} catch {
    [Console]::Error.WriteLine("Failed to import module '$ModulePath': $($_.Exception.Message)")
    exit 3
}

$moduleName = [System.IO.Path]::GetFileNameWithoutExtension($ModulePath)

try {
    $commands = @(Get-Command -Module $moduleName -CommandType Cmdlet, Function -ErrorAction Stop)
} catch {
    [Console]::Error.WriteLine("Failed to enumerate commands in '$ModulePath': $($_.Exception.Message)")
    exit 4
}

# All aliases defined by the module, in one pass. Used to attach an `aliases` array
# to each command entry. Limited to module-defined aliases (not built-in alias
# plumbing like `ls` for Get-ChildItem) so the surface matches what a user typing
# `Import-Module Microsoft.PowerShell.Management` would see. Get-Alias has no
# -Module filter in PowerShell 7, so the module name is applied post-hoc via the
# ModuleName property on each alias entry.
$allModuleAliases = @(Get-Alias -ErrorAction SilentlyContinue | Where-Object { $_.ModuleName -eq $moduleName })

$commandPayloads = @()
foreach ($cmd in $commands) {
    $paramSetPayloads = @()
    if ($cmd.ParameterSets) {
        foreach ($pset in $cmd.ParameterSets) {
            $paramSetPayloads += @{
                name      = [string]$pset.Name
                isDefault = [bool]$pset.IsDefault
            }
        }
    }
    $setNames = @($paramSetPayloads | ForEach-Object { $_.name })

    $paramPayloads = @()
    if ($cmd.Parameters) {
        foreach ($entry in $cmd.Parameters.GetEnumerator()) {
            $pname = $entry.Key
            $pinfo = $entry.Value
            $fullType = ''
            if ($pinfo.ParameterType) {
                $fullType = $pinfo.ParameterType.FullName
                if (-not $fullType) { $fullType = $pinfo.ParameterType.Name }
            }
            $isSwitch = ($pinfo.SwitchParameter -eq $true)
            $pAliases = @()
            if ($pinfo.Aliases) { $pAliases = @($pinfo.Aliases) }

            $isMandatory = $false
            $position = [int]::MinValue
            $vfp = $false
            $vfpbpn = $false
            $vfra = $false
            $pattr = @($pinfo.Attributes) | Where-Object {
                $_ -is [System.Management.Automation.ParameterAttribute]
            } | Select-Object -First 1
            if ($pattr) {
                $isMandatory = [bool]$pattr.Mandatory
                $position = if ($null -ne $pattr.Position) { [int]$pattr.Position } else { [int]::MinValue }
                $vfp = [bool]$pattr.ValueFromPipeline
                $vfpbpn = [bool]$pattr.ValueFromPipelineByPropertyName
                $vfra = [bool]$pattr.ValueFromRemainingArguments
            }

            # Determine which parameter sets this parameter actually belongs to.
            # CommandMetadata exposes parameter-set membership per parameter via
            # the set's Parameters collection (ReadOnlyCollection<CommandParameterInfo>);
            # we iterate the command-level sets and check each element's Name
            # property, rather than copying the full set list into every parameter
            # (which would over-report membership for mutually exclusive sets).
            $pSets = @()
            if ($cmd.ParameterSets) {
                foreach ($pset in $cmd.ParameterSets) {
                    if ($pset.Parameters | Where-Object { $_.Name -eq $pname }) {
                        $pSets += [string]$pset.Name
                    }
                }
            }

            $paramPayloads += @{
                name                            = [string]$pname
                type                            = [string]$fullType
                isMandatory                     = $isMandatory
                position                        = $position
                valueFromPipeline               = $vfp
                valueFromPipelineByPropertyName = $vfpbpn
                valueFromRemainingArguments     = $vfra
                aliases                         = $pAliases
                isSwitch                        = $isSwitch
                parameterSets                   = $pSets
            }
        }
    }

    $outputTypes = @()
    if ($cmd.OutputType) {
        $outputTypes = @($cmd.OutputType | ForEach-Object {
            if ($_.Type) { $_.Type.FullName } else { $_.ToString() }
        })
    }

    $supportsShouldProcess = $false
    if ($cmd.Parameters) {
        $supportsShouldProcess = [bool]$cmd.Parameters.ContainsKey('WhatIf') -or
            [bool]$cmd.Parameters.ContainsKey('Confirm')
    }

    $helpUri = $null
    if ($cmd.HelpFile) {
        $helpUri = [string]$cmd.HelpFile
    }

    $defaultSet = ''
    if ($cmd.DefaultParameterSet) {
        $defaultSet = [string]$cmd.DefaultParameterSet
    }

    # Module-defined aliases whose Definition matches this command's resolved name.
    # For a binary module, ResolvedCommand on a cmdlet is the cmdlet itself, so we
    # match by the cmdlet's Name directly. For an alias entry, the Definition of
    # Get-Alias is the resolved cmdlet name (e.g. "Get-ChildItem" for "gci"), so
    # matching by Name works for both surfaced and resolved views.
    $commandAliases = @($allModuleAliases | Where-Object { $_.Definition -eq $cmd.Name } | ForEach-Object { $_.Name })

    $commandPayloads += @{
        name                    = [string]$cmd.Name
        commandType             = [string]$cmd.CommandType
        supportsShouldProcess   = $supportsShouldProcess
        supportsPaging          = $false
        supportsTransactions    = $false
        defaultParameterSetName = $defaultSet
        helpUri                 = $helpUri
        outputType              = $outputTypes
        aliases                 = $commandAliases
        parameters              = $paramPayloads
        parameterSets           = $paramSetPayloads
    }
}

$payload = [ordered]@{
    moduleName = $moduleName
    modulePath = $ModulePath
    commands   = $commandPayloads
}

$json = ConvertTo-Json -InputObject $payload -Depth 10 -Compress
[Console]::Out.Write($json)
[Console]::Out.Flush()
exit 0
