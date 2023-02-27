using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Profiling;

public class EdgeCalcTester : MonoBehaviour
{
    private void Update()
    {
        var mf = GetComponent<MeshFilter>();
        var mesh = mf.sharedMesh;
        var controlResult = ComputeEdges_Control(mesh);
        var optimizedResult = ComputeEdges_Optimized(mesh);
        var optimized2Result = ComputeEdges_Optimized2(mesh);
        // var jobbedResult = ComputeEdges_Jobbed(mesh);
    }

    private int[] ComputeEdges_Control(Mesh mesh)
    {
        Profiler.BeginSample("EdgeCalcTester.ComputeEdges_Control");
        
        Profiler.BeginSample("EdgeCalcTester.ComputeEdges_Control/GetMeshData");
        var tris = mesh.triangles;
        var vertexCount = mesh.vertexCount;
        Profiler.EndSample();
        
        
        Profiler.BeginSample("EdgeCalcTester.ComputeEdges_Control/InitializeHashSet");
        HashSet<int>[] edges = new HashSet<int>[vertexCount];
        Profiler.EndSample();
        
        Profiler.BeginSample("EdgeCalcTester.ComputeEdges_Control/Loop1");
        int edgeCount = 0;
        for (var i = 0; i < tris.Length; i += 3)
        {
            Profiler.BeginSample("EdgeCalcTester.ComputeEdges_Control/Loop1/GetABC");
            var a = tris[i];
            var b = tris[i + 1];
            var c = tris[i + 2];
            Profiler.EndSample();
            
            Profiler.BeginSample("EdgeCalcTester.ComputeEdges_Control/Loop1/InitializeHashSets");
            if (edges[a] == null)
                edges[a] = new HashSet<int>(8);
            if (edges[b] == null)
                edges[b] = new HashSet<int>(8);
            if (edges[c] == null)
                edges[c] = new HashSet<int>(8);
            Profiler.EndSample();

            Profiler.BeginSample("EdgeCalcTester.ComputeEdges_Control/Loop1/AddEdges");
            if (edges[a].Add(b))
                edgeCount++;
            if (edges[a].Add(c))
                edgeCount++;
            if (edges[b].Add(a))
                edgeCount++;
            if (edges[b].Add(c))
                edgeCount++;
            if (edges[c].Add(a))
                edgeCount++;
            if (edges[c].Add(b))
                edgeCount++;
            Profiler.EndSample();
        }
        Profiler.EndSample();

        Profiler.BeginSample("EdgeCalcTester.ComputeEdges_Control/InitializeEdgeData");
        int[] edgeData = new int[vertexCount + edgeCount];
        Profiler.EndSample();
        
        Profiler.BeginSample("EdgeCalcTester.ComputeEdges_Control/Loop2");
        int iterator = 0;
        int maxLength = 0;
        for (var i = 0; i < vertexCount; i++)
        {
            var edge = edges[i];
            if (edge == null) continue;

            var edgeArray = edge.ToArray();
            for (var j = 0; j < edgeArray.Length; j++)
            {
                edgeData[iterator + vertexCount + j] = edgeArray[j];
            }

            var length = edgeArray.Length;
            edgeData[i] = iterator += length;

            if (length > maxLength) maxLength = length;
        }
        Profiler.EndSample();
        
        Profiler.EndSample();
        return edgeData;
    }

    private static int SubsetGet(ref NativeArray<int> data, int stride, int index, int subIndex)
    {
        return data[index * stride + subIndex];
    }
    
    private static bool SubsetContains(ref NativeArray<int> data, ref NativeArray<int> dataCounts, int index, int stride, int value)
    {
        Profiler.BeginSample("SubsetContains");
        
        var start = index * stride;
        var end = start + dataCounts[index];
        for (int i = start; i < end; i++)
        {
            if (data[i] == value)
            {
                Profiler.EndSample();
                return true;
            }
        }
        Profiler.EndSample();
        return false;
    }

    private static bool SubsetSetAdd(ref NativeArray<int> data, ref NativeArray<int> dataCounts, int index, int stride, int value)
    {
        Profiler.BeginSample("SubsetSetAdd");
        
        Profiler.BeginSample("SubsetSetAdd/ErrorCheck");
        if (dataCounts[index] >= stride)
        {
            Profiler.EndSample();
            Profiler.EndSample();
            throw new Exception("Added more to subset than stride");
        }
        Profiler.EndSample();


        if (SubsetContains(ref data, ref dataCounts, index, stride, value))
        {
            Profiler.EndSample();
            return false;
        }

        Profiler.BeginSample("SubsetSetAdd/Add");
        int i = index * stride + dataCounts[index];
        data[i] = value;
        dataCounts[index]++;
        Profiler.EndSample();
        
        Profiler.EndSample();
        return true;
    }

    private int[] ComputeEdges_Optimized(Mesh mesh)
    {
        const int MAX_EDGES = 16;
        Profiler.BeginSample("EdgeCalcTester.ComputeEdges_Optimized");
        
        Profiler.BeginSample("EdgeCalcTester.ComputeEdges_Optimized/GetMeshData");
        var tris = mesh.triangles;
        var vertexCount = mesh.vertexCount;
        Profiler.EndSample();
        
        
        Profiler.BeginSample("EdgeCalcTester.ComputeEdges_Optimized/InitializeHashSet");
        NativeArray<int> edges = new NativeArray<int>(vertexCount * MAX_EDGES, Allocator.Temp);
        NativeArray<int> edgeCounts = new NativeArray<int>(vertexCount, Allocator.Temp);
        Profiler.EndSample();
        
        Profiler.BeginSample("EdgeCalcTester.ComputeEdges_Optimized/Loop1");
        int edgeCount = 0;
        for (var i = 0; i < tris.Length; i += 3)
        {
            Profiler.BeginSample("EdgeCalcTester.ComputeEdges_Optimized/Loop1/GetABC");
            var a = tris[i];
            var b = tris[i + 1];
            var c = tris[i + 2];
            Profiler.EndSample();

            Profiler.BeginSample("EdgeCalcTester.ComputeEdges_Optimized/Loop1/AddEdges");
            if (SubsetSetAdd(ref edges, ref edgeCounts, a, MAX_EDGES, b)) edgeCount++;
            if (SubsetSetAdd(ref edges, ref edgeCounts, a, MAX_EDGES, c)) edgeCount++;
            if (SubsetSetAdd(ref edges, ref edgeCounts, b, MAX_EDGES, a)) edgeCount++;
            if (SubsetSetAdd(ref edges, ref edgeCounts, b, MAX_EDGES, c)) edgeCount++;
            if (SubsetSetAdd(ref edges, ref edgeCounts, c, MAX_EDGES, a)) edgeCount++;
            if (SubsetSetAdd(ref edges, ref edgeCounts, c, MAX_EDGES, b)) edgeCount++;
            Profiler.EndSample();
        }
        Profiler.EndSample();

        Profiler.BeginSample("EdgeCalcTester.ComputeEdges_Optimized/InitializeEdgeData");
        int[] edgeData = new int[vertexCount + edgeCount];
        Profiler.EndSample();
        
        Profiler.BeginSample("EdgeCalcTester.ComputeEdges_Optimized/Loop2");
        int iterator = 0;
        for (var i = 0; i < vertexCount; i++)
        {
            var vertexEdgeCount = edgeCounts[i];
            for (var j = 0; j < vertexEdgeCount; j++)
            {
                edgeData[iterator + vertexCount + j] = SubsetGet(ref edges, MAX_EDGES, i, j);
            }
            edgeData[i] = iterator += edgeCounts[i];
        }
        Profiler.EndSample();

        Profiler.BeginSample("EdgeCalcTester.ComputeEdges_Optimized/Cleanup");
        edges.Dispose();
        edgeCounts.Dispose();
        Profiler.EndSample();
        
        Profiler.EndSample();
        return edgeData;
    }
    
    private class EdgeComputer
    {
        public NativeArray<int> edges;
        public NativeArray<int> edgeCounts;

        public int[] tris;
        public int vertexCount;

        public readonly int stride;
        public int totalEdgeCount = 0;
        
        public EdgeComputer(Mesh mesh, int MAX_EDGES=16)
        {
            Profiler.BeginSample("EdgeComputer.Constructor");
            
            stride = MAX_EDGES;
            
            Profiler.BeginSample("EdgeComputer.Constructor/GetMeshData");
            tris = mesh.triangles;
            vertexCount = mesh.vertexCount;
            Profiler.EndSample();
            
            Profiler.BeginSample("EdgeComputer.Constructor/InitializeNativeArrays");
            edges = new NativeArray<int>(vertexCount * MAX_EDGES, Allocator.Temp);
            edgeCounts = new NativeArray<int>(vertexCount, Allocator.Temp);
            Profiler.EndSample();
            
            Profiler.EndSample();
        }

        public void Dispose()
        {
            if (edges != null) edges.Dispose();
            if (edgeCounts != null) edgeCounts.Dispose();
        }

        public void Execute()
        {
            for (var i = 0; i < tris.Length; i += 3)
            {
                Profiler.BeginSample("EdgeComputer.Execute/GetABC");
                var a = tris[i];
                var b = tris[i + 1];
                var c = tris[i + 2];
                Profiler.EndSample();

                Profiler.BeginSample("EdgeComputer.Execute/AddEdges");
                if (SubsetSetAdd(a, b)) totalEdgeCount++;
                if (SubsetSetAdd(a, c)) totalEdgeCount++;
                if (SubsetSetAdd(b, a)) totalEdgeCount++;
                if (SubsetSetAdd(b, c)) totalEdgeCount++;
                if (SubsetSetAdd(c, a)) totalEdgeCount++;
                if (SubsetSetAdd(c, b)) totalEdgeCount++;
                Profiler.EndSample();
            }
        }

        public int[] GetEdges()
        {
            Profiler.BeginSample("EdgeComputer.GetEdges");
            
            Profiler.BeginSample("EdgeComputer.GetEdges/InitializeEdgeData");
            int[] edgeData = new int[vertexCount + totalEdgeCount];
            Profiler.EndSample();
            
            Profiler.BeginSample("EdgeComputer.GetEdges/Loop");
            int iterator = 0;
            for (var i = 0; i < vertexCount; i++)
            {
                var vertexEdgeCount = edgeCounts[i];
                for (var j = 0; j < vertexEdgeCount; j++)
                {
                    edgeData[iterator + vertexCount + j] = SubsetGet(i, j);
                }
                edgeData[i] = iterator += edgeCounts[i];
            }
            Profiler.EndSample();
            
            Profiler.EndSample();
            return edgeData;
        }
        
        private int SubsetGet(int index, int subIndex)
        {
            return edges[index * stride + subIndex];
        }
        
        private bool SubsetContains(int index, int value)
        {
            Profiler.BeginSample("SubsetContains");

            for (int i = 0; i < stride; i++)
            {
                if (SubsetGet(index, i) == value)
                {
                    Profiler.EndSample();
                    return true;
                }
            }
            
            Profiler.EndSample();
            return false;
        }

        private bool SubsetSetAdd(int index, int value)
        {
            Profiler.BeginSample("SubsetSetAdd");
            
            Profiler.BeginSample("SubsetSetAdd/ErrorCheck");
            if (edgeCounts[index] >= stride)
            {
                Profiler.EndSample();
                Profiler.EndSample();
                throw new Exception("Added more to subset than stride");
            }
            Profiler.EndSample();


            if (SubsetContains(index, value))
            {
                Profiler.EndSample();
                return false;
            }

            Profiler.BeginSample("SubsetSetAdd/Add");
            int i = index * stride + edgeCounts[index];
            edges[i] = value;
            edgeCounts[index]++;
            Profiler.EndSample();
            
            Profiler.EndSample();
            return true;
        }
    }
    
    private int[] ComputeEdges_Optimized2(Mesh mesh)
    {
        Profiler.BeginSample("EdgeCalcTester.ComputeEdges_Optimized2");
        
        Profiler.BeginSample("EdgeCalcTester.ComputeEdges_Optimized2/InitializeComputer");
        var computer = new EdgeComputer(mesh, 16);
        Profiler.EndSample();
        
        Profiler.BeginSample("EdgeCalcTester.ComputeEdges_Optimized2/Execute");
        computer.Execute();
        Profiler.EndSample();

        Profiler.BeginSample("EdgeCalcTester.ComputeEdges_Optimized2/GetEdges");
        int[] edgeData = computer.GetEdges();
        Profiler.EndSample();

        Profiler.BeginSample("EdgeCalcTester.ComputeEdges_Optimized2/Cleanup");
        computer.Dispose();
        Profiler.EndSample();
        
        Profiler.EndSample();
        return edgeData;
    }
}
