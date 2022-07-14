using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;

namespace LunarHelper
{
    class Report
    {
        [JsonIgnore]
        public const int REPORT_FORMAT_VERSION = 2;
        public int report_format_version { get; set; }
        public DateTimeOffset build_time { get; set; }
        public string rom_hash { get; set; }
        public Dictionary<string, string> levels { get; set; }
        public List<DependencyGraphSerializer.JsonVertex> dependency_graph { get; set; }
        public string graphics { get; set; }
        public string exgraphics { get; set; }
        public string init_bps { get; set; }
        public string global_data { get; set; }
        public string title_moves { get; set; }
        public string shared_palettes { get; set; }
        public string map16 { get; set; }
        public string gps_options { get; set; }
        public string pixi_options { get; set; }
        public string uberasm_options { get; set; }
        public string addmusick_options { get; set; }
        public string flips { get; set; }
        public string lunar_magic { get; set; }
        public string lunar_magic_level_import_flags { get; set; }
        public string asar_options { get; set; }
        public string human_readable_map16 { get; set; }

        public static string HashFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(path))
                {
                    return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLower();
                }
            }
        }

        // SOURCE START
        // Source: Stack Overflow
        // Original question: https://stackoverflow.com/q/3625658/6875882
        // Question asked by: Igor Pistolyaka (https://stackoverflow.com/users/243319/igor-pistolyaka)
        // Code taken from answer: https://stackoverflow.com/a/15683147/6875882
        // Author of answer: Dunc (https://stackoverflow.com/users/188926/dunc)
        public static string HashFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return null;

            var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
                     .OrderBy(p => p).ToList();

            MD5 md5 = MD5.Create();

            for (int i = 0; i < files.Count; i++)
            {
                string file = files[i];

                // hash path
                string relativePath = file.Substring(path.Length + 1);
                byte[] pathBytes = Encoding.UTF8.GetBytes(relativePath.ToLower());
                md5.TransformBlock(pathBytes, 0, pathBytes.Length, pathBytes, 0);

                // hash contents
                byte[] contentBytes = File.ReadAllBytes(file);
                if (i == files.Count - 1)
                    md5.TransformFinalBlock(contentBytes, 0, contentBytes.Length);
                else
                    md5.TransformBlock(contentBytes, 0, contentBytes.Length, contentBytes, 0);
            }

            return BitConverter.ToString(md5.Hash).Replace("-", "").ToLower();
        }
        // SOURCE END
    }
}
