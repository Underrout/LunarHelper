using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace LunarHelper
{
    class Util
    {
        public static bool PathsEqual(string path1, string path2)
        {
            return string.Equals(NormalizePath(path1), NormalizePath(path2), StringComparison.OrdinalIgnoreCase);
        }

        public static string NormalizePath(string path)
        {
            return Path.GetFullPath(new Uri(Path.GetFullPath(path)).LocalPath)
                       .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                       .ToLowerInvariant();
        }
    }
}
