<# { pppm: 1.0 pack

    // only name and version are required the rest can be infered or optional
    Name: demopack
    Version: 3.1.4

    Author: ME!
    ProjectUrl: https://something.something.com
    IconImageUrl: https://something.something.com/someicon.png
    HeaderImageUrl: https://something.something.com/someheader.png
    Description:
        '''
        My pack for the demo
            On multiple lines with indentation taken care of
        This text won't be printed to console unless with the flag turned on
        '''
    License:
        '''
        write the entire license text here
        or just paste an url: https://something.something.com 
        '''
    Repository: https://github.com/vvvvpm/pppm.vpdb.git
    TargetApp: ue4
    CompatibleAppVersion: >4.20 & <4.24
    ForceGlobalScope: true
    Dependencies: [
        md.stdl
        MyOtherPack 2
        Something 1.2
        Foobar 2.2.3.3 @ https://github.com/some/other.repo.git
    ]
} #>

<#
    These functions are the ones which will be called
    by the action specified by the user for pppm.
#>

# only this function is mandatory
function Install {
    # ...
}
Export-ModuleMember -Function Install

# this function is recommended to have
function Uninstall {
    # ...
}
Export-ModuleMember -Function Uninstall

# Some pack specific behavior
function SayHi {
    # ...
}
Export-ModuleMember -Function SayHi