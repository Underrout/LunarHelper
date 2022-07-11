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

        public PatchResolver(DependencyGraph graph, string asar_exe_path, string asar_options)
        {
            this.graph = graph;

            asar_resolver = new AsarResolver(graph, seen, asar_exe_path, asar_options);
        }

        public void ResolveDependencies(PatchRootVertex vertex)
        {
            asar_resolver.ResolveDependencies(vertex);

            if (asar_resolver.stddefines_vertex != null)
            {
                // Connect patch root to asar stddefines vertex if it exists
                graph.TryAddUniqueEdge(vertex, asar_resolver.stddefines_vertex, "stddefines");
            }
        }
    }
}
