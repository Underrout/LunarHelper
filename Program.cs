using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using System.Text;

using LunarHelper;
using System.Linq;

namespace LunarHelper
{
    class Program
    {
        static public Config Config { get; private set; }

        static private string uberasm_success_string = "Codes inserted successfully.";

        static private readonly Regex LevelRegex = new Regex("[0-9a-fA-F]{3}");
        static private Process RetroArchProcess;
        static private Process LunarMagicProcess;

        static private DependencyGraph dependency_graph;

        private enum Result
        {
            NotSpecified,
            NotFound,
            Error,
            Success
        }

        static void Main(string[] args)
        {
            bool running = true;
            while (running)
            {
                Log("Welcome to Lunar Helper ^_^", ConsoleColor.Cyan);
                Log("B - (Re)Build, Q - Quick Build, R - Run");
                Log("T - Test (Build -> Run)");
                Log("E - Edit (in Lunar Magic)");
                Log("P - Package, H - Help, ESC - Exit");
                Console.WriteLine();

                var key = Console.ReadKey(true);
                Console.Clear();

                switch (key.Key)
                {
                    case ConsoleKey.Q:
                        if (Init())
                        {
                            if (!QuickBuild())
                            {
                                Log("Your ROM has not been altered!", ConsoleColor.Cyan);
                            }
                        }
                        break;

                    case ConsoleKey.B:
                        if (Init())
                        {
                            Build();
                        }
                        break;

                    case ConsoleKey.T:
                        if (Init() && Build())
                            Test();
                        break;

                    case ConsoleKey.R:
                        if (Init())
                            Test();
                        break;

                    case ConsoleKey.E:
                        if (Init())
                            Edit();
                        break;

                    case ConsoleKey.P:
                        if (Init() && Build())
                            Package();
                        break;

                    case ConsoleKey.H:
                        Help();
                        break;

                    case ConsoleKey.Escape:
                        running = false;
                        Log("Have a nice day!", ConsoleColor.Cyan);
                        Console.ForegroundColor = ConsoleColor.White;
                        break;

                    default:
                        string str = char.ToUpperInvariant(key.KeyChar).ToString().Trim();
                        if (str.Length > 0)
                            Log($"Key '{str}' is not a recognized option!", ConsoleColor.Red);
                        else
                            Log($"Key is not a recognized option!", ConsoleColor.Red);
                        Console.WriteLine();
                        break;
                }

                while (Console.KeyAvailable)
                    Console.ReadKey(true);
            }
        }

        static private bool QuickBuild()
        {
            dependency_graph = new DependencyGraph(Config);
            BuildPlan plan;

            try 
            {
                plan = BuildPlan.PlanBuild(Config, dependency_graph);
            }
            catch (BuildPlan.CannotBuildException)
            {
                Log("Quick Build failed!\n", ConsoleColor.Red);
                return false;
            }
            
            if (plan.uptodate)
            {
                return true;
            }

            if (plan.rebuild)
            {
                return Build();
            }

            // Actually doing quick build below

            // delete existing temp ROM
            if (File.Exists(Config.TempPath))
                File.Delete(Config.TempPath);

            // Lunar Magic required
            if (string.IsNullOrWhiteSpace(Config.LunarMagicPath))
            {
                Log("No path to Lunar Magic provided!", ConsoleColor.Red);
                return false;
            }
            else if (!File.Exists(Config.LunarMagicPath))
            {
                Log("Lunar Magic not found at provided path!", ConsoleColor.Red);
                return false;
            }

            File.Copy(Config.OutputPath, Config.TempPath);

            // GPS

            if (plan.apply_gps)
            {
                var res = ApplyGPS();

                if (res == Result.Error)
                {
                    return false;
                }
                else if (res == Result.NotFound || res == Result.NotSpecified)
                {
                    Log("GPS was either not specified or not found, but was used to build previous ROM. If this is not a mistake, " +
                        "please rebuild the ROM from scratch to remove the tool's code. Aborting.", ConsoleColor.Red);
                    return false;
                }
            }

            // PIXI

            if (plan.apply_pixi)
            {
                Log("PIXI", ConsoleColor.Cyan);
                var res = ApplyPixi();

                if (res == Result.Error)
                {
                    return false;
                }
                else if (res == Result.NotFound || res == Result.NotSpecified)
                {
                    Log("PIXI was either not specified or not found, but was used to build previous ROM. If this is not a mistake, " +
                        "please rebuild the ROM from scratch to remove the tool's code. Aborting.", ConsoleColor.Red);
                    return false;
                }
            }

            // patches

            if (plan.patches_to_apply.Count != 0)
            {
                // asar patches
                Log("Patches", ConsoleColor.Cyan);
                if (string.IsNullOrWhiteSpace(Config.AsarPath))
                {
                    Log("No path to Asar provided, but was used to insert patches previously. If this is not a mistake, " +
                        "please rebuild the ROM from scratch to remove the old patches. Aborting.", ConsoleColor.Red);
                    return false;
                }
                else if (!File.Exists(Config.AsarPath))
                {
                    Log("Asar not found at provided path. If this is not a mistake, " +
                        "please rebuild the ROM from scratch to remove the old patches. Aborting.", ConsoleColor.Red);
                    return false;
                }
                else
                {
                    foreach (var patch in plan.patches_to_apply)
                    {
                        Lognl($"- Applying patch '{patch}'...  ", ConsoleColor.Yellow);

                        ProcessStartInfo psi = new ProcessStartInfo(Config.AsarPath, $"{Config.AsarOptions ?? ""} \"{patch}\" \"{Config.TempPath}\"");

                        var p = Process.Start(psi);
                        p.WaitForExit();

                        if (p.ExitCode == 0)
                            Log("Success!", ConsoleColor.Green);
                        else
                        {
                            Log("Failure!", ConsoleColor.Red);
                            return false;
                        }
                    }

                    Log("Patching Success!", ConsoleColor.Green);
                    Console.WriteLine();
                }
            }

            if (plan.apply_uberasm)
            {
                var res = ApplyUberASM();

                if (res == Result.Error)
                {
                    return false;
                }
                else if (res == Result.NotFound || res == Result.NotSpecified)
                {
                    Log("UberASMTool was either not specified or not found, but was used to build previous ROM. If this is not a mistake, " +
                        "please rebuild the ROM from scratch to remove the tool's code. Aborting.", ConsoleColor.Red);
                    return false;
                }
            }

            if (plan.apply_addmusick)
            {
                var res = ApplyAddmusicK();

                if (res == Result.Error)
                {
                    return false;
                }
                else if (res == Result.NotFound || res == Result.NotSpecified)
                {
                    Log("AddmusicK was either not specified or not found, but was used to build previous ROM. If this is not a mistake, " +
                        "please rebuild the ROM from scratch to remove the tool's code. Aborting.", ConsoleColor.Red);
                    return false;
                }
            }

            if (plan.insert_gfx)
            {
                // import gfx
                Log("Graphics", ConsoleColor.Cyan);
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    ProcessStartInfo psi = new ProcessStartInfo(Config.LunarMagicPath,
                                $"-ImportGFX \"{Config.TempPath}\"");
                    var p = Process.Start(psi);
                    p.WaitForExit();

                    if (p.ExitCode == 0)
                        Log("Import Graphics Success!", ConsoleColor.Green);
                    else
                    {
                        Log("Import Graphics Failure!", ConsoleColor.Red);
                        return false;
                    }

                    Console.WriteLine();
                }
            }

            if (plan.insert_exgfx)
            {
                // import exgfx
                Log("ExGraphics", ConsoleColor.Cyan);
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    ProcessStartInfo psi = new ProcessStartInfo(Config.LunarMagicPath,
                                $"-ImportExGFX \"{Config.TempPath}\"");
                    var p = Process.Start(psi);
                    p.WaitForExit();

                    if (p.ExitCode == 0)
                        Log("Import ExGraphics Success!", ConsoleColor.Green);
                    else
                    {
                        Log("Import ExGraphics Failure!", ConsoleColor.Red);
                        return false;
                    }

                    Console.WriteLine();
                }
            }

            if (plan.insert_map16)
            {
                var res = ImportMap16();

                if (res == Result.Error)
                {
                    return false;
                }
                else if (res == Result.NotFound || res == Result.NotSpecified)
                {
                    Log("Map16 was either not specified or not found, but was used to build previous ROM. If this is not a mistake, " +
                        "please rebuild the ROM from scratch to remove the previously imported data. Aborting.", ConsoleColor.Red);
                    return false;
                }
            }

            if (plan.insert_title_moves)
            {
                var res = ImportTitleMoves();

                if (res == Result.Error)
                {
                    return false;
                }
                else if (res == Result.NotFound || res == Result.NotSpecified)
                {
                    Log("Title screen moves were either not specified or not found, but were used to build previous ROM. If this is not a mistake, " +
                        "please rebuild the ROM from scratch to remove the previously imported data. Aborting.", ConsoleColor.Red);
                    return false;
                }
            }

            if (plan.insert_shared_palettes)
            {
                var res = ImportSharedPalettes();

                if (res == Result.Error)
                {
                    return false;
                }
                else if (res == Result.NotFound || res == Result.NotSpecified)
                {
                    Log("Shared palettes were either not specified or not found, but were used to build previous ROM. If this is not a mistake, " +
                        "please rebuild the ROM from scratch to remove the previously imported data. Aborting.", ConsoleColor.Red);
                    return false;
                }
            }

            if (plan.insert_global_patch)
            {
                var res = ImportGlobalData();

                if (res == Result.Error)
                {
                    return false;
                }
                else if (res == Result.NotFound || res == Result.NotSpecified)
                {
                    Log("Global data patch was either not specified or not found, but was used to build previous ROM. If this is not a mistake, " +
                        "please rebuild the ROM from scratch to remove the previously imported data. Aborting.", ConsoleColor.Red);
                    return false;
                }
            }

            if (plan.insert_all_levels)
            {
                if (!ImportLevels(false))
                {
                    return false;
                }
            }
            else if (plan.levels_to_insert.Count != 0)
            {
                Log("Levels", ConsoleColor.Cyan);

                foreach (var levelPath in plan.levels_to_insert)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    ProcessStartInfo psi = new ProcessStartInfo(Config.LunarMagicPath,
                                $"-ImportLevel \"{Config.TempPath}\" \"{levelPath}\"");
                    var p = Process.Start(psi);
                    p.WaitForExit();

                    if (p.ExitCode != 0)
                    {
                        Log($"Level import failure on '{levelPath}'!", ConsoleColor.Red);
                        return false;
                    }
                }
                Log("Levels Import Success!", ConsoleColor.Green);
                Console.WriteLine();
            }

            if (plan.apply_pixi && plan.may_need_two_pixi_passes)
            {
                FileVersionInfo lunarMagicInfo = FileVersionInfo.GetVersionInfo(Config.LunarMagicPath);

                if (lunarMagicInfo.FileMajorPart >= 3 && lunarMagicInfo.FileMinorPart >= 31)
                {
                    Log("PIXI (Second pass for Lunar Magic version >= 3.31)", ConsoleColor.Cyan);
                    var res = ApplyPixi();

                    // really we should never get anything but Result.Success here since we already succeeded earlier,
                    // but I'll still check just in case
                    if (res == Result.Error)
                    {
                        return false;
                    }
                    else if (res == Result.NotFound || res == Result.NotSpecified)
                    {
                        Log("PIXI was either not specified or not found, but was used to build previous ROM. If this is not a mistake, " +
                            "please rebuild the ROM from scratch to remove the tool's code. Aborting.", ConsoleColor.Red);
                        return false;
                    }
                }
            }

            FinalizeOutputROM();
            WriteReport();

            Log($"ROM '{Config.OutputPath}' successfully updated!", ConsoleColor.Green);
            Console.WriteLine();

            WarnAboutProblematicDependencies();

            return true;
        }

        static private void WarnAboutProblematicDependencies()
        {
            IEnumerable<(MissingFileOrDirectoryVertex, IEnumerable<Vertex>)> missing_dependencies = new List<(MissingFileOrDirectoryVertex, IEnumerable<Vertex>)>();

            foreach (var missing_vertex in dependency_graph.dependency_graph.Vertices.Where(v => v is MissingFileOrDirectoryVertex))
            {
                missing_dependencies = missing_dependencies.Append(
                    ((MissingFileOrDirectoryVertex)missing_vertex, DependencyGraphAnalyzer.GetDependents(dependency_graph, missing_vertex)));
            }

            IEnumerable<(ArbitraryFileVertex, IEnumerable<Vertex>)> arbitrary_dependencies = new List<(ArbitraryFileVertex, IEnumerable<Vertex>)>();

            foreach (var arbitrary_vertex in dependency_graph.dependency_graph.Vertices.Where(v => v is ArbitraryFileVertex))
            {
                arbitrary_dependencies = arbitrary_dependencies.Append(
                    ((ArbitraryFileVertex)arbitrary_vertex, DependencyGraphAnalyzer.GetDependents(dependency_graph, arbitrary_vertex)));
            }

            if (!missing_dependencies.Any() && !arbitrary_dependencies.Any())
            {
                return;
            }

            var builder = new StringBuilder("WARNING: Build succeeded, but Lunar Helper found ");

            if (missing_dependencies.Any())
            {
                builder.Append($"{missing_dependencies.Count()} missing dependencies");
            }

            if (arbitrary_dependencies.Any())
            {
                if (missing_dependencies.Any())
                {
                    builder.Append(" and ");
                }
                builder.Append($"{arbitrary_dependencies.Count()} arbitrary dependencies");
            }

            builder.Append("\n");

            if (missing_dependencies.Any())
            {
                builder.Append("\nMissing dependencies may indicate that you are relying on an asar bug that existed at least until version 1.81 " +
                    "(see https://github.com/RPGHacker/asar/issues/253 for details).\n" +
                    "Please ensure the left file in the following list correctly includes the file on the right side or Quick Build may not " +
                    "behave correctly for you in the future. " +
                    "If you are sure that you are not relying on this asar bug and you are not doing anything else weird (like including " +
                    "files generated by a different tool from another tool) this may instead indicate a bug in Lunar Helper. If so, please " +
                    "open an issue at https://github.com/Underrout/LunarHelper/issues/new\n\nPotentially \"missing\" dependencies:\n");

                foreach ((var missing_dependency, var parents) in missing_dependencies)
                {
                    foreach (var parent in parents)
                    {
                        builder.Append(BuildPlan.DependencyChainAsString(Util.GetUri(Directory.GetCurrentDirectory()),
                            new List<Vertex> { parent, missing_dependency }));

                        builder.Append("\n");
                    }
                }
            }

            if (arbitrary_dependencies.Any())
            {
                builder.Append("\nArbitrary dependencies are dependencies that Lunar Helper is not currently capable of resolving. " +
                    "Examples include 'incsrc \"../<file>\"' and 'incsrc \"../!some_define\"'.\nIf a tool or patch relies on such " +
                    "arbitrary includes, Lunar Helper has to assume that the tool's or patch's dependencies may change between builds " +
                    "without it being aware of it and thus Lunar Helper will always reinsert the tool or patch in question.\n" +
                    "If you can replace or remove these arbitrary includes from the affected files, you may consider doing so in order to speed up the " +
                    "build process and get rid of this message. The files containing arbitrary includes are listed below.\n\nArbitrary dependencies:\n");

                foreach ((var arbitrary_dependency, var parents) in arbitrary_dependencies)
                {
                    foreach (var parent in parents)
                    {
                        builder.Append(BuildPlan.DependencyChainAsString(Util.GetUri(Directory.GetCurrentDirectory()),
                            new List<Vertex> { parent, arbitrary_dependency }));

                        builder.Append("\n");
                    }
                }
            }

            Log(builder.ToString(), ConsoleColor.Yellow);
        }

        static private void WriteReport()
        {
            var report = GetBuildReport();

            var serializer = new JsonSerializer();
            serializer.Formatting = Formatting.Indented;
            serializer.TypeNameHandling = TypeNameHandling.Objects;

            StringWriter sw = new StringWriter();

            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                serializer.Serialize(writer, report);
            }

            Directory.CreateDirectory(".lunar_helper");
            File.SetAttributes(".lunar_helper", File.GetAttributes(".lunar_helper") | FileAttributes.Hidden);
            File.WriteAllText(".lunar_helper\\build_report.json", sw.ToString());
        }

        static private Report GetBuildReport()
        {
            var report = new Report();

            report.build_time = DateTime.Now;

            report.dependency_graph = DependencyGraphSerializer.SerializeGraph(dependency_graph).ToList();

            report.levels = GetLevelReport();
            report.graphics = Report.HashFolder("Graphics");
            report.exgraphics = Report.HashFolder("ExGraphics");
            report.shared_palettes = Report.HashFile(Config.SharedPalettePath);
            report.init_bps = Report.HashFile(Config.InitialPatch);
            report.global_data = Report.HashFile(Config.GlobalDataPath);
            report.title_moves = Report.HashFile(Config.TitleMovesPath);

            if (string.IsNullOrWhiteSpace(Config.HumanReadableMap16CLI))
            {
                report.map16 = Report.HashFile(Config.Map16Path);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(Config.HumanReadableMap16Directory))
                {
                    report.map16 = Report.HashFolder(Path.Combine(
                        Path.GetDirectoryName(Config.Map16Path), Path.GetFileNameWithoutExtension(Config.Map16Path)
                    ));
                }
                else
                {
                    report.map16 = Report.HashFolder(Config.HumanReadableMap16Directory);
                }
            }

            report.rom_hash = Report.HashFile(Config.OutputPath);

            report.flips = Report.HashFile(Config.FlipsPath);
            report.lunar_magic = Report.HashFile(Config.LunarMagicPath);
            report.human_readable_map16 = Report.HashFile(Config.HumanReadableMap16CLI);

            report.asar_options = Config.AsarOptions;
            report.pixi_options = Config.PixiOptions;
            report.gps_options = Config.GPSOptions;
            report.addmusick_options = Config.AddmusicKOptions;
            report.uberasm_options = Config.UberASMOptions;
            report.lunar_magic_level_import_flags = Config.LunarMagicLevelImportFlags;

            return report;
        }

        static public Dictionary<string, string> GetLevelReport()
        {
            if (Config.LevelsPath == null || !Directory.Exists(Config.LevelsPath))
            {
                return null;
            }

            var dict = new Dictionary<string, string>();

            foreach (string level in Directory.GetFiles(Config.LevelsPath, "*.mwl", SearchOption.TopDirectoryOnly))
            {
                dict[level.Replace("\\", "/")] = Report.HashFile(level);
            }

            return dict;
        }

        static private bool Init()
        {
            Directory.SetCurrentDirectory(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName));

            // load config
            Config = Config.Load(out var err);
            if (Config == null)
            {
                Error($"Could not parse config.txt file(s)\n{err}");
                return false;
            }

            // set the working directory
            if (!string.IsNullOrWhiteSpace(Config.WorkingDirectory))
            {
                if (!Directory.Exists(Config.WorkingDirectory))
                {
                    Error("The configured Working Directory doesn't exist");
                    return false;
                }

                Directory.SetCurrentDirectory(Config.WorkingDirectory);
            } 

            // some error checks
            if (string.IsNullOrWhiteSpace(Config.CleanPath))
            {
                Error("No Clean ROM path provided");
                return false;
            }
            else if (!File.Exists(Config.CleanPath))
            {
                Error($"Clean ROM file '{Config.CleanPath}' does not exist");
                return false;
            }
            else if (string.IsNullOrWhiteSpace(Config.OutputPath))
            {
                Error("No Output ROM path provided");
                return false;
            }
            else if (string.IsNullOrWhiteSpace(Config.TempPath))
            {
                Error("No Temp ROM path provided");
                return false;
            }

            return true;
        }

        static private bool Build()
        {
            // Lunar Magic required
            if (string.IsNullOrWhiteSpace(Config.LunarMagicPath))
            {
                Log("No path to Lunar Magic provided!", ConsoleColor.Red);
                return false;
            }
            else if (!File.Exists(Config.LunarMagicPath))
            {
                Log("Lunar Magic not found at provided path!", ConsoleColor.Red);
                return false;
            }

            // delete existing temp ROM
            if (File.Exists(Config.TempPath))
                File.Delete(Config.TempPath);

            // initial patch
            if (!string.IsNullOrWhiteSpace(Config.InitialPatch))
            {
                Log("Initial Patch", ConsoleColor.Cyan);

                var fullPatchPath = Path.GetFullPath(Config.InitialPatch);
                var fullCleanPath = Path.GetFullPath(Config.CleanPath);
                var fullTempPath = Path.GetFullPath(Config.TempPath);
                if (ApplyPatch(fullCleanPath, fullTempPath, fullPatchPath))
                    Log("Initial Patch Success!", ConsoleColor.Green);
                else
                {
                    Log("Initial Patch Failure!", ConsoleColor.Red);
                    return false;
                }

                Console.WriteLine();
            }
            else
            {
                File.Copy(Config.CleanPath, Config.TempPath);
            }

            // run GPS

            if (ApplyGPS() == Result.Error)
            {
                return false;
            }

            // run PIXI
            Log("PIXI", ConsoleColor.Cyan);
            if (ApplyPixi() == Result.Error)
            {
                return false;
            }

            // asar patches
            Log("Patches", ConsoleColor.Cyan);
            if (string.IsNullOrWhiteSpace(Config.AsarPath))
                Log("No path to Asar provided, not applying any patches.", ConsoleColor.Red);
            else if (!File.Exists(Config.AsarPath))
                Log("Asar not found at provided path, not applying any patches.", ConsoleColor.Red);
            else if (Config.Patches.Count == 0)
                Log("Path to Asar provided, but no patches were registerd to be applied.", ConsoleColor.Red);
            else
            {
                foreach (var patch in Config.Patches)
                {
                    Lognl($"- Applying patch '{patch}'...  ", ConsoleColor.Yellow);

                    ProcessStartInfo psi = new ProcessStartInfo(Config.AsarPath, $"{Config.AsarOptions ?? ""} \"{patch}\" \"{Config.TempPath}\"");

                    var p = Process.Start(psi);
                    p.WaitForExit();

                    if (p.ExitCode == 0)
                        Log("Success!", ConsoleColor.Green);
                    else
                    {
                        Log("Failure!", ConsoleColor.Red);
                        return false;
                    }
                }

                Log("Patching Success!", ConsoleColor.Green);
                Console.WriteLine();
            }

            // uber ASM

            if (ApplyUberASM() == Result.Error)
            {
                return false;
            }

            // run AddMusicK

            if (ApplyAddmusicK() == Result.Error)
            {
                return false;
            }

            // import gfx
            Log("Graphics", ConsoleColor.Cyan);
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                ProcessStartInfo psi = new ProcessStartInfo(Config.LunarMagicPath,
                            $"-ImportAllGraphics \"{Config.TempPath}\"");
                var p = Process.Start(psi);
                p.WaitForExit();

                if (p.ExitCode == 0)
                    Log("Import Graphics Success!", ConsoleColor.Green);
                else
                {
                    Log("Import Graphics Failure!", ConsoleColor.Red);
                    return false;
                }

                Console.WriteLine();
            }

            // import map16

            if (ImportMap16() == Result.Error)
            {
                return false;
            }

            // import title moves

            if (ImportTitleMoves() == Result.Error)
            {
                return false;
            }

            // import shared palette

            if (ImportSharedPalettes() == Result.Error)
            {
                return false;
            }

            // import global data

            if (ImportGlobalData() == Result.Error)
            {
                return false;
            }

            // import levels
            if (!ImportLevels(false))
            {
                return false;
            }

            FileVersionInfo lunarMagicInfo = FileVersionInfo.GetVersionInfo(Config.LunarMagicPath);

            if (lunarMagicInfo.FileMajorPart >= 3 && lunarMagicInfo.FileMinorPart >= 31)
            {
                Log("PIXI (Second pass for Lunar Magic version >= 3.31)", ConsoleColor.Cyan);
                if (ApplyPixi() != Result.Success)
                {
                    return false;
                }
            }

            FinalizeOutputROM();
            Log($"ROM patched successfully to '{Config.OutputPath}'!", ConsoleColor.Cyan);
            Console.WriteLine();

            dependency_graph = new DependencyGraph(Config);
            WriteReport();

            return true;
        }

        static private bool Test()
        {
            Console.WriteLine();
            Log("Initiating Test routine!", ConsoleColor.Magenta);

            // test level
            if (!string.IsNullOrWhiteSpace(Config.TestLevel) && !string.IsNullOrWhiteSpace(Config.TestLevelDest))
            {
                var files = Directory.GetFiles(Config.LevelsPath, $"*{Config.TestLevel}*.mwl");

                if (!LevelRegex.IsMatch(Config.TestLevel))
                    Log("Test Level ID must be a 3-character hex value", ConsoleColor.Red);
                else if (!LevelRegex.IsMatch(Config.TestLevelDest))
                    Log("Test Level Dest ID must be a 3-character hex value", ConsoleColor.Red);
                else if (files.Length == 0)
                    Log($"Test Level {Config.TestLevel} not found in {Config.LevelsPath}", ConsoleColor.Red);
                else
                {
                    var path = files[0];

                    Log($"Importing level {Config.TestLevel} to {Config.TestLevelDest} for testing...  ", ConsoleColor.Yellow);

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    ProcessStartInfo psi = new ProcessStartInfo(Config.LunarMagicPath,
                        $"-ImportLevel \"{Config.OutputPath}\" \"{path}\" {Config.TestLevelDest}");
                    var p = Process.Start(psi);
                    p.WaitForExit();

                    if (p.ExitCode == 0)
                        Log("Test Level Import Success!", ConsoleColor.Green);
                    else
                    {
                        Log("Test Level Import Failure!", ConsoleColor.Red);
                        return false;
                    }
                }
            }

            // retroarch
            if (!string.IsNullOrWhiteSpace(Config.RetroArchPath))
            {
                Log("Launching RetroArch...", ConsoleColor.Yellow);
                var fullRom = Path.GetFullPath(Config.OutputPath);

                if (RetroArchProcess != null && !RetroArchProcess.HasExited)
                    RetroArchProcess.Kill(true);

                ProcessStartInfo psi = new ProcessStartInfo(Config.RetroArchPath,
                    $"-L \"{Config.RetroArchCore}\" \"{fullRom}\"");
                RetroArchProcess = Process.Start(psi);
            }

            Log("Test routine complete!", ConsoleColor.Magenta);
            Console.WriteLine();

            return true;
        }

        static private void FinalizeOutputROM()
        {
            // rename temp rom and generated files to final build output
            {
                var path = Path.GetDirectoryName(Path.GetFullPath(Config.TempPath));
                var temp_name = Path.GetFileNameWithoutExtension(Config.TempPath);
                var to = Path.GetDirectoryName(Path.GetFullPath(Config.OutputPath));
                to = Path.Combine(to, Path.GetFileNameWithoutExtension(Config.OutputPath));

                foreach (var file in Directory.EnumerateFiles(path, temp_name + "*"))
                    File.Move(file, $"{to}{Path.GetExtension(file)}", true);
            }
        }

        static private Result ImportGlobalData()
        {
            Log("Global Data", ConsoleColor.Cyan);
            if (string.IsNullOrWhiteSpace(Config.GlobalDataPath))
            {
                Log("No path to Global Data BPS provided, no global data will be imported.", ConsoleColor.Red);
                return Result.NotSpecified;
            }
            else if (!File.Exists(Config.GlobalDataPath))
            {
                Log("No path to Global Data BPS file found at the path provided, no global data will be imported.", ConsoleColor.Red);
                return Result.NotFound;
            }
            else
            {
                ProcessStartInfo psi;
                Process p;
                string globalDataROMPath = Path.Combine(
                    Path.GetFullPath(Path.GetDirectoryName(Config.GlobalDataPath)),
                    Path.GetFileNameWithoutExtension(Config.GlobalDataPath) + ".smc");

                //Apply patch to clean ROM
                {
                    var fullPatchPath = Path.GetFullPath(Config.GlobalDataPath);
                    var fullCleanPath = Path.GetFullPath(Config.CleanPath);

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    psi = new ProcessStartInfo(Config.FlipsPath,
                            $"--apply \"{fullPatchPath}\" \"{fullCleanPath}\" \"{globalDataROMPath}\"");
                    p = Process.Start(psi);
                    p.WaitForExit();

                    if (p.ExitCode == 0)
                        Log("Global Data Patch Success!", ConsoleColor.Green);
                    else
                    {
                        Log("Global Data Patch Failure!", ConsoleColor.Red);
                        return Result.Error;
                    }
                }

                //Overworld
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    psi = new ProcessStartInfo(Config.LunarMagicPath,
                                $"-TransferOverworld \"{Config.TempPath}\" \"{globalDataROMPath}\"");
                    p = Process.Start(psi);
                    p.WaitForExit();

                    if (p.ExitCode == 0)
                        Log("Overworld Import Success!", ConsoleColor.Green);
                    else
                    {
                        Log("Overworld Import Failure!", ConsoleColor.Red);
                        return Result.Error;
                    }
                }

                //Global EX Animations
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    psi = new ProcessStartInfo(Config.LunarMagicPath,
                                $"-TransferLevelGlobalExAnim \"{Config.TempPath}\" \"{globalDataROMPath}\"");
                    p = Process.Start(psi);
                    p.WaitForExit();

                    if (p.ExitCode == 0)
                        Log("Global EX Animation Import Success!", ConsoleColor.Green);
                    else
                    {
                        Log("Global EX Animation Import Failure!", ConsoleColor.Red);
                        return Result.Error;
                    }
                }

                //Title Screen
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    psi = new ProcessStartInfo(Config.LunarMagicPath,
                                $"-TransferTitleScreen \"{Config.TempPath}\" \"{globalDataROMPath}\"");
                    p = Process.Start(psi);
                    p.WaitForExit();

                    if (p.ExitCode == 0)
                        Log("Title Screen Import Success!", ConsoleColor.Green);
                    else
                    {
                        Log("Title Screen Import Failure!", ConsoleColor.Red);
                        return Result.Error;
                    }
                }

                //Credits
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    psi = new ProcessStartInfo(Config.LunarMagicPath,
                                $"-TransferCredits \"{Config.TempPath}\" \"{globalDataROMPath}\"");
                    p = Process.Start(psi);
                    p.WaitForExit();

                    if (p.ExitCode == 0)
                        Log("Credits Import Success!", ConsoleColor.Green);
                    else
                    {
                        Log("Credits Import Failure!", ConsoleColor.Red);
                        return Result.Error;
                    }
                }

                if (File.Exists(globalDataROMPath))
                    File.Delete(globalDataROMPath);

                Log("All Global Data Imported!", ConsoleColor.Green);
                Console.WriteLine();
            }
            return Result.Success;
        }

        static private Result ImportSharedPalettes()
        {
            Log("Shared Palette", ConsoleColor.Cyan);
            if (string.IsNullOrWhiteSpace(Config.SharedPalettePath))
            {
                Log("No path to Shared Palette provided, no palette will be imported.", ConsoleColor.Red);
                return Result.NotSpecified;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                ProcessStartInfo psi = new ProcessStartInfo(Config.LunarMagicPath,
                            $"-ImportSharedPalette \"{Config.TempPath}\" \"{Config.SharedPalettePath}\"");
                var p = Process.Start(psi);
                p.WaitForExit();

                if (p.ExitCode == 0)
                    Log("Shared Palette Import Success!", ConsoleColor.Green);
                else
                {
                    Log("Shared Palette Import Failure!", ConsoleColor.Red);
                    return Result.Error;
                }

                Console.WriteLine();
            }
            return Result.Success;
        }

        static private Result ImportTitleMoves()
        {
            Log("Title Moves", ConsoleColor.Cyan);
            if (string.IsNullOrWhiteSpace(Config.TitleMovesPath))
            {
                Log("No path to Title Moves provided, no title moves will be imported.", ConsoleColor.Red);
                return Result.NotSpecified;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                ProcessStartInfo psi = new ProcessStartInfo(Config.LunarMagicPath,
                            $"-ImportTitleMoves \"{Config.TempPath}\" \"{Config.TitleMovesPath}\"");
                var p = Process.Start(psi);
                p.WaitForExit();

                if (p.ExitCode == 0)
                    Log("Title Moves Import Success!", ConsoleColor.Green);
                else
                {
                    Log("Title Moves Import Failure!", ConsoleColor.Red);
                    return Result.Error;
                }

                Console.WriteLine();
            }
            return Result.Success;
        }

        static private Result ImportMap16()
        {
            Log("Map16", ConsoleColor.Cyan);
            if (string.IsNullOrWhiteSpace(Config.Map16Path))
            {
                Log("No path to map16 provided, no map16 will be imported.", ConsoleColor.Red);
                return Result.NotSpecified;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(Config.HumanReadableMap16CLI))
                    Log("No path to human-readable-map16-cli.exe provided, using binary map16 format", ConsoleColor.Red);
                else
                {
                    string humanReadableDirectory;
                    if (!string.IsNullOrWhiteSpace(Config.HumanReadableMap16Directory))
                        humanReadableDirectory = Config.HumanReadableMap16Directory;
                    else
                        humanReadableDirectory = Path.Combine(Path.GetDirectoryName(Config.Map16Path), Path.GetFileNameWithoutExtension(Config.Map16Path));

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    ProcessStartInfo process = new ProcessStartInfo(Config.HumanReadableMap16CLI,
                                $"--to-map16 \"{humanReadableDirectory}\" \"{Config.Map16Path}\"");
                    var proc = Process.Start(process);
                    proc.WaitForExit();

                    if (proc.ExitCode == 0)
                    {
                        Log("Human Readable Map16 Conversion Success!", ConsoleColor.Green);
                    }
                    else
                    {
                        Log("Human Readable Map16 Conversion Failure!", ConsoleColor.Red);
                        return Result.Error;
                    }
                }

                Console.ForegroundColor = ConsoleColor.Yellow;
                ProcessStartInfo psi = new ProcessStartInfo(Config.LunarMagicPath,
                            $"-ImportAllMap16 \"{Config.TempPath}\" \"{Config.Map16Path}\"");
                var p = Process.Start(psi);
                p.WaitForExit();

                if (p.ExitCode == 0)
                {
                    Log("Map16 Import Success!", ConsoleColor.Green);
                    Console.WriteLine();
                    return Result.Success;
                }
                else
                {
                    Log("Map16 Import Failure!", ConsoleColor.Red);
                    return Result.Error;
                }

            }
        }

        static private Result ApplyGPS()
        {
            Log("GPS", ConsoleColor.Cyan);
            if (string.IsNullOrWhiteSpace(Config.GPSPath))
            {
                Log("No path to GPS provided, no blocks will be inserted.", ConsoleColor.Red);
                return Result.NotSpecified;
            }
            else if (!File.Exists(Config.GPSPath))
            {
                Log("GPS not found at provided path, no blocks will be inserted.", ConsoleColor.Red);
                return Result.NotFound;
            }
            else
            {
                var dir = Path.GetFullPath(Path.GetDirectoryName(Config.GPSPath));
                var rom = Path.GetRelativePath(dir, Path.GetFullPath(Config.TempPath));
                Console.ForegroundColor = ConsoleColor.Yellow;

                ProcessStartInfo psi = new ProcessStartInfo(Config.GPSPath, $"-l \"{dir}/list.txt\" {Config.GPSOptions ?? ""} \"{rom}\"");
                psi.RedirectStandardInput = true;
                psi.WorkingDirectory = dir;

                var p = Process.Start(psi);
                p.WaitForExit();

                if (p.ExitCode == 0)
                    Log("GPS Success!", ConsoleColor.Green);
                else
                {
                    Log("GPS Failure!", ConsoleColor.Red);
                    return Result.Error;
                }

                Console.WriteLine();
            }
            return Result.Success;
        }

        static private Result ApplyAddmusicK()
        {
            Log("AddMusicK", ConsoleColor.Cyan);
            if (string.IsNullOrWhiteSpace(Config.AddMusicKPath))
            {
                Log("No path to AddMusicK provided, no music will be inserted.", ConsoleColor.Red);
                return Result.NotSpecified;
            }
            else if (!File.Exists(Config.AddMusicKPath))
            {
                Log("AddMusicK not found at provided path, no music will be inserted.", ConsoleColor.Red);
                return Result.NotFound;
            }
            else
            {
                var dir = Path.GetFullPath(Path.GetDirectoryName(Config.AddMusicKPath));

                // create bin folder if missing
                {
                    string bin = Path.Combine(dir, "asm", "SNES", "bin");
                    if (!Directory.Exists(bin))
                        Directory.CreateDirectory(bin);
                }

                var rom = Path.GetRelativePath(dir, Path.GetFullPath(Config.TempPath));
                Console.ForegroundColor = ConsoleColor.Yellow;

                ProcessStartInfo psi = new ProcessStartInfo(Config.AddMusicKPath, $" -noblock {Config.AddmusicKOptions ?? ""} \"{rom}\"");
                psi.RedirectStandardInput = true;
                psi.WorkingDirectory = dir;

                var p = Process.Start(psi);
                p.WaitForExit();

                if (p.ExitCode == 0)
                    Log("AddMusicK Success!", ConsoleColor.Green);
                else
                {
                    Log("AddMusicK Failure!", ConsoleColor.Red);
                    return Result.Error;
                }

                Console.WriteLine();
            }
            return Result.Success;
        }

        static private Result ApplyUberASM()
        {
            Log("Uber ASM", ConsoleColor.Cyan);
            if (string.IsNullOrWhiteSpace(Config.UberASMPath))
            {
                Log("No path to UberASMTool provided, no UberASM will be inserted.", ConsoleColor.Red);
                return Result.NotSpecified;
            }
            else if (!File.Exists(Config.UberASMPath))
            {
                Log("UberASMTool not found at provided path, no UberASM will be inserted.", ConsoleColor.Red);
                return Result.NotFound;
            }
            else
            {
                var dir = Path.GetFullPath(Path.GetDirectoryName(Config.UberASMPath));

                // create work folder if missing
                {
                    string bin = Path.Combine(dir, "asm", "work");
                    if (!Directory.Exists(bin))
                        Directory.CreateDirectory(bin);
                }

                var rom = Path.GetRelativePath(dir, Path.GetFullPath(Config.TempPath));
                Console.ForegroundColor = ConsoleColor.Yellow;

                ProcessStartInfo psi = new ProcessStartInfo(Config.UberASMPath, $"{Config.UberASMOptions ?? "list.txt"} \"{rom}\"");
                psi.RedirectStandardInput = true;
                psi.RedirectStandardOutput = true;
                psi.WorkingDirectory = dir;

                StringBuilder uberasm_output = new StringBuilder();

                var p = Process.Start(psi);
                p.OutputDataReceived += (sender, args) => { 
                    uberasm_output.AppendLine(args.Data); 
                    Log(args.Data, ConsoleColor.Yellow); 
                };
                p.BeginOutputReadLine();
                p.WaitForExit();

                string output = uberasm_output.ToString();

                if (output.Contains(uberasm_success_string))
                    Log("UberASM Success!", ConsoleColor.Green);
                else
                {
                    Log("UberASM Failure!", ConsoleColor.Red);
                    return Result.Error;
                }

                Console.WriteLine();
            }
            return Result.Success;
        }

        static private Result ApplyPixi()
        {
            if (string.IsNullOrWhiteSpace(Config.PixiPath))
            {
                Log("No path to Pixi provided, no sprites will be inserted.", ConsoleColor.Red);
                return Result.NotSpecified;
            }
            else if (!File.Exists(Config.PixiPath))
            {
                Log("Pixi not found at provided path, no sprites will be inserted.", ConsoleColor.Red);
                return Result.NotFound;
            }
            else
            {
                var dir = Path.GetFullPath(Path.GetDirectoryName(Config.PixiPath));
                var list = Path.Combine(dir, "list.txt");
                Console.ForegroundColor = ConsoleColor.Yellow;

                ProcessStartInfo psi = new ProcessStartInfo(Config.PixiPath, $"{Config.PixiOptions ?? ""} \"{Config.TempPath}\"");
                psi.RedirectStandardInput = true;

                var p = Process.Start(psi);
                p.WaitForExit();

                if (p.ExitCode == 0)
                    Log("Pixi Success!", ConsoleColor.Green);
                else
                {
                    Log("Pixi Failure!", ConsoleColor.Red);
                    return Result.Error;
                }

                Console.WriteLine();
            }
            return Result.Success;
        }

        static private bool ImportLevels(bool reinsert)
        {
            var romPath = (reinsert ? Config.OutputPath : Config.TempPath);

            Log("Levels", ConsoleColor.Cyan);
            if (reinsert && !File.Exists(romPath))
                Log("Output ROM does not exist! Build first.", ConsoleColor.Red);
            else if (string.IsNullOrWhiteSpace(Config.LevelsPath))
                Log("No path to Levels provided, no levels will be imported.", ConsoleColor.Red);
            else
            {
                // import levels
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    ProcessStartInfo psi = new ProcessStartInfo(Config.LunarMagicPath,
                                $"-ImportMultLevels \"{romPath}\" \"{Config.LevelsPath}\" {Config.LunarMagicLevelImportFlags ?? ""}");
                    var p = Process.Start(psi);
                    p.WaitForExit();

                    if (p.ExitCode == 0)
                        Log("Levels Import Success!", ConsoleColor.Green);
                    else
                    {
                        Log("Levels Import Failure!", ConsoleColor.Red);
                        return false;
                    }

                    Console.WriteLine();
                }
            }

            return true;
        }

        static private void Edit()
        {
            if (!File.Exists(Config.OutputPath))
                Error("Output ROM not found - build first!");
            else if (string.IsNullOrWhiteSpace(Config.LunarMagicPath))
                Log("No path to Lunar Magic provided, cannot open built ROM.", ConsoleColor.Red);
            else if (!File.Exists(Config.LunarMagicPath))
                Log("Lunar Magic not found at provided path, cannot open built ROM.", ConsoleColor.Red);
            else
            {
                if (LunarMagicProcess != null && !LunarMagicProcess.HasExited)
                    LunarMagicProcess.Kill(true);

                ProcessStartInfo psi = new ProcessStartInfo(Config.LunarMagicPath,
                            $"\"{Config.OutputPath}\"");
                LunarMagicProcess = Process.Start(psi);
            }
        }

        static private bool Package()
        {
            Log("Packaging BPS patch...", ConsoleColor.Cyan);

            if (File.Exists(Config.PackagePath))
                File.Delete(Config.PackagePath);

            if (!File.Exists(Config.OutputPath))
                Error("Output ROM not found!");
            else if (string.IsNullOrWhiteSpace(Config.PackagePath))
                Error("Package path not set in config!");
            else if (string.IsNullOrWhiteSpace(Config.CleanPath))
                Error("No clean SMW ROM path set in config!");
            else if (string.IsNullOrWhiteSpace(Config.FlipsPath))
                Error("No path to FLIPS provided in config!");
            else if (!File.Exists(Config.FlipsPath))
                Error("Could not find FLIPS at configured path!");
            else
            {
                var fullCleanPath = Path.GetFullPath(Config.CleanPath);
                var fullOutputPath = Path.GetFullPath(Config.OutputPath);
                var fullPackagePath = Path.GetFullPath(Config.PackagePath);

                if (CreatePatch(fullCleanPath, fullOutputPath, fullPackagePath))
                    Log("Packaging Success!", ConsoleColor.Green);
                else
                {
                    Log("Packaging Failure!", ConsoleColor.Red);
                    return false;
                }
            }

            //Open explorer and select patch file
            if (File.Exists(Config.PackagePath))
                Process.Start("explorer.exe", $"/select, \"{Config.PackagePath}\"");

            Console.WriteLine();

            return true;
        }

        static private void Help()
        {
            Log("Function list:", ConsoleColor.Magenta);

            Log("B - (Re)Build", ConsoleColor.Yellow);
            Log("Creates your ROM from scratch, using your provided clean SMW ROM as a base and inserting all the configured patches, graphics, levels, etc.\n");

            Log("Q - Quick Build", ConsoleColor.Yellow);
            Log("Attempts to reuse a previously built ROM for the build process if it is available. Automatically determines if a full rebuild is " +
                "needed or if only certain tools need to be reapplied or resources need to be reinserted.\n");

            Log("R - Run", ConsoleColor.Yellow);
            Log("Loads the previously-built ROM into the configured emulator for testing. The ROM must already be built first.\n");

            Log("T - Test (Build -> Run)", ConsoleColor.Yellow);
            Log("Executes the above two commands in sequence.\n");

            Log("E - Edit (in Lunar Magic)", ConsoleColor.Yellow);
            Log("Opens the previously-built ROM in Lunar Magic. The ROM must already be built first.\n");

            Log("P - Package", ConsoleColor.Yellow);
            Log("Creates a BPS patch for your ROM against the configured clean SMW ROM, so that you can share it!\n");
        }

        static private bool ApplyPatch(string cleanROM, string outROM, string patchBPS)
        {
            Log($"Patching {cleanROM}\n\tto {outROM}\n\twith {patchBPS}", ConsoleColor.Yellow);

            Console.ForegroundColor = ConsoleColor.Yellow;
            var psi = new ProcessStartInfo(Config.FlipsPath,
                    $"--apply \"{patchBPS}\" \"{cleanROM}\" \"{outROM}\"");
            var p = Process.Start(psi);
            p.WaitForExit();

            if (p.ExitCode == 0)
                return true;
            else
                return false;
        }

        static private bool CreatePatch(string cleanROM, string hackROM, string outputBPS)
        {
            Log($"Creating Patch {outputBPS}\n\twith {hackROM}\n\tover {cleanROM}", ConsoleColor.Yellow);

            Console.ForegroundColor = ConsoleColor.Yellow;
            var psi = new ProcessStartInfo(Config.FlipsPath,
                    $"--create --bps-delta \"{cleanROM}\" \"{hackROM}\" \"{outputBPS}\"");
            var p = Process.Start(psi);
            p.WaitForExit();

            if (p.ExitCode == 0)
                return true;
            else
                return false;
        }

        static public void Error(string error)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"ERROR: {error}\n");
        }

        static public void Log(string msg, ConsoleColor color = ConsoleColor.White)
        {
            Console.ForegroundColor = color;
            Console.WriteLine($"{msg}");
        }

        static public void Lognl(string msg, ConsoleColor color = ConsoleColor.White)
        {
            Console.ForegroundColor = color;
            Console.Write($"{msg}");
        }
    }
}
