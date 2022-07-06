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

        public PatchResolver(DependencyGraph graph, string asar_exe_path, string asar_options)
        {
            asar_resolver = new AsarResolver(graph, asar_exe_path, asar_options);
        }

        public void ResolveDependencies(PatchRootVertex vertex)
        {
            // TODO if our asar resolver found an stddefines file,
            // create/get a vertex for it here and connect the root patch 
            // vertex to it

            asar_resolver.ResolveDependencies(vertex);
        }
    }
}
