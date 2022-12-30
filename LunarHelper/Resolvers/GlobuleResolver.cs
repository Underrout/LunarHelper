using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LunarHelper.Resolvers
{
    class GlobuleResolver : IResolve<GlobuleRootVertex>
    {
        private AsarResolver asar_resolver;
        private HashSet<Vertex> seen = new HashSet<Vertex>();
        private readonly DependencyGraph graph;
        private readonly Vertex asar_dll_vertex;
        private readonly string globule_folder;

        public GlobuleResolver(DependencyGraph graph, string asar_dll_path, string globule_folder)
        {
            this.graph = graph;
            this.asar_dll_vertex = graph.GetOrCreateVertex(asar_dll_path);
            this.globule_folder = globule_folder;

            asar_resolver = new AsarResolver(graph, seen, asar_dll_path);
        }

        public void ResolveDependencies(GlobuleRootVertex vertex)
        {
            using (StreamReader sr = new StreamReader(vertex.uri.LocalPath))
            {
                string line;
                int imported = 0;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.StartsWith(";LH>"))
                    {
                        if (line.Substring(4).StartsWith(" import "))
                        {
                            var imports = line.Substring(12).Split(',').Select(import => import.Replace('"', ' ').Trim());

                            foreach (var import in imports)
                            {
                                var full_import_path = Path.Join(globule_folder, import);
                                if (!File.Exists(full_import_path))
                                {
                                    throw new GlobuleException($"Attempt to import '{full_import_path}' from '{vertex.uri.LocalPath}', but file was not found");
                                }
                                else if (import.ToLower() == Path.GetFileName(vertex.uri.LocalPath))
                                {
                                    throw new GlobuleException($"Attempt to import '{vertex.uri.LocalPath}' into itself");
                                }
                                else
                                {
                                    var imported_vertex = graph.GetOrCreateFileNameVertex(full_import_path);
                                    graph.AddEdge(vertex, imported_vertex, $"import_{imported++}");
                                }
                            }
                        }
                        else
                        {
                            throw new GlobuleException($"Malformed ;LH> command: '{line}' in '{vertex.uri.LocalPath}'");
                        }
                    }
                }
            }

            asar_resolver.ResolveDependencies(vertex);
            graph.TryAddUniqueEdge(vertex, asar_dll_vertex, "asar_dll");
        }
    }
}
