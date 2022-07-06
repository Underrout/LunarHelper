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

        private const string default_asm_directory = "asm";
        private const string default_sprites_directory = "sprites";
        private const string default_shooters_directory = "shooters";
        private const string default_generators_directory = "generators";
        private const string default_extended_directory = "extended";
        private const string default_cluster_directory = "cluster";
        private const string default_routine_directory = "routines";

        private string asm_directory;
        private string sprites_directory;
        private string shooters_directory;
        private string generators_directory;
        private string extended_directory;
        private string cluster_directory;
        private string routine_directory;

        public PixiResolver(DependencyGraph graph, string pixi_exe_path, string pixi_options)
        {
            this.graph = graph;
            pixi_directory = Util.NormalizePath(Path.GetDirectoryName(pixi_exe_path));

            DetermineDirectoryPaths(pixi_options);

            asar_resolver = CreateAsarResolver();
        }

        private AsarResolver CreateAsarResolver()
        {
            AsarResolver resolver = new AsarResolver(graph);

            foreach ((string generated_file_path, string tag) in asm_directory_generated_files)
            {
                var full_path = Util.NormalizePath(Path.Combine(asm_directory, generated_file_path));
                resolver.NameGeneratedFile(full_path, tag);
            }

            return resolver;
        }

        private void DetermineDirectoryPaths(string pixi_options)
        {
            // TODO make this actually account for different path specifiers in pixi_options

            asm_directory = default_asm_directory;
            sprites_directory = default_sprites_directory;
            shooters_directory = default_shooters_directory;
            generators_directory = default_generators_directory;
            extended_directory = default_extended_directory;
            cluster_directory = default_cluster_directory;
            routine_directory = default_routine_directory;
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

        public void ResolveToolRootDependencies(ToolRootVertex vertex)
        {
            throw new NotImplementedException();
        }
    }
}
