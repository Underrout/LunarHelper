using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.IO;

namespace LunarHelper.Resolvers
{
    using RootDependencyList = List<(string, string, UberAsmResolver.RootDependencyType)>;
    class UberAsmResolver : IToolRootResolve
    {
        private readonly DependencyGraph graph;
        private readonly AsarResolver asar_resolver;
        private string uberasm_directory;

        private readonly RootDependencyList root_dependencies;

        private readonly Regex list_section = new Regex(
            @"^\s*(?<section>(?:level|overworld|gamemode)):",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        private readonly Regex list_item = new Regex(
            @"^\s*(?<number>[a-fA-F0-9]+)\s*(?<path>.*?)(?:(?:\s*$)|(?:\s+;(?:.*)$))",
            RegexOptions.Compiled
        );

        private readonly Regex global_macro_statusbar = new Regex(
            @"^\s*(?<type>(?:global|statusbar|macrolib)):\s+(?<path>(.*?))(?:(?:\s*$)|(?:\s+;(?:.*)$))",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        public enum RootDependencyType
        {
            Asar,
            Binary,
            List
        }

        private enum ListVar
        {
            Level,
            Gamemode,
            Overworld,
            MacroLib,
            Statusbar,
            Global
        }

        private HashSet<Vertex> seen = new HashSet<Vertex>();

        private readonly RootDependencyList static_root_dependencies = new RootDependencyList
        {
            ( "asar.dll", "asar", RootDependencyType.Binary ),
            ( "asm/base/main.asm", "main", RootDependencyType.Asar )
        };

        private const string default_list_file = "list.txt";
        private const string default_library_directory = "library";
        private const string default_level_directory = "level";
        private const string default_gamemode_directory = "gamemode";
        private const string default_overworld_directory = "overworld";

        private string list_file;
        private string library_directory;
        private string level_directory;
        private string gamemode_directory;
        private string overworld_directory;

        private const string list_tag = "list";
        private const string library_file_tag = "library";

        public UberAsmResolver(DependencyGraph graph, string uberasm_exe_path, string uberasm_options)
        {
            this.graph = graph;
            this.uberasm_directory = Path.GetDirectoryName(uberasm_exe_path);

            DetermineDirectoryPaths(uberasm_options);

            asar_resolver = new AsarResolver(graph, seen);

            root_dependencies = DetermineRootDependencies(uberasm_exe_path);
        }

        private void DetermineDirectoryPaths(string uberasm_options)
        {
            // we don't actually have to resolve uberasm_options for now
            // since I believe uberasm doesn't let you customize directory 
            // paths as of version 1.4
            //
            // the one thing we do want to handle is if uberasm_options is not
            // empty, it must be an overriden list path, since that's currently
            // the only option you can actually pass (may change in the future,
            // watch out!)

            string list_file_path = string.IsNullOrWhiteSpace(uberasm_options) ? 
                default_list_file : uberasm_options.Trim();

            list_file = Path.Combine(uberasm_directory, list_file_path);
            library_directory = Path.Combine(uberasm_directory, default_library_directory);
            level_directory = Path.Combine(uberasm_directory, default_level_directory);
            gamemode_directory = Path.Combine(uberasm_directory, default_gamemode_directory);
            overworld_directory = Path.Combine(uberasm_directory, default_overworld_directory);
        }

        private RootDependencyList DetermineRootDependencies(string uberasm_exe_path)
        {
            RootDependencyList dependencies = new RootDependencyList { (uberasm_exe_path, "exe", RootDependencyType.Binary) };

            foreach ((string relative_path, string tag, RootDependencyType type) in static_root_dependencies)
            {
                var path = Path.Combine(uberasm_directory, relative_path);
                dependencies.Add((path, tag, type));
            }

            if (Directory.Exists(library_directory))
            {
                foreach (string path in Directory.EnumerateFiles(library_directory, "*.*", SearchOption.AllDirectories))
                {
                    dependencies.Add((path, library_file_tag, RootDependencyType.Asar));
                }
            }

            dependencies.Add((list_file, list_tag, RootDependencyType.List));

            return dependencies;
        }

        public void ResolveToolRootDependencies(ToolRootVertex vertex)
        {
            foreach (var root_dependency in root_dependencies)
            {
                (string path, string tag, RootDependencyType type) = root_dependency;

                Vertex dependency = tag != library_file_tag ? graph.GetOrCreateVertex(path) : graph.GetOrCreateFileNameVertex(path);
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

                        case RootDependencyType.List:
                            ResolveList(dependency_file);
                            break;
                    }
                }
            }

            if (!File.Exists(list_file))
            {
                Vertex missing_list_file = graph.GetOrCreateMissingFileVertex(list_file);
                graph.TryAddUniqueEdge(vertex, missing_list_file, list_tag);
            }

            if (!Directory.Exists(library_directory))
            {
                Vertex missing_library_dir = graph.GetOrCreateMissingFileVertex(library_directory);
                graph.TryAddUniqueEdge(vertex, missing_library_dir, "library_folder");
            }
        }

        private void ResolveList(HashFileVertex vertex)
        {
            seen.Add(vertex);

            using (StreamReader reader = new StreamReader(vertex.uri.LocalPath))
            {
                string line;
                bool section_set = false;
                ListVar curr_section = ListVar.Level;

                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith(';'))
                    {
                        continue;
                    }

                    Match match;

                    if (!section_set)
                    {
                        match = list_section.Match(line);

                        if (match.Success)
                        {
                            curr_section = DetermineListVar(match.Groups["section"].Value);
                            section_set = true;
                        }
                        continue;
                    }

                    match = list_item.Match(line);

                    if (match.Success)
                    {
                        var number = int.Parse(match.Groups["number"].Value, System.Globalization.NumberStyles.HexNumber);
                        var tag = $"{curr_section.ToString().ToLower()}_{number}";
                        ResolveListVar(vertex, match.Groups["path"].Value, tag, curr_section);
                        continue;
                    }

                    match = global_macro_statusbar.Match(line);

                    if (match.Success)
                    {
                        ListVar var_type = DetermineListVar(match.Groups["type"].Value);

                        ResolveListVar(vertex, match.Groups["path"].Value, match.Groups["type"].Value.ToLower(), var_type);
                        continue;
                    }

                    match = list_section.Match(line);

                    if (match.Success)
                    {
                        curr_section = DetermineListVar(match.Groups["section"].Value);
                    }
                }
            }
        }

        private ListVar DetermineListVar(string var_string)
        {
            switch (var_string.ToLowerInvariant())
            {
                default:
                // we should never default here since we only match these
                // three strings, but the compiler will complain if 
                // I don't do this

                case "level":
                    return ListVar.Level;

                case "overworld":
                    return ListVar.Overworld;

                case "macrolib":
                    return ListVar.MacroLib;

                case "statusbar":
                    return ListVar.Statusbar;

                case "global":
                    return ListVar.Global;

                case "gamemode":
                    return ListVar.Gamemode;
            }
        }


        private void ResolveListVar(HashFileVertex list_vertex, string relative_path, string tag, ListVar type)
        {
            string base_path = null;
            switch (type)
            {
                case ListVar.Level:
                    base_path = level_directory;
                    break;

                case ListVar.Overworld:
                    base_path = overworld_directory;
                    break;

                case ListVar.Gamemode:
                    base_path = gamemode_directory;
                    break;

                case ListVar.MacroLib:
                    // [FALLTHROUGH]

                case ListVar.Global:
                    // [FALLTHROUGH]

                case ListVar.Statusbar:
                    base_path = uberasm_directory;
                    break;
            }

            string full_path = Path.Combine(base_path, relative_path);

            Vertex asm_vertex = graph.GetOrCreateVertex(full_path);
            graph.AddEdge(list_vertex, asm_vertex, tag);

            if (asm_vertex is HashFileVertex)
            {
                asar_resolver.ResolveDependencies((HashFileVertex)asm_vertex);
            }
            else
            {
                seen.Add(asm_vertex);
            }
        }
    }
}
