using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using System;
using UnityObject = UnityEngine.Object;
using UnityEditor.SceneManagement;
using UnityEditor.Experimental;
using System.Reflection;
[CustomEditor(typeof(TensionData))]
public class TensionDataEditor : Editor
{
    [InitializeOnLoad]
    public class Style
    {
        static Style()
        {
            VisualizerIconOff = EditorGUIUtility.FindTexture("animationvisibilitytoggleon@2x");
            VisualizerIconOn = EditorGUIUtility.FindTexture("animationvisibilitytoggleoff@2x");

        }
        public readonly static Texture2D VisualizerIconOff;
        public readonly static Texture2D VisualizerIconOn;
        readonly static GUIContent VisualizerButtonLabel = new GUIContent();
        public static GUIContent GetVisualizerContent(bool on)
        {
            VisualizerButtonLabel.image = (on) ? VisualizerIconOn : VisualizerIconOff;
            return VisualizerButtonLabel;
        }

        public static GUIStyle HeaderStyle => EditorStyles.boldLabel;
        public readonly static GUIContent TensionPropertiesHeaderLabel = new GUIContent("Tension Properties");
    }  
    SkinnedMeshRenderer m_SkinnedMeshRenderer;

    TensionData m_TensionData;

    static Shader _VisualizerShader;
    static Material _VisualizerMaterial;
    Material _SharedMaterial;

    public void OnEnable()
    {
        if (_VisualizerShader == null)
            _VisualizerShader = Shader.Find("Shader Graphs/TensionVisualizer");
        if (_VisualizerMaterial == null)
            _VisualizerMaterial = new Material(_VisualizerShader);

        m_TensionData = (TensionData)target;
        m_SkinnedMeshRenderer = m_TensionData.Renderer;

        if(m_RenderPass == null)
        {
            m_RenderPass = new DrawRendererPass();
        }

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

        public UniversalAdditionalCameraData cameraData { get; private set; }

        public Bounds renderableBounds { get; private set; }

        public Vector3 currentPosition { get; set; }

        public bool useStaticAssetPreview { get; set; }

        public PreviewData(Renderer renderer)
        {
            renderUtility = new PreviewRenderUtility();
            renderUtility.camera.fieldOfView = 30.0f;
            cameraData = renderUtility.camera.GetUniversalAdditionalCameraData();
            if(cameraData == null)
            {
                Debug.LogError("Error: Camera Data is missing");
            }
            this.renderer = renderer;
            this.renderableBounds = renderer.bounds;
        }

        public void Dispose()
        {
            if (m_Disposed)
                return;
            renderUtility.Cleanup();
            renderer = null;
            m_Disposed = true;
        }
    }

    Dictionary<int, PreviewData> m_PreviewInstances = new Dictionary<int, PreviewData>();
    private DrawRendererPass m_RenderPass;
    Vector2 m_PreviewDir;
    Vector3 m_CurrentVelocity;
    float m_Zoom;
    Rect m_PreviewRect;
    private PropertyInfo referenceTargetIndexInfo = typeof(Editor).GetProperty("referenceTargetIndex", BindingFlags.NonPublic | BindingFlags.Instance);

    private int GetReferenceTargetIndex()
    {
        return (int) referenceTargetIndexInfo.GetValue(this);
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
        m_RenderPass.SetRenderer(previewData.renderer);
        m_TensionData.RevertToPreviousVertexBuffer();
        previewData.cameraData.scriptableRenderer.EnqueuePass(m_RenderPass);
        previewData.renderUtility.Render(true);
    }

    public override GUIContent GetPreviewTitle()
    {
        return new GUIContent($"{nameof(TensionData)}: {base.GetPreviewTitle().text}");
    }

    public override void OnPreviewSettings()
    {
        GUILayout.Button("Hello");
    }

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

   
}