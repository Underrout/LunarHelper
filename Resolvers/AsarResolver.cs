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

        private readonly DependencyGraph graph;
        private HashSet<Vertex> seen;
        private readonly IEnumerable<string> additional_include_directories = new List<string>();
        public readonly string stddefines_file;

        // normalized generated file path, tag for generated file
        private List<(string, string)> generated_files = new List<(string, string)>();

        public AsarResolver(DependencyGraph graph, HashSet<Vertex> seen, string asar_exe_path = null, string asar_options = null)
        {
            this.graph = graph;
            this.seen = seen;

            if (asar_exe_path != null)
            {
                stddefines_file = DetermineStddefinesFile(asar_exe_path);
            }

            if (asar_options != null)
            {
                additional_include_directories = DetermineAdditionalIncludeDirectories(asar_options);
            }
        }

        public void NameGeneratedFile(string normalized_file_path, string tag)
        {
            generated_files.Add((normalized_file_path, tag));
        }

        public void NameGeneratedFile((string, string) file_path_tag_tuple)
        {
            NameGeneratedFile(file_path_tag_tuple.Item1, file_path_tag_tuple.Item2);
        }

        private string DetermineStddefinesFile(string asar_exe_path)
        {
            // TODO implement this so it returns the path to the stddefines.txt file 
            // in the asar exe's folder, if there is such a file
            return null;
        }

        private IEnumerable<string> DetermineAdditionalIncludeDirectories(string asar_options)
        {
            // TODO implement this, see asar's -I and --include options
            return new List<string>();
        }

        public void ResolveDependencies(HashFileVertex vertex)
        {
            if (seen.Contains(vertex))
            {
                return;
            }

            seen.Add(vertex);

            int dependency_id = 0;

            using (StreamReader sr = new StreamReader(vertex.normalized_file_path))
            {
                string line;

                while ((line = sr.ReadLine()) != null)
                {
                    Match match = incsrc_incbin_regex.Match(line);

                    if (match.Success)
                    {
                        TryResolveDependency(vertex, match, ref dependency_id);
                    }
                }
            }
        }

        private void TryResolveDependency(FileVertex vertex, Match match, ref int dependency_id)
        {
            (var resolve_result, var attempted_paths) = TryResolveInclude(vertex, match);

            Vertex dependency = null;
            bool is_binary_dependency = match.Groups["method"].Value == "incbin";
            string tag = is_binary_dependency ? "binary" : "source";

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
                    tag = generated_files.Single(t => t.Item1 == attempted_paths.Last()).Item2;
                    break;
            }

            tag = $"{tag}_{dependency_id++}";

            graph.AddEdge(vertex, dependency, tag);

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
        public (IncludeResolveResult, List<string>) TryResolveInclude(FileVertex vertex, Match include)
        {
            // TODO make it actually try hard to resolve correctly
            // by using additional include directories if those are 
            // available

            // TODO if the include is unresolvable, return a plain vertex
            // Note that there are a lot of ways something could be unresolvable
            // It could be something like `incsrc <macro_variable>` or 
            // `incsrc !some_define` or even `incsrc "normal_stuff/!define_time/normal_stuff"
            // Probably just look for <> at start and end of the path -> unresolvable
            // Look for ! anywhere in the path -> unresolvable (even though it's not against
            // window's naming scheme to have ! in file names, I just can't resolve that without
            // evaluating defines, which I do not really want to do

            List<string> attempted_paths = new List<string>();

            string include_path = include.Groups["path"].Value;

            if (include_path.Contains('!'))
            {
                // have to assume it's an arbitrary include, since this 
                // could be something like incsrc !some_define
                // though it could also be a normal include like 
                // incsrc "!my_name_starts_with_!.asm" which is totally legal
                // on windows
                return (IncludeResolveResult.Arbitrary, attempted_paths);
            }
            else if (include_path.Contains('<') || include_path.Contains('>'))
            {
                // have to assume it's an arbitrary include, since this
                // could be incsrc <macro_variable>
                // unlike !, < and > are actually not legal in windows path names afaik,
                // so this should actually never flag on real file names
                return (IncludeResolveResult.Arbitrary, attempted_paths);
            }

            var generated_paths = generated_files.Select(t => t.Item1);

            if (Path.IsPathRooted(include_path))
            {
                var normalized_absolute_path = Util.NormalizePath(include_path);
                attempted_paths.Add(normalized_absolute_path);

                if (generated_paths.Contains(normalized_absolute_path))
                {
                    return (IncludeResolveResult.Generated, attempted_paths);
                }
                else if (File.Exists(normalized_absolute_path))
                {
                    return (IncludeResolveResult.Found, attempted_paths);
                }
                else
                {
                    return (IncludeResolveResult.NotFound, attempted_paths);
                }
            }

            var relative_include_path = Util.NormalizePath(Path.Combine(Path.GetDirectoryName(
                vertex.normalized_file_path), include_path));
            attempted_paths.Add(relative_include_path);

            if (generated_paths.Contains(relative_include_path))
            {
                return (IncludeResolveResult.Generated, attempted_paths);
            }
            else if (File.Exists(relative_include_path))
            {
                return (IncludeResolveResult.Found, attempted_paths);
            }

            foreach (var include_directory in additional_include_directories)
            {
                var potential_path = Util.NormalizePath(Path.Combine(include_directory, include_path));
                attempted_paths.Add(potential_path);

                if (generated_paths.Contains(potential_path))
                {
                    return (IncludeResolveResult.Generated, attempted_paths);
                }
                else if (File.Exists(potential_path))
                {
                    return (IncludeResolveResult.Found, attempted_paths);
                }
            }

            return (IncludeResolveResult.NotFound, attempted_paths);
        }
    }
}
