@{
    # Script module or binary module file associated with this manifest.
    RootModule           = 'LLMEmpoweredCommandPredictor.dll'

    # Version number of this module.
    ModuleVersion        = '1.0.0'

    # ID used to uniquely identify this module
    GUID                 = '1cda7101-34d4-4efd-887f-1923ff2b4b6e'

    # Author of this module
    Author               = 'LLM Command Predictor Team'

    # Company or vendor of this module
    CompanyName          = 'Hackathon Team'

    # Copyright statement for this module
    Copyright            = '(c) 2025 Hackathon Team. All rights reserved.'

    # Description of the functionality provided by this module
    Description          = 'LLM Empowered Command Predictor for PowerShell'

    # Minimum version of the PowerShell engine required by this module
    PowerShellVersion    = '7.0'

    # Minimum version of the .NET Framework required by this module
    # Note: For .NET 6+ projects, use CompatiblePSEditions instead of DotNetFrameworkVersion
    # DotNetFrameworkVersion = '4.7.2'
    
    # Compatible PowerShell editions
    CompatiblePSEditions = @('Core')
    
    # Required modules that must be loaded before this module
    RequiredModules      = @()
    
    # Required assemblies that must be loaded prior to importing this module
    RequiredAssemblies   = @()
    
    # Type files (.ps1xml) to be loaded when importing this module
    TypesToProcess       = @()
    
    # Format files (.ps1xml) to be loaded when importing this module
    FormatsToProcess     = @()

    # Functions to export from this module, for best performance, do not use wildcards and do not delete the entry, use an empty array if there are no functions to export.
    FunctionsToExport    = @()

    # Cmdlets to export from this module, for best performance, do not use wildcards and do not delete the entry, use an empty array if there are no cmdlets to export.
    CmdletsToExport      = @()

    # Variables to export from this module
    VariablesToExport    = @()

    # Aliases to export from this module, for best performance, do not use wildcards and do not delete the entry, use an empty array if there are no aliases to export.
    AliasesToExport      = @()

    # Private data to pass to the module specified in RootModule/ModuleToProcess. This may also contain a PSData hashtable with additional module metadata used by PowerShell.
    PrivateData          = @{
        PSData = @{
            # Tags applied to this module. These help with module discovery in online galleries.
            Tags = @('PowerShell', 'Predictor', 'LLM', 'AI', 'CommandLine', 'IntelliSense', 'Hackathon')

            # A URL to the license for this module.
            # LicenseUri = ''

            # A URL to the main website for this project.
            # ProjectUri = ''

            # A URL to an icon representing this module.
            # IconUri = ''

            # ReleaseNotes of this module
            # ReleaseNotes = ''

            # Prerelease string of this module
            # Prerelease = ''

            # Flag to indicate whether the module requires explicit user acceptance for install/update/save
            # RequireLicenseAcceptance = $false

            # External dependent modules of this module
            # ExternalModuleDependencies = @()
        } # End of PSData hashtable
    } # End of PrivateData hashtable

    # HelpInfo URI of this module
    # HelpInfoURI = ''

    # Default prefix for commands exported from this module. Override the default prefix using Import-Module -Prefix.
    # DefaultCommandPrefix = ''
}
