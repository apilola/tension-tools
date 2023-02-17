using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using System.Reflection;
using Unity.Collections.LowLevel.Unsafe;

namespace TensionTools
{
    [RequireComponent(typeof(SkinnedMeshRenderer)), ExecuteAlways]
    public class TensionData : MonoBehaviour
    {
        public enum Mode
        {
            Baked,
            OnAwake,
        }

        [System.Serializable]
        public class Property
        {
            private const string INTENSITY_FORMAT = "_{0}Intensity";
            private const string LIMIT_FORMAT = "_{0}Limit";
            private const string POWER_FORMAT = "_{0}Power";

            public const string SQUASH = "Squash";
            public const string STRETCH = "Stretch";

            private readonly int LimitProp;
            private readonly int IntensityProp;
            private readonly int PowerProp;
            private readonly string ModeID;

            public Property(string ModeID)
            {
                this.ModeID = ModeID;
                LimitProp = Shader.PropertyToID(string.Format(LIMIT_FORMAT, ModeID));
                IntensityProp = Shader.PropertyToID(string.Format(INTENSITY_FORMAT, ModeID));
                PowerProp = Shader.PropertyToID(string.Format(POWER_FORMAT, ModeID));
            }

            [SerializeField, Min(0)] private float m_Intensity = 1;
            [SerializeField, Range(0, 1)] private float m_Limit = 1;
            [SerializeField, Min(.01f)] private float m_Power = 1;
            internal TensionData m_TensionData;

            public float Intensity
            {
                get => m_Intensity;
                set
                {
                    value = Mathf.Max(0, value);
                    if (value != m_Intensity)
                    {
                        m_TensionData.m_MaterialPropertyBlock.SetFloat(IntensityProp, m_Intensity);
                        m_Intensity = value;
                    }
                }
            }

            public float Limit
            {
                get => m_Limit;
                set
                {
                    value = Mathf.Clamp01(value);
                    if (value != m_Limit)
                    {
                        m_TensionData.m_MaterialPropertyBlock.SetFloat(LimitProp, m_Limit);
                        m_Limit = value;
                    }
                }
            }

            public float Power
            {
                get => m_Power;
                set
                {
                    value = Mathf.Max(0, value);
                    if (value != m_Power)
                    {
                        m_TensionData.m_MaterialPropertyBlock.SetFloat(PowerProp, m_Power);
                        m_Power = value;
                    }
                }
            }

            public void ApplyMaterialProperties()
            {
                UpdateMaterialPropertyBlock(m_TensionData.m_MaterialPropertyBlock);
            }

            public void ForceApplyMaterialProperties()
            {
                UpdateMaterialPropertyBlock(m_TensionData.m_MaterialPropertyBlock);
                m_TensionData.ForceMaterialPropertyBlockUpdate();
            }

            public void UpdateMaterialPropertyBlock(MaterialPropertyBlock propertyBlock)
            {
                propertyBlock.SetFloat(IntensityProp, m_Intensity);
                propertyBlock.SetFloat(LimitProp, m_Limit);
                propertyBlock.SetFloat(PowerProp, m_Power);
            }
        }

        [SerializeField, HideInInspector] SkinnedMeshRenderer m_SkinnedMeshRenderer;

        public SkinnedMeshRenderer Renderer => m_SkinnedMeshRenderer;

        MaterialPropertyBlock m_MaterialPropertyBlock;

        int _VertexBufferStride, _VertexCount;
        GraphicsBuffer _VertexBuffer;
        ComputeBuffer _EdgeBuffer, _RawEdgeDeltaBuffer;
        [SerializeField, HideInInspector] int[] m_Edges = new int[0];
        [SerializeField, HideInInspector] Vector3[] m_EdgeDeltas = new Vector3[0];
        [SerializeField, HideInInspector] bool m_IsBaked = false;
        [SerializeField, HideInInspector] Mesh m_BakedMesh;

        [NonSerialized] bool isSetUp;

        [SerializeField] Mode m_Mode = Mode.OnAwake;

        [SerializeField]
        private Property m_Squash = new Property(Property.SQUASH);
        [SerializeField]
        private Property m_Stretch = new Property(Property.STRETCH);
        public Property Squash => m_Squash;
        public Property Stretch => m_Stretch;

        private void OnValidate()
        {
            if (m_SkinnedMeshRenderer == null)
                m_SkinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();

            if (m_SkinnedMeshRenderer == null)
            {
                throw new UnityEngine.MissingComponentException($"This component requires a \"{nameof(SkinnedMeshRenderer)}\"");
            }

            if (m_SkinnedMeshRenderer.sharedMesh == null)
            {
                throw new UnityEngine.MissingReferenceException($"The renderer is missing a \"Shared Mesh\"");
            }

            if (m_SkinnedMeshRenderer.sharedMesh != m_BakedMesh)
            {
                ForceBakeMeshInfo();
            }

            if (m_MaterialPropertyBlock == null)
                m_MaterialPropertyBlock = new MaterialPropertyBlock();

            m_Stretch.m_TensionData = this;
            m_Squash.m_TensionData = this;
            m_Stretch.ApplyMaterialProperties();
            m_Squash.ForceApplyMaterialProperties();

            if (isSetUp)
            {
                UpdateVertexBuffer();
            }
            else
            {
                TearDown();
                SetUp();
            }
        }

        private void Awake()
        {

#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += TearDown;
#endif


            if (m_SkinnedMeshRenderer == null)
                m_SkinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();

            if (m_SkinnedMeshRenderer == null)
            {
                throw new UnityEngine.MissingComponentException($"This component requires a \"{nameof(SkinnedMeshRenderer)}\"");
            }

            if (m_SkinnedMeshRenderer.sharedMesh == null)
            {
                throw new UnityEngine.MissingReferenceException($"The renderer is missing a \"Shared Mesh\"");
            }

            if (m_MaterialPropertyBlock == null)
                m_MaterialPropertyBlock = new MaterialPropertyBlock();

            m_Stretch.m_TensionData = this;
            m_Squash.m_TensionData = this;

            SetUp();
        }

        private void OnWillRenderObject()
        {
            if (!isSetUp)
            {
                Debug.LogError($"{nameof(TensionData)}:: Attempting to Update Vertex Buffer but the component failed to setup", this);
                return;
            }
            UpdateVertexBuffer();
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
            public static readonly int RAW_EDGE_DELTA_BUFFER = Shader.PropertyToID(nameof(_RawEdgeDeltaBuffer));
            public static readonly int SCALE = Shader.PropertyToID("_Scale");
            public static readonly int SQUASH_INTENSITY = Shader.PropertyToID("_SquashIntensity");
            public static readonly int SQUASH_LIMIT = Shader.PropertyToID("_SquashLimit");
            public static readonly int STRETCH_INTENSITY = Shader.PropertyToID("_StretchIntensity");
            public static readonly int STRETCH_LIMIT = Shader.PropertyToID("_StretchLimit");
            public static readonly int STRETCH_POWER = Shader.PropertyToID("_StretchPower");
            public static readonly int SQUASH_POWER = Shader.PropertyToID("_SquashPower");
        }

        bool IsMaterialValid(Material mat)
        {

            return mat != null;// && mat.HasBuffer(Params.EDGE_BUFFER) && mat.HasBuffer(Params.BASE_VERTEX_BUFFER) && mat.HasBuffer(Params.VERTEX_BUFFER) && mat.HasInt(Params.VERTEX_BUFFER_STRIDE);
        }

        bool CanSetUp()
        {
            if (m_SkinnedMeshRenderer == null)
                Debug.LogError($"{nameof(CanSetUp)} : {m_SkinnedMeshRenderer != null}");
            if (m_SkinnedMeshRenderer.sharedMaterial == null)
                Debug.LogError($"{nameof(CanSetUp)} skinnedMeshRenderer.sharedMaterial != null: {m_SkinnedMeshRenderer.sharedMaterial != null}");
            if (m_SkinnedMeshRenderer.sharedMesh == null)
                Debug.LogError($"{nameof(CanSetUp)} skinnedMeshRenderer.sharedMesh != null: {m_SkinnedMeshRenderer.sharedMesh != null}");
            return m_SkinnedMeshRenderer != null && m_SkinnedMeshRenderer.sharedMaterial != null && m_SkinnedMeshRenderer.sharedMesh != null && !isSetUp;
        }

        private void SetUp()
        {
            if (!CanSetUp() || isSetUp)
            {
                return;
            }
            var mesh = m_SkinnedMeshRenderer.sharedMesh;
            var info = GetEdgeInfo(mesh);

            _EdgeBuffer = new ComputeBuffer(info.edges.Length, 4);
            _EdgeBuffer.SetData(info.edges);
            _RawEdgeDeltaBuffer = new ComputeBuffer(info.edgeDeltas.Length, 12);
            _RawEdgeDeltaBuffer.SetData(info.edgeDeltas);

            if (m_MaterialPropertyBlock == null)
                m_MaterialPropertyBlock = new MaterialPropertyBlock();

            SetUpMaterialPropertyBlock(mesh.vertexCount, m_Squash, m_Stretch, m_MaterialPropertyBlock, _EdgeBuffer, _RawEdgeDeltaBuffer);
            isSetUp = true;

            SetUp();
        }

        private (int[] edges, Vector3[] edgeDeltas) GetEdgeInfo(Mesh mesh)
        {

            if (m_Mode == Mode.Baked && m_IsBaked && mesh == m_BakedMesh)
            {
                return (m_Edges, m_EdgeDeltas);
            }

            return BakeMeshInfo(mesh);
        }

        private void TearDown()
        {
            ReleaseBuffer(ref _EdgeBuffer);
            ReleaseBuffer(ref _RawEdgeDeltaBuffer);
            ReleaseBuffer(ref _VertexBuffer);
            isSetUp = false;
        }

        [ContextMenu("Bake Mesh Info")]
        public void ForceBakeMeshInfo()
        {
            if (m_SkinnedMeshRenderer == null)
            {
                throw new UnityEngine.MissingComponentException($"This component requires a \"{nameof(SkinnedMeshRenderer)}\"");
            }

            if (m_SkinnedMeshRenderer.sharedMesh == null)
            {
                throw new UnityEngine.MissingReferenceException($"The renderer is missing a \"Shared Mesh\"");
            }
            var mesh = m_SkinnedMeshRenderer.sharedMesh;
            var info = BakeMeshInfo(mesh);

            if (isSetUp)
            {
                ReleaseBuffer(ref _EdgeBuffer);
                ReleaseBuffer(ref _RawEdgeDeltaBuffer);

                _EdgeBuffer = new ComputeBuffer(info.edges.Length, 4);
                _EdgeBuffer.SetData(info.edges);
                _RawEdgeDeltaBuffer = new ComputeBuffer(info.edgeDeltas.Length, 12);
                _RawEdgeDeltaBuffer.SetData(info.edgeDeltas);

                SetUpMaterialPropertyBlock(mesh.vertexCount, m_Squash, m_Stretch, m_MaterialPropertyBlock, _EdgeBuffer, _RawEdgeDeltaBuffer);
            }
        }

        private (int[] edges, Vector3[] edgeDeltas) BakeMeshInfo(Mesh mesh)
        {
            if (mesh == null)
            {
                throw new UnityEngine.MissingReferenceException($"The renderer is missing a \"Shared Mesh\"");
            }

            Debug.Log($"{nameof(TensionData)}: Baking MeshInfo for mesh named \"{mesh.name}\" for \"{this.gameObject.name}\"", this);

            m_Edges = BakeEdges(mesh);
            m_EdgeDeltas = BakeRawVerts(mesh, m_Edges);
            m_BakedMesh = mesh;
            m_IsBaked = true;
            return (m_Edges, m_EdgeDeltas);
        }

        private void SetUpMaterialPropertyBlock(int vertexCount, Property squash, Property stretch, MaterialPropertyBlock propertyBlock, ComputeBuffer edgeBuffer, ComputeBuffer baseEdgeBuffer)
        {
            propertyBlock.SetBuffer(Params.EDGE_BUFFER, edgeBuffer);
            propertyBlock.SetInteger(Params.VERTEX_COUNT, vertexCount);
            propertyBlock.SetBuffer(Params.RAW_EDGE_DELTA_BUFFER, baseEdgeBuffer);
            propertyBlock.SetFloat(Params.SQUASH_INTENSITY, squash.Intensity);
            propertyBlock.SetFloat(Params.SQUASH_LIMIT, squash.Limit);
            propertyBlock.SetFloat(Params.SQUASH_LIMIT, squash.Limit);
            propertyBlock.SetFloat(Params.STRETCH_INTENSITY, stretch.Intensity);
            propertyBlock.SetFloat(Params.STRETCH_LIMIT, stretch.Limit);
        }

        private void ApplyVertexBufferToMaterialPropertyBlock(MaterialPropertyBlock propertyBlock, GraphicsBuffer vertexBuffer)
        {
            propertyBlock.SetInteger(Params.VERTEX_BUFFER_STRIDE, vertexBuffer.stride / 4);
            propertyBlock.SetBuffer(Params.VERTEX_BUFFER, vertexBuffer);
            propertyBlock.SetVector(Params.SCALE, transform.lossyScale);
        }

        private static int[] BakeEdges(Mesh mesh)
        {
            int vertexCount = mesh.vertexCount;
            int[] tris = mesh.triangles;
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

            return edgeData;
        }

        private Vector3[] BakeRawVerts(Mesh mesh, int[] edgeData)
        {
            var vertexCount = mesh.vertexCount;
            var edgeCount = edgeData.Length - (vertexCount);
            var vertices = mesh.vertices;
            Vector3[] edges = new Vector3[edgeCount];
            int iterator = 0;
            for (var i = 0; i < vertexCount; i++)
            {
                int end = edgeData[i];
                while (iterator < end)
                {
                    edges[iterator] = vertices[i] - vertices[edgeData[iterator + vertexCount]];
                    iterator++;
                }
            }

            return edges;
        }

        private GraphicsBuffer SetUpVertexBuffer(SkinnedMeshRenderer skinnedMeshRenderer)
        {
            skinnedMeshRenderer.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
            var buffer = skinnedMeshRenderer.GetVertexBuffer();
            return buffer;
        }

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
            buffer?.Dispose();
            buffer = null;
        }

        void ReleaseBuffer(ref GraphicsBuffer buffer)
        {
            buffer?.Dispose();
            buffer = null;
        }
    }
}