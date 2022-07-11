using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.IO;

namespace LunarHelper.Resolvers
{
    // relative path, tag to assign, dependency type
    using RootDependencyList = List<(string, string, AmkResolver.RootDependencyType)>;

    class AmkResolver : IToolRootResolve
    {
        private readonly AsarResolver asar_resolver;

        private HashSet<Vertex> seen = new HashSet<Vertex>();

        private Vertex exe_vertex;

        public enum RootDependencyType
        {
            Asar,
            Binary,
            SongList,
            SampleGroups,
            SoundEffectList
        }

        private readonly RootDependencyList static_root_dependencies = new RootDependencyList
        {
            ( amk_asar_exe_name, "asar", RootDependencyType.Binary ),
            ( "Addmusic_list.txt", "song_list", RootDependencyType.SongList ),
            ( "Addmusic_sample groups.txt", "sample_groups", RootDependencyType.SampleGroups ),
            ( "Addmusic_sound effects.txt", "sound_effect_list", RootDependencyType.SoundEffectList ),
            ( "asm\\main.asm", "main", RootDependencyType.Asar ),
            ( "asm\\SNES\\AMUndo.asm", "am_undo", RootDependencyType.Asar ),
            ( "asm\\SNES\\patch.asm", "patch", RootDependencyType.Asar ),
            ( "asm\\SNES\\patch2.asm", "patch2", RootDependencyType.Asar ),
            ( "asm\\SNES\\SPCBase.bin", "spc_base", RootDependencyType.Binary ),
            ( "asm\\SNES\\SPCDSPBase.bin", "scpdsp_base", RootDependencyType.Binary )
        };

        private const string amk_samples_folder_name = "samples";
        private const string amk_music_folder_name = "music";
        private const string amk_1DF9_folder_name = "1DF9";
        private const string amk_1DFC_folder_name = "1DFC";

        private const string amk_asar_exe_name = "asar.exe";

        private (string, string) amk_song_sample_list_ignore = ( "asm\\SNES\\SongSampleList.asm", "song_sample_list" );

        private readonly string amk_directory;
        private readonly DependencyGraph graph;

        // matches #path and #samples constructs
        // if #path is matched, the single <path> capture will contain the new #path
        // if #samples is matched, the <samples> captures will contain the sample paths contained in the contstruct
        private static readonly Regex music_samples_and_paths = new Regex(
            "(?:(?:#(?i)path(?-i)\\s*(?:\"(?<path>.*)\"))|(?:#(?i)samples(?-i)\\s*{\\s*(?:(?:(?:#[^\\s]*)|(?:\"(?<samples>.*)\"))\\s*)*}))",
            RegexOptions.Compiled
        );

        // single group that's captured should contain all samples mentioned in the "Addmusic_sample groups.txt" file
        private static readonly Regex samples_in_sample_groups = new Regex(
            "#(?<sample_group>[^\\s]+)\\s+{\\s*(?:\"(?<samples>.*)\"\\s*!?\\s*)*}", RegexOptions.Compiled);

        // grabs file names for every sound effect in <path> and also the address (1DF9 or 1DFC) that they're used in in <sfx_addr>
        // to be used on "Addmusic_sound effects.txt"
        private static readonly Regex sound_effects_and_addresses = new Regex(
            @"^(?:(?<number>[a-fA-F0-9]{1,2})\s+[?*]?\s*(?<path>.*.txt)\s*)|(?:\s*SFX(?<sfx_addr>.*):\s*)", 
            RegexOptions.Compiled | RegexOptions.Multiline
        );

        private static readonly Regex songs_regex = new Regex(
            @"^(?<number>[a-fA-F0-9]{1,2})\s+(?<path>.*.txt)\s*$", RegexOptions.Compiled | RegexOptions.Multiline
        );

        public AmkResolver(DependencyGraph graph, string amk_exe_path)
        {
            this.graph = graph;
            amk_directory = Path.GetDirectoryName(amk_exe_path);
            asar_resolver = new AsarResolver(graph, seen, Path.Combine(amk_directory, amk_asar_exe_name));

            var full_song_sample_list_ignore_path = Path.Combine(amk_directory, amk_song_sample_list_ignore.Item1);

            asar_resolver.NameGeneratedFile(full_song_sample_list_ignore_path, amk_song_sample_list_ignore.Item2);

            exe_vertex = graph.GetOrCreateVertex(amk_exe_path);
        }

        public void ResolveToolRootDependencies(ToolRootVertex vertex)
        {
            foreach (var root_dependency in static_root_dependencies)
            {
                (string relative_path, string tag, RootDependencyType type) = root_dependency;

                Vertex dependency = graph.GetOrCreateVertex(Path.Combine(amk_directory, relative_path));
                graph.TryAddUniqueEdge(vertex, dependency, tag);

                if (dependency is HashFileVertex)
                {
                    HashFileVertex depencency_file = (HashFileVertex)dependency;

                    switch (type)
                    {
                        case RootDependencyType.Asar: 
                            asar_resolver.ResolveDependencies(depencency_file);
                            break;

                        case RootDependencyType.SongList:
                            ResolveSongListDependencies(depencency_file);
                            break;

                        case RootDependencyType.SampleGroups:
                            ResolveSampleGroupDependencies(depencency_file);
                            break;

                        case RootDependencyType.SoundEffectList:
                            ResolveSoundEffectListDependencies(depencency_file);
                            break;

                        case RootDependencyType.Binary:
                            seen.Add(dependency);
                            break;

                        default:
                            break;
                    }
                }
            }

            graph.TryAddUniqueEdge(vertex, exe_vertex, "exe");

            if (asar_resolver.stddefines_vertex != null)
            {
                // I believe since amk comes with an asar exe rather than dll, you 
                // actually can use stddefines with it, so we're going to handle that case
                graph.TryAddUniqueEdge(vertex, asar_resolver.stddefines_vertex, "stddefines");
            }
        }

        private void ResolveSongListDependencies(HashFileVertex vertex)
        {
            if (seen.Contains(vertex))
            {
                return;
            }

            seen.Add(vertex);

            string contents = File.ReadAllText(vertex.uri.LocalPath);

            var matches = songs_regex.Matches(contents);

            var base_music_path = Path.Combine(amk_directory, amk_music_folder_name);

            foreach (Match match in matches)
            {
                var song_path = Path.Combine(base_music_path, match.Groups["path"].Value);
                var song_number = int.Parse(match.Groups["number"].Value, System.Globalization.NumberStyles.HexNumber);
                var tag = $"song_{song_number}";

                Vertex song_vertex = graph.GetOrCreateVertex(song_path);
                graph.AddEdge(vertex, song_vertex, tag);

                if (song_vertex is HashFileVertex)
                {
                    ResolveSongDependencies((HashFileVertex)song_vertex);
                }
                else
                {
                    seen.Add(song_vertex);
                }
            }
        }

        private void ResolveSampleGroupDependencies(HashFileVertex vertex)
        {
            if (seen.Contains(vertex))
            {
                return;
            }

            seen.Add(vertex);

            string contents = File.ReadAllText(vertex.uri.LocalPath);

            var matches = samples_in_sample_groups.Matches(contents);

            var base_sample_path = Path.Combine(amk_directory, amk_samples_folder_name);

            foreach (Match match in matches)
            {
                int sample_id = 0;
                var sample_group = match.Groups["sample_group"].Value;

                foreach (Capture capture in match.Groups["samples"].Captures)
                {
                    var sample_path = Path.Combine(base_sample_path, capture.Value);
                    var tag = $"sample_group_{sample_group}_{sample_id++}";

                    Vertex sample_vertex = graph.GetOrCreateVertex(sample_path);
                    graph.AddEdge(vertex, sample_vertex, tag);
                    seen.Add(sample_vertex);

                    // not calling resolve on sample vertex here since it's a binary file
                }
            }
        }

        private void ResolveSoundEffectListDependencies(HashFileVertex vertex)
        {
            if (seen.Contains(vertex))
            {
                return;
            }

            seen.Add(vertex);

            // BIG NOTE: sound effects can have asm in them, and yes, they can incsrc/incbin!
            //           probably can just treat them as asar files when resolving dependencies

            string contents = File.ReadAllText(vertex.uri.LocalPath);

            var matches = sound_effects_and_addresses.Matches(contents);

            var sfx_addr_1DF9_folder = Path.Combine(amk_directory, amk_1DF9_folder_name);
            var sfx_addr_1DFC_folder = Path.Combine(amk_directory, amk_1DFC_folder_name);

            var tag_prefix_1DF9 = amk_1DF9_folder_name.ToLower();
            var tag_prefix_1DFC = amk_1DFC_folder_name.ToLower();

            // I have no basis for this choice of initialization, but I don't know if it matters,
            // cause I'm pretty sure the first statement in the file has to be a label anyway
            var curr_sfx_folder = sfx_addr_1DF9_folder;
            var curr_tag_prefix = tag_prefix_1DF9;

            foreach (Match match in matches)
            {
                if (match.Groups["path"].Success)
                {
                    var sound_effect_path = Path.Combine(curr_sfx_folder, match.Groups["path"].Value);
                    var sound_effect_number = int.Parse(match.Groups["number"].Value, System.Globalization.NumberStyles.HexNumber);
                    var tag = $"sound_effect_{curr_tag_prefix}_{sound_effect_number}";

                    Vertex sound_effect_vertex = graph.GetOrCreateVertex(sound_effect_path);
                    graph.AddEdge(vertex, sound_effect_vertex, tag);

                    if (sound_effect_vertex is HashFileVertex)
                    {
                        // resolving this with our instance of the asar resolver, since sound effects can contain asm
                        asar_resolver.ResolveDependencies((HashFileVertex)sound_effect_vertex);
                    }
                }
                else if (match.Groups["sfx_addr"].Success)
                {
                    if (match.Groups["sfx_addr"].Captures[0].Value == "1DF9")
                    {
                        curr_sfx_folder = sfx_addr_1DF9_folder;
                        curr_tag_prefix = tag_prefix_1DF9;
                    }
                    else
                    {
                        curr_sfx_folder = sfx_addr_1DFC_folder;
                        curr_tag_prefix = tag_prefix_1DFC;
                    }
                }
            }
        }

        private void ResolveSongDependencies(HashFileVertex vertex)
        {
            if (seen.Contains(vertex))
            {
                return;
            }

            seen.Add(vertex);

            string contents = File.ReadAllText(vertex.uri.LocalPath);

            var matches = music_samples_and_paths.Matches(contents);

            var base_sample_path = Path.Combine(amk_directory, amk_samples_folder_name);
            var curr_path = base_sample_path;

            int sample_id = 0;
            foreach (Match match in matches)
            {
                if (match.Groups["path"].Success)
                {
                    curr_path = Path.Combine(base_sample_path, match.Groups["path"].Value);
                }
                else if (match.Groups["samples"].Success)
                {
                    foreach (Capture capture in match.Groups["samples"].Captures)
                    {
                        var sample_path = Path.Combine(curr_path, capture.Value);
                        var tag = $"sample_{sample_id}";

                        Vertex sample_vertex = graph.GetOrCreateVertex(sample_path);
                        if (graph.TryAddUniqueEdge(vertex, sample_vertex, tag))
                        {
                            sample_id++;
                        }

                        seen.Add(sample_vertex);
                        // not resolving sample vertex since it's binary
                    }
                }
            }
        }
    }
}
