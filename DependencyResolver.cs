using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LunarHelper
{
    class DependencyResolver
    {
        public class Dependency
        {
            public string absolute_dependency_path;
            // true if the absolute_dependency_path points to a file that should not be scanned for further dependencies
            // i.e. if it was included via incbin
            public bool is_deadend;

            public Dependency(string absolute_dependency_path, bool is_deadend)
            {
                this.absolute_dependency_path = absolute_dependency_path;
                this.is_deadend = is_deadend;
            }
        }

        private static readonly Regex incsrc_incbin_regex = new Regex("^\\s*(incsrc|incbin)\\s+(\"(.*)\"|[^\\s]*)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static HashSet<Dependency> GetAmkListDependencies(string absolute_amk_list_path)
        {
            HashSet<Dependency> dependencies = new HashSet<Dependency>();

            // TODO

            return dependencies;
        }

        public static HashSet<Dependency> GetUberAsmListDependencies(string absolute_uberasm_list_path)
        {
            HashSet<Dependency> dependencies = new HashSet<Dependency>();

            // TODO

            return dependencies;
        }

        public static HashSet<Dependency> GetGpsListDependencies(string absolute_gps_list_path)
        {
            HashSet<Dependency> dependencies = new HashSet<Dependency>();

            // TODO

            return dependencies;
        }

        public static HashSet<Dependency> GetPixiListDependencies(string absolute_pixi_list_path)
        {
            HashSet<Dependency> dependencies = new HashSet<Dependency>();

            // TODO

            return dependencies;
        }

        public static HashSet<Dependency> GetPixiFileDependencies(string absolute_pixi_file_path)
        {
            switch (Path.GetExtension(absolute_pixi_file_path))
            {
                case ".asm": return GetAsarFileDependencies(absolute_pixi_file_path);
                case ".json": return GetPixiCfgFileDependencies(absolute_pixi_file_path);
                case ".cfg": return GetPixiJsonFileDependencies(absolute_pixi_file_path);
                default: return new HashSet<Dependency>();
            }
        }

        private static HashSet<Dependency> GetPixiJsonFileDependencies(string absolute_json_path)
        {
            HashSet<Dependency> dependencies = new HashSet<Dependency>();

            try
            {
                string contents = File.ReadAllText(absolute_json_path);
                JsonNode node = JsonNode.Parse(contents);
                // FIXME Handle different sprite, generator, shooter, etc. folders and resolve "AsmFile" path 
                // to an actual full path
                dependencies.Add(new Dependency(node["AsmFile"].ToString(), false));
            }
            catch (Exception)
            {
                // pass
            }

            return dependencies;
        }

        private static HashSet<Dependency> GetPixiCfgFileDependencies(string absolute_cfg_path)
        {
            HashSet<Dependency> dependencies = new HashSet<Dependency>();

            try
            {
                using (StreamReader sr = new StreamReader(absolute_cfg_path))
                {
                    int idx = 0;

                    while (++idx != 4)
                    {
                        sr.ReadLine();
                    }

                    // FIXME Handle different sprite, generator, shooter, etc. folders and resolve "AsmFile" path 
                    // to an actual full path
                    dependencies.Add(new Dependency(sr.ReadLine(), false));
                }
            }
            catch (Exception)
            {
                // pass
            }

            return dependencies;
        }

        public static HashSet<Dependency> GetAsarFileDependencies(string absolute_asar_path)
        {
            HashSet<Dependency> dependencies = new HashSet<Dependency>();

            try
            {
                using (StreamReader sr = new StreamReader(absolute_asar_path))
                {
                    string line;

                    while ((line = sr.ReadLine()) != null)
                    {
                        Match match = incsrc_incbin_regex.Match(line);

                        if (match.Success)
                        {
                            string trimmed_include_path = match.Groups[2].Value.Trim('"');
                            if (!Path.IsPathRooted(trimmed_include_path))
                            {
                                trimmed_include_path = Path.GetFullPath(
                                    Path.Combine(Path.GetDirectoryName(absolute_asar_path), trimmed_include_path));
                            }

                            // if the include is an incbin, mark this dependency as a deadend
                            dependencies.Add(new Dependency(trimmed_include_path, match.Groups[1].Value.ToLower() == "incbin"));
                        }
                    }
                }
            }
            catch (Exception)
            {
                // pass
            }

            return dependencies;
        }
    }
}
