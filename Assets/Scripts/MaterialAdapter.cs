using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
[RequireComponent(typeof(SkinnedMeshRenderer))]
public class MaterialAdapter : MonoBehaviour
{
    [SerializeField, NonReorderable] BlendShapeDescriptor[] blendShapeDescriptors;

    [ContextMenu("Setup")]
    void SetUp()
    {
        var skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();
        if (skinnedMeshRenderer == null)
            return;

        var mesh = skinnedMeshRenderer.sharedMesh;
        if (mesh == null)
            return;

        blendShapeDescriptors = new BlendShapeDescriptor[mesh.blendShapeCount];

        for(var i = 0; i < mesh.blendShapeCount; i++)
        {
            mesh.GetBlendShapeName(i);
            blendShapeDescriptors[i] = new BlendShapeDescriptor(mesh, i);
        }
    }

    static ComputeBuffer CreateShapeConfigBuffer(BlendShapeDescriptor[] blendShapeDescriptors)
    {
        List<BlendShapeConfig> gpuShapeData = new List<BlendShapeConfig>();
        for(var i = 0; i < blendShapeDescriptors.Length; i++)
        {
            var descriptor = blendShapeDescriptors[i];
            if (descriptor.IsValid())
                gpuShapeData.Add(descriptor.GetShapeConfig());
        }
        var buffer = new ComputeBuffer(gpuShapeData.Count, BlendShapeConfig.size);
        buffer.SetData(gpuShapeData);
        return buffer;
    }

    static ComputeBuffer CreateBlendShapeDeltaBuffer(Mesh mesh)
    {
        int blendShapeCount = mesh.blendShapeCount;
        int vertexCount = mesh.vertexCount;
        Vector3[] blendShapes = new Vector3[blendShapeCount * vertexCount];
        for (var i = 0; i < blendShapeCount; i++)
        {
            var frameCount = mesh.GetBlendShapeFrameCount(i);
            var name = mesh.GetBlendShapeName(i);
            Debug.Log($"{i} Name: {name} frameCount {frameCount}");
            Vector3[] verts = new Vector3[vertexCount];
            mesh.GetBlendShapeFrameVertices(i, 0, verts, null, null);
            verts.CopyTo(blendShapes, i * vertexCount);
            if (i == 0)
            {
                float max = 0;
                Vector3 maxVert = Vector3.zero;
                foreach (var vert in verts)
                {
                    if (vert.magnitude > max)
                    {
                        max = vert.magnitude;
                        maxVert = vert;
                    }
                }
                Debug.Log($"Magnitude: {max} x: {maxVert.x}    y: {maxVert.y}    z: {maxVert.z}");
            }
            Debug.Log($"lastIndex: {verts[vertexCount - 1]} {verts[vertexCount - 1] == blendShapes[i * vertexCount + vertexCount - 1]}  Verts Length:{verts.Length}");
        }

        var blendShapeBuffer = new ComputeBuffer(vertexCount * blendShapeCount, 12);
        blendShapeBuffer.SetData(blendShapes);
        return blendShapeBuffer;
    }

    static ComputeBuffer CreateBlendShapeWeightsBuffer(Mesh mesh, float[] blendShapeWeights)
    {        
        var buffer = new ComputeBuffer(mesh.blendShapeCount, 4);
        buffer.SetData(blendShapeWeights);
        return buffer;
    }

    static void UpdateBlendShapeWeightsBuffer(ComputeBuffer buffer, SkinnedMeshRenderer renderer, float[] blendShapeWeights)
    {
        for (var i = 0; i < blendShapeWeights.Length; i++)
        {
            blendShapeWeights[i] = renderer.GetBlendShapeWeight(i) / 100f;
        }
        buffer.SetData(blendShapeWeights);
    }

    public SkinnedMeshRenderer skinnedMeshRenderer; 
    public Mesh mesh;
    public ComputeShader computeShader;

    ComputeBuffer blendShapeBuffer, maxDeltaBuffer, blendShapeWeightsBuffer, vertexActivationsBuffer, blendShapeConfigBuffer;
    GraphicsBuffer colorBuffer, indexBuffer;
    KernelParams CalcMaxVertex, CalcActivation, ApplyColors;
    Vector3[] blendShapeDeltas;
    float[] blendShapeWeights;
    int blendShapeCount, vertexCount, triangleCount;
    private void Awake()
    {
        skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();
        skinnedMeshRenderer.sharedMesh = mesh = Instantiate(skinnedMeshRenderer.sharedMesh);
        computeShader = Instantiate(computeShader);
    }

    void OnEnable()
    {


        blendShapeCount = mesh.blendShapeCount;
        vertexCount = mesh.vertexCount;
        triangleCount = mesh.triangles.Length / 3;
        blendShapeWeights = new float[blendShapeCount];

        CalcMaxVertex = new KernelParams("CalcMaxDelta", computeShader, mesh.blendShapeCount);
        CalcActivation = new KernelParams("CalcActivation", computeShader, mesh.vertexCount * mesh.blendShapeCount);
        ApplyColors = new KernelParams("ApplyColors", computeShader, mesh.vertexCount);
        computeShader.SetInt("_BlendShapeCount", blendShapeCount);
        computeShader.SetInt("_VertexCount", vertexCount);
        computeShader.SetInt("_TriangleCount", triangleCount);

        blendShapeBuffer = CreateBlendShapeDeltaBuffer(mesh);
        CalcMaxVertex.SetBuffer("_BlendShapeBuffer", blendShapeBuffer);
        CalcActivation.SetBuffer("_BlendShapeBuffer", blendShapeBuffer);

        maxDeltaBuffer = new ComputeBuffer(blendShapeCount, 12);
        CalcMaxVertex.SetBuffer("_VertexMaxBuffer", maxDeltaBuffer);
        CalcActivation.SetBuffer("_VertexMaxBuffer", maxDeltaBuffer);

        blendShapeWeightsBuffer = CreateBlendShapeWeightsBuffer(mesh, blendShapeWeights);
        CalcActivation.SetBuffer("_BlendShapeWeightBuffer", blendShapeWeightsBuffer);

        vertexActivationsBuffer = new ComputeBuffer(vertexCount * blendShapeCount, 4);
        CalcActivation.SetBuffer("_VertexActivationBuffer", vertexActivationsBuffer);
        ApplyColors.SetBuffer("_VertexActivationBuffer", vertexActivationsBuffer);

        blendShapeConfigBuffer = CreateShapeConfigBuffer(blendShapeDescriptors);
        CalcActivation.SetBuffer("_BlendShapeConfigBuffer", blendShapeConfigBuffer);
        ApplyColors.SetBuffer("_BlendShapeConfigBuffer", blendShapeConfigBuffer);

        CalcMaxVertex.Dispatch();


    }

    private void Update()
    {
        mesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
        colorBuffer = mesh.GetVertexBuffer(1);
        mesh.indexBufferTarget |= GraphicsBuffer.Target.Structured;
        indexBuffer = mesh.GetIndexBuffer();

        UpdateBlendShapeWeightsBuffer(blendShapeWeightsBuffer, skinnedMeshRenderer, blendShapeWeights);
        CalcActivation.Dispatch();

        ApplyColors.SetBuffer("_IndexBuffer", indexBuffer);
        ApplyColors.SetBuffer("_ColorBuffer", colorBuffer);

        ApplyColors.Dispatch();

        indexBuffer?.Release();
        indexBuffer = null;
        colorBuffer?.Release();
        colorBuffer = null;
    }

    void OnDisable()
    {
        if (mesh)
#if UNITY_EDITOR            
            DestroyImmediate(mesh);
#else
            Destroy(mesh);
#endif
        blendShapeConfigBuffer?.Release();
        blendShapeConfigBuffer = null;
        indexBuffer?.Release();
        indexBuffer = null;
        colorBuffer?.Release();
        colorBuffer = null;
        blendShapeBuffer?.Release();
        blendShapeBuffer = null;
        maxDeltaBuffer?.Release();
        maxDeltaBuffer = null;
        blendShapeWeightsBuffer?.Release();
        blendShapeWeightsBuffer = null;
        vertexActivationsBuffer?.Release();
        vertexActivationsBuffer = null;
    }

}

[Serializable]
public struct BlendShapeDescriptor
{
    [SerializeField, HideInInspector]
    private string name;
    [SerializeField, HideInInspector]
    private int index;
    [SerializeField]
    private TexChannel positiveChannel;

    public string Name { get => name; set => name = value; }
    public int Index { get => index; set => index = value; }
    public TexChannel PositiveChannel { get => positiveChannel; set => positiveChannel = value; }

    public BlendShapeDescriptor(Mesh mesh, int index)
    {
        name = mesh.GetBlendShapeName(index);
        this.index = index;
        positiveChannel = TexChannel.R;
    }

    public BlendShapeConfig GetShapeConfig()
    {
        return new BlendShapeConfig(this);
    }

    public bool IsValid()
    {
        return positiveChannel != TexChannel.None;
    }

}

public struct BlendShapeConfig
{
    public const int size = sizeof(int) + sizeof(int);
    public BlendShapeConfig(BlendShapeDescriptor blendShapeDescriptor)
    {
        index = blendShapeDescriptor.Index;
        channel = (int) blendShapeDescriptor.PositiveChannel;
    }
    int index;
    int channel;

    public int Index { get => index; set => index = value; }
    public int Channel { get => channel; set => channel = value; }
}
public enum TexChannel
{
    None,
    R,
    G,
    B,
    A
}
