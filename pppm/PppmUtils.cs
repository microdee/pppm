using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Unimpressive.Poweshell;

namespace pppm
{
    /// <summary>
    /// To support isolation for individual package management sessions
    /// this class allows to have extra state stored for said session
    /// </summary>
    /// <remarks>
    /// This class is partial so thematically necessary "global" states
    /// can be written in the respective source file.
    /// </remarks>
    public partial class PppmCmdletState : ICmdletHosted
    {
        /// <inheritdoc cref="ICmdletHosted"/>
        public PSCmdlet CmdletHost { get; set; }

        [CanBeNull] private string _workingDirectory = null;

        /// <summary>
        /// Assignable synonym to <see cref="Environment.CurrentDirectory"/>
        /// </summary>
        [NotNull] public string WorkingDirectory
        {
            get => _workingDirectory ?? CmdletHost.SessionState.Path.CurrentFileSystemLocation.Path;
            set
            {
                var prevworkdir = _workingDirectory ?? CmdletHost.SessionState.Path.CurrentFileSystemLocation.Path;
                if (!Directory.Exists(value))
                {
                    CmdletHost.WriteWarning("Trying to override the working directory with one which doesn't exist. Creating it.");
                    try
                    {
                        Directory.CreateDirectory(value);
                    }
                    catch (Exception e)
                    {
                        CmdletHost.WriteError("Overriding working directory failed, previously set or default is used.", targetObj: this);
                        return;
                    }
                }
                _workingDirectory = value;
            }
        }
    }

    /// <summary>
    /// Pppm utilities
    /// </summary>
    public static class Pppm
    {
        private static readonly ConcurrentDictionary<PSCmdlet, PppmCmdletState> _cmdletStates =
            new ConcurrentDictionary<PSCmdlet, PppmCmdletState>();

        /// <summary>
        /// Gets the attached state of a Cmdlet, or create one if it doesn't exist yet.
        /// </summary>
        /// <param name="cmdlet"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PppmCmdletState GetPppmState(this PSCmdlet cmdlet) => 
            _cmdletStates.GetOrAdd(cmdlet, key => new PppmCmdletState().HostedIn(key));

        /// <summary>
        /// Version of the currently loaded uppm.Core
        /// </summary>
        public static PppmVersion Version { get; } = new PppmVersion(typeof(PppmVersion).Assembly.GetName().Version);
    }
}
