using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unimpressive.Core;

namespace pppm
{
    public abstract class Versionable
    {
        /// <summary>
        /// Version of the referred package
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Is the versioning of referred pack semantical?
        /// </summary>
        /// <param name="semversion">Parsed version</param>
        /// <returns>True if semantical</returns>
        public bool IsSemanticalVersion(out PppmVersion semversion)
        {
            var res = PppmVersion.TryParse(Version, out semversion);
            if (!res && IsLatest)
            {
                semversion = new PppmVersion(int.MaxValue);
                res = true;
            }
            return res;
        }

        /// <summary>
        /// Is the versioning of referred pack is not semantical?
        /// </summary>
        public bool IsSpecialVersion => Version != null && !IsSemanticalVersion(out _) && !IsLatest;

        /// <summary>
        /// Is the reference points to the latest version of the referred pack?
        /// </summary>
        public bool IsLatest => Version != null && Version.EqualsCaseless("latest");
    }
}
