<# pppm 1.0 {
    Name: demopack
    Version: 3.1.4
    Author: ME!
    ProjectUrl: https://something.something.com
    Description:
        '''
        my pack for the demo
        on multiple lines with indentation taken care of
        '''
    License:
        '''
        write the entire license text here
        or just paste an url: https://something.something.com 
        '''
    Repository: https://github.com/vvvvpm/uppm.vpdb.git
    TargetApp: ue4
    CompatibleAppVersion: >36 & <38.1
    ForceGlobal: true
    Dependencies: [
        md.stdl
        MyOtherPack 2
        Something 1.2
        Foobar 2.2.3.3 @ https://github.com/some/other.repo.git
    ]
} #>

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