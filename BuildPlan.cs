using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace LunarHelper
{
    class BuildPlan
    {
        public bool uptodate { get; set; } = true;
        public bool rebuild { get; set; } = false;
        public bool apply_gps { get; set; } = false;
        public bool apply_pixi { get; set; } = false;
        public bool may_need_two_pixi_passes { get; set; } = false;
        public bool apply_uberasm { get; set; } = false;
        public bool apply_addmusick { get; set; } = false;
        public bool insert_map16 { get; set; } = false;
        public bool insert_gfx { get; set; } = false;
        public bool insert_exgfx { get; set; } = false;
        public bool insert_shared_palettes { get; set; } = false;
        public bool insert_global_patch { get; set; } = false;
        public bool insert_title_moves { get; set; } = false;
        public IList<string> patches_to_apply { get; set; } = new List<string>();
        public IList<string> levels_to_insert { get; set; } = new List<string>();
        public bool insert_all_levels { get; set; } = false;

        public class CannotBuildException : Exception
        {
            public CannotBuildException(string message) : base(message)
            {

            }
        }

        public static BuildPlan PlanBuild(Config config, DependencyGraph dependency_graph)
        {
            var plan = new BuildPlan();

            // check whether we need to rebuild completely

            Program.Log("Analyzing previous build result", ConsoleColor.Cyan);

            if (!File.Exists(config.OutputPath))
            {
                Program.Log("No previously built ROM found, rebuilding ROM...", ConsoleColor.Yellow);
                plan.rebuild = true;
                plan.uptodate = false;
                return plan;
            }

            Program.Log($"Previously built ROM found at '{config.OutputPath}'!", ConsoleColor.Green);
            Console.WriteLine();

            if (!File.Exists(".lunar_helper\\build_report.json"))
            {
                Program.Log("No previous build report found, rebuilding ROM...", ConsoleColor.Yellow);
                Console.WriteLine();
                plan.rebuild = true;
                plan.uptodate = false;
                return plan;
            }

            Report report;
            try
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.TypeNameHandling = TypeNameHandling.Objects;

                using (StreamReader sr = new StreamReader(".lunar_helper\\build_report.json"))
                using (JsonReader reader = new JsonTextReader(sr))
                {
                    report = (Report)serializer.Deserialize(reader, typeof(Report));
                }
            }
            catch(Exception)
            {
                Program.Log("Previous build report was found but corrupted, rebuilding ROM...", ConsoleColor.Yellow);
                Console.WriteLine();
                plan.rebuild = true;
                plan.uptodate = false;
                return plan;
            }

            if (report.report_format_version != Report.REPORT_FORMAT_VERSION)
            {
                Program.Log("Previous build report found but used old format, rebuilding ROM...", ConsoleColor.Yellow);
                Console.WriteLine();
                plan.rebuild = true;
                plan.uptodate = false;
                return plan;
            }

            foreach (var patch_root in dependency_graph.patch_roots)
            {
                if (patch_root is not PatchRootVertex)
                {
                    var exception = new CannotBuildException($"Patch \"{((MissingFileOrDirectoryVertex)patch_root).uri.LocalPath}\" could not be found!");
                    Program.Error(exception.Message);
                    throw exception;
                }
            }

            IEnumerable<PatchRootVertex> patch_roots = dependency_graph.patch_roots.Cast<PatchRootVertex>();
            (var need_rebuild, var results) = DependencyGraphAnalyzer.Analyze(dependency_graph, patch_roots, report.dependency_graph);

            if (need_rebuild)
            {
                Program.Log("Previously built ROM contains patches that need to be removed, rebuilding ROM...", ConsoleColor.Yellow);
                plan.rebuild = true;
                plan.uptodate = false;
                return plan;
            }

            Dictionary<string, string> oldLevels = report.levels;
            Dictionary<string, string> newLevels = Program.GetLevelReport();

            if (report.levels != null)
            {
                var oldLevelPaths = new HashSet<string>(report.levels.Keys);
                var newLevelPaths = (config.LevelsPath == null || !Directory.Exists(config.LevelsPath)) ? null : 
                    new HashSet<string>(Directory.GetFiles(config.LevelsPath, "*.mwl", SearchOption.TopDirectoryOnly).Select(levelPath => levelPath.Replace("\\", "/")));

                if (newLevelPaths == null || !oldLevelPaths.IsSubsetOf(newLevelPaths))
                {
                    Program.Log("Previously built ROM contains levels that need to be removed, rebuilding ROM...", ConsoleColor.Yellow);
                    Console.WriteLine();
                    plan.rebuild = true;
                    plan.uptodate = false;
                    return plan;
                }
            }

            if (Report.HashFile(config.InitialPatch) != report.init_bps)
            {
                Program.Log("Change in initial patch detected, rebuilding ROM...", ConsoleColor.Yellow);
                Console.WriteLine();
                plan.rebuild = true;
                plan.uptodate = false;
                return plan;
            }

            Program.Log("Attempting to reuse previously built ROM...", ConsoleColor.Cyan);
            Console.WriteLine();

            var tools = new[]
{
                ("GPS", ToolRootVertex.Tool.Gps, dependency_graph.gps_root, config.GPSOptions, report.gps_options),
                ("PIXI", ToolRootVertex.Tool.Pixi, dependency_graph.pixi_root, config.PixiOptions, report.pixi_options),
                ("AddmusicK", ToolRootVertex.Tool.Amk, dependency_graph.amk_root, config.AddmusicKOptions, report.addmusick_options),
                ("UberASM Tool", ToolRootVertex.Tool.UberAsm, dependency_graph.uberasm_root, config.UberASMOptions, report.uberasm_options)
            };

            foreach ((var tool_name, var tool_type, ToolRootVertex tool_root, string new_options, string old_options) in tools)
            {
                Program.Log($"Analyzing {tool_name} dependencies...", ConsoleColor.Cyan);

                if (new_options != old_options)
                {
                    Program.Log($"{tool_name} command line options changed from \"{old_options}\" to \"{new_options}\", {tool_name} will be reinserted...",
                        ConsoleColor.Yellow);
                    plan.uptodate = false;

                    switch (tool_type)
                    {
                        case ToolRootVertex.Tool.Gps:
                            plan.apply_gps = true;
                            break;

                        case ToolRootVertex.Tool.Pixi:
                            plan.apply_pixi = true;
                            break;

                        case ToolRootVertex.Tool.Amk:
                            plan.apply_addmusick = true;
                            break;

                        case ToolRootVertex.Tool.UberAsm:
                            plan.apply_uberasm = true;
                            break;
                    }
                    Console.WriteLine();
                    continue;
                }

                (var result, var dependency_chain) = DependencyGraphAnalyzer.Analyze(dependency_graph, tool_type, tool_root, report.dependency_graph);
                if (result == DependencyGraphAnalyzer.Result.Identical)
                {
                    Program.Log($"{tool_name} dependencies already up-to-date!", ConsoleColor.Green);
                }
                else
                {
                    if (result == DependencyGraphAnalyzer.Result.NoRoots)
                    {
                        Program.Log($"No old or new {tool_name} dependencies found, tool will not be inserted", ConsoleColor.Red);
                        continue;
                    }
                    else if (result == DependencyGraphAnalyzer.Result.OldRoot)
                    {
                        var exception = new CannotBuildException($"{tool_name} was previously inserted into the ROM but is no longer designated for insertion, if this is" +
                            $" not a mistake, please rebuild the ROM from scratch using the Build function to remove the tool from the ROM, aborting...");
                        Program.Error(exception.Message);
                        throw exception;
                    }

                    Program.Log(GetQuickBuildReasonString(config, tool_name, result, dependency_chain), ConsoleColor.Yellow);

                    plan.uptodate = false;

                    switch (tool_type)
                    {
                        case ToolRootVertex.Tool.Gps:
                            plan.apply_gps = true;
                            break;

                        case ToolRootVertex.Tool.Pixi:
                            plan.apply_pixi = true;
                            break;

                        case ToolRootVertex.Tool.Amk:
                            plan.apply_addmusick = true;
                            break;

                        case ToolRootVertex.Tool.UberAsm:
                            plan.apply_uberasm = true;
                            break;
                    }
                }
                Console.WriteLine();
            }

            bool no_reinsertions = true;
            Program.Log("Analyzing patch dependencies...", ConsoleColor.Cyan);

            foreach ((var patch_root, (var result, var dependency_chain)) in results)
            {
                if (result != DependencyGraphAnalyzer.Result.Identical)
                {
                    no_reinsertions = false;
                    Program.Log(GetQuickBuildReasonString(config, $"Patch \"{patch_root.normalized_relative_patch_path}\"", result, dependency_chain),
                        ConsoleColor.Yellow);
                    plan.patches_to_apply.Add(patch_root.normalized_relative_patch_path);
                    plan.uptodate = false;
                    Console.WriteLine();
                }
            }

            if (no_reinsertions)
            {
                Program.Log("Patches already up-to-date!", ConsoleColor.Green);
                Console.WriteLine();
            }

            // check resources

            // graphics

            // gfx

            Program.Log("Checking for GFX changes...", ConsoleColor.Cyan);
            if (Report.HashFolder("Graphics") != report.graphics)
            {
                Program.Log("Change in GFX detected, will insert GFX...", ConsoleColor.Yellow);
                Console.WriteLine();
                plan.insert_gfx = true;
                plan.uptodate = false;
            }
            else
            {
                Program.Log("GFX already up-to-date!\n", ConsoleColor.Green);
            }

            // exgfx

            Program.Log("Checking for ExGFX changes...", ConsoleColor.Cyan);
            if (Report.HashFolder("ExGraphics") != report.exgraphics)
            {
                Program.Log("Change in ExGFX detected, will insert ExGFX...", ConsoleColor.Yellow);
                Console.WriteLine();
                plan.insert_exgfx = true;
                plan.uptodate = false;
            }
            else
            {
                Program.Log("ExGFX already up-to-date!\n", ConsoleColor.Green);
            }

            // map16

            Program.Log("Checking for map16 changes...", ConsoleColor.Cyan);
            string map16hash;
            if (config.HumanReadableMap16CLI == null)
            {
                map16hash = Report.HashFile(config.Map16Path);
            }
            else
            {
                if (config.HumanReadableMap16Directory == null)
                {
                    map16hash = Report.HashFolder(Path.Combine(
                        Path.GetDirectoryName(config.Map16Path), Path.GetFileNameWithoutExtension(config.Map16Path)
                    ));
                }
                else
                {
                    map16hash = Report.HashFolder(config.HumanReadableMap16Directory);
                }
            }

            if (map16hash != report.map16)
            {
                Program.Log("Change in map16 detected, will insert map16...", ConsoleColor.Yellow);
                Console.WriteLine();
                plan.insert_map16 = true;
                plan.uptodate = false;
            }
            else
            {
                Program.Log("Map16 already up-to-date!\n", ConsoleColor.Green);
            }

            // title moves

            Program.Log("Checking for title moves changes...", ConsoleColor.Cyan);
            if (Report.HashFile(config.TitleMovesPath) != report.title_moves)
            {
                Program.Log("Change in title moves detected, will insert title moves...", ConsoleColor.Yellow);
                Console.WriteLine();
                plan.insert_title_moves = true;
                plan.uptodate = false;
            }
            else
            {
                Program.Log("Title moves already up-to-date!\n", ConsoleColor.Green);
            }

            // shared palettes

            Program.Log("Checking for shared palettes changes...", ConsoleColor.Cyan);
            if (Report.HashFile(config.SharedPalettePath) != report.shared_palettes)
            {
                Program.Log("Change in shared palettes detected, will insert shared palettes...", ConsoleColor.Yellow);
                Console.WriteLine();
                plan.insert_shared_palettes = true;
                plan.uptodate = false;
            }
            else
            {
                Program.Log("Shared palettes already up-to-date!\n", ConsoleColor.Green);
            }

            // global data
            Program.Log("Checking for global data changes...", ConsoleColor.Cyan);
            if (Report.HashFile(config.GlobalDataPath) != report.global_data)
            {
                Program.Log("Change in global data detected, will insert global data...", ConsoleColor.Yellow);
                Console.WriteLine();
                plan.insert_global_patch = true;
                plan.uptodate = false;
            }
            else
            {
                Program.Log("Global data already up-to-date!\n", ConsoleColor.Green);
            }

            // check which levels to insert
            Program.Log("Checking for level changes...", ConsoleColor.Cyan);
            if (report.lunar_magic_level_import_flags == config.LunarMagicLevelImportFlags)
            {
                foreach (var (level, hash) in newLevels)
                {
                    if (!oldLevels.ContainsKey(level))
                    {
                        Program.Log($"New level '{level}' detected, will be inserted...", ConsoleColor.Yellow);
                        Console.WriteLine();
                        plan.levels_to_insert.Add(level);
                        plan.uptodate = false;
                    }
                    else if (oldLevels[level] != hash)
                    {
                        Program.Log($"Changed level '{level}' detected, will be reinserted...", ConsoleColor.Yellow);
                        Console.WriteLine();
                        plan.levels_to_insert.Add(level);
                        plan.uptodate = false;
                    }
                }

                if (plan.levels_to_insert.Count == 0)
                {
                    Program.Log("Levels already up-to-date!\n", ConsoleColor.Green);
                }
            }
            else
            {
                Program.Log("Change in Lunar Magic level import flags detected, reinserting all levels...", ConsoleColor.Yellow);
                Console.WriteLine();
                plan.insert_all_levels = true;
                plan.uptodate = false;
            }

            // log whether already up to date

            if (plan.uptodate)
            {
                Program.Log("Previously built ROM should already be up to date!", ConsoleColor.Green);
                Console.WriteLine();
            }

            return plan;
        }

        private static string GetQuickBuildReasonString(Config config, string resource_name, DependencyGraphAnalyzer.Result result, IEnumerable<Vertex> dependency_chain)
        {
            var dependency_chain_string = DependencyChainAsString(Util.GetUri(Directory.GetCurrentDirectory()), dependency_chain);
            return $"{resource_name} must be (re)inserted due to the following dependency:\n{dependency_chain_string} ({ResultAsString(result)})";
        }

        public static string ResultAsString(DependencyGraphAnalyzer.Result result)
        {
            switch (result)
            {
                case DependencyGraphAnalyzer.Result.NewRoot:
                    return "New dependency";

                case DependencyGraphAnalyzer.Result.Missing:
                    return "Missing dependency";

                case DependencyGraphAnalyzer.Result.Arbitrary:
                    return "Arbitrary dependency";

                case DependencyGraphAnalyzer.Result.Identical:
                    return "Unchanged dependency";

                case DependencyGraphAnalyzer.Result.Modified:
                    return "Modified dependency";

                default:
                    // not reachable
                    return "Unknown dependency";
            }
        }

        public static string DependencyChainAsString(Uri base_directory, IEnumerable<Vertex> dependency_chain)
        {
            StringBuilder builder = new StringBuilder();

            foreach (var vertex in dependency_chain)
            {
                if (vertex is ToolRootVertex)
                {
                    builder.Append(((ToolRootVertex)vertex).type.ToString());
                }
                else if (vertex is PatchRootVertex)
                {
                    builder.Append(((PatchRootVertex)vertex).normalized_relative_patch_path);
                }
                else if (vertex is FileVertex)
                {
                    builder.Append(Uri.UnescapeDataString(base_directory.MakeRelativeUri(((FileVertex)vertex).uri).OriginalString));
                }
                else if (vertex is ArbitraryFileVertex)
                {
                    builder.Append("Arbitrary file(s)");
                }
                else if (vertex == null)
                {
                    // not sure if this actually ever happens
                    builder.Append("Missing file");
                }
                
                if (vertex != dependency_chain.Last())
                {
                    builder.Append(" -> ");
                }
            }

            return builder.ToString();
        }
    }
}
