function Get-Server {
<#
.SYNOPSIS
Gets a server.
.PARAMETER Name
The server name.
.PARAMETER Environment
The deployment environment.
.PARAMETER Tags
The tags to filter by.
#>
[CmdletBinding()]
[OutputType('ServerInfo')]
param(
    [Parameter(Mandatory)]
    [string]$Name,

    [Parameter()]
    [ValidateSet('prod', 'staging', 'dev')]
    [string]$Environment = 'prod',

    [Parameter()]
    [string[]]$Tags = @()
)
}

function Set-Config {
<#
.SYNOPSIS
Sets a config value.
.PARAMETER MaxRetries
Maximum retry count.
.PARAMETER Prefix
The key prefix.
.PARAMETER Force
Overwrite.
.PARAMETER Password
The password.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateRange(1, 100)]
    [int]$MaxRetries,

    [Parameter()]
    [ValidatePattern('^[A-Z][a-zA-Z0-9]*$')]
    [string]$Prefix = 'Default',

    [Parameter()]
    [switch]$Force,

    [Parameter()]
    [SecureString]$Password
)
}
