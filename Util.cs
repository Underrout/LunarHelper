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
        public static Uri GetUri(string relative_or_absolute_path)
        {
            var absolute_path = Path.IsPathRooted(relative_or_absolute_path) ?
                relative_or_absolute_path : Path.GetFullPath(relative_or_absolute_path);

            UriCreationOptions options = new UriCreationOptions();
            options.DangerousDisablePathAndQueryCanonicalization = !File.Exists(absolute_path);

            return new Uri(absolute_path, options);
        }
    }
}
