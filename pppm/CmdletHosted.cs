using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Text;
using JetBrains.Annotations;

namespace pppm
{
    /// <summary>
    /// Extension methods for <see cref="ICmdletHosted"/>
    /// </summary>
    public static class CmdletHosted
    {
        /// <summary>
        /// Fluid assignment of a Cmdlet context to an <see cref="ICmdletHosted"/> implementation preferably when constructed 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="target"></param>
        /// <param name="host"></param>
        /// <returns></returns>
        [NotNull]
        public static T HostedIn<T>(this T target, PSCmdlet host) where T : ICmdletHosted
        {
            target.CmdletHost = host;
            return target;
        }
    }
    
    /// <summary>
    /// This interface helps components to work with individual Cmdlet instances
    /// (the execution context for pppm)
    /// Mostly used for output
    /// </summary>
    public interface ICmdletHosted
    {
        /// <summary>
        /// The Cmdlet to be used as a context for the implementer
        /// </summary>
        PSCmdlet CmdletHost { get; set; }
    }
}
