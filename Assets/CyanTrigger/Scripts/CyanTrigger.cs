using System;
using UnityEngine;
using VRC.Udon;

namespace CyanTrigger
{
    [DisallowMultipleComponent]
    public class CyanTrigger : MonoBehaviour
    {
        public CyanTriggerSerializableInstance triggerInstance;

#if UNITY_EDITOR
        public void Reset()
        {
            bool dirty = false;
            if (triggerInstance == null)
            {
                triggerInstance = CyanTriggerSerializableInstance.CreateInstance();
                dirty = true;
            }
            
            if (triggerInstance.udonBehaviour == null)
            {
                triggerInstance.udonBehaviour = gameObject.AddComponent<UdonBehaviour>();
                dirty = true;
            }

            if (dirty)
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }

        public void Verify()
        {
            // Verify that trigger data is valid.
            // When importing CyanTriggers, any compile errors will cause prefabs to import without data.
            // This checks both that data is missing and the object is part of a prefab. If both are true, reimport it. 
            if (triggerInstance == null)
            {
                string path = UnityEditor.PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);
                if (path != null)
                {
                    UnityEditor.AssetDatabase.ImportAsset(path);
                }
            }
        }
#endif


        private void OnDrawGizmosSelected()
        {
            var data = triggerInstance?.triggerDataInstance;
            if (data == null || data.variables == null || data.events == null)
            {
                return;
            }
            
            foreach (var variable in data.variables)
            {
                DrawLineToObject(variable);
            }
            
            foreach (var trigEvent in data.events)
            {
                DrawLineToObjects(trigEvent.eventInstance);

                foreach (var action in trigEvent.actionInstances)
                {
                    DrawLineToObjects(action);
                }
            }
        }

        private void DrawLineToObjects(CyanTriggerActionInstance actionInstance)
        {
            foreach (var input in actionInstance.inputs)
            {
                DrawLineToObject(input);
            }

            if (actionInstance.multiInput != null)
            {
                foreach (var input in actionInstance.multiInput)
                {
                    DrawLineToObject(input);
                }
            }
        }

        private void DrawLineToObject(CyanTriggerActionVariableInstance variableInstance)
        {
            if (variableInstance.isVariable || variableInstance.data?.obj == null)
            {
                return;
            }

            if (variableInstance.data.obj.GetType().IsSubclassOf(typeof(Component)))
            {
                Component component = variableInstance.data.obj as Component;
                if (component != null && component.gameObject != gameObject)
                {
                    Gizmos.DrawLine(transform.position, component.transform.position);
                }
            }
            
            if (variableInstance.data.obj is GameObject otherGameObject && 
                otherGameObject != null &&
                otherGameObject != gameObject)
            {
                Gizmos.DrawLine(transform.position, otherGameObject.transform.position);
            }
        }
    }

    // TODO delete as inline in Udon Behaviour did not work as expected...
    public class CyanTriggerScriptableObject : ScriptableObject
    {
        public CyanTriggerSerializableInstance triggerInstance;
        
#if UNITY_EDITOR  
        public void Reset()
        {
            if (triggerInstance == null)
            {
                triggerInstance = CyanTriggerSerializableInstance.CreateInstance();
            }
            
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
    
    [Serializable]
    public class CyanTriggerSerializableInstance
    {
        public float proximity = 2f;
        public string interactText = "Use";
        public CyanTriggerDataInstance triggerDataInstance; // TODO encode this directly instead of encoding each children individually?
        
        [HideInInspector]
        public UdonBehaviour udonBehaviour;

        public static CyanTriggerSerializableInstance CreateInstance()
        {
            var instance = new CyanTriggerSerializableInstance
            {
                triggerDataInstance = new CyanTriggerDataInstance
                {
                    version = CyanTriggerDataInstance.DataVersion,
                    events =  new CyanTriggerEvent[0],
                    variables = new CyanTriggerVariable[0],
                    programSyncMode = CyanTriggerProgramSyncMode.ManualWithAutoRequest,
                }
            };
            return instance;
        }

        private CyanTriggerSerializableInstance() { }
    }
}