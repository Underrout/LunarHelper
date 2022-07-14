using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.IO;

namespace LunarHelper.Resolvers
{
    internal class AsarResolver : IResolve<HashFileVertex>
    {
        public enum IncludeResolveResult
        {
            Found,
            NotFound,
            Generated,
            Arbitrary
        }

        private static readonly Regex incsrc_incbin_regex = new Regex(
            "^\\s*(?<method>incsrc|incbin)\\s+(?:\"(?<path>.*)\"|(?<path>[^\\s]*))",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        private static readonly Regex additional_include_directories_regex = new Regex(
            "(?:--include\\s+(?:(?:\"(?<path>.*?)\")|(?<path>[^\\s]*)))|(?:-I(?:(?:\"(?<path>.*?)\")|(?<path>[^\\s\"]+)))",
            RegexOptions.Compiled
        );

        private static readonly Regex table_regex = new Regex(
            "^\\s*table\\s+(?:(?:\"(?<path>.*?)\")|(?<path>[^\\s,]*))",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        private const string stddefines_file_name = "stddefines.txt";
        private const string stddefines_tag = "stddefines";

        private readonly DependencyGraph graph;
        private HashSet<Vertex> seen;
        private readonly IEnumerable<string> additional_include_directories = new List<string>();
        public readonly HashFileVertex stddefines_vertex = null;

        // normalized generated file path, tag for generated file
        private List<(Uri, string)> generated_files = new List<(Uri, string)>();

        public AsarResolver(DependencyGraph graph, HashSet<Vertex> seen, string asar_exe_path = null, string asar_options = null)
        {
            this.graph = graph;
            this.seen = seen;

            if (!string.IsNullOrWhiteSpace(asar_options))
            {
                additional_include_directories = DetermineAdditionalIncludeDirectories(asar_options);
            }

            // afaik you can only have a stddefines file with the exe, not a dll
            if (!string.IsNullOrWhiteSpace(asar_exe_path) && Path.GetExtension(asar_exe_path) == ".exe")
            {
                stddefines_vertex = DetermineStddefinesFile(asar_exe_path);

                if (stddefines_vertex != null)
                {
                    // stddefines file apparently cannot contain incsrc/incbin, so I'm going to 
                    // treat it as if it were a binary file, since it presumably cannot cause 
                    // further dependencies
                    seen.Add(stddefines_vertex);
                }
            }
        }

        public void NameGeneratedFile(string normalized_file_path, string tag)
        {
            generated_files.Add((Util.GetUri(normalized_file_path), tag));
        }

        public void NameGeneratedFile((string, string) file_path_tag_tuple)
        {
            NameGeneratedFile(file_path_tag_tuple.Item1, file_path_tag_tuple.Item2);
        }

        private HashFileVertex DetermineStddefinesFile(string asar_exe_path)
        {
            var asar_dir = Path.GetDirectoryName(asar_exe_path);
            var potential_stddefines = Path.Combine(asar_dir, stddefines_file_name);

            if (File.Exists(potential_stddefines))
            {
                return (HashFileVertex)graph.GetOrCreateVertex(potential_stddefines);
            }
            else
            {
                return null;
            }
        }

        private IEnumerable<string> DetermineAdditionalIncludeDirectories(string asar_options)
        {
            return additional_include_directories_regex.Matches(asar_options)
                .SelectMany(m => m.Groups["path"].Captures.Select(c => c.Value));
        }

        public void ResolveDependencies(HashFileVertex vertex)
        {
            if (seen.Contains(vertex))
            {
                return;
            }

            seen.Add(vertex);

            int dependency_id = 0;

            using (StreamReader sr = new StreamReader(vertex.uri.LocalPath))
            {
                string line;

                while ((line = sr.ReadLine()) != null)
                {
                    Match match = incsrc_incbin_regex.Match(line);

                    if (match.Success)
                    {
                        TryResolveDependency(vertex, match, ref dependency_id);
                    }
                    else
                    {
                        match = table_regex.Match(line);

                        if (match.Success)
                        {
                            TryResolveDependency(vertex, match, ref dependency_id, true);
                        }
                    }
                }
            }
        }

        private void TryResolveDependency(FileVertex vertex, Match match, ref int dependency_id, bool is_table_include = false)
        {
            (var resolve_result, var attempted_paths, var found_path) = TryResolveInclude(vertex, match);

            Vertex dependency = null;
            bool is_binary_dependency = is_table_include || match.Groups["method"].Value == "incbin";
            string tag = is_table_include ? "table" : (is_binary_dependency ? "binary" : "source");

            switch (resolve_result)
            {
                case IncludeResolveResult.Arbitrary:
                    dependency = graph.CreateArbitraryFileVertex();
                    tag = "arbitrary_" + tag;
                    break;

                case IncludeResolveResult.NotFound:
                    // [FALLTHROUGH]

                case IncludeResolveResult.Found:
                    // if the file was not found, GetOrCreateVertex should handle it, 
                    // so I don't differentiate here
                    dependency = graph.GetOrCreateVertex(attempted_paths.Last());
                    // tag should be fine as is
                    break;

                case IncludeResolveResult.Generated:
                    dependency = graph.GetOrCreateVertex(attempted_paths.Last(), true);
                    tag = generated_files.Single(t => t.Item1.Equals(found_path)).Item2;
                    break;
            }

            tag = $"{tag}_{dependency_id++}";

            graph.TryAddUniqueEdge(vertex, dependency, tag, true);

            if (!is_binary_dependency && dependency is HashFileVertex)
            {
                ResolveDependencies((HashFileVertex)dependency);
            }
            else
            {
                seen.Add(dependency);
            }
        }

        // tries to resolve the include to the best of its abilities
        //
        // works like this:
        //
        // X. if include can't be resolved (i.e. incsrc <macro_var>) return (Arbitrary, [])
        // 1. if the include path is rooted
        //    a. check if file exists or is generated, if so -> return (Found/Generated, [absolute_include_path])
        //    b. otherwise return (NotFound, [absolute_include_path]) since it can't possibly
        //       refer to any other file (I think)
        // 2. else
        //    a. check if file exists at vertex.path + include_path or is generated there,
        //       if so -> return (Found/Generated, [relative_include_path])
        // 3. list += attempted_path
        // 4. foreach include_directory 
        //    a. check if file exists at include_directory + include_path or is generated there
        //       if so -> return (Found/Generated, list + path_we_just_tried)
        //    b. else add the path we tried to the list
        // 5. return (NotFound, list)
        public (IncludeResolveResult, List<string>, Uri) TryResolveInclude(FileVertex vertex, Match include)
        {
            List<string> attempted_paths = new List<string>();

            string include_path = include.Groups["path"].Value;

            if (include_path.Contains('!'))
            {
                // have to assume it's an arbitrary include, since this 
                // could be something like incsrc !some_define
                // though it could also be a normal include like 
                // incsrc "!my_name_starts_with_!.asm" which is totally legal
                // on windows
                return (IncludeResolveResult.Arbitrary, attempted_paths, null);
            }
            else if (include_path.Contains('<') || include_path.Contains('>'))
            {
                // have to assume it's an arbitrary include, since this
                // could be incsrc <macro_variable>
                // unlike !, < and > are actually not legal in windows path names afaik,
                // so this should actually never flag on real file names
                return (IncludeResolveResult.Arbitrary, attempted_paths, null);
            }

            var generated_paths = generated_files.Select(t => t.Item1);
            Uri uri;

            if (Path.IsPathRooted(include_path))
            {
                var absolute_path = include_path;
                attempted_paths.Add(absolute_path);
                uri = Util.GetUri(absolute_path);

                if (generated_paths.Contains(new Uri(absolute_path)))
                {
                    return (IncludeResolveResult.Generated, attempted_paths, uri);
                }
                else if (File.Exists(absolute_path))
                {
                    return (IncludeResolveResult.Found, attempted_paths, uri);
                }
                else
                {
                    return (IncludeResolveResult.NotFound, attempted_paths, null);
                }
            }

            var relative_include_path = Path.Combine(Path.GetDirectoryName(vertex.uri.LocalPath), include_path);
            attempted_paths.Add(relative_include_path);
            uri = Util.GetUri(relative_include_path);

            if (generated_paths.Contains(uri))
            {
                return (IncludeResolveResult.Generated, attempted_paths, uri);
            }
            else if (File.Exists(relative_include_path))
            {
                return (IncludeResolveResult.Found, attempted_paths, uri);
            }

            foreach (var include_directory in additional_include_directories)
            {
                var potential_path = Path.Combine(include_directory, include_path);
                attempted_paths.Add(potential_path);
                uri = Util.GetUri(potential_path);

                if (generated_paths.Contains(uri))
                {
                    return (IncludeResolveResult.Generated, attempted_paths, uri);
                }
                else if (File.Exists(potential_path))
                {
                    return (IncludeResolveResult.Found, attempted_paths, uri);
                }
            }

            return (IncludeResolveResult.NotFound, attempted_paths, null);
        }
    }
}
