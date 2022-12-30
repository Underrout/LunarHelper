using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;

namespace LunarHelper.Resolvers
{
    class PatchResolver : IResolve<PatchRootVertex>
    {
        private AsarResolver asar_resolver;
        private HashSet<Vertex> seen = new HashSet<Vertex>();
        private readonly DependencyGraph graph;
        private readonly Vertex asar_dll_vertex;

        public PatchResolver(DependencyGraph graph, string asar_dll_path)
        {
            this.graph = graph;
            this.asar_dll_vertex = graph.GetOrCreateVertex(asar_dll_path);

            asar_resolver = new AsarResolver(graph, seen, asar_dll_path);
        }

        public void ResolveDependencies(PatchRootVertex vertex)
        {
            asar_resolver.ResolveDependencies(vertex);
            graph.TryAddUniqueEdge(vertex, asar_dll_vertex, "asar_dll");
        }
    }
}
