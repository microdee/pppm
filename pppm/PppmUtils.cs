using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Hjson;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using Unimpressive.Core.json;
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

    public class IncompatiblePppmScriptException : Exception
    {
        public IncompatiblePppmScriptException()
            : base($"Couldn't determined the required pppm script standard of the script")
        { }
        public IncompatiblePppmScriptException(string message) : base(message) { }
    }

    public enum ScriptUsage : byte
    {
        Unknown,
        Pack,
        App,
        AppCache,
        // Any = 0xFF
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

        /// <summary>
        /// Version of the currently loaded uppm.Core
        /// </summary>
        public static PppmVersion CompatibleScriptStandard { get; } = new PppmVersion(1,0);

        /// <summary>
        /// Function checking the compatibility of the script with the standard the current version of pppm compatible with.
        /// </summary>
        /// <param name="meta"></param>
        /// <param name="targetUsage"></param>
        /// <param name="throwOnIncompatible"></param>
        /// <returns></returns>
        public static bool IsScriptCompatible(JObject meta, ScriptUsage targetUsage, bool throwOnIncompatible = false)
        {
            if (!meta.TryGetFromPath("$.pppm", out string verText))
            {
                if(throwOnIncompatible)
                    throw new IncompatiblePppmScriptException();
                return false;
            }

            var splitted = verText.Split(' ');
            if (splitted.Length < 2)
            {
                if (throwOnIncompatible)
                    throw new IncompatiblePppmScriptException($"Invalid syntax while determining the required pppm standard of a script\n({verText})");
                return false;
            }

            if (!ScriptUsage.TryParse(splitted[1], out ScriptUsage usage))
            {
                if (throwOnIncompatible)
                    throw new IncompatiblePppmScriptException($"Specified usage of pppm script is invalid\n({verText})");
                return false;
            }

            if (usage != targetUsage)
            {
                if (throwOnIncompatible)
                    throw new IncompatiblePppmScriptException($"Wrong usage is specified for a pppm script\n(specified: {usage}, expected: {targetUsage})");
                return false;
            }

            if (!PppmVersion.TryParse(splitted[0], out var reqVersion))
            {
                if (throwOnIncompatible)
                    throw new IncompatiblePppmScriptException($"Invalid syntax while determining the required pppm standard of a script\n({verText})");
                return false;
            }

            if (
                CompatibleScriptStandard <= reqVersion ||
                CompatibleScriptStandard.Major != reqVersion.Major
            ) {
                if (throwOnIncompatible)
                    throw new IncompatiblePppmScriptException($"Trying to use a script incompatible with the current script standard of pppm.\n(pppm {Version} is compatible with {CompatibleScriptStandard}, but script requires {reqVersion})");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Get the pppm Hjson meta comment of a Powershell script/module. Returns null if there's none
        /// </summary>
        /// <param name="psScript"></param>
        /// <returns></returns>
        [CanBeNull]
        public static JObject GetPsMetaComment([NotNull] string psScript)
        {
            var metargx = new Regex(@"\<#\s*(?<hjson>{\s*pppm:.*?})\s*?#\>", RegexOptions.Singleline);
            var match = metargx.Match(psScript);
            if (!match.Success)
                return null;
            return JObject.Parse(HjsonValue.Parse(match.Groups["hjson"].Value));
        }
    }
}
