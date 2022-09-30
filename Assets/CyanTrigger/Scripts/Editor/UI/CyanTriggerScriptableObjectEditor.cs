
using System;
using UnityEditor;

namespace CyanTrigger
{
    [CustomEditor(typeof(CyanTriggerScriptableObject))]
    public class CyanTriggerScriptableObjectEditor : Editor
    {
        private CyanTriggerScriptableObject _cyanTriggerScriptableObject;
        private CyanTriggerSerializableInstanceEditor _editor;
        
        private void OnEnable()
        {
            _cyanTriggerScriptableObject = (CyanTriggerScriptableObject)target;
            var triggerInstance = _cyanTriggerScriptableObject.triggerInstance;
            var instanceProperty = serializedObject.FindProperty(nameof(CyanTriggerScriptableObject.triggerInstance));

            _editor = new CyanTriggerSerializableInstanceEditor(instanceProperty, triggerInstance, this);
        }

        public override void OnInspectorGUI()
        {
            _editor.OnInspectorGUI();
        }

        private void OnDisable()
        {
            _editor.Dispose();
        }
    }
}