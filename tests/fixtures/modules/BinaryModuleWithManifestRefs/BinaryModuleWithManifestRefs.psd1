@{
    RootModule        = 'BinaryModuleWithManifestRefs.dll'
    ModuleVersion     = '1.0.0'
    GUID              = '55555555-5555-5555-5555-555555555555'
    Author            = 'ps2mcp-tests'
    Description       = 'Binary module fixture exercising NestedModules, FileList, RequiredModules.'
    NestedModules     = @(
        'BinaryModuleWithManifestRefs.psm1'
    )
    FileList          = @(
        'README.md'
        'LICENSE'
        'dependent/Helpers.ps1'
    )
    RequiredModules   = @(
        @{ ModuleName = 'Microsoft.PowerShell.Archive' }
    )
    CmdletsToExport   = @('Invoke-RefsThing')
}
