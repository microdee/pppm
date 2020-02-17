<# { pppm: 1.0 app
    
    ShortName: temp
    DefaultArchitecture: x64
} #>

<#
    variables are determined when pppm reads this module
    and it will cache its results into the user's database
    of available target applications on their computer

    during processing TargetApp descriptions Current Folder
    is set to the folder containing this pack
#> 

[string] $executable = "$(Get-Location)\path\to.exe"
Export-ModuleMember -Variable $executable

[string] $appRoot = (Get-Location)
Export-ModuleMember -Variable $appRoot

<#
    As with any other powershell module packages and apps
    can have their private functions, variables or any other
    members they don't export. Those will be ignored by pppm
#>

function GetGlobalPacksFolder {
    return "$($targetApp.Dir)/packs"
}

function GetLocalPacksFolder {
    return "$($pppm.WorkDir)/packs"
}

<#
    Functions can be executed any time during a pppm session
    this includes:
      * during reading the module
      * whenever respective information is needed
      * even after the module is removed from the Powershellf
        context
        * Any variables used in the function outside of its scope
          are actually captured in their backing ScriptBlock,
          so they "keep their value" even if the module is removed
          from the session.

    When calling the functions Current Folder is left untouched
    which means it's supposedly the folder pppm was called from.
    However rather use $pppm.WorkDir because that can change
    depending on the situation.
#> 

# Get a target folder where a pack would be located or installed to.
function Get-FolderForPack {
    param (
        [bool] $isGlobal,
        [string] $packName,
        [string] $version
    )
    if($isGlobal) {
        return "$(GetGlobalPacksFolder)\$packName\$version"
    } else {
        return "$(GetLocalPacksFolder)\$packName\$version"
    }
}
Export-ModuleMember -Function Get-FolderForPack

# get all global packs via writing their content to the output stream
function Get-InstalledPacks {
    param (
        [bool] $isGlobal
    )
    Get-ChildItem (if($isGlobal) {GetGlobalPacksFolder} else {GetLocalPacksFolder}) -Filter *.3pm.psm1 |
        ForEach-Object { Get-Content $_ }
}
Export-ModuleMember -Function Get-InstalledPacks

