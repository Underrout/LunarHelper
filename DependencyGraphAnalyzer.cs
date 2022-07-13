using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LunarHelper
{
    using static DependencyGraphSerializer;

    class DependencyGraphAnalyzer
    {
        public enum Result
        {
            // indicates that compared subgraphs were identical
            Identical,

            // indicates that no old root was found and thus the new root is very new indeed
            NewRoot,

            // indicates that (a) modified dependency/dependencies was/were found in the subgraph
            ModifiedDependencies,

            // indicates that a modified file was found in the subgraph
            ModifiedFile,

            // indicates that a file in the subgraph is missing
            Missing,

            // indicates that an arbitrary file is part of the subgraph
            Arbitrary
        }

        public static (Result, Vertex) Analyze(DependencyGraph new_graph, ToolRootVertex tool_root_vertex, List<JsonVertex> old_graph)
        {
            var old_root = old_graph.SingleOrDefault(v => v is JsonToolRootVertex && ((JsonToolRootVertex)v).tool == tool_root_vertex.type);

            if (old_root == null)
            {
                return (Result.NewRoot, tool_root_vertex);
            }

            return CompareSubgraphs(new_graph, tool_root_vertex, old_root, old_graph);
        }

        private static (Result, Vertex) CompareSubgraphs(DependencyGraph new_graph, Vertex current_new_vertex, JsonVertex current_old_vertex,
            List<JsonVertex> old_graph, HashSet<Vertex> seen = null)
        {
            if (seen == null)
            {
                seen = new HashSet<Vertex>();
            }

            if (seen.Contains(current_new_vertex))
            {
                return (Result.Identical, current_new_vertex);
            }

            seen.Add(current_new_vertex);

            if (current_new_vertex is MissingFileOrDirectoryVertex)
            {
                return (Result.Missing, current_new_vertex);
            }

            if (current_new_vertex is ArbitraryFileVertex)
            {
                return (Result.Arbitrary, current_new_vertex);
            }

            var new_hash = current_new_vertex is HashFileVertex ? ((HashFileVertex)current_new_vertex).hash : null;
            var old_hash = current_old_vertex is JsonHashVertex ? ((JsonHashVertex)current_old_vertex).hash : null;

            if (new_hash != old_hash)
            {
                return (Result.ModifiedFile, current_new_vertex);
            }

            if (current_new_vertex is HashFileNameVertex)
            {
                if (current_old_vertex is not JsonHashFileNameVertex)
                {
                    return (Result.ModifiedFile, current_new_vertex);
                }
                else if (((JsonHashFileNameVertex)current_old_vertex).file_name != ((HashFileNameVertex)current_new_vertex).file_name)
                {
                    return (Result.ModifiedFile, current_new_vertex);
                }
            }

            var new_dependencies = new_graph.dependency_graph.OutEdges(current_new_vertex)
                .Distinct()
                .OrderBy(e => e.Tag)
                .ThenBy(e => e.Target is HashFileNameVertex ? ((HashFileNameVertex)e.Target).file_name : "")
                .Select(e => (e.Source, e.Target is not GeneratedFileVertex ? e.Target : null, e.Tag));

            var old_dependencies = current_old_vertex.dependencies
                .Distinct()
                .OrderBy(d => d.tag)
                .ThenBy(d => d.idx >= 0 && old_graph[d.idx] is JsonHashFileNameVertex ? ((JsonHashFileNameVertex)old_graph[d.idx]).file_name : "")
                .Select(d => (current_old_vertex, d.idx >= 0 ? old_graph[d.idx] : null, d.tag));

            if (!AreIdentical(new_dependencies, old_dependencies))
            {
                return (Result.ModifiedDependencies, current_new_vertex);
            }

            // filtering out the generated vertices
            new_dependencies = new_dependencies.Where(t => t.Item2 != null);

            // filtering out generated, arbitrary and missing vertices
            old_dependencies = old_dependencies.Where(t => t.Item2 != null);

            foreach (var dependency in new_dependencies)
            {
                if (dependency.Item2 is ArbitraryFileVertex)
                {
                    return (Result.Arbitrary, dependency.Item2);
                }

                if (dependency.Item2 is MissingFileOrDirectoryVertex)
                {
                    return (Result.Missing, dependency.Item2);
                }
            }

            if (new_dependencies.Count() != old_dependencies.Count())
            {
                // if nothing fired in the foreach above, but we still ended up 
                // in here, it must mean that the old dependencies included 
                // arbitrary and/or missing vertices but the new ones don't
                return (Result.ModifiedDependencies, current_new_vertex);
            }

            foreach ((var new_dependency, var old_dependency) in new_dependencies.Zip(old_dependencies))
            {
                (var result, var vertex) = CompareSubgraphs(new_graph, new_dependency.Item2, old_dependency.Item2, old_graph);
                if (result != Result.Identical)
                {
                    return (result, vertex);
                }
            }

            return (Result.Identical, null);
        }

        private static bool AreIdentical(IEnumerable<(Vertex, Vertex, string)> new_dependencies, 
            IEnumerable<(JsonVertex, JsonVertex, string)> old_dependencies)
        {
            if (new_dependencies.Count() != old_dependencies.Count())
            {
                return false;
            }

            var new_cast = new_dependencies.Select(d => (d.Item2 is HashFileVertex ? ((HashFileVertex)d.Item2).hash : null, d.Item3));
            var old_cast = old_dependencies.Select(d => (d.Item2 is JsonHashVertex ? ((JsonHashVertex)d.Item2).hash : null, d.Item3));

            return new_cast.All(old_cast.Contains);
        }
    }
}
