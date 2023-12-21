using System.Text;
using UnityEditor;
using UnityEngine;

namespace ImGuiNET.Unity.Editor
{
    [CustomEditor(typeof(DearImGui))]
    class DearImGuiEditor : UnityEditor.Editor
    {
        private SerializedProperty doGlobalLayout;

        private SerializedProperty guiCamera;
        private SerializedProperty renderFeature;

        private SerializedProperty rendererType;
        private SerializedProperty platform;

        private SerializedProperty initialConfiguration;
        private SerializedProperty fontList;
        private SerializedProperty fontAtlasConfiguration;
        private SerializedProperty iniSettings;

        private SerializedProperty shaders;
        private SerializedProperty style;
        private SerializedProperty cursorShapes;

        private readonly StringBuilder messages = new StringBuilder();

        private void OnEnable() {
            doGlobalLayout = serializedObject.FindProperty("doGlobalLayout");
            guiCamera = serializedObject.FindProperty("guiCamera");
            renderFeature = serializedObject.FindProperty("renderFeature");
            rendererType = serializedObject.FindProperty("rendererType");
            platform = serializedObject.FindProperty("platformType");
            initialConfiguration = serializedObject.FindProperty("initialConfiguration");
            fontList = serializedObject.FindProperty("fontList");
            fontAtlasConfiguration = serializedObject.FindProperty("fontAtlasConfiguration");
            iniSettings = serializedObject.FindProperty("iniSettings");
            shaders = serializedObject.FindProperty("shaders");
            style = serializedObject.FindProperty("style");
            cursorShapes = serializedObject.FindProperty("cursorShapes");
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();

            CheckRequirements();

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(doGlobalLayout);

            if (RenderUtils.IsUsingURP()) {
                EditorGUILayout.PropertyField(renderFeature);
            }

            EditorGUILayout.PropertyField(guiCamera);
            EditorGUILayout.PropertyField(rendererType);
            EditorGUILayout.PropertyField(platform);
            EditorGUILayout.PropertyField(initialConfiguration);
            EditorGUILayout.PropertyField(fontList);
            EditorGUILayout.PropertyField(fontAtlasConfiguration);
            EditorGUILayout.PropertyField(iniSettings);
            EditorGUILayout.PropertyField(shaders);
            EditorGUILayout.PropertyField(style);
            EditorGUILayout.PropertyField(cursorShapes);

            var changed = EditorGUI.EndChangeCheck();
            if (changed) {
                serializedObject.ApplyModifiedProperties();
            }

            if (!Application.isPlaying) {
                return;
            }

            var reload = GUILayout.Button("Reload");
            if (changed || reload) {
                (target as DearImGui)?.Reload();
            }
        }

        private void CheckRequirements() {
            messages.Clear();

            if (guiCamera.objectReferenceValue == null) {
                messages.AppendLine("Must assign a Camera.");
            }

            if (RenderUtils.IsUsingURP() && renderFeature.objectReferenceValue == null) {
                messages.AppendLine("Must assign a RenderFeature when using the URP.");
            }

            if (!Platform.IsAvailable((Platform.Type)platform.enumValueIndex)) {
                messages.AppendLine("Platform not available.");
            }

            if (messages.Length > 0) {
                EditorGUILayout.HelpBox(messages.ToString(), MessageType.Error);
            }
        }
    }
}