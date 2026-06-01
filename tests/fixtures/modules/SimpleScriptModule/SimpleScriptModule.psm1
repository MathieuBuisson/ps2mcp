function Get-SimpleThing {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string] $Name
    )
    "Hello, $Name"
}
