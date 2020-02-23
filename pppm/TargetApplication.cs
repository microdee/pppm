using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Win32;
using pppm.Repositories;
using Unimpressive.Core;
using Unimpressive.Core.json;
using Unimpressive.Poweshell;

namespace pppm
{
    /// <summary>
    /// Machine type of an executable
    /// </summary>
    public enum Architecture
    {
        Native = 0,
        x86 = 0x014c,
        Itanium = 0x0200,
        x64 = 0x8664
    }

    /// <summary>
    /// Indicates a target installation scope for a package.
    /// </summary>
    /// <remarks>
    /// It's marked as flags so <code>InstalledPackageScope.Global | InstalledPackageScope.Local</code>
    /// means that work with both. Of course this can only apply to enumerating, packs have to decide on one.
    /// </remarks>
    [Flags]
    public enum InstalledPackageScope
    {
        None = 0,

        /// <summary>
        /// Packages in global scope are placed to a central location specified by the
        /// target application and presumably they are available to use everywhere at least
        /// in the scope of the current user.
        /// </summary>
        Global = 1,

        /// <summary>
        /// Packages in local scope are specific to the current working directory
        /// if the target application supports that.
        /// </summary>
        Local = 2,
    }

    /// <inheritdoc/>
    public partial class PppmCmdletState
    {
        private TargetApp _currentTargetApp;

        /// <summary>
        /// Known applications for the current Cmdlet session
        /// </summary>
        public readonly ConcurrentDictionary<string, TargetApp> KnownTargetApps = new ConcurrentDictionary<string, TargetApp>();

        private Stack<TargetApp> _currentAppStack;

        /// <summary>
        /// The globally inferred target application, unless a package specifies a different one
        /// </summary>
        public TargetApp CurrentTargetApp
        {
            get => _currentTargetApp;
            set
            {
                if (value == null)
                {
                    var stacktrace = new StackTrace();
                    CmdletHost.WriteError($"Trying to set current target application to null. Refused to do that. At:\n{stacktrace}");
                    return;
                }
                CmdletHost.WriteVerbose($"Set {value.ShortName} as current target application");
                _currentTargetApp = value;
            }
        }
    }

    /// <summary>
    /// Global methods for TargetApps for Cmdlet contexts
    /// </summary>
    public static class TargetAppExt
    {
        /// <summary>
        /// Tries to get a known target application
        /// </summary>
        /// <param name="sname"></param>
        /// <param name="app"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetKnownApp(this PSCmdlet cmdlet, string sname, out TargetApp app) =>
            cmdlet.GetPppmState().KnownTargetApps.TryGetValue(sname, out app);

        /// <summary>
        /// Tries to set the current target application to the specified one.
        /// </summary>
        /// <param name="sname"></param>
        /// <param name="app"></param>
        /// <returns></returns>
        public static bool TrySetCurrentApp(this PSCmdlet cmdlet, string sname, out TargetApp app)
        {
            if (!cmdlet.TryGetKnownApp(sname, out app)) return false;
            cmdlet.GetPppmState().CurrentTargetApp?.DefaultRepository?.UnregisterDefaultRepository();
            cmdlet.GetPppmState().CurrentTargetApp = app;
            app.DefaultRepository.RegisterDefaultRepository();
            return true;
        }
    }

    // TODO: Applications report their packages
    // TODO: Cache already reported packages
    /// <summary>
    /// Packages can target any associated application it manages packages for.
    /// This class contains information about such a target application
    /// </summary>
    public class TargetApp : ICmdletHosted
    {
        private bool _isInitialized;
        private string _defaultRepoRef;
        private PSModuleInfo _appModule;
        private FunctionInfo _getFolderForPackFunc;
        private FunctionInfo _getInstalledPacksFunc;
        private PSVariable _exePath;
        private PSVariable _appRoot;

        private Architecture _appArch = Architecture.Native;

        public PSCmdlet CmdletHost { get; set; }

        private void SafeGet<T>(Dictionary<string, T> dict, string key, string prefix, string type, out T value)
        {
            if (!dict.TryGetValue(key, out value))
                throw new KeyNotFoundException($"The {prefix}{key} {type} was not exported from the //TODO:appName module.");
        }

        [NotNull]
        public TargetApp Initialize(string appModulePath)
        {
            if (_isInitialized) return this;
            if (CmdletHost == null)
                throw new NullReferenceException("The host Cmdlet for this Target App was not set before initialization.");

            _isInitialized = true;

            if (string.IsNullOrWhiteSpace(appModulePath) ||
                !appModulePath.EndsWithCaseless(".3pmTarget.psm1") ||
                !File.Exists(appModulePath))
                throw new ArgumentException("Referenced path does not point to a valid pppm Target Application module.");

            var meta = Pppm.GetPsMetaComment(File.ReadAllText(appModulePath));
            if(meta == null)
                throw new ArgumentException("Referenced pppm Target Application module doesn't contain required meta comment.");

            Pppm.IsScriptCompatible(meta, ScriptUsage.App, true);

            ShortName = meta["ShortName"].ToString();
            _defaultRepoRef = meta["DefaultRepository"].ToString();

            if (!PackageRepository.TryCreateRepository(_defaultRepoRef, out var defRepo))
                throw new ArgumentException($"Cannot gather default repository for {ShortName}.");
            DefaultRepository = defRepo;

            if (meta.TryGetValue("DefaultArchitecture", out var defArchJt))
                DefaultArchitecture = defArchJt.ToObject<Architecture>();

            _appModule = CmdletHost.ImportModule(appModulePath);

            var vars = _appModule.ExportedVariables;
            SafeGet(vars, "executable", "$", "variable", out _exePath);
            SafeGet(vars, "appRoot", "$", "variable", out _appRoot);

            var funcs = _appModule.ExportedFunctions;
            SafeGet(funcs, "Get-FolderForPack", "", "function", out _getFolderForPackFunc);
            SafeGet(funcs, "Get-InstalledPacks", "", "function", out _getInstalledPacksFunc);

            CmdletHost.RemoveModule(_appModule.Name);

            return this;
        }

        public static readonly Architecture[] SupportedArchitectures = {Architecture.x64, Architecture.x86};

        /// <summary>
        /// Short, friendly name of the application (like "ue4" or "vvvv")
        /// </summary>
        public string ShortName { get; private set; }

        /// <summary>
        /// Desired architecture if it can't be determined from the target application (i.e.: the executable doesn't exist yet)
        /// </summary>
        public Architecture DefaultArchitecture { get; private set; } = Architecture.x64;

        /// <summary>
        /// Actual machine type of the target application
        /// </summary>
        public Architecture AppArchitecture
        {
            get
            {
                if (File.Exists(Executable))
                {
                    if (_appArch == Architecture.Native)
                    {
                        _appArch = GetArchitecture();
                        CmdletHost.WriteVerbose($"Determining architecture of {ShortName} for the first time ({_appArch})");
                    }
                }
                else _appArch = DefaultArchitecture;

                if (_appArch == Architecture.Native)
                {
                    _appArch = CmdletHost.PromptForEnum(
                        $"Please choose an arhitecture for {ShortName}:",
                        $"Default architecture for {ShortName} was not specified and it can't be automatically determined.",
                        SupportedArchitectures,
                        Architecture.x64
                    );
                }
                return _appArch;
            }
        }

        /// <summary>
        /// Try to get an installed package via a reference.
        /// </summary>
        /// <param name="packref"></param>
        /// <param name="pack"></param>
        /// <returns></returns>
        /// <remarks>Implementation must construct the full package including the script engine. Constructing the dependency tree is not required</remarks>
        public bool TryGetInstalledPackage(PartialPackageReference packref, InstalledPackageScope scope, out Package pack);

        /// <summary>
        /// Enumerate all installed packages in specified scope. Return false in your function to break enumeration.
        /// </summary>
        /// <param name="scope"></param>
        /// <param name="action"></param>
        public void EnumerateInstalledPackages(InstalledPackageScope scope, Func<Package, bool> action);
                
        /// <summary>
        /// Containing folder of the application. Override setter for validation and inference.
        /// </summary>
        public string AppRoot => _appRoot.Value.ToString();

        /// <summary>
        /// Path to Executable of the TargetApp
        /// </summary>
        public string Executable => _exePath.Value.ToString();

        /// <summary>
        /// The default package repository for this application
        /// </summary>
        public virtual IPackageRepository DefaultRepository { get; private set; }

        public TargetApp SetAsCurrentApp()
        {
            CmdletHost.GetPppmState().CurrentTargetApp?.DefaultRepository?.UnregisterDefaultRepository();
            DefaultRepository.RegisterRepository();
            CmdletHost.GetPppmState().KnownTargetApps.UpdateGeneric(ShortName, this);
            CmdletHost.GetPppmState().CurrentTargetApp = this;
            return this;
        }

        /// <summary>
        /// Gets the processor architecture or the machine type of the target application.
        /// </summary>
        /// <returns></returns>
        protected virtual Architecture GetArchitecture()
        {
            const int PE_POINTER_OFFSET = 60;
            const int MACHINE_OFFSET = 4;
            byte[] data = new byte[4096];
            using (Stream s = new FileStream(Executable, FileMode.Open, FileAccess.Read))
            {
                s.Read(data, 0, 4096);
            }
            // dos header is 64 bytes, last element, long (4 bytes) is the address of the PE header
            int PE_HEADER_ADDR = BitConverter.ToInt32(data, PE_POINTER_OFFSET);
            int machineUint = BitConverter.ToUInt16(data, PE_HEADER_ADDR + MACHINE_OFFSET);
            return (Architecture)machineUint;
        }

        /// <summary>
        /// Gets the file version of the target application
        /// </summary>
        /// <returns></returns>
        public virtual PppmVersion GetVersion()
        {
            var vinfo = FileVersionInfo.GetVersionInfo(Executable);
            return new PppmVersion(vinfo.FileMajorPart, vinfo.FileMinorPart, vinfo.FileBuildPart);
        }
    }
}
