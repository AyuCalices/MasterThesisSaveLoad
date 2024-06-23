using System;
using UnityEditor;
using UnityEngine;

namespace SaveLoadCore.Editor
{
    [CustomEditor(typeof(Savable))]
    public class SavableEditor : UnityEditor.Editor
    {
        private SerializedProperty _sceneGuidProperty;
        private SerializedProperty _hierarchyPathProperty;
        private SerializedProperty _prefabSourceProperty;
        private SerializedProperty _currentSavableListProperty;
        private SerializedProperty _removedSavableListProperty;
        private SerializedProperty _savableReferenceListProperty;
        
        private static bool _showCurrentSavableList;
        private static bool _showRemovedSavableList;
        private static bool _showSavableReferenceList;

        private void OnEnable()
        {
            _sceneGuidProperty = serializedObject.FindProperty("serializeFieldSceneGuid");
            _hierarchyPathProperty = serializedObject.FindProperty("hierarchyPath");
            _prefabSourceProperty = serializedObject.FindProperty("prefabSource");
            _currentSavableListProperty = serializedObject.FindProperty("serializeFieldCurrentSavableList");
            _removedSavableListProperty = serializedObject.FindProperty("serializeFieldRemovedSavableList");
            _savableReferenceListProperty = serializedObject.FindProperty("serializeFieldSavableReferenceList");
        }
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            // Disable editing
            GUI.enabled = false;
            
            // Display the fields
            EditorGUILayout.PropertyField(_sceneGuidProperty);
            EditorGUILayout.PropertyField(_hierarchyPathProperty);
            EditorGUILayout.PropertyField(_prefabSourceProperty);
            ComponentContainerListLayout(_currentSavableListProperty, "Current Savable List", ref _showCurrentSavableList);
            ComponentContainerListLayout(_removedSavableListProperty, "Removed Savable List", ref _showRemovedSavableList);
            
            // Enable editing back
            GUI.enabled = true;
            
            SavableReferenceListPropertyLayout(_savableReferenceListProperty, "Save References", ref _showSavableReferenceList, true);

            serializedObject.ApplyModifiedProperties();
        }

        private void ComponentContainerListLayout(SerializedProperty serializedProperty, string layoutName, ref bool foldout)
        {
            foldout = EditorGUILayout.Foldout(foldout, layoutName);
            if (!foldout) return;
            
            EditorGUI.indentLevel++;
                
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
            // Headers
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Component", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Guid", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
                
            for (var i = 0; i < serializedProperty.arraySize; i++)
            {
                var elementProperty = serializedProperty.GetArrayElementAtIndex(i);
                var componentProperty = elementProperty.FindPropertyRelative("component");
                var pathProperty = elementProperty.FindPropertyRelative("guid");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(componentProperty, GUIContent.none);
                EditorGUILayout.PropertyField(pathProperty, GUIContent.none);
                EditorGUILayout.EndHorizontal();
            }
                
            EditorGUILayout.EndVertical();
                
            EditorGUI.indentLevel--;
        }

        private void SavableReferenceListPropertyLayout(SerializedProperty serializedProperty, string layoutName,
            ref bool foldout, bool componentEditable = false, bool guidEditable = false)
        {
            foldout = EditorGUILayout.Foldout(foldout, layoutName);
            if (!foldout) return;
            
            EditorGUI.indentLevel++;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Headers
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Component", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Guid", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            for (var i = 0; i < serializedProperty.arraySize; i++)
            {
                var elementProperty = serializedProperty.GetArrayElementAtIndex(i);
                var componentProperty = elementProperty.FindPropertyRelative("component");
                var pathProperty = elementProperty.FindPropertyRelative("guid");

                EditorGUILayout.BeginHorizontal();
                EditableGUILayoutAction(componentEditable, () => EditorGUILayout.PropertyField(componentProperty, GUIContent.none));
                EditableGUILayoutAction(guidEditable, () => EditorGUILayout.PropertyField(pathProperty, GUIContent.none));

                // Add a button to remove the element
                if (GUILayout.Button("Remove", GUILayout.Width(60)))
                {
                    serializedProperty.DeleteArrayElementAtIndex(i);
                }

                EditorGUILayout.EndHorizontal();
            }

            // Add button to add new element
            if (GUILayout.Button("Add Component"))
            {
                var newIndex = serializedProperty.arraySize;
                serializedProperty.InsertArrayElementAtIndex(newIndex);

                var newElementProperty = serializedProperty.GetArrayElementAtIndex(newIndex);
                var newComponentProperty = newElementProperty.FindPropertyRelative("component");
                var newPathProperty = newElementProperty.FindPropertyRelative("guid");

                // Initialize new element properties if necessary
                if (newComponentProperty != null) newComponentProperty.objectReferenceValue = null;
                if (newPathProperty != null) newPathProperty.stringValue = string.Empty;
            }

            EditorGUILayout.EndVertical();

            EditorGUI.indentLevel--;
        }

        private void EditableGUILayoutAction(bool isEditable, Action action)
        {
            var currentlyEditable = GUI.enabled;
            GUI.enabled = isEditable;
            action.Invoke();
            GUI.enabled = currentlyEditable;
        }
    }
}
