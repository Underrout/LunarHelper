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

        public enum SpriteType
        {
            Normal,
            Shooter,
            Generator,
            Cluster,
            Extended
        }

        // range of sprite numbers that correspond to shooters (inclusive on both ends)
        private (int, int) shooter_sprite_range = (0xC0, 0xCF);

        // range of sprite numbers that correspond to generators (inclusive on both ends)
        private (int, int) generator_sprite_range = (0xD0, 0xFF);

        // for cfg files
        private const string cfg_sprite_tag = "cfg_sprite";

        // for json files
        private const string json_sprite_tag = "json_sprite";

        // for asm files (cluster & extended don't use config files)
        private const string asm_sprite_tag = "asm_sprite";

        private const string normal_sprite_tag = "normal";

        private const string shooter_sprite_tag = "shooter";

        private const string generator_sprite_tag = "generator";

        private const string cluster_sprite_tag = "cluster";

        private const string extended_sprite_tag = "extended";

        // tag used between config files and the asm file they refer to
        private const string config_to_asm_tag = "asm_file";

        private readonly Regex list_section = new Regex(
            @"^\s*(?i)(?<section>SPRITE|EXTENDED|CLUSTER|)(?-i):",
            RegexOptions.Compiled | RegexOptions.Multiline
        );

        // by "normal" I mean not per-level
        private readonly Regex normal_sprite = new Regex(
            @"^\s*(?<number>[a-fA-F0-9]+)\s+(?<path>(.*?)(?<extension>\.cfg|\.json|\.asm))",
            RegexOptions.Compiled | RegexOptions.Multiline
        );

        private readonly Regex per_level_sprite = new Regex(
            @"^\s*(?<number>[a-fA-F0-9]+:[a-fA-F0-9]+)\s+(?<path>(.*?)(?<extension>\.cfg|\.json|\.asm))",
            RegexOptions.Compiled | RegexOptions.Multiline
        );

        private readonly Regex cfg_asm_path = new Regex(
            @"^\s*(?<path>(?:.*).asm)",
            RegexOptions.Compiled | RegexOptions.Multiline
        );

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
            ( "_ClusterPtr.bin", "cluster_ptr" ),
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
            root_dependencies = DetermineRootDependencies(pixi_exe_path);
        }

        private RootDependencyList DetermineRootDependencies(string pixi_exe_path)
        {
            RootDependencyList dependencies = new RootDependencyList { (pixi_exe_path, "exe", RootDependencyType.Binary) };

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
                    HashFileVertex dependency_file = (HashFileVertex)dependency;

                    switch (type)
                    {
                        case RootDependencyType.Asar:
                            asar_resolver.ResolveDependencies(dependency_file);
                            break;

                        case RootDependencyType.Binary:
                            seen.Add(dependency);
                            break;

                        case RootDependencyType.SpriteList:
                            ResolveSpriteList(dependency_file);
                            break;
                    }
                }
            }
        }

        private void ResolveSpriteList(HashFileVertex vertex)
        {
            seen.Add(vertex);

            using (StreamReader sr = new StreamReader(vertex.normalized_file_path))
            {
                string line;

                SpriteType curr_section = SpriteType.Normal;

                while ((line = sr.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }
                    else
                    {
                        Match match = normal_sprite.Match(line);

                        if (match.Success)
                        {
                            var number = int.Parse(match.Groups["number"].Value, System.Globalization.NumberStyles.HexNumber);

                            SpriteType type = curr_section;

                            if (type == SpriteType.Normal)
                            {
                                // check what kind of "normal" sprite it is
                                if (number >= shooter_sprite_range.Item1 && number <= shooter_sprite_range.Item2)
                                {
                                    type = SpriteType.Shooter;
                                }
                                else if (number >= generator_sprite_range.Item1 && number <= generator_sprite_range.Item2)
                                {
                                    type = SpriteType.Generator;
                                }
                            }

                            ResolveSprite(vertex, match.Groups["path"].Value, number.ToString(), type);
                            continue;
                        }

                        match = list_section.Match(line);

                        if (match.Success)
                        {
                            switch (match.Groups["section"].Value.ToLower())
                            {
                                case "sprite":
                                    curr_section = SpriteType.Normal;
                                    break;

                                case "extended":
                                    curr_section = SpriteType.Extended;
                                    break;

                                case "cluster":
                                    curr_section = SpriteType.Cluster;
                                    break;
                            }
                            continue;
                        }

                        match = per_level_sprite.Match(line);

                        if (match.Success)
                        {
                            var number = match.Groups["number"].Value.ToLowerInvariant();

                            ResolveSprite(vertex, match.Groups["path"].Value, number, curr_section);
                            continue;
                        }
                    }
                }
            }
        }

        private void ResolveSprite(Vertex list_vertex, string relative_path, string number, SpriteType type)
        {
            string base_path = null;

            string tag_base = null;

            switch (type)
            {
                case SpriteType.Normal:
                    base_path = sprites_directory;
                    tag_base = normal_sprite_tag;
                    break;

                case SpriteType.Cluster:
                    base_path = cluster_directory;
                    tag_base = cluster_sprite_tag;
                    break;

                case SpriteType.Extended:
                    base_path = extended_directory;
                    tag_base = extended_sprite_tag;
                    break;

                case SpriteType.Shooter:
                    base_path = shooters_directory;
                    tag_base = shooter_sprite_tag;
                    break;

                case SpriteType.Generator:
                    base_path = generators_directory;
                    tag_base = generator_sprite_tag;
                    break;
            }

            tag_base += "_";

            // we know one of these three must be the extension, because we matched it in the regex!
            //
            // note that if we combine this switch case with the one from above, we're assuming that 
            // every sprite has the correct extension for its section, which is definitely not always 
            // the case judging by the times I've accidentally inserted an .asm file as a normal sprite
            // and then debugged it for hours
            switch (Path.GetExtension(relative_path))
            {
                case ".cfg":
                    tag_base += cfg_sprite_tag;
                    break;

                case ".json":
                    tag_base += json_sprite_tag;
                    break;

                case ".asm":
                    tag_base += asm_sprite_tag;
                    break;
            }

            var tag = $"{tag_base}_{number}";

            var path = Util.NormalizePath(Path.Combine(base_path, relative_path));

            Vertex sprite_config_file_vertex = graph.GetOrCreateVertex(path);
            graph.AddEdge(list_vertex, sprite_config_file_vertex, tag);
            
            if (sprite_config_file_vertex is HashFileVertex)
            {
                switch (Path.GetExtension(relative_path))
                {
                    case ".cfg":
                        ResolveCfgFileDependencies((HashFileVertex)sprite_config_file_vertex);
                        break;

                    case ".json":
                        ResolveJsonFileDependencies((HashFileVertex)sprite_config_file_vertex);
                        break;

                    case ".asm":
                        // not actually a sprite config file, it's just an actual extended or cluster sprite,
                        // but I'm not going to rename the variable just to reflect that so, have this comment
                        // instead
                        asar_resolver.ResolveDependencies((HashFileVertex)sprite_config_file_vertex);
                        break;
                }
            }
            else
            {
                seen.Add(sprite_config_file_vertex);
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
            seen.Add(vertex);

            string contents = File.ReadAllText(vertex.normalized_file_path);
            JsonNode node;

            try
            {
                node = JsonNode.Parse(contents);
            }
            catch
            {
                node = null;
            }

            var relative_asm_path = node != null && node["AsmFile"] != null ? node["AsmFile"].ToString() : "";

            var asm_file_path = Util.NormalizePath(Path.Combine(Path.GetDirectoryName(
                    vertex.normalized_file_path), relative_asm_path));

            Vertex asm_vertex = graph.GetOrCreateVertex(asm_file_path);
            graph.TryAddUniqueEdge(vertex, asm_vertex, config_to_asm_tag);

            if (asm_vertex is HashFileVertex)
            {
                asar_resolver.ResolveDependencies((HashFileVertex)asm_vertex);
            }
            else
            {
                seen.Add(asm_vertex);
            }
        }

        private void ResolveCfgFileDependencies(HashFileVertex vertex)
        {
            seen.Add(vertex);

            var contents = File.ReadAllText(vertex.normalized_file_path);
            Match match = cfg_asm_path.Match(contents);
            var path = "";

            if (match.Success)
            {
                path = match.Groups["path"].Value;
            }

            var asm_file_path = Util.NormalizePath(Path.Combine(Path.GetDirectoryName(
                vertex.normalized_file_path), path));

            Vertex asm_vertex = graph.GetOrCreateVertex(asm_file_path);
            graph.TryAddUniqueEdge(vertex, asm_vertex, config_to_asm_tag);

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
