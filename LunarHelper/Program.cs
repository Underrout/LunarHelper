using System;
using FluentArgs;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using System.Text;

using System.Linq;
using System.Reflection;

using AsarCLR;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace LunarHelper
{
    class Program
    {
        static public Config Config { get; private set; }

        static private int ROM_HEADER_SIZE = 0x200;

        static private int COMMENT_FIELD_SFC_ROM_OFFSET = 0x7F120;
        static private int COMMENT_FIELD_SMC_ROM_OFFSET = COMMENT_FIELD_SFC_ROM_OFFSET + ROM_HEADER_SIZE;
        static private int COMMENT_FIELD_LENGTH = 0x20;
        static private string DEFAULT_COMMENT = "I am Naaall, and I love fiiiish!";
        static private string ALTERED_COMMENT = "   Mario says     TRANS RIGHTS  ";

        static private int CHECKSUM_COMPLEMENT_OFFSET = 0x07FDC;
        static private int CHECKSUM_OFFSET = CHECKSUM_COMPLEMENT_OFFSET + 2;

        static private readonly Regex LevelRegex = new Regex("[0-9a-fA-F]{3}");
        static private Process EmulatorProcess = null;

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

        static private Dictionary<InsertableType, Func<Config, bool>> insertion_functions = new Dictionary<InsertableType, Func<Config, bool>>()
        {
            { InsertableType.Pixi, Importer.ApplyPixi },
            { InsertableType.AddMusicK, Importer.ApplyAddmusicK },
            { InsertableType.Gps, Importer.ApplyGPS },
            { InsertableType.UberAsm, Importer.ApplyUberASM },
            { InsertableType.Graphics, Importer.ImportGraphics },
            { InsertableType.ExGraphics, Importer.ImportExGraphics },
            { InsertableType.Map16, Importer.ImportMap16 },
            { InsertableType.SharedPalettes, Importer.ImportSharedPalettes },
            { InsertableType.GlobalData, Importer.ImportGlobalData },
            { InsertableType.TitleMoves, Importer.ImportTitleMoves },
            { InsertableType.Levels, Importer.ImportAllLevels }
        };

        static int Main(string[] args)
        {
            Directory.SetCurrentDirectory(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName));

            if (!Asar.init())
            {
                Error("Failed to initialize asar.dll!");
                return -1;
            }

            if (args.Length > 0)
                return HandleCommandLineInvocation(args);

            profile_manager = new ProfileManager();
            if (profile_manager.current_profile != null)
                curr_profile_color = profile_colors[ProfileManager.GetAllProfiles().ToList().IndexOf(profile_manager.current_profile)];

            bool running = true;
            while (running)
            {
                bool show_profiles = profile_manager.current_profile != null;

                Lognl("Welcome to Lunar Helper v", ConsoleColor.Cyan);
                Lognl(Assembly.GetExecutingAssembly().GetName().Version.ToString().Substring(0, 5), ConsoleColor.Cyan);
                Log(" ^_^", ConsoleColor.Cyan);

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
                        Asar.close();
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
                        var success = Init(true) && Build(volatile_pref);
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
                        var success = Init(true) && QuickBuild(volatile_pref);
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
                        var success = Init(true) && Build(volatile_pref) && Package();
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

        static private bool ExecuteInsertionPlan(List<Insertable> plan)
        {
            foreach (var insertable in plan)
            {
                bool result = false;

                switch (insertable.type)
                {
                    case InsertableType.SingleLevel:
                        result = Importer.ImportSingleLevel(Config, insertable.normalized_relative_path);
                        break;

                    case InsertableType.SinglePatch:
                        result = Importer.ApplyAsarPatch(Config, insertable.normalized_relative_path);
                        break;

                    default:
                        result = insertion_functions[insertable.type](Config);
                        break;
                }

                if (!result)
                    return false;
            }

            return true;
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

        static private bool QuickBuild(char volatile_resource_handling_preference = ' ')
        {
            var output_folder = Path.GetDirectoryName(Config.OutputPath);

            if (profile_manager.current_profile != null)
                profile_manager.WriteCurrentProfileToFile(output_folder);
            else
                profile_manager.DeleteCurrentProfileFile(output_folder);

            if (!HandleVolatileResources(Config.InvokedOnCommandLine, volatile_resource_handling_preference))
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

            // delete existing temp ROM
            if (File.Exists(Config.TempPath))
                File.Delete(Config.TempPath);

            // create temp ROM from potentially existing output ROM
            if (File.Exists(Config.OutputPath))
                File.Copy(Config.OutputPath, Config.TempPath);

            Log("Building dependency graph...\n", ConsoleColor.Cyan);
            dependency_graph = new DependencyGraph(Config);

            if (!string.IsNullOrWhiteSpace(Config.GlobulesPath))
                dependency_graph.ResolveGlobules();
            // dependency_graph.ResolveNonGlobules();  // these get resolved while planning the build now, it's a little sketchy

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
                return Build(volatile_resource_handling_preference, true);
            }
            catch (Exception e)
            {
                Log($"Encountered exception: '{e.Message}' while planning quick build, falling back to full rebuild...", ConsoleColor.Yellow);
                Log(e.StackTrace);
                return Build(volatile_resource_handling_preference, true);
            }
            
            if (plan.Count == 0)
            {
                // nothing to insert, we're up to date
                return true;
            }

            // Actually doing quick build below

            if (!string.IsNullOrWhiteSpace(Config.GlobulesPath))
            {
                Importer.WriteCallGlobuleMacroFile(Path.GetDirectoryName(Config.OutputPath));
            }

            // Lunar Monitor Loader required
            if (string.IsNullOrWhiteSpace(Config.LunarMonitorLoaderPath))
            {
                Log("No path to Lunar Monitor Loader provided!", ConsoleColor.Red);
                return false;
            }
            else if (!File.Exists(Config.LunarMonitorLoaderPath))
            {
                Log("Lunar Monitor Loader not found at provided path!", ConsoleColor.Red);
                return false;
            }

            if (!ExecuteInsertionPlan(plan))
                return false;

            MarkRomAsNonVolatile(Config.TempPath);

            if (!FinalizeOutputROM(Config.InvokedOnCommandLine))
            {
                return false;
            }

            Log("Writing build report...\n", ConsoleColor.Cyan);
            WriteReport(output_folder);

            Importer.FinalizeGlobuleImprints(output_folder);

            Log($"ROM '{Config.OutputPath}' successfully updated!", ConsoleColor.Green);
            Console.WriteLine();

            if (Config.ReloadEmulatorAfterBuild && EmulatorProcess != null && !EmulatorProcess.HasExited)
            {
                Log("Attempting to auto-relaunch your emulator...", ConsoleColor.Cyan);
                TryLaunchEmulator();
            }

            return true;
        }

        static private void WriteReport(string output_folder)
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

            Directory.CreateDirectory(Path.Combine(output_folder, ".lunar_helper"));
            File.WriteAllText(Path.Combine(output_folder,".lunar_helper/build_report.json"), sw.ToString());
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

            report.build_order_hash = Report.HashBuildOrder(Config.BuildOrder);

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
            report.lunar_magic = Report.HashFile(Config.LunarMonitorLoaderPath);
            report.human_readable_map16 = Report.HashFile(Config.HumanReadableMap16CLI);

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

        static private bool Init(bool invoked_on_command_line = false)
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

            Config.InvokedOnCommandLine = invoked_on_command_line;

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

            Importer.CreateStandardDefines(Path.GetDirectoryName(Path.GetFullPath(Config.OutputPath)));

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

        static void MarkRomAsNonVolatile(string rom_path)
        {
            if (!WriteAlteredCommentToRom(rom_path))
            {
                Log("WARNING: Failed to mark ROM as non-volatile, your ROM may be corrupted!\n", ConsoleColor.Red);
            }
            else
            {
                if (!UpdateChecksum(rom_path))
                {
                    Log("WARNING: Failed to update ROM's checksum, this should be a purely cosmetic issue " +
                        "that can be fixed by saving any level with Lunar Magic\n", ConsoleColor.Yellow);
                }
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

        static bool UpdateChecksum(string rom_path)
        {
            try
            {
                var bytes = File.ReadAllBytes(rom_path);

                var header_length = Config.OutputPath.EndsWith(".smc") ? ROM_HEADER_SIZE : 0;

                var complement_offset = CHECKSUM_COMPLEMENT_OFFSET + header_length;
                var checksum_offset = CHECKSUM_OFFSET + header_length;

                int sum = 0;
                int i = 0;
                foreach (var b in bytes)
                {
                    if (i == complement_offset || i == complement_offset + 1)
                        sum += 0xFF;
                    else if (i == checksum_offset || i == checksum_offset + 1)
                        sum += 0x00;
                    else
                        sum += b;

                    ++i;
                }

                var checksum = BitConverter.GetBytes(sum);
                var complement = BitConverter.GetBytes(sum ^ 0xFFFF);

                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(checksum);
                    Array.Reverse(complement);
                }

                using (Stream stream = File.Open(rom_path, FileMode.Open))
                {
                    stream.Position = complement_offset;

                    stream.Write(complement, 0, 2);
                    stream.Write(checksum, 0, 2);
                }

                return true;
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
                        if (Exporter.ExportAll(Config))
                        {
                            MarkRomAsNonVolatile(Config.OutputPath);  // save to mark as non-volatile since export succeeded
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
                        if (Exporter.ExportAll(Config))
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

        static private bool Build(char volatile_resource_handling_preference = ' ', bool skip_volatile_check = false)
        {
            var output_folder = Path.GetDirectoryName(Config.OutputPath);

            if (profile_manager.current_profile != null)
                profile_manager.WriteCurrentProfileToFile(output_folder);
            else
                profile_manager.DeleteCurrentProfileFile(output_folder);

            if (!skip_volatile_check && !HandleVolatileResources(Config.InvokedOnCommandLine, volatile_resource_handling_preference))
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

            // Lunar Monitor Loader required
            if (string.IsNullOrWhiteSpace(Config.LunarMonitorLoaderPath))
            {
                Log("No path to Lunar Monitor Loader provided!", ConsoleColor.Red);
                return false;
            }
            else if (!File.Exists(Config.LunarMonitorLoaderPath))
            {
                Log("Lunar Monitor Loader not found at provided path!", ConsoleColor.Red);
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
                if (Importer.ApplyBpsPatch(Config, fullCleanPath, fullTempPath, fullPatchPath))
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

            if (!string.IsNullOrWhiteSpace(Config.GlobulesPath))
            {
                var res = Importer.ApplyAllGlobules(output_folder, Config.TempPath, Config.GlobulesPath);
                if (!res)
                    return false;

                Importer.WriteCallGlobuleMacroFile(Path.GetDirectoryName(Config.OutputPath));
            }
            else
            {
                Importer.ClearGlobuleFolder(output_folder);
            }

            if (!ExecuteInsertionPlan(plan))
                return false;

            MarkRomAsNonVolatile(Config.TempPath);

            Log("Building dependency graph...\n", ConsoleColor.Cyan);
            dependency_graph = new DependencyGraph(Config);

            if (!string.IsNullOrWhiteSpace(Config.GlobulesPath))
            {
                dependency_graph.ResolveGlobules();
            }

            Importer.FinalizeGlobuleImprints(output_folder);

            dependency_graph.ResolveNonGlobules();

            if (!FinalizeOutputROM(Config.InvokedOnCommandLine))
            {
                return false;
            }

            Log("Writing build report...\n", ConsoleColor.Cyan);
            WriteReport(output_folder);

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
                    ProcessStartInfo psi = new ProcessStartInfo(Config.LunarMonitorLoaderPath,
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
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Log("No emulator specified, attempting to use Lunar Magic's emulator...", ConsoleColor.Yellow);

                    TryLaunchLunarMagicEmulator();

                    return;
                }

                Log("No emulator specified!", ConsoleColor.Red);
            }
        }

        static private void TryLaunchLunarMagicEmulator()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Log("No Lunar Magic emulator found", ConsoleColor.Red);
                return;
            }

            var emulator_path = (string)Registry.GetValue(
                @"HKEY_CURRENT_USER\SOFTWARE\LunarianConcepts\LunarMagic\Settings", "Emulator", null);

            if (string.IsNullOrEmpty(emulator_path))
            {
                Log("No Lunar Magic emulator found", ConsoleColor.Red);
                return;
            }

            var options = (string)Registry.GetValue(
                @"HKEY_CURRENT_USER\SOFTWARE\LunarianConcepts\LunarMagic\Settings", "EmulatorArg", "");

            options = options.Replace("%1", Path.GetFullPath(Config.OutputPath));

            Log("Launching Lunar Magic's emulator...", ConsoleColor.Yellow);

            if (EmulatorProcess != null && !EmulatorProcess.HasExited)
                EmulatorProcess.Kill(true);

            ProcessStartInfo psi = new ProcessStartInfo(emulator_path, options);

            EmulatorProcess = Process.Start(psi);

            if (EmulatorProcess != null)
            {
                Log("Lunar Magic emulator launched!", ConsoleColor.Green);
            }
            else
            {
                Error("Lunar Magic emulator launch failed");
            }
        }

        static private bool FinalizeOutputROM(bool on_cli)
        {
            // rename temp rom and generated files to final build output
            var path = Path.GetDirectoryName(Path.GetFullPath(Config.TempPath));
            var temp_name = Path.GetFileNameWithoutExtension(Config.TempPath);
            var to = Path.GetDirectoryName(Path.GetFullPath(Config.OutputPath));
            to = Path.Combine(to, Path.GetFileNameWithoutExtension(Config.OutputPath));

            foreach (var file in Directory.EnumerateFiles(path, temp_name + "*"))
            {
                while (true)
                {
                    var out_file = $"{to}{Path.GetExtension(file)}";
                    try
                    {
                        File.Move(file, out_file, true);
                        break;
                    }
                    catch (Exception e) when (e is IOException || e is SystemException)
                    {
                        if (on_cli)
                        {
                            Error($"Failed to rename file '{file}' to '{out_file}':\n{e.StackTrace}");
                            return false;
                        }

                        Log($"Failed to rename file '{file}' to '{out_file}', retry? Y/N");

                        var response = Console.ReadLine();

                        if (response.ToLower() == "n")
                        {
                            Error($"Failed to rename file '{file}' to '{out_file}':\n{e.StackTrace}");
                            return false;
                        }
                    }
                }
            }

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
            else if (string.IsNullOrWhiteSpace(Config.LunarMonitorLoaderPath))
                Log("No path to Lunar Monitor Loader provided, cannot open built ROM.", ConsoleColor.Red);
            else if (!File.Exists(Config.LunarMonitorLoaderPath))
                Log("Lunar Monitor Loader not found at provided path, cannot open built ROM.", ConsoleColor.Red);
            else
            {
                ProcessStartInfo psi = new ProcessStartInfo(Config.LunarMonitorLoaderPath,
                            $"\"{Path.GetFileName(Config.OutputPath)}\"");

                psi.WorkingDirectory = Path.GetDirectoryName(Config.OutputPath);

                Process.Start(psi);
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

                if (Exporter.CreatePatch(Config, fullCleanPath, fullOutputPath, fullPackagePath))
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
            Log("Attempts to reuse a previously built ROM for the build process if it is available.\n" +
                "Automatically determines if a full rebuild is " +
                "needed or if only certain tools need to be reapplied or resources need to be reinserted.\n");

            Log("R - Run", ConsoleColor.Yellow);
            Log("Loads the previously-built ROM into the configured emulator for testing. The ROM must already be built first.\n" +
                "If no emulator was configured, an attempt will be made to retrieve and use the emulator configured in Lunar Magic.\n");

            Log("T - Test (Quick Build -> Run)", ConsoleColor.Yellow);
            Log("Executes the above two commands in sequence.\n");

            Log("E - Edit (in Lunar Magic)", ConsoleColor.Yellow);
            Log("Opens the previously-built ROM in Lunar Magic. The ROM must already be built first.\n");

            Log("P - Package", ConsoleColor.Yellow);
            Log("Creates a BPS patch for your ROM against the configured clean SMW ROM, so that you can share it!\n");
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
