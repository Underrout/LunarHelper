using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LunarHelper
{
    public class Config
    {
        public string WorkingDirectory;

        public string OutputPath;
        public string TempPath;
        public string CleanPath;
        public string PackagePath;

        public string AsarPath;
        public string AsarOptions;

        public string UberASMPath;
        public string UberASMOptions;

        public string GPSPath;
        public string GPSOptions;

        public string PixiPath;
        public string PixiOptions;

        public string AddMusicKPath;
        public string AddmusicKOptions;

        public string LunarMagicPath;
        public string LunarMagicLevelImportFlags;

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

        #region load

        static public Config Load(out string error)
        {
            error = "";

            try
            {
                var data = new List<string>();
                foreach (var file in Directory.EnumerateFiles(Directory.GetCurrentDirectory(), "config*.txt", SearchOption.TopDirectoryOnly))
                    data.Add(File.ReadAllText(file));
                return Load(data);
            }
            catch (Exception e)
            {
                error = e.Message;
                return null;
            }
        }

        static private Config Load(List<string> data)
        {
            Config config = new Config();

            HashSet<string> flags = new HashSet<string>();
            Dictionary<string, string> vars = new Dictionary<string, string>();
            Dictionary<string, List<string>> lists = new Dictionary<string, List<string>>();
            foreach (var d in data)
                Parse(d, flags, vars, lists);

            vars.TryGetValue("dir", out config.WorkingDirectory);
            vars.TryGetValue("output", out config.OutputPath);
            vars.TryGetValue("temp", out config.TempPath);
            vars.TryGetValue("clean", out config.CleanPath);
            vars.TryGetValue("package", out config.PackagePath);
            vars.TryGetValue("asar_path", out config.AsarPath);
            vars.TryGetValue("asar_options", out config.AsarOptions);
            vars.TryGetValue("uberasm_path", out config.UberASMPath);
            vars.TryGetValue("uberasm_options", out config.UberASMOptions);
            vars.TryGetValue("gps_path", out config.GPSPath);
            vars.TryGetValue("gps_options", out config.GPSOptions);
            vars.TryGetValue("pixi_path", out config.PixiPath);
            vars.TryGetValue("pixi_options", out config.PixiOptions);
            vars.TryGetValue("addmusick_path", out config.AddMusicKPath);
            vars.TryGetValue("addmusick_options", out config.AddmusicKOptions);
            vars.TryGetValue("lm_path", out config.LunarMagicPath);
            vars.TryGetValue("lm_level_import_flags", out config.LunarMagicLevelImportFlags);
            vars.TryGetValue("flips_path", out config.FlipsPath);
            vars.TryGetValue("human_readable_map16_cli_path", out config.HumanReadableMap16CLI);
            vars.TryGetValue("human_readable_map16_directory_path", out config.HumanReadableMap16Directory);
            vars.TryGetValue("levels", out config.LevelsPath);
            vars.TryGetValue("map16", out config.Map16Path);
            vars.TryGetValue("shared_palette", out config.SharedPalettePath);
            vars.TryGetValue("global_data", out config.GlobalDataPath);
            vars.TryGetValue("title_moves", out config.TitleMovesPath);
            vars.TryGetValue("initial_patch", out config.InitialPatch);
            lists.TryGetValue("patches", out config.Patches);

            vars.TryGetValue("test_level", out config.TestLevel);
            vars.TryGetValue("test_level_dest", out config.TestLevelDest);
            vars.TryGetValue("emulator_path", out config.EmulatorPath);
            vars.TryGetValue("emulator_options", out config.EmulatorOptions);

            return config;
        }

        static private void Parse(string data, HashSet<string> flags, Dictionary<string, string> vars, Dictionary<string, List<string>> lists)
        {
            var lines = data.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string str = lines[i];
                string peek = null;
                if (i < lines.Length - 1)
                    peek = lines[i + 1];

                if (str.StartsWith("--"))
                {
                    // comment
                }
                else if (str.Contains('='))
                {
                    // var
                    var sp = str.Split('=');
                    if (sp.Length != 2)
                        throw new Exception("Malformed assignment");

                    var key = sp[0].Trim();
                    if (vars.ContainsKey(key))
                        throw new Exception($"Duplicate config key: '{key}'");
                    vars.Add(key, sp[1].Trim());
                }
                else if (peek != null && peek.Trim() == "[")
                {
                    // list
                    var list = new List<string>();
                    lists.Add(str.Trim(), list);
                    i += 2;

                    while (true)
                    {
                        if (i >= lines.Length)
                            throw new Exception("Malformed list");

                        str = lines[i];
                        if (str.Trim() == "]")
                            break;
                        else
                            list.Add(str.Trim().Replace("\\", "/"));

                        i++;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(str))
                {
                    // flag
                    flags.Add(str.Trim());
                }
            }
        }

        #endregion
    }
}
