using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using QuickGraph;
using System.Reflection;

namespace LunarHelper
{
    class BuildPlan
    {
        public class MustRebuildException : Exception
        {
            public MustRebuildException() : base()
            {

            }

            public MustRebuildException(string message) : base(message)
            {

            }
        }

        public class CannotBuildException : Exception
        {
            public CannotBuildException(string message) : base(message)
            {

            }
        }

        private static bool IsInBuildOrder(List<Insertable> build_order, InsertableType type)
        {
            return build_order.Any(i => i.type == type);
        }

        public static List<Insertable> PlanQuickBuild(Config config, DependencyGraph dependency_graph)
        {
            // check whether we need to rebuild completely

            Program.Log("Analyzing previous build result", ConsoleColor.Cyan);

            if (!File.Exists(config.OutputPath))
            {
                Program.Log("No previously built ROM found, rebuilding ROM...", ConsoleColor.Yellow);
                throw new MustRebuildException();
            }

            Program.Log($"Previously built ROM found at '{config.OutputPath}'!", ConsoleColor.Green);
            Console.WriteLine();

            if (!File.Exists(".lunar_helper\\build_report.json"))
            {
                Program.Log("No previous build report found, rebuilding ROM...", ConsoleColor.Yellow);
                Console.WriteLine();
                throw new MustRebuildException();
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

                if (report == null)
                {
                    throw new Exception();
                }
            }
            catch(Exception)
            {
                Program.Log("Previous build report was found but corrupted, rebuilding ROM...\n", ConsoleColor.Yellow);
                throw new MustRebuildException();
            }

            if (report.report_format_version != Report.REPORT_FORMAT_VERSION)
            {
                Program.Log("Previous build report found but used old format, rebuilding ROM...\n", ConsoleColor.Yellow);
                throw new MustRebuildException();
            }

            if (report.build_order_hash != Report.HashBuildOrder(config.BuildOrder))
            {
                Program.Log("'build_order' has changed, rebuilding ROM...\n", ConsoleColor.Yellow);
                throw new MustRebuildException();
            }

            if (report.lunar_helper_version != Assembly.GetExecutingAssembly().GetName().Version.ToString())
            {
                Program.Log($"Previous ROM was built with Lunar Helper {report.lunar_helper_version.Substring(0, 5)}, rebuilding ROM...\n", ConsoleColor.Yellow);
                throw new MustRebuildException();
            }

            IEnumerable<PatchRootVertex> patch_roots = dependency_graph.patch_roots.Cast<PatchRootVertex>();
            (var need_rebuild, var results) = DependencyGraphAnalyzer.Analyze(dependency_graph, patch_roots, report.dependency_graph);

            if (need_rebuild)
            {
                Program.Log("Previously built ROM contains patches that need to be removed, rebuilding ROM...", ConsoleColor.Yellow);
                throw new MustRebuildException();
            }

            Dictionary<string, string> oldLevels = report.levels;
            Dictionary<string, string> newLevels = Program.GetLevelReport();

            if (IsInBuildOrder(config.BuildOrder, InsertableType.Levels) || report.levels != null)
            {
                var oldLevelPaths = new HashSet<string>(report.levels.Keys);
                var newLevelPaths = (config.LevelsPath == null || !Directory.Exists(config.LevelsPath)) ? null : 
                    new HashSet<string>(Directory.GetFiles(config.LevelsPath, "*.mwl", SearchOption.TopDirectoryOnly).Select(levelPath => levelPath.Replace("\\", "/")));

                if (newLevelPaths == null || !oldLevelPaths.IsSubsetOf(newLevelPaths))
                {
                    Program.Log("Previously built ROM contains levels that need to be removed, rebuilding ROM...", ConsoleColor.Yellow);
                    Console.WriteLine();
                    throw new MustRebuildException();
                }
            }

            if (Report.HashFile(config.InitialPatch) != report.init_bps)
            {
                Program.Log("Change in initial patch detected, rebuilding ROM...", ConsoleColor.Yellow);
                Console.WriteLine();
                throw new MustRebuildException();
            }

            Program.Log("Attempting to reuse previously built ROM...", ConsoleColor.Cyan);
            Console.WriteLine();

            var tools = new[]
            {           
                ("GPS", InsertableType.Gps, ToolRootVertex.Tool.Gps, dependency_graph.gps_root, config.GPSOptions, report.gps_options),
                ("PIXI", InsertableType.Pixi, ToolRootVertex.Tool.Pixi, dependency_graph.pixi_root, config.PixiOptions, report.pixi_options),
                ("AddmusicK", InsertableType.AddMusicK, ToolRootVertex.Tool.Amk, dependency_graph.amk_root, config.AddmusicKOptions, report.addmusick_options),
                ("UberASM Tool", InsertableType.UberAsm, ToolRootVertex.Tool.UberAsm, dependency_graph.uberasm_root, config.UberASMOptions, report.uberasm_options)
            };

            var require_insertion = new List<Insertable>();

            foreach ((var tool_name, var insertable_type, var tool_type, ToolRootVertex tool_root, string new_options, string old_options) in tools)
            {
                if (!IsInBuildOrder(config.BuildOrder, insertable_type))
                    continue;  // tool is not in build order, ignore

                Program.Log($"Analyzing {tool_name} dependencies...", ConsoleColor.Cyan);

                if (new_options != old_options)
                {
                    Program.Log($"{tool_name} command line options changed from \"{old_options}\" to \"{new_options}\", {tool_name} will be reinserted...",
                        ConsoleColor.Yellow);

                    require_insertion.Add(new Insertable(insertable_type));
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
                        Program.Log($"No old or new {tool_name} dependencies found, tool will not be inserted", ConsoleColor.Yellow);
                        Console.WriteLine();
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

                    require_insertion.Add(new Insertable(insertable_type));
                }
                Console.WriteLine();
            }

            if (IsInBuildOrder(config.BuildOrder, InsertableType.SinglePatch) || IsInBuildOrder(config.BuildOrder, InsertableType.Patches))
            {
                bool no_reinsertions = true;
                Program.Log("Analyzing patch dependencies...", ConsoleColor.Cyan);

                if (config.AsarOptions != report.asar_options)
                {
                    Program.Log($"asar command line options changed from \"{report.asar_options}\" to \"{config.AsarOptions}\", all patches will be reinserted...\n",
                        ConsoleColor.Yellow);
                    require_insertion.AddRange(patch_roots.Select(p => new Insertable(InsertableType.SinglePatch, p.normalized_relative_patch_path)));
                }
                else
                {
                    foreach ((var patch_root, (var result, var dependency_chain)) in results)
                    {
                        if (result != DependencyGraphAnalyzer.Result.Identical)
                        {
                            no_reinsertions = false;
                            Program.Log(GetQuickBuildReasonString(config, $"Patch \"{patch_root.normalized_relative_patch_path}\"", result, dependency_chain),
                                ConsoleColor.Yellow);
                            Console.WriteLine();
                            require_insertion.Add(new Insertable(InsertableType.SinglePatch, patch_root.normalized_relative_patch_path));
                        }
                    }

                    if (no_reinsertions)
                    {
                        Program.Log("Patches already up-to-date!", ConsoleColor.Green);
                        Console.WriteLine();
                    }
                }
            }

            // check resources

            // graphics

            // gfx

            if (IsInBuildOrder(config.BuildOrder, InsertableType.Graphics))
            {
                Program.Log("Checking for GFX changes...", ConsoleColor.Cyan);
                if (Report.HashFolder("Graphics") != report.graphics)
                {
                    Program.Log("Change in GFX detected, will insert GFX...", ConsoleColor.Yellow);
                    Console.WriteLine();
                    require_insertion.Add(new Insertable(InsertableType.Graphics));
                }
                else
                {
                    Program.Log("GFX already up-to-date!\n", ConsoleColor.Green);
                }
            }

            // exgfx
            if (IsInBuildOrder(config.BuildOrder, InsertableType.ExGraphics))
            {
                Program.Log("Checking for ExGFX changes...", ConsoleColor.Cyan);
                if (Report.HashFolder("ExGraphics") != report.exgraphics)
                {
                    Program.Log("Change in ExGFX detected, will insert ExGFX...", ConsoleColor.Yellow);
                    Console.WriteLine();
                    require_insertion.Add(new Insertable(InsertableType.ExGraphics));
                }
                else
                {
                    Program.Log("ExGFX already up-to-date!\n", ConsoleColor.Green);
                }
            }

            // map16
            if (IsInBuildOrder(config.BuildOrder, InsertableType.Map16))
            {
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
                    require_insertion.Add(new Insertable(InsertableType.Map16));
                }
                else
                {
                    Program.Log("Map16 already up-to-date!\n", ConsoleColor.Green);
                }
            }

            // title moves

            if (IsInBuildOrder(config.BuildOrder, InsertableType.TitleMoves))
            {
                Program.Log("Checking for title moves changes...", ConsoleColor.Cyan);
                if (Report.HashFile(config.TitleMovesPath) != report.title_moves)
                {
                    Program.Log("Change in title moves detected, will insert title moves...", ConsoleColor.Yellow);
                    Console.WriteLine();
                    require_insertion.Add(new Insertable(InsertableType.TitleMoves));
                }
                else
                {
                    Program.Log("Title moves already up-to-date!\n", ConsoleColor.Green);
                }
            }

            // shared palettes

            if (IsInBuildOrder(config.BuildOrder, InsertableType.SharedPalettes))
            {
                Program.Log("Checking for shared palettes changes...", ConsoleColor.Cyan);
                if (Report.HashFile(config.SharedPalettePath) != report.shared_palettes)
                {
                    Program.Log("Change in shared palettes detected, will insert shared palettes...", ConsoleColor.Yellow);
                    Console.WriteLine();
                    require_insertion.Add(new Insertable(InsertableType.SharedPalettes));
                }
                else
                {
                    Program.Log("Shared palettes already up-to-date!\n", ConsoleColor.Green);
                }
            }

            // global data

            if (IsInBuildOrder(config.BuildOrder, InsertableType.GlobalData))
            {
                Program.Log("Checking for global data changes...", ConsoleColor.Cyan);
                if (Report.HashFile(config.GlobalDataPath) != report.global_data)
                {
                    Program.Log("Change in global data detected, will insert global data...", ConsoleColor.Yellow);
                    Console.WriteLine();
                    require_insertion.Add(new Insertable(InsertableType.GlobalData));
                }
                else
                {
                    Program.Log("Global data already up-to-date!\n", ConsoleColor.Green);
                }
            }

            // check which levels to insert

            if (IsInBuildOrder(config.BuildOrder, InsertableType.Levels))
            {
                Program.Log("Checking for level changes...", ConsoleColor.Cyan);
                if (report.lunar_magic_level_import_flags == config.LunarMagicLevelImportFlags)
                {
                    bool any_levels_changed = false;

                    foreach (var (level, hash) in newLevels)
                    {
                        if (!oldLevels.ContainsKey(level))
                        {
                            any_levels_changed = true;
                            Program.Log($"New level '{level}' detected, will be inserted...", ConsoleColor.Yellow);
                            Console.WriteLine();
                            require_insertion.Add(new Insertable(InsertableType.SingleLevel, level));
                        }
                        else if (oldLevels[level] != hash)
                        {
                            any_levels_changed = true;
                            Program.Log($"Changed level '{level}' detected, will be reinserted...", ConsoleColor.Yellow);
                            Console.WriteLine();
                            require_insertion.Add(new Insertable(InsertableType.SingleLevel, level));
                        }
                    }

                    if (!any_levels_changed)
                    {
                        Program.Log("Levels already up-to-date!\n", ConsoleColor.Green);
                    }
                }
                else
                {
                    Program.Log("Change in Lunar Magic level import flags detected, reinserting all levels...", ConsoleColor.Yellow);
                    Console.WriteLine();
                    require_insertion.Add(new Insertable(InsertableType.Levels));
                }
            }

            // log whether already up to date

            if (require_insertion.Count == 0)
            {
                Program.Log("Previously built ROM should already be up to date!", ConsoleColor.Green);
                Console.WriteLine();
            }

            return DetermineQuickBuildInsertionOrder(require_insertion, config.QuickBuildTriggerGraph);
        }

        private static List<Insertable> DetermineQuickBuildInsertionOrder(List<Insertable> detected_changes,
            BidirectionalGraph<Insertable, Edge<Insertable>> trigger_graph)
        {
            Program.Log("Checking Quick Build triggers...\n", ConsoleColor.Cyan);

            bool any_triggered = false;

            var graph_copy = trigger_graph.Clone();

            List<Insertable> insertion_order = new List<Insertable>();

            HashSet<Insertable> triggered_insertions = new HashSet<Insertable>();

            while (graph_copy.VertexCount != 0)
            {
                // find vertex with no in-edges, we know one must exist whil there is at least one
                // vertex in the graph, since the graph is acyclic
                var source = graph_copy.Vertices.First(v => graph_copy.InDegree(v) == 0);

                var corresponding_changes = GatherInsertables(source, detected_changes);
                detected_changes.RemoveAll(x => corresponding_changes.Contains(x));

                if (corresponding_changes.Count != 0)
                {
                    insertion_order.AddRange(corresponding_changes);

                    // our source had a change, this should trigger reinsertion of all neighbors of 
                    // this vertex
                    triggered_insertions = triggered_insertions.Concat(graph_copy.OutEdges(source).Select(e => e.Target)).ToHashSet();
                }
                else if (triggered_insertions.Contains(source))
                {
                    // our vertex has no changes of its own but was triggered by some previous source
                    // vertex having had changes in its subtree, reinsert our vertex and also trigger
                    // reinsertion of our neighboring vertices
                    var as_string = source.type == InsertableType.SinglePatch ? $"Patch '{source.normalized_relative_path}'" :
                        source.type.ToString();
                    Program.Lognl(as_string, ConsoleColor.Cyan);
                    Program.Log($" must be (re)inserted due to Quick Build triggers specification", ConsoleColor.Yellow);

                    insertion_order.Add(source);
                    triggered_insertions = triggered_insertions.Concat(graph_copy.OutEdges(source).Select(e => e.Target)).ToHashSet();
                    triggered_insertions.Remove(source);

                    any_triggered = true;
                }

                // remove our current vertex so that we can find a new source vertex in the next iteration
                graph_copy.RemoveVertex(source);
            }

            if (any_triggered)
                Program.Log("");

            foreach (var non_trigger_change in detected_changes)
            {
                insertion_order.Add(non_trigger_change);
            }

            return insertion_order;
        }


        private static List<Insertable> GatherInsertables(Insertable insertable, List<Insertable> insertables)
        {
            switch (insertable.type)
            {
                case InsertableType.Levels:
                    return insertables.FindAll(i => i.type == InsertableType.Levels || i.type == InsertableType.SingleLevel);

                case InsertableType.SinglePatch:
                    return insertables.FindAll(i => i.type == InsertableType.SinglePatch
                        && i.normalized_relative_path == insertable.normalized_relative_path);

                default:
                    return insertables.FindAll(i => i.type == insertable.type);
            }
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
                    return "Potentially missing dependency";

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
                    builder.Append("Potentially missing file");
                }
                
                if (vertex != dependency_chain.Last())
                {
                    builder.Append(" -> ");
                }
            }

            return builder.ToString();
        }

        public static List<Insertable> PlanBuild(Config config)
        {
            List<Insertable> plan = new List<Insertable>();

            foreach (var insertable in config.BuildOrder)
            {
                if (insertable.type == InsertableType.Patches)
                {
                    plan.AddRange(
                        config.Patches.Select(p => new Insertable(InsertableType.SinglePatch, p)).ToList()
                        .FindAll(i => !config.BuildOrder.Contains(i))
                    );  // add all patches from the list that are not otherwise mentioned in the build order 
                }
                else
                {
                    plan.Add(insertable);
                }
            }

            return plan;
        }
    }
}
