using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
#if USING_URP
using UnityEngine.Rendering.Universal;
#elif USING_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif
using UnityEngine.Rendering;
using System;
using UnityObject = UnityEngine.Object;
using System.Reflection;

namespace TensionTools
{
    [CustomEditor(typeof(TensionData))]
    public class TensionDataEditor : Editor
    {
        public enum VisualizerMode
        {
            Both,
            Squash,
            Stretch,
            Off
        }

        public const string VISUALIZER_SHADER_NAME =
#if USING_URP
        "Shader Graphs/TensionVisualizerURP";
#else
        "Shader Graphs/TensionVisualizer";
#endif

        SkinnedMeshRenderer m_SkinnedMeshRenderer;

        TensionData m_TensionData;

        static Shader _VisualizerShader;
        static Material _VisualizerMaterial;
        Material _SharedMaterial;
        VisualizerMode _VisualizerMode;

        Dictionary<int, PreviewData> m_PreviewInstances = new Dictionary<int, PreviewData>();

#if USING_URP
        private DrawRendererPass m_RenderPass;
#else
        private Mesh m_PreviewMesh;
#endif

        Vector2 m_PreviewDir;
        Vector3 m_CurrentVelocity;
        float m_Zoom;
        Rect m_PreviewRect;
        private PropertyInfo referenceTargetIndexInfo = typeof(Editor).GetProperty("referenceTargetIndex", BindingFlags.NonPublic | BindingFlags.Instance);

        private int GetReferenceTargetIndex()
        {
            return (int)referenceTargetIndexInfo.GetValue(this);
        }

        public void OnEnable()
        {
            if (_VisualizerShader == null)
                _VisualizerShader = Shader.Find(VISUALIZER_SHADER_NAME);
            if (_VisualizerMaterial == null)
                _VisualizerMaterial = new Material(_VisualizerShader);

            m_TensionData = (TensionData)target;
            m_SkinnedMeshRenderer = m_TensionData.Renderer;

            if(m_SkinnedMeshRenderer == null)
            {
                throw new MissingComponentException($"{nameof(TensionDataEditor)}:: Requires a {nameof(SkinnedMeshRenderer)}");
            }

#if USING_URP
            if(m_RenderPass == null)
            {
                m_RenderPass = new DrawRendererPass();
            }
#else
            if(m_PreviewMesh == null)
            {
                m_PreviewMesh = new Mesh();
            }

            m_SkinnedMeshRenderer.BakeMesh(m_PreviewMesh);
#endif

            if (EditorSettings.defaultBehaviorMode == EditorBehaviorMode.Mode2D)
                m_PreviewDir = new Vector2(0, 0);
            else
            {
                m_PreviewDir = new Vector2(120, -20);

                //Fix for FogBugz case : 1364821 Inspector Model Preview orientation is reversed when Bake Axis Conversion is enabled
                UnityObject importedObject = PrefabUtility.IsPartOfVariantPrefab(target)
                    ? PrefabUtility.GetCorrespondingObjectFromSource(target) as GameObject
                    : target;

                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(importedObject)) as ModelImporter;
                if (importer && importer.bakeAxisConversion)
                {
                    m_PreviewDir += new Vector2(180, 0);
                }
            }
        }

        private void OnDisable()
        {
            foreach (var previewData in m_PreviewInstances.Values)
                previewData.Dispose();
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            EditorGUILayout.HelpBox("Unity may show warnings for undisposed Compute/Graphics Buffers. " +
                "\n\nLeak detection mode may be toggled through: Menu->TensionTools->LeakDetectionMode" +
                "\n\nOn my machine, leak detection does not seem to be outputting any stack traces, feedback would be appreciated... ", MessageType.Warning);
        }

        PreviewData GetPreviewData()
        {
            PreviewData previewData;
            int referenceTargetIndex = GetReferenceTargetIndex();
            if (!m_PreviewInstances.TryGetValue(referenceTargetIndex, out previewData))
            {
                previewData = new PreviewData((target as TensionData).Renderer);
                m_PreviewInstances.Add(referenceTargetIndex, previewData);
            }
            ReloadPreviewInstances();
            return previewData;
        }

        class PreviewData : IDisposable
        {
            bool m_Disposed;

            public readonly PreviewRenderUtility renderUtility;
            public Renderer renderer { get; private set; }

            public string prefabAssetPath { get; private set; }

#if USING_URP
            public UniversalAdditionalCameraData cameraData { get; private set; }
#else
            public Mesh previewMesh { get; private set; }
#endif
            public Bounds renderableBounds { get; private set; }

            public Vector3 currentPosition { get; set; }

            public bool useStaticAssetPreview { get; set; }

            internal static Mesh GetPreviewSphere()
            {
                var handleGo = (GameObject)EditorGUIUtility.LoadRequired("Previews/PreviewMaterials.fbx");
                // Temp workaround to make it not render in the scene
                handleGo.SetActive(false);
                foreach (Transform t in handleGo.transform)
                {
                    if (t.name == "sphere")
                        return t.GetComponent<MeshFilter>().sharedMesh;
                }
                return null;
            }

            public PreviewData(Renderer renderer)
            {
                renderUtility = new PreviewRenderUtility();
                renderUtility.camera.fieldOfView = 30.0f;

                this.renderer = renderer;
                this.renderableBounds = renderer.bounds;
#if USING_URP
                cameraData = renderUtility.camera.GetUniversalAdditionalCameraData();
#else
                var cameraData = renderUtility.camera.gameObject.AddComponent<HDAdditionalCameraData>();
                previewMesh = new Mesh();
                previewMesh.hideFlags = HideFlags.HideAndDontSave;
                previewMesh.MarkDynamic();

                if (renderer is SkinnedMeshRenderer smRenderer)
                {
                    smRenderer.BakeMesh(previewMesh);
                }
#endif
            }

#if !USING_URP
            public Mesh GetUpdatedPreviewMesh()
            {
                if(renderer is SkinnedMeshRenderer smRenderer)
                {
                    smRenderer.BakeMesh(previewMesh);
                }

                return previewMesh;
            }
#endif

            public void Dispose()
            {
                if (m_Disposed)
                    return;
                renderUtility.Cleanup();
                renderer = null;
                m_Disposed = true;

#if !USING_URP
                if(previewMesh)
                {
                    DestroyImmediate(previewMesh);
                    previewMesh = null;
                }
#endif
            }
        }

        public static Vector2 Drag2D(Vector2 scrollPosition, Rect position, ref float zoom)
        {
            int controlID = GUIUtility.GetControlID("Slider".GetHashCode(), FocusType.Passive);
            Event current = Event.current;
            switch (current.GetTypeForControl(controlID))
            {
                case EventType.MouseDown:
                    if (position.Contains(current.mousePosition) && position.width > 50f)
                    {
                        GUIUtility.hotControl = controlID;
                        current.Use();
                        EditorGUIUtility.SetWantsMouseJumping(1);
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlID)
                    {
                        GUIUtility.hotControl = 0;
                    }
                    EditorGUIUtility.SetWantsMouseJumping(0);
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlID)
                    {
                        scrollPosition -= current.delta * (float)((!current.shift) ? 1 : 3) / Mathf.Min(position.width, position.height) * 140f;
                        scrollPosition.y = Mathf.Clamp(scrollPosition.y, -90f, 90f);
                        current.Use();
                        GUI.changed = true;
                    }
                    break;
                case EventType.ScrollWheel:
                    //if (GUIUtility.hotControl == controlID)
                    {
                        zoom = Mathf.Max(current.delta.y * .5f + zoom, 0);
                    
                        current.Use();
                        GUI.changed = true;
                    }
                    break;
            }
            return scrollPosition;
        }

        public override bool HasPreviewGUI()
        {
            return true;
        }
    
        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            var previewData = GetPreviewData();
            DrawPreview(r, background, previewData);

            var direction = Drag2D(m_PreviewDir, r, ref m_Zoom);
            if (direction != m_PreviewDir)
            {
                m_PreviewDir = direction;
            }
        }

        private void DrawPreview(Rect r, GUIStyle background, PreviewData previewData)
        {
            if (Event.current.type == EventType.Repaint)
            {


                if (m_PreviewRect != r)
                {
                    m_PreviewRect = r;
                }

                var previewUtility = previewData.renderUtility;
                previewUtility.BeginPreview(r, background);
                DoRenderPreview(previewData);
                previewUtility.EndAndDrawPreview(r);

                if (m_CurrentVelocity != Vector3.zero)
                {
                    Repaint();
                }

            }
        }

        public override GUIContent GetPreviewTitle()
        {
            return new GUIContent($"{nameof(TensionData)}: {base.GetPreviewTitle().text}");
        }

        public override void OnPreviewSettings()
        {
            {
                EditorGUI.BeginChangeCheck();
                var mode = (VisualizerMode)EditorGUILayout.EnumPopup(GUIContent.none, _VisualizerMode, EditorStyles.toolbarPopup, GUILayout.Width(60));
                if (EditorGUI.EndChangeCheck())
                {
                    var value = $"_MODE_{mode.ToString().ToUpper()}";
                    Debug.Log(value);
                    var keyword = new LocalKeyword(_VisualizerShader, value);
                    _VisualizerMaterial.EnableKeyword(keyword);

                    var current = $"_MODE_{_VisualizerMode.ToString().ToUpper()}";
                    var currentKeyword = new LocalKeyword(_VisualizerShader, current);
                    _VisualizerMaterial.DisableKeyword(currentKeyword);


                    var Both = $"_MODE_{VisualizerMode.Both.ToString().ToUpper()}";
                    var isBothEnabled = _VisualizerMaterial.IsKeywordEnabled(Both);
                    Debug.Log($"Is Both Enabled: {isBothEnabled}");
                    _VisualizerMode = mode;
                }
            }
        }

        private void DoRenderPreview(PreviewData previewData)
        {
            var bounds = previewData.renderableBounds;
            float halfSize = Mathf.Max(bounds.extents.magnitude, 0.0001f);
            float distance = halfSize * (3.8f + m_Zoom);

            Quaternion rot = Quaternion.Euler(-m_PreviewDir.y, -m_PreviewDir.x, 0);
            previewData.currentPosition = Vector3.SmoothDamp(previewData.currentPosition, previewData.renderer.bounds.center, ref m_CurrentVelocity, .75f);
            Vector3 pos = previewData.currentPosition - rot * (Vector3.forward * distance);

            previewData.renderUtility.camera.transform.position = pos;//Vector3.SmoothDamp(previewData.renderUtility.camera.transform.position, pos, ref m_CurrentVelocity, .075f);
            previewData.renderUtility.camera.transform.rotation = rot;
            previewData.renderUtility.camera.nearClipPlane = distance - halfSize * 1.1f;
            previewData.renderUtility.camera.farClipPlane = distance + halfSize * 1.1f;

            previewData.renderUtility.lights[0].intensity = .7f;
            previewData.renderUtility.lights[0].transform.rotation = rot * Quaternion.Euler(40f, 40f, 0);
            previewData.renderUtility.lights[1].intensity = .7f;
            previewData.renderUtility.lights[1].transform.rotation = rot * Quaternion.Euler(340, 218, 177);

            previewData.renderUtility.ambientColor = new Color(.1f, .1f, .1f, 0);
            if(m_TensionData == null)
            {
                throw new MissingReferenceException($"Missing {nameof(TensionData)}");
            }

#if USING_URP
            m_TensionData.RevertToPreviousVertexBuffer();
            m_RenderPass.SetRenderer(previewData.renderer);
            previewData.cameraData.scriptableRenderer.EnqueuePass(m_RenderPass);
            previewData.renderUtility.Render(true);
#else
            UnityEngine.Profiling.Profiler.BeginSample($"{nameof(TensionDataEditor)}:: Draw Preview");

            m_TensionData.UpdateVertexBuffer();
            var matrix = Matrix4x4.TRS(m_TensionData.transform.position, m_TensionData.transform.rotation, m_TensionData.transform.lossyScale);
            var cam = previewData.renderUtility.camera;
            var cmd = new CommandBuffer();
            cam.AddCommandBuffer(CameraEvent.AfterForwardOpaque, cmd);
            cmd.DrawRenderer(previewData.renderer, _VisualizerMaterial, 0, 0);
            previewData.renderUtility.Render(false);

            UnityEngine.Profiling.Profiler.EndSample();
#endif
        }

#if USING_URP
        public class DrawRendererPass : ScriptableRenderPass
        {
            Renderer m_Target;
            bool m_CachedOffScreenState;
            public void SetRenderer(Renderer renderer)
            {
                m_Target = renderer;
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (m_Target == null)
                    return;
            
                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, new ProfilingSampler(nameof(DrawRendererPass))))
                {
                    cmd.DrawRenderer(m_Target, _VisualizerMaterial, 0,0);
                }           

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }
#endif
    }
}