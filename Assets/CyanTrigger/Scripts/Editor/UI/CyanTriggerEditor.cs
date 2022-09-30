using UnityEditor;
using UnityEngine;

namespace CyanTrigger
{
    [CustomEditor(typeof(CyanTrigger))]
    public class CyanTriggerEditor : Editor
    {
        private CyanTrigger _cyanTrigger;
        private CyanTriggerSerializableInstanceEditor _editor;
        
#if CYAN_TRIGGER_DEBUG
        private bool _showHash;
#endif
        
        private void OnEnable()
        {
            if (EditorApplication.isPlaying)
            {
                return;
            }
            
            _cyanTrigger = (CyanTrigger)target;
            _cyanTrigger.Verify();
            CreateEditor();

            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }
        
        private void OnDisable()
        {
            if (EditorApplication.isPlaying)
            {
                return;
            }
            
            DisposeEditor();
            
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void CreateEditor()
        {
            DisposeEditor();

            var triggerInstance = _cyanTrigger.triggerInstance;
            var instanceProperty = serializedObject.FindProperty(nameof(CyanTriggerScriptableObject.triggerInstance));

            _editor = new CyanTriggerSerializableInstanceEditor(instanceProperty, triggerInstance, this);
        }

        private void DisposeEditor()
        {
            _editor?.Dispose();
            _editor = null;
        }
        
        public override void OnInspectorGUI()
        {
            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.BeginVertical(new GUIStyle(EditorStyles.helpBox), GUILayout.MaxWidth(EditorGUIUtility.currentViewWidth - 30));
                
                EditorGUILayout.LabelField("Exit Playmode to Edit CyanTrigger.");
                EditorGUILayout.LabelField("This will be improved later.");
                
                EditorGUILayout.EndVertical();
                return;
            }

            if (_editor == null)
            {
                if (!CyanTriggerActionInfoHolder.GetActionInfoHolder(null, "Event_Custom").IsValid())
                {
                    return;
                }

                CreateEditor();
            }
            
            _editor.OnInspectorGUI();
            
            EditorGUILayout.BeginVertical(GUILayout.MaxWidth(EditorGUIUtility.currentViewWidth - 30));
            
            if (GUILayout.Button("Compile Triggers"))
            {
                CyanTriggerSerializerManager.RecompileAllTriggers(true);
            }
            
            CyanTriggerSettingsWindow.DrawHeader("CyanTrigger");
         
#if CYAN_TRIGGER_DEBUG   
            _showHash = EditorGUILayout.Foldout(_showHash, "Trigger Hash", true);
            if (_showHash)
            {
                EditorGUI.BeginDisabledGroup(true);

                EditorGUILayout.TextArea(
                    CyanTriggerInstanceDataHash.HashCyanTriggerInstanceData(_cyanTrigger.triggerInstance
                        .triggerDataInstance));
                EditorGUILayout.TextArea(
                    CyanTriggerInstanceDataHash.GetProgramUniqueStringForCyanTrigger(_cyanTrigger.triggerInstance
                        .triggerDataInstance));

                EditorGUI.EndDisabledGroup();
            }
#endif
            
            EditorGUILayout.EndVertical();
        }
        
        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            DisposeEditor();
        }
    }
}