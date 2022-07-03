using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.IO;

using QuickGraph.Algorithms;
using QuickGraph;

using SMWPatcher;

namespace LunarHelper
{
    class DependencyGraph
    {
        private class Vertex
        {
            public string absolute_file_path;
            public string hash;

            public Vertex(string absolute_file_path)
            {
                this.absolute_file_path = absolute_file_path;
                hash = Report.HashFile(absolute_file_path);
            }
        }

        private AdjacencyGraph<Vertex, SEdge<Vertex>> dependency_graph;
        private Dictionary<string, Vertex> file_vertex_dict = new Dictionary<string, Vertex>();

        private HashSet<Vertex> pixi_sources;
        private HashSet<Vertex> uberasm_sources;
        private HashSet<Vertex> gps_sources;
        private HashSet<Vertex> amk_sources;
        private HashSet<Vertex> patch_sources;

        public DependencyGraph(ICollection<string> pixi_sources, ICollection<string> uberasm_sources, ICollection<string> gps_sources,
            ICollection<string> amk_sources, ICollection<string> patch_list_sources, ICollection<string> patch_folder_sources)
        {
            dependency_graph = new AdjacencyGraph<Vertex, SEdge<Vertex>>();

            // this.pixi_sources = CreateSourceSet(pixi_sources);
            // this.uberasm_sources = CreateSourceSet(uberasm_sources);
            // this.gps_sources = CreateSourceSet(gps_sources);
            // this.amk_sources = CreateSourceSet(amk_sources);
            this.patch_sources = CreateSourceSet(patch_list_sources);


        }

        private void AddPatchListVertexDependencies(Vertex vertex)
        {
            HashSet<Vertex> visited = new HashSet<Vertex>();

            foreach (var dependency in DependencyResolver.GetAsarFileDependencies(vertex.absolute_file_path))
            {
                visited.Add(vertex);
                Vertex dependency_vertex = GetOrCreateVertex(dependency.absolute_dependency_path);
                dependency_graph.AddEdge(new SEdge<Vertex>(vertex, dependency_vertex));
                if (!visited.Contains(dependency_vertex) && !dependency.is_deadend)
                {
                    AddPatchListVertexDependencies(dependency_vertex);
                }
            }
        }

        private HashSet<Vertex> CreateSourceSet(ICollection<string> sources)
        {
            HashSet<Vertex> source_set = new HashSet<Vertex>();
            foreach (string source in sources)
            {
                source_set.Add(GetOrCreateVertex(source));
            }
            return source_set;
        }

        private Vertex GetOrCreateVertex(string absolute_file_path)
        {
            Vertex existing_vertex;
            file_vertex_dict.TryGetValue(absolute_file_path, out existing_vertex);

            if (existing_vertex != null)
            {
                return existing_vertex;
            }
            
            Vertex vertex = new Vertex(absolute_file_path);
            dependency_graph.AddVertex(vertex);
            file_vertex_dict.Add(vertex.absolute_file_path, vertex);
            return vertex;
        }
    }
}
