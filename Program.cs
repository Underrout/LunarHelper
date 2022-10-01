using System;
using FluentArgs;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using System.Text;

using LunarHelper;
using System.Linq;
using System.Numerics;
using System.Reflection;

namespace LunarHelper
{
    class Program
    {
        static public Config Config { get; private set; }

        static private string uberasm_success_string = "Codes inserted successfully.";

        static private int COMMENT_FIELD_SFC_ROM_OFFSET = 0x7F120;
        static private int COMMENT_FIELD_SMC_ROM_OFFSET = 0x7F320;
        static private int COMMENT_FIELD_LENGTH = 0x20;
        static private string DEFAULT_COMMENT = "I am Naaall, and I love fiiiish!";
        static private string ALTERED_COMMENT = "   Mario says     TRANS RIGHTS  ";

        static private readonly Regex LevelRegex = new Regex("[0-9a-fA-F]{3}");
        static private Process EmulatorProcess = null;
        static private Process LunarMagicProcess;

        static private ConsoleColor[] profile_colors = { 
            ConsoleColor.Gray, ConsoleColor.Cyan, ConsoleColor.Green,
            ConsoleColor.Red, ConsoleColor.Magenta, ConsoleColor.Yellow,
            ConsoleColor.Blue, ConsoleColor.DarkCyan, ConsoleColor.DarkGreen,
            ConsoleColor.DarkMagenta
        };

        static ConsoleColor curr_profile_color = ConsoleColor.Gray;

        static private DependencyGraph dependency_graph;

        static private ProfileManager profile_manager;

        private enum Result
        {
            NotSpecified,
            NotFound,
            Error,
            Success
        }

        static private Dictionary<InsertableType, Func<bool>> insertion_functions = new Dictionary<InsertableType, Func<bool>>()
        {
            { InsertableType.Pixi, ApplyPixi },
            { InsertableType.AddMusicK, ApplyAddmusicK },
            { InsertableType.Gps, ApplyGPS },
            { InsertableType.UberAsm, ApplyUberASM },
            { InsertableType.Graphics, ImportGraphics },
            { InsertableType.ExGraphics, ImportExGraphics },
            { InsertableType.Map16, ImportMap16 },
            { InsertableType.SharedPalettes, ImportSharedPalettes },
            { InsertableType.GlobalData, ImportGlobalData },
            { InsertableType.TitleMoves, ImportTitleMoves },
            { InsertableType.Levels, ImportLevels }
        };

        static int Main(string[] args)
        {
            Directory.SetCurrentDirectory(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName));

            if (args.Length > 0)
                return HandleCommandLineInvocation(args);

            profile_manager = new ProfileManager();
            if (profile_manager.current_profile != null)
                curr_profile_color = profile_colors[ProfileManager.GetAllProfiles().ToList().IndexOf(profile_manager.current_profile)];

            bool running = true;
            while (running)
            {
                bool show_profiles = profile_manager.current_profile != null;

                Log("Welcome to Lunar Helper ^_^", ConsoleColor.Cyan);

                if (show_profiles)
                {
                    Lognl("Current profile: ");
                    Lognl(profile_manager.current_profile ?? "Default", ConsoleColor.Black, curr_profile_color);
                    Log("");
                }

                Log("B - (Re)Build, Q - Quick Build, R - Run");
                Log("T - Test (Quick Build -> Run)");

                if (show_profiles)
                    Log("S - Switch profile");

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
                        if (Init() && QuickBuild())
                            Test();
                        break;

                    case ConsoleKey.S:
                        if (show_profiles)
                        {
                            SwitchProfile();
                            Console.Clear();
                        }
                        else
                            goto default;
                        break;

                    case ConsoleKey.R:
                        if (Init())
                            TryLaunchEmulator();
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
                        Console.BackgroundColor = ConsoleColor.Black;
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
            return 0;
        }

        static private int HandleCommandLineInvocation(string[] args)
        {
            var profile_list = ProfileManager.GetAllProfiles().ToList();
            profile_manager = new ProfileManager();
            if (profile_manager.current_profile != null)
                curr_profile_color = profile_colors[profile_list.IndexOf(profile_manager.current_profile)];

            int return_code = 0;

            FluentArgsBuilder.New()
                .Given.Flag("-h", "--help")
                    .Then(() => ShowCommandLineHelp())
                .Given.Flag("--profiles").Then(() =>
                {
                    var output = profile_list.Count() != 0 ? string.Join(" ", profile_list) : "";
                    Log(output);
                })
                .Given.Flag("-b", "--build")
                    .Then(b => b
                    .Parameter<char>("-v", "--volatile-resource-pref")
                        .WithDescription("Preference for handling of volatile resources in pre-existing ROM, " +
                        "valid options: Y (export & build), N (build without export), C (cancel build)")
                        .WithValidation(v => new[] {'Y', 'N', 'C'}.Contains(v), "Valid volatile resource handling options are 'Y', 'N', 'C'") 
                        .IsOptionalWithDefault('C')
                    .PositionalArgument<string>()
                        .WithDescription("Profile to use during build")
                        .WithValidation(profile => profile_list.Contains(profile), "Profile not found")
                        .IsOptional()
                    .Call(profile => volatile_pref =>
                    {
                        if (profile != null)
                            SwitchToProfile(profile);
                        var success = Init() && Build(true, volatile_pref);
                        return_code = success ? 0 : 1;
                    }))
                .Given.Flag("-q", "--quickbuild").Then(b => b
                    .Parameter<char>("-v", "--volatile-resource-pref")
                        .WithDescription("Preference for handling of volatile resources in pre-existing ROM, " +
                        "valid options: Y (export & build), N (build without export), C (cancel build)")
                        .WithValidation(v => new[] { 'Y', 'N', 'C' }.Contains(v), "Valid volatile resource handling options are 'Y', 'N', 'C'")
                        .IsOptionalWithDefault('C')
                    .PositionalArgument<string>()
                        .WithDescription("Profile to use during quick build")
                        .WithValidation(profile => profile_list.Contains(profile), "Profile not found")
                        .IsOptional()
                    .Call(profile => volatile_pref =>
                    {
                        if (profile != null)
                            SwitchToProfile(profile);
                        var success = Init() && QuickBuild(true, volatile_pref);
                        return_code = success ? 0 : 1;
                    }))
                .Given.Flag("-p", "--package").Then(b => b
                    .Parameter<char>("-v", "--volatile-resource-pref")
                        .WithDescription("Preference for handling of volatile resources in pre-existing ROM, " +
                        "valid options: Y (export & build), N (build without export), C (cancel build)")
                        .WithValidation(v => new[] { 'Y', 'N', 'C' }.Contains(v), "Valid volatile resource handling options are 'Y', 'N', 'C'")
                        .IsOptionalWithDefault('C')
                    .PositionalArgument<string>()
                        .WithDescription("Profile to use during quick build")
                        .WithValidation(profile => profile_list.Contains(profile), "Profile not found")
                        .IsOptional()
                    .Call(profile => volatile_pref =>
                    {
                        if (profile != null)
                            SwitchToProfile(profile);
                        var success = Init() && Build(true, volatile_pref) && Package();
                        return_code = success ? 0 : 1;
                    }))
                .Invalid()
                .Parse(args);

            return return_code;
        }

        static private void ShowCommandLineHelp()
        {
            Log("Usage:\n\n" +
                "LunarHelper.exe --build [profile to use] [-v volatile resource handling preference]\n" +
                "\tBuild ROM from scratch with given profile (uses default profile if second argument omitted)\n\n" +
                "LunarHelper.exe --quickbuild [profile to use] [-v volatile resource handling preference]\n" +
                "\tAttempt to update pre-existing ROM in-place with given profile (uses default profile if second argument omitted)\n\n" +
                "LunarHelper.exe --package [profile to use] [-v volatile resource handling preference]\n" +
                "\tAttempt to build and package ROM into BPS with given profile (uses default profile if second argument omitted)\n\n" +
                "LunarHelper.exe --profiles\n" +
                "\tList names of available profiles separated by spaces, will output an empty string if no profiles are found\n\n" +
                "Valid volatile resource handling preferences:\n\n" +
                "'Y': Export all resources and continue build (recommended)\n" +
                "'N': Don't export resources and continue build (NOT RECOMMENDED)\n" +
                "'C': Cancel build (default)");
        }

        static private void SwitchToProfile(string profile)
        {
            profile_manager.SwitchProfile(profile);
            curr_profile_color = profile_colors[ProfileManager.GetAllProfiles().ToList().IndexOf(profile)];
        }

        static private void SwitchProfile()
        {
            Console.Clear();

            while (true)
            {
                var all_profiles = ProfileManager.GetAllProfiles();

                Log("Choose profile to switch to:", ConsoleColor.Cyan);
                int i = 0;
                foreach (var profile in all_profiles)
                {
                    var profile_string = profile;
                    if (profile == profile_manager.current_profile)
                        profile_string += " (Current profile)";

                    Log($"{i++} - {profile_string}");
                }
                Log("ESC - Back");

                var key = Console.ReadKey(true);
                Console.Clear();

                if (key.Key == ConsoleKey.Escape)
                {
                    return;
                }

                if (char.IsDigit(key.KeyChar))
                {
                    var num = int.Parse(key.KeyChar.ToString());

                    if (num >= 0 && num < all_profiles.Count())
                    {
                        profile_manager.SwitchProfile(all_profiles.ElementAt(num));
                        curr_profile_color = profile_colors[ProfileManager.GetAllProfiles().ToList().IndexOf(profile_manager.current_profile)];
                        return;
                    }
                }

                string str = char.ToUpperInvariant(key.KeyChar).ToString().Trim();
                if (str.Length > 0)
                    Log($"Key '{str}' is not a recognized option!", ConsoleColor.Red);
                else
                    Log($"Key is not a recognized option!", ConsoleColor.Red);
                Console.WriteLine();
            }
        }

        static private bool QuickBuild(bool invoked_from_cli = false, char volatile_resource_handling_preference = ' ')
        {
            if (profile_manager.current_profile != null)
                profile_manager.WriteCurrentProfileToFile();
            else
                profile_manager.DeleteCurrentProfileFile();

            if (!HandleVolatileResources(invoked_from_cli, volatile_resource_handling_preference))
            {
                return false;
            }

            Lognl("Starting Quick Build");
            if (profile_manager.current_profile != null)
            {
                Lognl(" using profile ");
                Lognl(profile_manager.current_profile, ConsoleColor.Black, curr_profile_color);
            }
            Log("\n");

            Log("Building dependency graph...\n", ConsoleColor.Cyan);
            dependency_graph = new DependencyGraph(Config);
            List<Insertable> plan;

            try 
            {
                plan = BuildPlan.PlanQuickBuild(Config, dependency_graph);
            }
            catch (BuildPlan.CannotBuildException)
            {
                Log("Quick Build failed!\n", ConsoleColor.Red);
                return false;
            }
            catch (BuildPlan.MustRebuildException)
            {
                return Build();
            }
            catch (Exception e)
            {
                Log($"Encountered exception: '{e.Message}' while planning quick build, falling back to full rebuild...", ConsoleColor.Yellow);
                return Build();
            }
            
            if (plan.Count == 0)
            {
                // nothing to insert, we're up to date
                return true;
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

            foreach (var insertable in plan)
            {
                bool result = false;

                switch (insertable.type)
                {
                    case InsertableType.SingleLevel:
                        result = ImportSingleLevel(insertable.normalized_relative_path);
                        break;

                    case InsertableType.SinglePatch:
                        result = ApplyAsarPatch(insertable.normalized_relative_path);
                        break;

                    default:
                        result = insertion_functions[insertable.type]();
                        break;
                }

                if (!result)
                    return false;
            }

            Log("Marking ROM as non-volatile...", ConsoleColor.Yellow);
            if (WriteAlteredCommentToRom(Config.TempPath))
            {
                Log("Successfully marked ROM as non-volatile!", ConsoleColor.Green);
            }
            else
            {
                Log("WARNING: Failed to mark ROM as non-volatile, your ROM may be corrupted!", ConsoleColor.Red);
            }
            Console.WriteLine();

            FinalizeOutputROM();

            Log("Writing build report...\n", ConsoleColor.Cyan);
            WriteReport();

            Log($"ROM '{Config.OutputPath}' successfully updated!", ConsoleColor.Green);
            Console.WriteLine();

            WarnAboutProblematicDependencies();

            if (Config.ReloadEmulatorAfterBuild && EmulatorProcess != null && !EmulatorProcess.HasExited)
            {
                Log("Attempting to auto-relaunch your emulator...", ConsoleColor.Cyan);
                TryLaunchEmulator();
            }

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

            if (!Config.SuppressArbitraryDepsWarning)  // don't need to discover arbitrary dependencies if warning is disabled anyway, just disregard them
            {
                foreach (var arbitrary_vertex in dependency_graph.dependency_graph.Vertices.Where(v => v is ArbitraryFileVertex))
                {
                    arbitrary_dependencies = arbitrary_dependencies.Append(
                        ((ArbitraryFileVertex)arbitrary_vertex, DependencyGraphAnalyzer.GetDependents(dependency_graph, arbitrary_vertex)));
                }
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
                    "(see https://github.com/RPGHacker/asar/issues/253 for details).\n\nPotentially \"missing\" dependencies:\n");

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

            report.report_format_version = Report.REPORT_FORMAT_VERSION;

            report.build_time = DateTime.Now;

            report.dependency_graph = DependencyGraphSerializer.SerializeGraph(dependency_graph).ToList();

            report.levels = GetLevelReport();
            report.graphics = Report.HashFolder("Graphics");
            report.exgraphics = Report.HashFolder("ExGraphics");
            report.shared_palettes = Report.HashFile(Config.SharedPalettePath);
            report.init_bps = Report.HashFile(Config.InitialPatch);
            report.global_data = Report.HashFile(Config.GlobalDataPath);
            report.title_moves = Report.HashFile(Config.TitleMovesPath);

            report.build_order_hash = Report.HashList(Config.BuildOrder);

            report.lunar_helper_version = Assembly.GetExecutingAssembly().GetName().Version.ToString();

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

            if (profile_manager.current_profile != null && !ProfileManager.GetAllProfiles().Contains(profile_manager.current_profile))
            {
                // if profile no longer exists, throw error
                Error("The configured profile seems to no longer exist");
                profile_manager.SwitchProfile(profile_manager.DetermineCurrentProfile());
                if (profile_manager.current_profile != null)
                    curr_profile_color = profile_colors[ProfileManager.GetAllProfiles().ToList().IndexOf(profile_manager.current_profile)];
                return false;
            }

            // load config
            Config = profile_manager.DetermineConfig();

            if (Config == null)
                return false;

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

            try
            {
                Config.VerifyConfig(Config);
            }
            catch (Exception e)
            {
                Error(e.Message);
                return false;
            }

            return true;
        }

        static bool PotentiallyVolatileResourcesInRom(string rom_path)
        {
            try
            {
                using (BinaryReader br = new BinaryReader(File.OpenRead(rom_path)))
                {
                    var offset = Config.OutputPath.EndsWith(".smc") ? COMMENT_FIELD_SMC_ROM_OFFSET : COMMENT_FIELD_SFC_ROM_OFFSET;

                    br.BaseStream.Seek(offset, SeekOrigin.Begin);
                    byte[] comment_bytes = br.ReadBytes(COMMENT_FIELD_LENGTH);
                    string comment = Encoding.ASCII.GetString(comment_bytes);

                    // if the comment is unaltered, the last edit was made by an uninjected Lunar Magic, which means
                    // we could have volatile resources!
                    return comment == DEFAULT_COMMENT;
                }
            }
            catch (Exception)
            {
                // if we failed to read the comment string from the ROM we probably
                // have a corrupt ROM, which means even if there are volatile resources, we
                // probably can't really get them out anyway...
                return false;
            }
        }

        static bool WriteAlteredCommentToRom(string rom_path)
        {
            try
            {
                using (Stream stream = File.Open(rom_path, FileMode.Open))
                {
                    stream.Position = Config.OutputPath.EndsWith(".smc") ? COMMENT_FIELD_SMC_ROM_OFFSET : COMMENT_FIELD_SFC_ROM_OFFSET;
                    stream.Write(Encoding.ASCII.GetBytes(ALTERED_COMMENT), 0, COMMENT_FIELD_LENGTH);
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        // bool returned is true if it's ok to continue with build, false if it should be cancelled
        // defaults to cancelling the build if invoked from the command line without specifying a preference
        static bool HandleVolatileResources(bool cli_invoked = false, char cli_decision = 'C')
        {
            if (!File.Exists(Config.OutputPath))
            {
                return true;
            }

            Log("Checking for potentially unexported resources in pre-existing ROM...", ConsoleColor.Yellow);

            if (!PotentiallyVolatileResourcesInRom(Config.OutputPath))
            {
                Log("No potentially unexported resources detected in ROM!\n", ConsoleColor.Green);
                return true;
            }

            if (cli_invoked)
            {
                Log("Potentially unexported resources detected in ROM!", ConsoleColor.Red);

                switch (cli_decision)
                {
                    case 'Y':
                        Log("Attempting to export all resources and continue with build...", ConsoleColor.Yellow);
                        if (Save())
                        {
                            WriteAlteredCommentToRom(Config.OutputPath);  // save to mark as non-volatile since export succeeded
                            Console.WriteLine();
                            return true;
                        }
                        else
                        {
                            Log("Failed to export at least one potentially volatile resource, please fix any errors shown " +
                                "and attempt this process again!", ConsoleColor.Yellow);
                            return false;
                        }

                    case 'N':
                        Log("Continuing with build as user has specified to ignore volatile resources via command line\n", ConsoleColor.Yellow);
                        return true;

                    case 'C':
                        Error("Cancelling build due to potentially volatile resources in ROM\n");
                        return false;
                }
            }

            while (true)
            {
                Console.Clear();
                Log("WARNING: Potentially unexported resources detected", ConsoleColor.Red);
                Log($"There may be unexported resources present in ROM '{Config.OutputPath}'.\n" +
                    "It is recommended that all resources are exported from the ROM prior to building at " +
                    "this point, as it could potentially be overwritten during the build process.\n\nWould you " +
                    "like to export all resources now?\n\nY - Yes, export resources and build afterwards (recommended)\n" +
                    "N - No, build anyway (use at your own risk, any unexported resources in the ROM will be LOST!)\n" +
                    "C - Cancel build, do not export resources", ConsoleColor.Yellow);

                var key = Console.ReadKey(true);
                Console.Clear();

                switch (key.Key)
                {
                    case ConsoleKey.Y:
                        if (Save())
                        {
                            Console.WriteLine();
                            return true;
                        }
                        else
                        {
                            Log("Failed to export at least one potentially volatile resource, please fix any errors shown " +
                                "and attempt this process again!", ConsoleColor.Yellow);
                            return false;
                        }

                    case ConsoleKey.N:
                        return true;

                    case ConsoleKey.C:
                        return false;
                }

                string str = char.ToUpperInvariant(key.KeyChar).ToString().Trim();
                if (str.Length > 0)
                    Log($"Key '{str}' is not a recognized option!", ConsoleColor.Red);
                else
                    Log($"Key is not a recognized option!", ConsoleColor.Red);
                Console.WriteLine();
            }
        }

        static private bool Build(bool invoked_from_cli = false, char volatile_resource_handling_preference = ' ')
        {
            if (profile_manager.current_profile != null)
                profile_manager.WriteCurrentProfileToFile();
            else
                profile_manager.DeleteCurrentProfileFile();

            if (!HandleVolatileResources(invoked_from_cli, volatile_resource_handling_preference))
            {
                return false;
            }

            List<Insertable> plan = BuildPlan.PlanBuild(Config);

            Lognl("Starting Build");
            if (profile_manager.current_profile != null)
            {
                Lognl(" using profile ");
                Lognl(profile_manager.current_profile, ConsoleColor.Black, curr_profile_color);
            }
            Log("\n");

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

            foreach (var insertable in plan)
            {
                bool result = false;

                switch (insertable.type)
                {
                    case InsertableType.SingleLevel:
                        result = ImportSingleLevel(insertable.normalized_relative_path);
                        break;

                    case InsertableType.SinglePatch:
                        result = ApplyAsarPatch(insertable.normalized_relative_path);
                        break;

                    default:
                        result = insertion_functions[insertable.type]();
                        break;
                }

                if (!result)
                    return false;
            }

            Log("Marking ROM as non-volatile...", ConsoleColor.Yellow);
            if (WriteAlteredCommentToRom(Config.TempPath))
            {
                Log("Successfully marked ROM as non-volatile!", ConsoleColor.Green);
            }
            else
            {
                Log("WARNING: Failed to mark ROM as non-volatile, your ROM may be corrupted!", ConsoleColor.Red);
            }
            Console.WriteLine();

            Log("Building dependency graph...\n", ConsoleColor.Cyan);
            dependency_graph = new DependencyGraph(Config);

            FinalizeOutputROM();

            Log("Writing build report...\n", ConsoleColor.Cyan);
            WriteReport();

            Log($"ROM patched successfully to '{Config.OutputPath}'!", ConsoleColor.Green);
            Console.WriteLine();

            if (Config.ReloadEmulatorAfterBuild && EmulatorProcess != null && !EmulatorProcess.HasExited)
            {
                Log("Attempting to auto-relaunch your emulator...", ConsoleColor.Cyan);
                TryLaunchEmulator();
            }

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

            TryLaunchEmulator();

            Log("Test routine complete!", ConsoleColor.Magenta);
            Console.WriteLine();

            return true;
        }

        static private void TryLaunchEmulator()
        {
            // emulator
            if (!string.IsNullOrWhiteSpace(Config.EmulatorPath))
            {
                Log("Launching emulator...", ConsoleColor.Yellow);
                var fullRom = Path.GetFullPath(Config.OutputPath);

                if (EmulatorProcess != null && !EmulatorProcess.HasExited)
                    EmulatorProcess.Kill(true);

                ProcessStartInfo psi = new ProcessStartInfo(Config.EmulatorPath,
                    $"{Config.EmulatorOptions} \"{fullRom}\"");

                EmulatorProcess = Process.Start(psi);

                if (EmulatorProcess != null)
                {
                    Log("Emulator launched!", ConsoleColor.Green);
                }
                else
                {
                    Error("Emulator launch failed");
                }
            }
            else
            {
                Log("No emulator specified!", ConsoleColor.Red);
            }
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

        static private bool ImportGlobalData()
        {
            Log("Global Data", ConsoleColor.Cyan);

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
                    return false;
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
                    return false;
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
                    return false;
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
                    return false;
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
                    return false;
                }
            }

            if (File.Exists(globalDataROMPath))
                File.Delete(globalDataROMPath);

            Log("All Global Data Imported!", ConsoleColor.Green);
            Console.WriteLine();

            return true;
        }

        static private bool ImportSharedPalettes()
        {
            Log("Shared Palette", ConsoleColor.Cyan);

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
                return false;
            }

            Console.WriteLine();

            return true;
        }

        static private bool ImportTitleMoves()
        {
            Log("Title Moves", ConsoleColor.Cyan);

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
                return false;
            }

            Console.WriteLine();

            return true;
        }

        static private bool ImportMap16()
        {
            Log("Map16", ConsoleColor.Cyan);

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
                    return false;
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
                return true;
            }
            else
            {
                Log("Map16 Import Failure!", ConsoleColor.Red);
                return false;
            }
        }

        static private bool ApplyGPS()
        {
            Log("GPS", ConsoleColor.Cyan);

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
                return false;
            }

            Console.WriteLine();

            return true;
        }

        static private bool ApplyAddmusicK()
        {
            Log("AddmusicK", ConsoleColor.Cyan);
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
                Log("AddmusicK Success!", ConsoleColor.Green);
            else
            {
                Log("AddmusicK Failure!", ConsoleColor.Red);
                return false;
            }

            Console.WriteLine();

            return true;
        }

        static private bool ApplyUberASM()
        {
            Log("UberASM", ConsoleColor.Cyan);
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
                return false;
            }

            Console.WriteLine();

            return true;
        }

        static private bool ApplyPixi()
        {
            Log("PIXI", ConsoleColor.Cyan);

            var dir = Path.GetFullPath(Path.GetDirectoryName(Config.PixiPath));
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
                return false;
            }

            Console.WriteLine();
            return true;
        }

        static private bool ImportGraphics()
        {
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
                return true;
            }
        }

        static private bool ImportSingleLevel(string level_path)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            ProcessStartInfo psi = new ProcessStartInfo(Config.LunarMagicPath,
                        $"-ImportLevel \"{Config.TempPath}\" \"{level_path}\"");
            var p = Process.Start(psi);
            p.WaitForExit();

            if (p.ExitCode != 0)
            {
                Log($"Level import failure on '{level_path}'!", ConsoleColor.Red);
                return false;
            }

            return true;
        }

        static private bool ImportExGraphics()
        {
            Log("ExGraphics", ConsoleColor.Cyan);
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
            return true;
        }

        static private bool ApplyAsarPatch(string patch_path)
        {
            Log($"Patch '{patch_path}'", ConsoleColor.Cyan);

            ProcessStartInfo psi = new ProcessStartInfo(Config.AsarPath, $"{Config.AsarOptions ?? ""} \"{patch_path}\" \"{Config.TempPath}\"");

            var p = Process.Start(psi);
            p.WaitForExit();

            if (p.ExitCode == 0)
            {
                Log("Patch Success!\n", ConsoleColor.Green);
                return true;
            }
            else
            {
                Log("Patch Failure!\n", ConsoleColor.Red);
                return false;
            }
        }

        static private bool ImportLevels()
        {
            Log("Levels", ConsoleColor.Cyan);
            // import levels
            Console.ForegroundColor = ConsoleColor.Yellow;
            ProcessStartInfo psi = new ProcessStartInfo(Config.LunarMagicPath,
                        $"-ImportMultLevels \"{Config.TempPath}\" \"{Config.LevelsPath}\" {Config.LunarMagicLevelImportFlags ?? ""}");
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

            return true;
        }

        [System.Runtime.InteropServices.DllImport("User32.dll")]
        private static extern bool SetForegroundWindow(IntPtr handle);
        [System.Runtime.InteropServices.DllImport("User32.dll")]
        private static extern bool ShowWindow(IntPtr handle, int nCmdShow);
        [System.Runtime.InteropServices.DllImport("User32.dll")]
        private static extern bool IsIconic(IntPtr handle);

        const int SW_RESTORE = 9;

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
                {
                    IntPtr handle = LunarMagicProcess.MainWindowHandle;
                    if (IsIconic(handle))
                    {
                        ShowWindow(handle, SW_RESTORE);
                    }

                    SetForegroundWindow(handle);
                    return;
                }

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

            Log("T - Test (Quick Build -> Run)", ConsoleColor.Yellow);
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

        static private bool Save()
        {
            if (!File.Exists(Config.OutputPath))
            {
                Log("Output ROM does not exist! Build first!", ConsoleColor.Red);
                return false;
            }

            // save global data
            Log("Saving Global Data BPS...", ConsoleColor.Cyan);
            if (string.IsNullOrWhiteSpace(Config.GlobalDataPath))
                Log("No path for GlobalData BPS provided!", ConsoleColor.Red);
            else if (string.IsNullOrWhiteSpace(Config.CleanPath))
                Log("No path for Clean ROM provided!", ConsoleColor.Red);
            else if (!File.Exists(Config.CleanPath))
                Log("Clean ROM does not exist!", ConsoleColor.Red);
            else if (string.IsNullOrWhiteSpace(Config.FlipsPath))
                Log("No path to Flips provided!", ConsoleColor.Red);
            else if (!File.Exists(Config.FlipsPath))
                Log("Flips not found at the provided path!", ConsoleColor.Red);
            else
            {
                if (File.Exists(Config.GlobalDataPath))
                    File.Delete(Config.GlobalDataPath);

                var fullCleanPath = Path.GetFullPath(Config.CleanPath);
                var fullOutputPath = Path.GetFullPath(Config.OutputPath);
                var fullPackagePath = Path.GetFullPath(Config.GlobalDataPath);
                if (CreatePatch(fullCleanPath, fullOutputPath, fullPackagePath))
                    Log("Global Data Patch Success!", ConsoleColor.Green);
                else
                {
                    Log("Global Data Patch Failure!", ConsoleColor.Red);
                    return false;
                }
            }

            // export map16
            Log("Exporting Map16...", ConsoleColor.Cyan);
            if (string.IsNullOrWhiteSpace(Config.Map16Path))
                Log("No path for Map16 provided!", ConsoleColor.Red);
            else if (string.IsNullOrWhiteSpace(Config.LunarMagicPath))
                Log("No Lunar Magic Path provided!", ConsoleColor.Red);
            else if (!File.Exists(Config.LunarMagicPath))
                Log("Could not find Lunar Magic at the provided path!", ConsoleColor.Red);
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                ProcessStartInfo psi = new ProcessStartInfo(Config.LunarMagicPath,
                            $"-ExportAllMap16 \"{Config.OutputPath}\" \"{Config.Map16Path}\"");
                var p = Process.Start(psi);
                p.WaitForExit();

                if (p.ExitCode == 0)
                    Log("Map16 Export Success!", ConsoleColor.Green);
                else
                {
                    Log("Map16 Export Failure!", ConsoleColor.Red);
                    return false;
                }

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
                                $"--from-map16 \"{Config.Map16Path}\" \"{humanReadableDirectory}\"");
                    var proc = Process.Start(process);
                    proc.WaitForExit();

                    if (proc.ExitCode == 0)
                        Log("Human Readable Map16 Conversion Success!", ConsoleColor.Green);
                    else
                    {
                        Log("Human Readable Map16 Conversion Failure!", ConsoleColor.Red);
                        return false;
                    }
                }
            }

            // export shared palette
            Log("Exporting Shared Palette...", ConsoleColor.Cyan);
            if (string.IsNullOrWhiteSpace(Config.SharedPalettePath))
                Log("No path for Shared Palette provided!", ConsoleColor.Red);
            else if (string.IsNullOrWhiteSpace(Config.LunarMagicPath))
                Log("No Lunar Magic Path provided!", ConsoleColor.Red);
            else if (!File.Exists(Config.LunarMagicPath))
                Log("Could not find Lunar Magic at the provided path!", ConsoleColor.Red);
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                ProcessStartInfo psi = new ProcessStartInfo(Config.LunarMagicPath,
                            $"-ExportSharedPalette \"{Config.OutputPath}\" \"{Config.SharedPalettePath}\"");
                var p = Process.Start(psi);
                p.WaitForExit();

                if (p.ExitCode == 0)
                    Log("Shared Palette Export Success!", ConsoleColor.Green);
                else
                {
                    Log("Shared Palette Export Failure!", ConsoleColor.Red);
                    return false;
                }
            }

            // export title moves
            Log("Exporting Title Moves...", ConsoleColor.Cyan);
            if (string.IsNullOrWhiteSpace(Config.TitleMovesPath))
                Log("No path for Title Moves provided!", ConsoleColor.Red);
            else if (string.IsNullOrWhiteSpace(Config.LunarMagicPath))
                Log("No Lunar Magic Path provided!", ConsoleColor.Red);
            else if (!File.Exists(Config.LunarMagicPath))
                Log("Could not find Lunar Magic at the provided path!", ConsoleColor.Red);
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                ProcessStartInfo psi = new ProcessStartInfo(Config.LunarMagicPath,
                            $"-ExportTitleMoves \"{Config.OutputPath}\" \"{Config.TitleMovesPath}\"");
                var p = Process.Start(psi);
                p.WaitForExit();

                if (p.ExitCode == 0)
                    Log("Title Moves Export Success!", ConsoleColor.Green);
                else
                {
                    Log("Title Moves Export Failure!", ConsoleColor.Red);
                    return false;
                }
            }

            // save levels
            if (!ExportLevels())
                return false;

            Console.WriteLine();
            return true;
        }

        static private bool ExportLevels()
        {
            Log("Exporting All Levels...", ConsoleColor.Cyan);
            if (string.IsNullOrWhiteSpace(Config.LevelsPath))
                Log("No path for Levels provided!", ConsoleColor.Red);
            else if (string.IsNullOrWhiteSpace(Config.LunarMagicPath))
                Log("No Lunar Magic Path provided!", ConsoleColor.Red);
            else if (!File.Exists(Config.LunarMagicPath))
                Log("Could not find Lunar Magic at the provided path!", ConsoleColor.Red);
            else if (!File.Exists(Config.OutputPath))
                Log("Output ROM does not exist! Build first!", ConsoleColor.Red);
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                ProcessStartInfo psi = new ProcessStartInfo(Config.LunarMagicPath,
                            $"-ExportMultLevels \"{Config.OutputPath}\" \"{Config.LevelsPath}{Path.DirectorySeparatorChar}level\"");
                var p = Process.Start(psi);
                p.WaitForExit();

                if (p.ExitCode == 0)
                {
                    Log("Levels Export Success!", ConsoleColor.Green);
                    return true;
                }
                else
                {
                    Log("Levels Export Failure!", ConsoleColor.Red);
                }
            }

            return false;
        }

        static public void Error(string error, ConsoleColor? background_color = null)
        {
            if (background_color != null)
                Console.BackgroundColor = (ConsoleColor)background_color;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"ERROR: {error}\n");
            Console.ResetColor();
        }

        static public void Log(string msg, ConsoleColor color = ConsoleColor.White, ConsoleColor? background_color = null)
        {
            if (background_color != null)
                Console.BackgroundColor = (ConsoleColor)background_color;
            Console.ForegroundColor = color;
            Console.WriteLine($"{msg}");
            Console.ResetColor();
        }

        static public void Lognl(string msg, ConsoleColor color = ConsoleColor.White, ConsoleColor? background_color = null)
        {
            if (background_color != null)
                Console.BackgroundColor = (ConsoleColor)background_color;
            Console.ForegroundColor = color;
            Console.Write($"{msg}");
            Console.ResetColor();
        }
    }
}
