using QuickGraph;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;

using QuickGraph.Algorithms;
using QuickGraph.Algorithms.Search;
using QuickGraph.Algorithms.ConnectedComponents;

namespace LunarHelper
{
    public class Config
    {
        public string WorkingDirectory;

        public string OutputPath;
        public string TempPath;
        public string CleanPath;
        public string PackagePath;

        public string GlobulesPath;

        public string UberASMPath;
        public string UberASMOptions;

        public string GPSPath;
        public string GPSOptions;

        public string PixiPath;
        public string PixiOptions;

        public string AddMusicKPath;
        public string AddmusicKOptions;

        public string LunarMonitorLoaderPath;
        public string LunarMagicLevelImportFlags;
        public string LunarMonitorLoaderOptions;

        public string FlipsPath;

        public string HumanReadableMap16CLI;
        public string HumanReadableMap16Directory;

        public string InitialPatch;
        public string LevelsPath;
        public string Map16Path;
        public string SharedPalettePath;
        public string GlobalDataPath;
        public string TitleMovesPath;

        public List<string> Patches = new List<string>();

        public string TestLevel;
        public string TestLevelDest;
        public string EmulatorPath;
        public string EmulatorOptions;

        public bool ReloadEmulatorAfterBuild;
        public bool SuppressArbitraryDepsWarning;

        public bool InvokedOnCommandLine;

        public List<Insertable> BuildOrder = null;
        List<(Insertable, Insertable)> QuickBuildTriggers = new List<(Insertable, Insertable)>();
        public BidirectionalGraph<Insertable, Edge<Insertable>> QuickBuildTriggerGraph = null;

        #region load

        static public Config Load(out string error, Config config = null, string search_path_prefix = "")
        {
            error = "";

            try
            {
                var data = new List<string>();
                foreach (var file in Directory.EnumerateFiles(Directory.GetCurrentDirectory(), search_path_prefix + "config*.txt", SearchOption.TopDirectoryOnly))
                    data.Add(File.ReadAllText(file));
                return Load(data, config);
            }
            catch (Exception e)
            {
                error = e.Message;
                return null;
            }
        }

        static private Config Load(List<string> data, Config config)
        {
            if (config == null)
                config = new Config();

            var common_patches = new List<string>(config.Patches);

            Dictionary<string, string> vars = new Dictionary<string, string>();
            Dictionary<string, List<string>> lists = new Dictionary<string, List<string>>();
            List<Insertable> build_order = new List<Insertable>();
            List<(Insertable, Insertable)> quick_build_triggers = new List<(Insertable, Insertable)>();

            foreach (var d in data)
                Parse(d, vars, lists, build_order, quick_build_triggers);

            config.WorkingDirectory = vars.GetValueOrDefault("dir", config.WorkingDirectory);
            config.OutputPath = vars.GetValueOrDefault("output", config.OutputPath);
            config.TempPath = vars.GetValueOrDefault("temp", config.TempPath);
            config.CleanPath = vars.GetValueOrDefault("clean", config.CleanPath);
            config.PackagePath = vars.GetValueOrDefault("package", config.PackagePath);
            config.GlobulesPath = vars.GetValueOrDefault("globules_path", config.GlobulesPath);
            config.UberASMPath = vars.GetValueOrDefault("uberasm_path", config.UberASMPath);
            config.UberASMOptions = vars.GetValueOrDefault("uberasm_options", config.UberASMOptions);
            config.GPSPath = vars.GetValueOrDefault("gps_path", config.GPSPath);
            config.GPSOptions = vars.GetValueOrDefault("gps_options", config.GPSOptions);
            config.PixiPath = vars.GetValueOrDefault("pixi_path", config.PixiPath);
            config.PixiOptions = vars.GetValueOrDefault("pixi_options", config.PixiOptions);
            config.AddMusicKPath = vars.GetValueOrDefault("addmusick_path", config.AddMusicKPath);
            config.AddmusicKOptions = vars.GetValueOrDefault("addmusick_options", config.AddmusicKOptions);
            config.LunarMonitorLoaderPath = vars.GetValueOrDefault("lunar_monitor_loader_path", config.LunarMonitorLoaderPath);
            config.LunarMagicLevelImportFlags = vars.GetValueOrDefault("lm_level_import_flags", config.LunarMagicLevelImportFlags);
            config.LunarMonitorLoaderOptions = vars.GetValueOrDefault("lunar_monitor_loader_options", config.LunarMonitorLoaderOptions);
            config.FlipsPath = vars.GetValueOrDefault("flips_path", config.FlipsPath);
            config.HumanReadableMap16CLI = vars.GetValueOrDefault("human_readable_map16_cli_path", config.HumanReadableMap16CLI);
            config.HumanReadableMap16Directory = vars.GetValueOrDefault("human_readable_map16_directory_path", config.HumanReadableMap16Directory);
            config.LevelsPath = vars.GetValueOrDefault("levels", config.LevelsPath);
            config.Map16Path = vars.GetValueOrDefault("map16", config.Map16Path);
            config.SharedPalettePath = vars.GetValueOrDefault("shared_palette", config.SharedPalettePath);
            config.GlobalDataPath = vars.GetValueOrDefault("global_data", config.GlobalDataPath);
            config.TitleMovesPath = vars.GetValueOrDefault("title_moves", config.TitleMovesPath);
            config.InitialPatch = vars.GetValueOrDefault("initial_patch", config.InitialPatch);

            if (lists.ContainsKey("patches"))
            {
                config.Patches = common_patches.Concat(lists.GetValueOrDefault("patches")).ToList();
            }

            config.TestLevel = vars.GetValueOrDefault("test_level", config.TestLevel);
            config.TestLevelDest = vars.GetValueOrDefault("test_level_dest", config.TestLevelDest);
            config.EmulatorPath = vars.GetValueOrDefault("emulator_path", config.EmulatorPath);
            config.EmulatorOptions = vars.GetValueOrDefault("emulator_options", config.EmulatorOptions);

            if (vars.ContainsKey("reload_emulator_after_build"))
            {
                vars.TryGetValue("reload_emulator_after_build", out string reload_emulator);
                config.ReloadEmulatorAfterBuild = reload_emulator != null ? (new[] { "yes", "true" }).AsSpan().Contains(reload_emulator.Trim()) : false;
            }

            if (vars.ContainsKey("suppress_arbitrary_dependency_warning"))
            {
                vars.TryGetValue("suppress_arbitrary_dependency_warning", out string suppress_arbitrary_deps_warning);
                config.SuppressArbitraryDepsWarning = suppress_arbitrary_deps_warning != null ? (new[] { "yes", "true" }).AsSpan().Contains(suppress_arbitrary_deps_warning.Trim()) : false;
            }

            if (build_order.Count() != 0)
            {
                config.BuildOrder = build_order;
            }

            if (quick_build_triggers.Count() != 0)
            {
                config.QuickBuildTriggers = quick_build_triggers;
            }

            return config;
        }

        static private void Parse(string data, Dictionary<string, string> vars, Dictionary<string, 
            List<string>> lists, List<Insertable> build_order, List<(Insertable, Insertable)> quick_build_triggers)
        {
            var lines = data.Split('\n');

            List<string> cleaned_lines = new List<string>();

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                if (trimmed.StartsWith("--"))
                    continue;  // line is just comment, skip it

                if (trimmed == "")
                    continue;  // blank line, skip it

                int comment_start = trimmed.IndexOf("--");

                if (comment_start != -1)
                    trimmed = trimmed.Substring(0, comment_start);

                cleaned_lines.Add(trimmed);
            }

            lines = cleaned_lines.ToArray();

            for (int i = 0; i < lines.Length; i++)
            {
                string str = lines[i];
                string peek = null;
                if (i < lines.Length - 1)
                    peek = lines[i + 1];

                if (str.Contains('='))
                {
                    // var
                    var sp = str.Split('=', 2);
                    // if (sp.Length != 2)
                    //    throw new Exception("Malformed assignment");

                    var key = sp[0].Trim();
                    if (vars.ContainsKey(key))
                        throw new Exception($"Duplicate config key: '{key}'");
                    vars.Add(key, sp[1].Trim());
                }
                else if (peek != null && peek.Trim() == "[")
                {
                    var trimmed = str.Trim();
                    if (new[] {"quick_build_triggers", "build_order"}.Contains(trimmed))
                    {
                        ParseDependencyList(trimmed, build_order, quick_build_triggers, lines, i + 2);
                        continue;
                    }
                    // list
                    var list = new List<string>();
                    lists.Add(trimmed, list);
                    i += 2;

                    while (true)
                    {
                        if (i >= lines.Length)
                            throw new Exception("Malformed list");

                        str = lines[i];
                        if (str.Trim() == "]")
                            break;
                        else
                            list.Add(str.Trim().Replace('/', '\\').TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                .ToLowerInvariant());

                        i++;
                    }
                }
            }
        }

        static private void ParseDependencyList(string list_name, List<Insertable> build_order, 
            List<(Insertable, Insertable)> quick_build_triggers, string[] lines, int line_idx)
        {
            while (true)
            {
                if (line_idx >= lines.Length)
                    throw new Exception("Malformed list");

                string str = lines[line_idx];
                if (str.Trim() == "]")
                    break;
                else
                {
                    if (list_name == "build_order")
                    {
                        build_order.Add(ParseInsertable(str));
                    }
                    else
                    {
                        (var trigger, var insertable) = ParseQuickBuildTrigger(str);
                        quick_build_triggers.Add((trigger, insertable));
                    }
                }

                line_idx++;
            }
        }

        static private (Insertable, Insertable) ParseQuickBuildTrigger(string line)
        {
            string trimmed_line = line.Trim();

            var split = trimmed_line.Split(" -> ");

            if (split.Length != 2)
                throw new Exception($"Malformed Quick Build trigger: '{line}'");

            return (ParseInsertable(split[0], true), ParseInsertable(split[1], true));
        }

        static private Insertable ParseInsertable(string insertable_string, bool is_quickbuild = false)
        {
            string trimmed = insertable_string.Trim();

            InsertableType insertable_type;
            bool success = Enum.TryParse(trimmed, true, out insertable_type);

            if (is_quickbuild && insertable_type == InsertableType.Patches)
            {
                throw new Exception("'Patches' cannot be used in Quick Build triggers, please specify individual patches instead");
            }

            if (success && insertable_type != InsertableType.SinglePatch && insertable_type != InsertableType.SingleLevel)
            {
                return new Insertable(insertable_type);
            }
            else
            {
                return new Insertable(InsertableType.SinglePatch, trimmed);
            }
        }

        static private Dictionary<InsertableType, Action<Config>> verifications = new Dictionary<InsertableType, Action<Config>>()
        {
            { InsertableType.Pixi, VerifyPixi },
            { InsertableType.AddMusicK, VerifyAmk },
            { InsertableType.Gps, VerifyGps },
            { InsertableType.UberAsm, VerifyUberAsm },
            { InsertableType.Graphics, NoVerify },
            { InsertableType.ExGraphics, NoVerify },
            { InsertableType.Map16, VerifyMap16 },
            { InsertableType.SharedPalettes, VerifySharedPalettes },
            { InsertableType.GlobalData, VerifyGlobalData },
            { InsertableType.TitleMoves, VerifyTitleMoves },
            { InsertableType.Levels, VerifyLevels },
            { InsertableType.Patches, VerifyPatches }
        };

        private static void VerifyInsertable(Config config, Insertable insertable)
        {
            var type = insertable.type;

            if (type == InsertableType.SinglePatch)
                VerifyPatch(config, insertable.normalized_relative_path);
            else
                verifications[type](config);
        }

        public static void VerifyConfig(Config config)
        {
            if (config.BuildOrder == null)
            {
                throw new Exception("'build_order' list must be specified");
            }
            if (config.QuickBuildTriggers == null)
            {
                throw new Exception("'quick_build_triggers' list must be specified");
            }

            if (config.Patches.Count() != 0 && !(config.BuildOrder.Any(i => i.type == InsertableType.Patches) ||
                config.Patches.All(p => config.BuildOrder.Contains(new Insertable(InsertableType.SinglePatch, p)))))
            {
                throw new Exception("Not all patches listed in 'patches' appear in 'build_order'");
            }

            foreach (var item in config.BuildOrder)
                VerifyInsertable(config, item);

            BidirectionalGraph<Insertable, Edge<Insertable>> trigger_graph = new BidirectionalGraph<Insertable, Edge<Insertable>>();

            foreach ((var trigger, var insertable) in config.QuickBuildTriggers)
            {
                if (!config.BuildOrder.Contains(trigger))
                {
                    if (trigger.type != InsertableType.SinglePatch || !config.BuildOrder.Any(i => i.type == InsertableType.Patches))
                        throw new Exception("Resources used in 'quick_build_triggers' must also be present in 'build_order'");
                }

                if (!config.BuildOrder.Contains(insertable))
                {
                    if (insertable.type != InsertableType.SinglePatch || !config.BuildOrder.Any(i => i.type == InsertableType.Patches))
                        throw new Exception("Resources used in 'quick_build_triggers' must also be present in 'build_order'");
                }

                trigger_graph.AddVertex(trigger);
                trigger_graph.AddVertex(insertable);
                trigger_graph.AddEdge(new Edge<Insertable>(trigger, insertable));
            }

            if (trigger_graph.Edges.Any(e => e.Source.Equals(e.Target)))
                throw new Exception("Cyclic triggers detected in Quick Build trigger list");

            var ssc = new StronglyConnectedComponentsAlgorithm<Insertable, Edge<Insertable>>(trigger_graph);

            ssc.Compute();

            bool cycles_present = ssc.ComponentCount != trigger_graph.VertexCount;  // if all strongly connected components are
                                                                                    // single vertices, our graph is acyclic

            if (cycles_present)
                throw new Exception("Cyclic triggers detected in Quick Build trigger list");

            config.QuickBuildTriggerGraph = trigger_graph;

            foreach ((var trigger, var insertable) in config.QuickBuildTriggers)
            {
                VerifyInsertable(config, trigger);
                VerifyInsertable(config, insertable);
            }
        }

        private static void VerifyPatch(Config config, string patch_path)
        {
            if (!File.Exists(patch_path))
                throw new Exception($"Patch '{patch_path}' not found");

            if (!config.Patches.Contains(patch_path))
                throw new Exception($"Patch '{patch_path}' not found");
        }

        private static void VerifyPatches(Config config)
        {
            foreach (var patch in config.Patches)
            {
                VerifyPatch(config, patch);
            }
        }

        private static void VerifyUnspecifiedOrExists(Config config, InsertableType insertable_type, 
            string corresponding_variable, string tool_name, string tool_path)
        {
            if (string.IsNullOrWhiteSpace(tool_path))
                VerifyNotInBuildOrQuickTriggers(config, insertable_type, corresponding_variable);

            if (!File.Exists(tool_path))
                throw new Exception($"{tool_name} not found at path '{tool_path}'");
        }

        private static void VerifyNotInBuildOrQuickTriggers(Config config, InsertableType type, string corresponding_variable)
        {
            if (config.BuildOrder.Any(i => i.type == type))
            {
                throw new Exception($"{corresponding_variable} not specified, but {type} found in build_order list");
            }

            if (config.QuickBuildTriggerGraph.Vertices.Any(i => i.type == type))
            {
                throw new Exception($"{corresponding_variable} not specified, but {type} found in quick_build_triggers list");
            }
        }

        private static void VerifyPixi(Config config)
        {
            VerifyUnspecifiedOrExists(config, InsertableType.Pixi, "pixi_path", "PIXI", config.PixiPath);
        }

        private static void VerifyAmk(Config config)
        {
            VerifyUnspecifiedOrExists(config, InsertableType.AddMusicK, "addmusick_path", "AddMusicK", config.AddMusicKPath);
        }

        private static void VerifyGps(Config config)
        {
            VerifyUnspecifiedOrExists(config, InsertableType.Gps, "gps_path", "GPS", config.GPSPath);
        }

        private static void VerifyUberAsm(Config config)
        {
            VerifyUnspecifiedOrExists(config, InsertableType.UberAsm, "uberasm_path", "UberASM Tool", config.UberASMPath);
        }

        private static void NoVerify(Config config)
        {
            // nothing to verify
        }

        private static void VerifyMap16(Config config)
        {
            if (string.IsNullOrWhiteSpace(config.Map16Path))
                VerifyNotInBuildOrQuickTriggers(config, InsertableType.Map16, "map16");

            if (string.IsNullOrWhiteSpace(config.HumanReadableMap16CLI))
                return;

            if (!File.Exists(config.HumanReadableMap16CLI))
                throw new Exception($"Human Readable Map16 CLI not found at path '{config.HumanReadableMap16CLI}'");
        }

        private static void VerifyTitleMoves(Config config)
        {
            if (string.IsNullOrWhiteSpace(config.TitleMovesPath))
                VerifyNotInBuildOrQuickTriggers(config, InsertableType.TitleMoves, "title_moves");
        }

        private static void VerifySharedPalettes(Config config)
        {
            if (string.IsNullOrWhiteSpace(config.SharedPalettePath))
                VerifyNotInBuildOrQuickTriggers(config, InsertableType.SharedPalettes, "shared_palette");
        }

        private static void VerifyLevels(Config config)
        {
            if (string.IsNullOrWhiteSpace(config.LevelsPath))
                VerifyNotInBuildOrQuickTriggers(config, InsertableType.Levels, "levels");
        }

        private static void VerifyGlobalData(Config config)
        {
            if (string.IsNullOrWhiteSpace(config.GlobalDataPath))
                VerifyNotInBuildOrQuickTriggers(config, InsertableType.GlobalData, "global_data");
        }

        #endregion
    }
}
