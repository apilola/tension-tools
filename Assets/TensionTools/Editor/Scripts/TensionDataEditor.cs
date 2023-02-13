using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

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

    bool _Visualizer
    {
        get
        {
            return m_VisualizerEditorProperty.boolValue;
        }
        set
        {
            m_VisualizerEditorProperty.boolValue = value;
        }
    }

    static Shader _VisualizerShader;
    static Material _VisualizerMaterial;
    Material _SharedMaterial;


    SerializedProperty m_VisualizerEditorProperty;
    SerializedProperty m_TensionDataPropertiesProp;
    SerializedProperty m_SquashIntensityProp;
    SerializedProperty m_SquashLimitProp;
    SerializedProperty m_StretchIntensityProp;
    SerializedProperty m_StretchLimitProp;

    public void OnEnable()
    {
        if (_VisualizerShader == null)
            _VisualizerShader = Shader.Find("Shader Graphs/TensionVisualizer");
        if (_VisualizerMaterial == null)
            _VisualizerMaterial = new Material(_VisualizerShader);

        m_TensionData = (TensionData)target;
        m_SkinnedMeshRenderer = m_TensionData.GetComponent<SkinnedMeshRenderer>();
        m_VisualizerEditorProperty = serializedObject.FindProperty("m_VisualizerEditor");
        m_TensionDataPropertiesProp = serializedObject.FindProperty("m_Properties");
        m_SquashIntensityProp = m_TensionDataPropertiesProp.FindPropertyRelative("m_SquashIntensity");
        m_SquashLimitProp = m_TensionDataPropertiesProp.FindPropertyRelative("m_SquashLimit");
        m_StretchIntensityProp = m_TensionDataPropertiesProp.FindPropertyRelative("m_StretchIntensity");
        m_StretchLimitProp = m_TensionDataPropertiesProp.FindPropertyRelative("m_StretchLimit");
        _SharedMaterial = m_SkinnedMeshRenderer.sharedMaterial;

        if(_Visualizer)
        {
            m_SkinnedMeshRenderer.material = _VisualizerMaterial;
        }
    }

    private void OnDisable()
    {
        if (_Visualizer)
        {
            m_SkinnedMeshRenderer.material = _SharedMaterial;
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var visualizerButtonRect = GUILayoutUtility.GetRect(25f, 25f);
        EditorGUI.BeginChangeCheck();
        _Visualizer = GUI.Toggle(new Rect(visualizerButtonRect.x, visualizerButtonRect.y, 25f, 25f), _Visualizer, Style.GetVisualizerContent(_Visualizer), GUI.skin.button);
        if (EditorGUI.EndChangeCheck())
        {
            if (_Visualizer)
                m_SkinnedMeshRenderer.material = _VisualizerMaterial;
            else
                m_SkinnedMeshRenderer.material = _SharedMaterial;
        }

        bool tensionPropertiesHaveChanged = DoTensionProperties();
        if(tensionPropertiesHaveChanged)
        {
            
        }
        serializedObject.ApplyModifiedProperties();
    }

    private bool DoTensionProperties()
    {
        bool hasChanged = false;
        GUILayout.Label(Style.TensionPropertiesHeaderLabel, Style.HeaderStyle);
        {
            EditorGUI.BeginChangeCheck();
            float value = EditorGUILayout.FloatField(m_SquashIntensityProp.displayName, m_SquashIntensityProp.floatValue);
            if (EditorGUI.EndChangeCheck())
            {
                m_TensionData.SetSquashIntensity(value);
            }
        }
        {
            EditorGUI.BeginChangeCheck();
            float value = EditorGUILayout.Slider(m_SquashLimitProp.displayName, m_SquashLimitProp.floatValue, 0, 1);
            if (EditorGUI.EndChangeCheck())
            {
                m_TensionData.SetSquashLimit(value);
            }
        }
        {
            EditorGUI.BeginChangeCheck();
            float value = EditorGUILayout.FloatField(m_StretchIntensityProp.displayName, m_StretchIntensityProp.floatValue);
            if (EditorGUI.EndChangeCheck())
            {
                m_TensionData.SetStretchIntensity(value);
            }
        }
        {
            EditorGUI.BeginChangeCheck();
            float value = EditorGUILayout.Slider(m_StretchLimitProp.displayName, m_StretchLimitProp.floatValue, 0, 1);
            if (EditorGUI.EndChangeCheck())
            {
                m_TensionData.SetStretchLimit(value);
            }
        }
        return hasChanged;
    }
}
