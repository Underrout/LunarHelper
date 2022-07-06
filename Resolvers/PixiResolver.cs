using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace LunarHelper.Resolvers
{
    // relative path, tag to assign, dependency type
    using RootDependencyList = List<(string, string, PixiResolver.RootDependencyType)>;

    class PixiResolver : IToolRootResolve
    {
        private HashSet<Vertex> seen = new HashSet<Vertex>();
        private readonly DependencyGraph graph;
        private readonly AsarResolver asar_resolver;
        private readonly string pixi_directory;

        public enum RootDependencyType
        {
            Asar,
            Binary,
            SpriteList
        }

        private readonly RootDependencyList static_root_dependencies = new RootDependencyList
        {
            ( "asar.dll", "asar", RootDependencyType.Binary )
        };

        private readonly RootDependencyList root_dependencies;

        private readonly RootDependencyList asm_directory_dependencies = new RootDependencyList
        {
            ( "cluster.asm", "cluster", RootDependencyType.Asar ),
            ( "extended.asm", "extended", RootDependencyType.Asar ),
            ( "main.asm", "main", RootDependencyType.Asar ),
            ( "spritetool_clean.asm", "sprite_tool_clean", RootDependencyType.Asar )
        };

        private readonly IEnumerable<(string, string)> asm_directory_generated_files = new List<(string, string)>
        {
            ( "_ClusterPtr.asm", "cluster_ptr" ),
            ( "_ExtendedPtr.bin", "extended_ptr" ),
            ( "_ExtendedCapePtr.bin", "extended_cape_ptr" ),
            ( "_versionflag.bin", "version_flag" ),
            ( "_CustomSize.bin", "custom_size" ),
            ( "_DefaultTables.bin", "default_tables" ),
            ( "_CustomStatusPtr.bin", "custom_status_ptr" ),
            ( "_PerLevelLvlPtrs.bin", "per_level_lvl_ptrs" ),
            ( "_PerLevelT.bin", "per_level_t" ),
            ( "_PerLevelCustomPtrTable.bin", "per_level_custom_ptr_table" ),
            ( "_PerLevelSprPtrs.bin", "per_level_spr_ptrs" ),
            ( "config.asm", "config" )
        };

        private readonly (string, RootDependencyType) routine_tag_and_type = ( "routine", RootDependencyType.Asar );

        // these header files may or may not exist, you can delete them as long as you don't insert any sprites
        // of the corresponding type (not that you should)
        // if they're there, create a HashFileVertex and attach it to pixi's root
        private readonly (string, string, RootDependencyType) potential_sprite_folder_header_file_dependency = (
            "_header.asm", "header", RootDependencyType.Asar
        );

        private const string list_tag = "list";

        private const string default_list_file = "list.txt";
        private const string default_asm_directory = "asm";
        private const string default_sprites_directory = "sprites";
        private const string default_shooters_directory = "shooters";
        private const string default_generators_directory = "generators";
        private const string default_extended_directory = "extended";
        private const string default_cluster_directory = "cluster";
        private const string default_routine_directory = "routines";

        private string list_file;
        private string asm_directory;
        private string sprites_directory;
        private string shooters_directory;
        private string generators_directory;
        private string extended_directory;
        private string cluster_directory;
        private string routine_directory;

        public PixiResolver(DependencyGraph graph, string pixi_exe_path, string pixi_options, string output_path)
        {
            this.graph = graph;
            pixi_directory = Util.NormalizePath(Path.GetDirectoryName(pixi_exe_path));

            DetermineDirectoryPaths(pixi_options, output_path);

            asar_resolver = CreateAsarResolver();
            root_dependencies = DetermineRootDependencies();
        }

        private RootDependencyList DetermineRootDependencies()
        {
            RootDependencyList dependencies = new RootDependencyList();

            foreach ((string relative_path, string tag, RootDependencyType type) in static_root_dependencies)
            {
                var path = Util.NormalizePath(Path.Combine(pixi_directory, relative_path));
                dependencies.Add((path, tag, type));
            }

            foreach ((string relative_path, string tag, RootDependencyType type) in asm_directory_dependencies)
            {
                var path = Util.NormalizePath(Path.Combine(asm_directory, relative_path));
                dependencies.Add((path, tag, type));
            }

            int header_id = 0;
            foreach (string directory_path in new[] { sprites_directory, shooters_directory, generators_directory,
                extended_directory, cluster_directory })
            {
                var path = Util.NormalizePath(Path.Combine(directory_path, potential_sprite_folder_header_file_dependency.Item1));
                if (!File.Exists(path))
                {
                    // these are sometimes optional, so if one doesn't exist, just skip it (pixi will complain about it if it's 
                    // not ok to do and everything will be fine)

                    // still increment the id tho, potentially matters for order
                    ++header_id;

                    continue;
                }
                var tag = $"{potential_sprite_folder_header_file_dependency.Item2}_{header_id++}";

                dependencies.Add((path, tag, potential_sprite_folder_header_file_dependency.Item3));
            }

            foreach (var routine_path in Directory.EnumerateFiles(routine_directory, "*.asm", SearchOption.TopDirectoryOnly))
            {
                var normalized_path = Util.NormalizePath(routine_path);
                
                // not numbering these tags since the order of routines probably doesn't matter
                dependencies.Add((normalized_path, routine_tag_and_type.Item1, routine_tag_and_type.Item2));
            }

            dependencies.Add((list_file, list_tag, RootDependencyType.SpriteList));

            return dependencies;
        }

        private AsarResolver CreateAsarResolver()
        {
            AsarResolver resolver = new AsarResolver(graph, seen);

            foreach ((string generated_file_path, string tag) in asm_directory_generated_files)
            {
                var full_path = Util.NormalizePath(Path.Combine(asm_directory, generated_file_path));
                resolver.NameGeneratedFile(full_path, tag);
            }

            return resolver;
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
                    HashFileVertex depencency_file = (HashFileVertex)dependency;

                    switch (type)
                    {
                        case RootDependencyType.Asar:
                            asar_resolver.ResolveDependencies(depencency_file);
                            break;

                        case RootDependencyType.Binary:
                            break;

                        default:
                            break;
                    }
                }
            }
        }

        private void DetermineDirectoryPaths(string pixi_options, string output_path)
        {
            // TODO make this actually account for different path specifiers in pixi_options

            // pixi always resolves the list file relative to the rom's dir unless an absolute
            // -l option is passed
            var rom_dir = Util.NormalizePath(Path.GetDirectoryName(Path.GetFullPath(output_path)));

            // everything else is resolved relative to the pixi directory
            list_file = Util.NormalizePath(Path.Combine(pixi_directory, default_list_file));
            asm_directory = Util.NormalizePath(Path.Combine(pixi_directory, default_asm_directory));
            sprites_directory = Util.NormalizePath(Path.Combine(pixi_directory, default_sprites_directory));
            shooters_directory = Util.NormalizePath(Path.Combine(pixi_directory, default_shooters_directory));
            generators_directory = Util.NormalizePath(Path.Combine(pixi_directory, default_generators_directory));
            extended_directory = Util.NormalizePath(Path.Combine(pixi_directory, default_extended_directory));
            cluster_directory = Util.NormalizePath(Path.Combine(pixi_directory, default_cluster_directory));
            routine_directory = Util.NormalizePath(Path.Combine(pixi_directory, default_routine_directory));
        }

        private void ResolveJsonFileDependencies(HashFileVertex vertex)
        {
            string contents = File.ReadAllText(vertex.normalized_file_path);
            JsonNode node = JsonNode.Parse(contents);
            var asm_file_path = Util.NormalizePath(Path.Combine(Path.GetDirectoryName(
                    vertex.normalized_file_path), node["AsmFile"].ToString()));

            Vertex asm_vertex = graph.GetOrCreateVertex(asm_file_path);
            graph.TryAddUniqueEdge(vertex, asm_vertex, "asm_file");

            if (asm_vertex is HashFileVertex)
            {
                asar_resolver.ResolveDependencies((HashFileVertex)asm_vertex);
            }
        }

        private void ResolveCfgFileDependencies(HashFileVertex vertex)
        {
            using (StreamReader sr = new StreamReader(vertex.normalized_file_path))
            {
                int idx = 0;

                while (++idx != 4)
                {
                    sr.ReadLine();
                }

                var asm_file_path = Util.NormalizePath(Path.Combine(Path.GetDirectoryName(
                    vertex.normalized_file_path), sr.ReadLine()));

                Vertex asm_vertex = graph.GetOrCreateVertex(asm_file_path);
                graph.TryAddUniqueEdge(vertex, asm_vertex, "asm_file");

                if (asm_vertex is HashFileVertex)
                {
                    asar_resolver.ResolveDependencies((HashFileVertex)asm_vertex);
                }
            }
        }
    }
}
