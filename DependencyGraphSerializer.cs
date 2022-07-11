using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Specialized;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace LunarHelper
{
    class DependencyGraphSerializer
    {
        public const int missing_file_idx = -1;
        public const int arbitrary_file_idx = -2;
        public const int generated_file_idx = -3;

        public class JsonDependency
        {
            public string tag { get; set; }
            public int idx { get; set; }
        }

        public class JsonVertex
        {
            public List<JsonDependency> dependencies { get; set; } = new List<JsonDependency>();
        }

        public class JsonToolRootVertex : JsonVertex
        {
            public ToolRootVertex.Tool tool { get; set; }
        }

        public class JsonPatchRootVertex : JsonHashVertex
        {
            public string path { get; set; }
        }

        public class JsonHashVertex : JsonVertex
        {
            public string hash { get; set; }
        }

        public static IEnumerable<JsonVertex> SerializeGraph(DependencyGraph graph)
        {
            OrderedDictionary vertices = new OrderedDictionary();

            var g = graph.dependency_graph;

            foreach (Vertex sink in g.Vertices.Where(v => g.OutDegree(v) == 0))
            {
                RecursivelySerialize(graph, sink, vertices);
            }

            return vertices.Values.Cast<JsonVertex>();
        }

        private static void RecursivelySerialize(DependencyGraph graph, Vertex vertex, OrderedDictionary vertices)
        {
            if (vertices.Contains(vertex))
            {
                return;
            }

            int idx = ResolveVertex(vertex, vertices);

            foreach (var edge in graph.dependency_graph.InEdges(vertex))
            {
                Vertex dependent = edge.Source;

                RecursivelySerialize(graph, dependent, vertices);

                JsonDependency dependency = new JsonDependency { idx = idx, tag = edge.Tag };

                ((JsonVertex)vertices[dependent]).dependencies.Add(dependency);
            }
        }

        private static int ResolveVertex(Vertex vertex, OrderedDictionary vertices)
        {
            var type = vertex.GetType();
            int idx;

            if (type == typeof(HashFileVertex))
            {
                HashFileVertex hash_vertex = (HashFileVertex)vertex;
                JsonHashVertex json_hash_vertex = new JsonHashVertex { hash = hash_vertex.hash };
                idx = vertices.Count;

                vertices.Add(vertex, json_hash_vertex);
            }
            else if (type == typeof(MissingFileOrDirectoryVertex))
            {
                idx = missing_file_idx;
            }
            else if (type == typeof(ArbitraryFileVertex))
            {
                idx = arbitrary_file_idx;
            }
            else if (type == typeof(GeneratedFileVertex))
            {
                idx = generated_file_idx;
            }
            else if (type == typeof(PatchRootVertex))
            {
                PatchRootVertex patch_root_vertex = (PatchRootVertex)vertex;
                JsonPatchRootVertex json_patch_root = new JsonPatchRootVertex
                {
                    hash = patch_root_vertex.hash,
                    path = patch_root_vertex.normalized_relative_patch_path
                };

                idx = vertices.Count;

                vertices.Add(vertex, json_patch_root);
            }
            else if (type == typeof(ToolRootVertex))
            {
                ToolRootVertex tool_root_vertex = (ToolRootVertex)vertex;
                JsonToolRootVertex json_tool_root = new JsonToolRootVertex
                {
                    tool = tool_root_vertex.type
                };

                idx = vertices.Count;

                vertices.Add(vertex, json_tool_root);
            }
            else
            {
                throw new Exception("Attempt to serialize unhandled vertex type");
            }

            return idx;
        }
    }
}
