@{
    RootModule        = 'ModuleWithManifestRefs.psm1'
    ModuleVersion     = '1.0.0'
    GUID              = '22222222-2222-2222-2222-222222222222'
    Author            = 'ps2mcp-tests'
    Description       = 'Fixture exercising NestedModules, FileList, and RequiredModules manifest references.'
    NestedModules     = @(
        'SubModule.psm1'
    )
    FileList          = @(
        'README.md'
        'LICENSE'
    )
    RequiredModules   = @(
        @{ ModuleName = 'Az.Accounts' }
        @{ ModuleName = 'Az.Compute'; ModuleVersion = '5.0.0' }
    )
    FunctionsToExport = @('Get-Primary', 'Get-Secondary')
}
