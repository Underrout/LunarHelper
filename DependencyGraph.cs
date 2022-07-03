using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.IO;

using QuickGraph.Algorithms;
using QuickGraph.Collections;

using SMWPatcher;

namespace LunarHelper
{
    class DependencyGraph
    {
        private static readonly Regex include_regex = new Regex("^\\s*(incsrc|incbin)\\s+(\"(.*)\"|[^\\s]*)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static HashSet<string> GetIncludes(string file_path)
        {
            HashSet<string> includes = new HashSet<string>();

            using (StreamReader sr = new StreamReader(file_path))
            {
                string line;

                while ((line = sr.ReadLine()) != null)
                {
                    Match match = include_regex.Match(line);

                    if (match.Success)
                    {
                        includes.Add(match.Groups[2].Value.Trim('"'));
                    }
                }
            }

            return includes;
        }
    }
}
