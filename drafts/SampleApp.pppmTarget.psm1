<# pppm 1.0 {
    ShortName: temp
    AppFolder: ./relative/to/this/file
    Executable: ./relative/to/this/file
    DefaultArchitecture: x64
} #>

# Get a folder where globally installed pack files supposed to be
function Get-GlobalPacksFolder {
    return "$($targetApp.Dir)\packs"
}
Export-ModuleMember -Function Get-GlobalPacksFolder

# get all global packs via writing their content to the output stream
function Get-GloballyInstalledPacks {
    Get-ChildItem (Get-GlobalPacksFolder) -Filter *.pppm.psm1 |
        ForEach-Object { Get-Content $_ }
}
Export-ModuleMember -Function Get-GloballyInstalledPacks

# Get a folder where locally installed pack files supposed to be
function Get-LocalPacksFolder {
    return "$($pppm.WorkDir)\packs"
}
Export-ModuleMember -Function Get-LocalPacksFolder

# get all local packs via writing their content to the output stream
function Get-LocallyInstalledPacks {
    Get-ChildItem (Get-LocalPacksFolder) -Filter *.pppm.psm1 |
        ForEach-Object { Get-Content $_ }
}

Export-ModuleMember -Function Get-LocallyInstalledPacks

