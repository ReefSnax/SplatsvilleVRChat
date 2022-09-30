using UnityEngine;
using UnityEditor;

namespace CyanTrigger
{
    public static class CyanTriggerResourceManager
    {
        private const string UdonResourcesPrefabLocation = "Assets/CyanTrigger/Resources/Prefabs/CyanTriggerResources.prefab";

        private static CyanTriggerResources _cyanTriggerResources;
        public static CyanTriggerResources CyanTriggerResources
        {
            get
            {
                if (_cyanTriggerResources == null)
                {
                    _cyanTriggerResources = Object.FindObjectOfType<CyanTriggerResources>();
                    if (_cyanTriggerResources == null)
                    {
                        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UdonResourcesPrefabLocation);
                        GameObject resources = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                        _cyanTriggerResources = resources.GetComponent<CyanTriggerResources>();
                    }
                }

                return _cyanTriggerResources;
            }
        }
    }
}