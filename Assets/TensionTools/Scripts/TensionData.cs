using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using System.Reflection;

[RequireComponent(typeof(SkinnedMeshRenderer)), ExecuteAlways]
public class TensionData : MonoBehaviour
{
#if UNITY_EDITOR

    [SerializeField, HideInInspector] bool m_VisualizerEditor;
#endif
    public enum Mode
    {
        OnAwake,
        Baked,
        OnEnable,
        Manual       
    }

    [SerializeField, HideInInspector] SkinnedMeshRenderer m_SkinnedMeshRenderer;

    public SkinnedMeshRenderer Renderer => m_SkinnedMeshRenderer;

    MaterialPropertyBlock m_MaterialPropertyBlock;

    int _VertexBufferStride, _VertexCount;
    GraphicsBuffer _VertexBuffer;
    //ComputeBuffer _BaseVertexBuffer, _EdgeBuffer, _BaseEdgeBuffer;
    ComputeBuffer _EdgeBuffer, _BaseEdgeBuffer;
    [SerializeField, HideInInspector] int[] edgeData;
    [NonSerialized] bool isSetUp;

    [SerializeField] Properties m_Properties = new Properties()
    {
        SquashIntensity = 1f,
        SquashLimit = 1f,
        StretchIntensity = 1f,
        StretchLimit = 1f,
    };

    [Serializable]
    public struct Properties
    {
        [SerializeField] private float m_SquashIntensity;
        [SerializeField] private float m_SquashLimit;
        [SerializeField] private float m_StretchIntensity;
        [SerializeField] private float m_StretchLimit;

        public float SquashIntensity { get => m_SquashIntensity; set => m_SquashIntensity = Mathf.Max(value, 0); }
        public float SquashLimit { get => m_SquashLimit; set => m_SquashLimit = Mathf.Clamp01(value); }
        public float StretchIntensity { get => m_StretchIntensity; set => m_StretchIntensity = Mathf.Max(value, 0); }
        public float StretchLimit { get => m_StretchLimit; set => m_StretchLimit = Mathf.Clamp01(value); }

    }    

    public void SetSquashIntensity(float value)
    {
        value = m_Properties.SquashIntensity = value;
        if (value != m_MaterialPropertyBlock.GetFloat(Params.SQUASH_INTENSITY))
        {
            m_MaterialPropertyBlock.SetFloat(Params.SQUASH_INTENSITY, value);
        }
    }

    public void SetSquashLimit(float value)
    {
        value = m_Properties.SquashLimit = value;
        if (value != m_MaterialPropertyBlock.GetFloat(Params.SQUASH_LIMIT))
        {
            m_MaterialPropertyBlock.SetFloat(Params.SQUASH_LIMIT, value);
        }
    }

    public void SetStretchIntensity(float value)
    {
        value = m_Properties.StretchIntensity = value;
        if(value != m_MaterialPropertyBlock.GetFloat(Params.STRETCH_INTENSITY))
        {
            m_MaterialPropertyBlock.SetFloat(Params.STRETCH_INTENSITY, value);
        }
    }

    public void SetStretchLimit(float value)
    {
        value = m_Properties.StretchLimit = value;
        if (value != m_MaterialPropertyBlock.GetFloat(Params.STRETCH_LIMIT))
        {
            m_MaterialPropertyBlock.SetFloat(Params.STRETCH_LIMIT, value);
        }
    }


    private void OnValidate()
    {
        if (m_SkinnedMeshRenderer == null)
        m_SkinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();
        if (m_MaterialPropertyBlock == null)
            m_MaterialPropertyBlock = new MaterialPropertyBlock();
        if (!isSetUp)
            UnityEditor.EditorApplication.delayCall += SetUp;
        else if (!CanSetUp())
            TearDown();
        else
        {
            UpdateVertexBuffer();
        }
    }

    private void Start()
    {
        Debug.Log("Awake Called");
        if (m_SkinnedMeshRenderer == null)
            m_SkinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();
        if (m_MaterialPropertyBlock == null)
            m_MaterialPropertyBlock = new MaterialPropertyBlock();
        //skinnedMeshRenderer.re
        //SetUp();
    }
    private void OnWillRenderObject()
    {
        if (!isSetUp)
            return;
        UpdateVertexBuffer();       
    }
    private void OnEnable()
    {
        SetUp();
    }

    private void OnDisable()
    {
        TearDown();
    }
    private void OnDestroy()
    {
        TearDown();
    }


    public static class Params
    {
        public static readonly int EDGE_BUFFER = Shader.PropertyToID(nameof(_EdgeBuffer));
        public static readonly int VERTEX_BUFFER = Shader.PropertyToID(nameof(_VertexBuffer));
        public static readonly int VERTEX_BUFFER_STRIDE = Shader.PropertyToID(nameof(_VertexBufferStride));
        public static readonly int VERTEX_COUNT = Shader.PropertyToID(nameof(_VertexCount));
        public static readonly int BASE_EDGE_BUFFER = Shader.PropertyToID(nameof(_BaseEdgeBuffer));
        public static readonly int SCALE = Shader.PropertyToID("_Scale");
        public static readonly int SQUASH_INTENSITY = Shader.PropertyToID("_SquashIntensity");
        public static readonly int SQUASH_LIMIT = Shader.PropertyToID("_SquashLimit");
        public static readonly int STRETCH_INTENSITY = Shader.PropertyToID("_StretchIntensity");
        public static readonly int STRETCH_LIMIT = Shader.PropertyToID("_StretchLimit");
    }

    bool IsMaterialValid(Material mat)
    {

        return mat != null;// && mat.HasBuffer(Params.EDGE_BUFFER) && mat.HasBuffer(Params.BASE_VERTEX_BUFFER) && mat.HasBuffer(Params.VERTEX_BUFFER) && mat.HasInt(Params.VERTEX_BUFFER_STRIDE);
    }

    bool CanSetUp()
    {
        if (m_SkinnedMeshRenderer == null)
            Debug.Log($"{nameof(CanSetUp)} : {m_SkinnedMeshRenderer != null}");
        if (m_SkinnedMeshRenderer == null)
            Debug.Log($"{nameof(CanSetUp)} skinnedMeshRenderer.sharedMaterial != null: {m_SkinnedMeshRenderer.sharedMaterial != null}");
        if (m_SkinnedMeshRenderer == null)
            Debug.Log($"{nameof(CanSetUp)} skinnedMeshRenderer.sharedMesh != null: {m_SkinnedMeshRenderer.sharedMesh != null}");
        return m_SkinnedMeshRenderer != null && m_SkinnedMeshRenderer.sharedMaterial != null && m_SkinnedMeshRenderer.sharedMesh != null;
    }


    [ContextMenu("Help")]
    public void Help()
    {
        var methods = typeof(SkinnedMeshRenderer).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach(var method in methods)
        {
            Debug.Log(method.Name);

        }
    }

    [ContextMenu("SetUp")]
    public void SetUp()
    {
        if (!CanSetUp() || isSetUp)
        {
            return;
        }
        var mesh = m_SkinnedMeshRenderer.sharedMesh;
        var tris = mesh.triangles;
        var vertexCount = mesh.vertexCount;
        _EdgeBuffer = SetUpEdgeBuffer(tris, vertexCount);
        _BaseEdgeBuffer = SetUpBaseEdgeBuffer(m_SkinnedMeshRenderer);

        if (m_MaterialPropertyBlock == null)
            m_MaterialPropertyBlock = new MaterialPropertyBlock();

        SetUpMaterialPropertyBlock(vertexCount, m_Properties, m_MaterialPropertyBlock, _EdgeBuffer, _BaseEdgeBuffer);
        isSetUp = true;
    }

    [ContextMenu("TearDown")]
    public void TearDown()
    {
        if (!isSetUp)
            return;
        ReleaseBuffer(ref _EdgeBuffer);
        ReleaseBuffer(ref _BaseEdgeBuffer);
        ReleaseBuffer(ref _VertexBuffer);
        isSetUp = false;
    }

    private void SetUpMaterialPropertyBlock(int vertexCount, Properties tensionProperties, MaterialPropertyBlock propertyBlock, ComputeBuffer edgeBuffer, ComputeBuffer baseEdgeBuffer)
    {
        propertyBlock.SetBuffer(Params.EDGE_BUFFER, edgeBuffer);
        propertyBlock.SetInteger(Params.VERTEX_COUNT, vertexCount);
        propertyBlock.SetBuffer(Params.BASE_EDGE_BUFFER, baseEdgeBuffer);
        propertyBlock.SetFloat(Params.SQUASH_INTENSITY, tensionProperties.SquashIntensity);
        propertyBlock.SetFloat(Params.SQUASH_LIMIT, tensionProperties.SquashLimit);
        propertyBlock.SetFloat(Params.STRETCH_INTENSITY, tensionProperties.StretchIntensity);
        propertyBlock.SetFloat(Params.STRETCH_LIMIT, tensionProperties.StretchLimit);
    }

    private void ApplyVertexBufferToMaterialPropertyBlock(MaterialPropertyBlock propertyBlock, GraphicsBuffer vertexBuffer)
    {
        propertyBlock.SetInteger(Params.VERTEX_BUFFER_STRIDE, vertexBuffer.stride / 4);
        propertyBlock.SetBuffer(Params.VERTEX_BUFFER, vertexBuffer);
        propertyBlock.SetVector(Params.SCALE, transform.lossyScale);
    }

    private ComputeBuffer SetUpEdgeBuffer(int[] tris, int vertexCount)
    {
        HashSet<int>[] edges = new HashSet<int>[vertexCount];
        int edgeCount = 0;
        for (var i = 0; i < tris.Length; i += 3)
        {
            var a = tris[i];
            var b = tris[i + 1];
            var c = tris[i + 2];
            if (edges[a] == null)
                edges[a] = new HashSet<int>(8);
            if (edges[b] == null)
                edges[b] = new HashSet<int>(8);
            if (edges[c] == null)
                edges[c] = new HashSet<int>(8);

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
        }

        int[] edgeData = new int[vertexCount + edgeCount];
        int iterator = 0;
        for (var i = 0; i < vertexCount; i++)
        {
            var edgeArray = edges[i].ToArray();
            for (var j = 0; j < edgeArray.Length; j++)
            {
                edgeData[iterator + vertexCount + j] = edgeArray[j];
            }
            edgeData[i] = iterator += edgeArray.Length;
        }

        ComputeBuffer buffer = new ComputeBuffer(edgeData.Length, 4);
        buffer.SetData(edgeData);
        this.edgeData = edgeData;
        return buffer;
    }
    /*
    private ComputeBuffer SetUpBaseVertexBuffer(SkinnedMeshRenderer skinnedMeshRenderer)
    {
        var mesh = skinnedMeshRenderer.sharedMesh;
        ComputeBuffer buffer = new ComputeBuffer(mesh.vertices.Length, 12);
        var fverts = new vert[mesh.vertexCount];
        var vertexBuffer = skinnedMeshRenderer.GetVertexBuffer();
        vertexBuffer.GetData(fverts);
        ReleaseBuffer(ref vertexBuffer);
        baseVerts = new Vector3[mesh.vertexCount];
        for (var i = 0; i < mesh.vertexCount; i++)
        {
            baseVerts[i] = fverts[i].pos;
        }
        buffer.SetData(baseVerts);
        return buffer;
    }*/

    private ComputeBuffer SetUpBaseEdgeBuffer(SkinnedMeshRenderer skinnedMeshRenderer)
    {
        var mesh = skinnedMeshRenderer.sharedMesh;
        var vertexCount = mesh.vertexCount;
        var edgeCount = edgeData.Length - (vertexCount);
        var vertices = mesh.vertices;
        ComputeBuffer buffer = new ComputeBuffer(edgeCount, 12);
        Vector3[] edges = new Vector3[edgeCount];
        int iterator = 0;
        for (var i = 0; i < vertexCount; i++)
        {       
            int end = edgeData[i];
            while(iterator < end)
            {
                edges[iterator] = vertices[i] - vertices[edgeData[iterator + vertexCount]];
                iterator++;
            }
        }
        buffer.SetData(edges);
        return buffer;
    }

    private GraphicsBuffer SetUpVertexBuffer(SkinnedMeshRenderer skinnedMeshRenderer)
    {
        skinnedMeshRenderer.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
        var buffer = skinnedMeshRenderer.GetVertexBuffer();
        return buffer;
    }

    [ContextMenu("UpdateVertexBuffer")]
    private void UpdateVertexBuffer()
    {
        ReleaseBuffer(ref _VertexBuffer);
        m_SkinnedMeshRenderer.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
        _VertexBuffer = m_SkinnedMeshRenderer.GetVertexBuffer();
        if (_VertexBuffer != null)
        {
            ApplyVertexBufferToMaterialPropertyBlock(m_MaterialPropertyBlock, _VertexBuffer);
            m_SkinnedMeshRenderer.SetPropertyBlock(m_MaterialPropertyBlock);
        }
    }

    public void ForceMaterialPropertyBlockUpdate()
    {
        m_SkinnedMeshRenderer.SetPropertyBlock(m_MaterialPropertyBlock);
    }

    public void RevertToPreviousVertexBuffer()
    {
        ReleaseBuffer(ref _VertexBuffer);
        m_SkinnedMeshRenderer.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
        _VertexBuffer = m_SkinnedMeshRenderer.GetPreviousVertexBuffer();
        if (_VertexBuffer != null)
        {
            ApplyVertexBufferToMaterialPropertyBlock(m_MaterialPropertyBlock, _VertexBuffer);
            m_SkinnedMeshRenderer.SetPropertyBlock(m_MaterialPropertyBlock);
        }
    }

    void ReleaseBuffer(ref ComputeBuffer buffer)
    {
        buffer?.Release();
        buffer = null;
    }

    void ReleaseBuffer(ref GraphicsBuffer buffer)
    {
        buffer?.Release();
        buffer = null;
    }
}
