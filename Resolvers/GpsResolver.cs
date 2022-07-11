using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;

namespace LunarHelper.Resolvers
{
    using RootDependencyList = List<(string, string, GpsResolver.RootDependencyType)>;

    class GpsResolver : IToolRootResolve
    {
        private readonly DependencyGraph graph;
        private readonly AsarResolver asar_resolver;

        private HashSet<Vertex> seen = new HashSet<Vertex>();

        public enum RootDependencyType
        {
            Asar,
            Binary,
            BlockList
        }

        // indicator that anything on lines below in the block list 
        // is descriptions, not block specifications
        private const string dsc_indicator = "@dsc";

        private readonly Regex block_regex = new Regex(
            @"^\s*(?<number>(?:R\s*)?(?:[a-fA-F0-9]+(?::\s*[a-fA-F0-9]+)?)(?:-\s*[a-fA-F0-9]+)?(?::\s*[a-fA-F0-9]+)?)\s+(?<path>[^\n;]+?)\s*$",
            RegexOptions.Compiled
        );

        private readonly Regex passed_directories = new Regex(
            "-(?<type>l|b|s)\\s+(?:(?:\"(?<path>.*?)\")|(?<path>[^\\s\"]+))",
            RegexOptions.Compiled
        );

        private readonly RootDependencyList static_root_dependencies = new RootDependencyList
        {
            ( "asar.dll", "asar", RootDependencyType.Binary ),
            ( "main.asm", "main", RootDependencyType.Asar )
        };

        private RootDependencyList root_dependencies = new RootDependencyList();

        private readonly IEnumerable<(string, string)> generated_files = new List<(string, string)>
        {
            ( "__temp_settings.asm", "temp_settings" ),
            ( "__acts_likes_1.bin", "acts_like_1" ),
            ( "__acts_likes_2.bin", "acts_like_2" ),
            ( "_versionflag.bin", "version_flag" ),
            ( "__banks.bin", "banks" ),
            ( "__pointers_1.bin", "pointers_1" ),
            ( "__pointers_2.bin", "pointers_2" )
        };

        private const string default_list_file = "list.txt";
        private const string default_block_directory = "blocks";
        private const string default_routine_directory = "routines";

        private const string block_tag_base = "block";

        private const string list_tag = "list";
        private readonly (string, RootDependencyType) routine_tag_and_type = ("routine", RootDependencyType.Asar);

        private readonly string gps_directory;

        private string list_file;
        private string block_directory;
        private string routine_directory;

        public GpsResolver(DependencyGraph graph, string gps_exe_path, string gps_options)
        {
            this.graph = graph;
            this.gps_directory = Path.GetDirectoryName(gps_exe_path);

            DetermineDirectoryPaths(gps_options);

            asar_resolver = CreateAsarResolver();

            root_dependencies = DetermineRootDependencies(gps_exe_path);
        }

        public void ResolveToolRootDependencies(ToolRootVertex vertex)
        {
            foreach (var root_dependency in root_dependencies)
            {
                (string path, string tag, RootDependencyType type) = root_dependency;

                Vertex dependency = graph.GetOrCreateVertex(path);
                graph.TryAddUniqueEdge(vertex, dependency, tag);

                if (dependency is HashFileVertex)
                {
                    HashFileVertex dependency_file = (HashFileVertex)dependency;

                    switch (type)
                    {
                        case RootDependencyType.Asar:
                            asar_resolver.ResolveDependencies(dependency_file);
                            break;

                        case RootDependencyType.Binary:
                            seen.Add(dependency);
                            break;

                        case RootDependencyType.BlockList:
                            ResolveBlockList(dependency_file);
                            break;
                    }
                }
            }

            if (!File.Exists(list_file))
            {
                Vertex missing_list_file = graph.GetOrCreateMissingFileVertex(list_file);
                graph.TryAddUniqueEdge(vertex, missing_list_file, list_tag);
            }

            if (!Directory.Exists(routine_directory))
            {
                Vertex missing_routine_dir = graph.GetOrCreateMissingFileVertex(routine_directory);
                graph.TryAddUniqueEdge(vertex, missing_routine_dir, "routine_folder");
            }
        }

        private void DetermineDirectoryPaths(string gps_options)
        {
            var matches = passed_directories.Matches(gps_options);

            // iterating in reverse so that the last path will stick if the same 
            // option is passed multiple times
            foreach (Match match in matches.Reverse())
            {
                switch (match.Groups["type"].Value)
                {
                    case "l":
                        // only set list_file if it's not already been set (we're iterating in reverse!)
                        list_file ??= match.Groups["path"].Value; 
                        break;

                    case "b":
                        block_directory ??= match.Groups["path"].Value;
                        break;

                    case "s":
                        routine_directory ??= match.Groups["path"].Value;
                        break;
                }

                // if all paths are already set, we don't need to scan the remaining matches
                if (list_file != null && block_directory != null && routine_directory != null)
                {
                    break;
                }
            }

            // use defaults if any paths are still null
            list_file ??= Path.Combine(gps_directory, default_list_file);
            block_directory ??= Path.Combine(gps_directory, default_block_directory);
            routine_directory ??= Path.Combine(gps_directory, default_routine_directory);
        }

        private AsarResolver CreateAsarResolver()
        {
            AsarResolver resolver = new AsarResolver(graph, seen);

            foreach ((string generated_file_path, string tag) in generated_files)
            {
                var full_path = Path.Combine(gps_directory, generated_file_path);
                resolver.NameGeneratedFile(full_path, tag);
            }

            return resolver;
        }

        private RootDependencyList DetermineRootDependencies(string gps_exe_path)
        {
            RootDependencyList dependencies = new RootDependencyList { (gps_exe_path, "exe", RootDependencyType.Binary) };

            foreach ((string relative_path, string tag, RootDependencyType type) in static_root_dependencies)
            {
                var path = Path.Combine(gps_directory, relative_path);
                dependencies.Add((path, tag, type));
            }

            if (Directory.Exists(routine_directory))
            {
                foreach (var routine_path in Directory.EnumerateFiles(routine_directory, "*.asm", SearchOption.TopDirectoryOnly))
                {
                    // not numbering these tags since the order of routines probably doesn't matter
                    dependencies.Add((routine_path, routine_tag_and_type.Item1, routine_tag_and_type.Item2));
                }
            }

            dependencies.Add((list_file, list_tag, RootDependencyType.BlockList));

            return dependencies;
        }

        private void ResolveBlockList(HashFileVertex vertex)
        {
            seen.Add(vertex);

            using (StreamReader reader = new StreamReader(vertex.uri.LocalPath))
            {
                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    if (line.TrimStart().StartsWith(dsc_indicator))
                    {
                        break;
                    }

                    Match match = block_regex.Match(line);

                    if (match.Success)
                    {
                        var trimmed_number = Regex.Replace(match.Groups["number"].Value, @"\s+", "");
                        ResolveBlock(vertex, trimmed_number, match.Groups["path"].Value);
                    }
                }
            }
        }

        private void ResolveBlock(HashFileVertex list_vertex, string number, string relative_path)
        {
            var full_path = Path.Combine(block_directory, relative_path);

            Vertex block_vertex = graph.GetOrCreateVertex(full_path);
            graph.AddEdge(list_vertex, block_vertex, $"{block_tag_base}_{number}");

            if (block_vertex is HashFileVertex)
            {
                asar_resolver.ResolveDependencies((HashFileVertex)block_vertex);
            }
            else
            {
                seen.Add(block_vertex);
            }
        }
    }
}
