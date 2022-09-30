using System;
using UnityEditor;
using UnityEngine;

namespace CyanTrigger
{
    public class CyanTriggerSettingsData : ScriptableObject
    {
        public bool actionDetailedView;
    }

    public static class CyanTriggerSettings
    {
        private const string SettingsName = "Settings/CyanTriggerSettings";
        private const string SettingsPathName = "Assets/CyanTrigger/Resources/";
        
        private static CyanTriggerSettingsData _instance;
        public static CyanTriggerSettingsData Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = LoadSettings();
                }
                return _instance;
            }
        }
        
        private static CyanTriggerSettingsData LoadSettings()
        {
            CyanTriggerSettingsData settings = Resources.Load<CyanTriggerSettingsData>(SettingsName);

            if (settings == null)
            {
                settings = new CyanTriggerSettingsData();
                Debug.LogError("Could not load CyanTrigger settings!");

                //settings = CreateAndImportSettings();
            }
            
            return settings;
        }

        private static CyanTriggerSettingsData CreateAndImportSettings()
        {
            string path = SettingsPathName + SettingsName + ".asset";
            CyanTriggerSettingsData settings = ScriptableObject.CreateInstance<CyanTriggerSettingsData>();
            AssetDatabase.CreateAsset(settings, path);
            AssetDatabase.ImportAsset(path);
            return settings;
        }
    }
}

