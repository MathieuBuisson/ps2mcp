@{
    RootModule        = "ModuleWithDoubleQuotedManifest.psm1"
    ModuleVersion     = "1.0.0"
    GUID              = "66666666-6666-6666-6666-666666666666"
    Author            = "ps2mcp-tests"
    Description       = "Fixture mirroring ModuleWithManifestRefs but with all string values double-quoted."
    NestedModules     = @(
        "SubModule.psm1"
    )
    FileList          = @(
        "README.md"
        "LICENSE"
    )
    RequiredModules   = @(
        @{ ModuleName = "Az.Accounts" }
        @{ ModuleName = "Az.Compute"; ModuleVersion = "5.0.0" }
    )
    FunctionsToExport = @("Get-DoubleQuotedThing")
}
