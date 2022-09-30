using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace CyanTrigger
{
    public class CyanTriggerSerializedProgramManager : UnityEditor.AssetModificationProcessor
    {
        public const string SerializedUdonAssetNamePrefix = "CyanTrigger_";
        public const string SerializedUdonPath = "CyanTriggerSerialized";
        public const string DefaultProgramAssetGuid = "Empty";
        
        private static CyanTriggerSerializedProgramManager _instance;
        public static CyanTriggerSerializedProgramManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new CyanTriggerSerializedProgramManager();
                }

                return _instance;
            }
        }

        private readonly Dictionary<string, CyanTriggerProgramAsset> _programAssets =
            new Dictionary<string, CyanTriggerProgramAsset>();

        public CyanTriggerProgramAsset DefaultProgramAsset => _defaultProgramAsset;
        private CyanTriggerProgramAsset _defaultProgramAsset;

        public static string GetExpectedProgramName(string guid)
        {
            return SerializedUdonAssetNamePrefix + guid + ".asset";
        }

        public static CyanTriggerProgramAsset CreateTriggerProgramAsset(string guid)
        {
            DirectoryInfo directory = new DirectoryInfo(Application.dataPath + "/" + SerializedUdonPath);
            if (!directory.Exists)
            {
                directory.Create();
            }
            
            string assetPath = SerializedUdonPath + "/" + GetExpectedProgramName(guid);
            var programAsset = ScriptableObject.CreateInstance<CyanTriggerProgramAsset>();
            AssetDatabase.CreateAsset(programAsset, "Assets/" + assetPath);
            AssetDatabase.ImportAsset("Assets/" + assetPath);
            return AssetDatabase.LoadAssetAtPath<CyanTriggerProgramAsset>("Assets/" + assetPath);
        }
        
        public static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions options)
        {
            // Skip, since there is nothing to update
            if (_instance == null || _instance._programAssets.Count == 0)
            {
                return AssetDeleteResult.DidNotDelete;
            }
            
            string path = "Assets/" + SerializedUdonPath + "/" + SerializedUdonAssetNamePrefix;
            if (assetPath.Contains(path) && assetPath.EndsWith(".asset"))
            {
                int startIndex = assetPath.IndexOf(path, StringComparison.Ordinal) + path.Length;
                int len = assetPath.Length - 6 - startIndex;
                string guid = assetPath.Substring(startIndex, len);
                if (_instance._programAssets.ContainsKey(guid))
                {
                    _instance._programAssets.Remove(guid);
                }
            }
            
            return AssetDeleteResult.DidNotDelete;
        }
        
        private CyanTriggerSerializedProgramManager()
        {
            if (EditorApplication.isPlaying)
            {
                return;
            }

            LoadSerializedData();
        }

        private static IEnumerable<FileInfo> GetAllSerializedCyanTriggerFileInfo()
        {
            DirectoryInfo directory = new DirectoryInfo(Application.dataPath + "/" + SerializedUdonPath);
            if (!directory.Exists)
            {
                directory.Create();
            }
            
            foreach (var item in directory.EnumerateFiles())
            {
                if (!item.Extension.Equals(".asset"))
                {
                    continue;
                }
                
                yield return item;
            }
        }
        
        public void LoadSerializedData()
        {
            string defaultAsset = GetExpectedProgramName(DefaultProgramAssetGuid);
            string filePath = "Assets/" + SerializedUdonPath + "/";
            
            foreach (var item in GetAllSerializedCyanTriggerFileInfo())
            {
                string fileName = filePath + item.Name;
                var serializedTrigger = AssetDatabase.LoadAssetAtPath<CyanTriggerProgramAsset>(fileName);
                if (serializedTrigger == null)
                {
                    Debug.LogWarning("File was not a proper CyanTriggerProgramAsset: " + fileName);
                    continue;
                }

                if (item.Name == defaultAsset)
                {
                    _defaultProgramAsset = serializedTrigger;
                    continue;
                }

                // Handle cases where duplicates exist, eg collab
                if (_programAssets.ContainsKey(serializedTrigger.triggerHash))
                {
                    serializedTrigger.InvalidateData();
                }
                
                _programAssets.Add(serializedTrigger.triggerHash, serializedTrigger);
            }

            if (_defaultProgramAsset == null)
            {
                _defaultProgramAsset = CreateTriggerProgramAsset(DefaultProgramAssetGuid);
            }
            _defaultProgramAsset.SetCyanTriggerData(null, DefaultProgramAssetGuid);
        }

        public void ClearSerializedData()
        {
            _defaultProgramAsset = null;
            _programAssets.Clear();

            try
            {
                AssetDatabase.StartAssetEditing();
                
                string filePath = "Assets/" + SerializedUdonPath + "/";
                foreach (var item in GetAllSerializedCyanTriggerFileInfo())
                {
                    string fileName = filePath + item.Name;
                    var serializedTrigger = AssetDatabase.LoadAssetAtPath<CyanTriggerProgramAsset>(fileName);
                    if (serializedTrigger == null)
                    {
                        continue;
                    }

                    AssetDatabase.DeleteAsset(fileName);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }

        public void ApplyTriggerPrograms(List<CyanTrigger> triggers, bool force = false)
        {
            Profiler.BeginSample("CyanTriggerSerializedProgramManager.ApplyTriggerPrograms");

            CyanTriggerCompiler.PreBatchCompile();
            
            Dictionary<string, List<CyanTrigger>> hashToTriggers = new Dictionary<string, List<CyanTrigger>>();
            foreach (var trigger in triggers)
            {
                try
                {
                    string hash =
                        CyanTriggerInstanceDataHash.HashCyanTriggerInstanceData(trigger.triggerInstance
                            .triggerDataInstance);

                    // Debug.Log("Trigger hash "+ hash +" " +VRC.Tools.GetGameObjectPath(trigger.gameObject));
                    if (!hashToTriggers.TryGetValue(hash, out var triggerList))
                    {
                        triggerList = new List<CyanTrigger>();
                        hashToTriggers.Add(hash, triggerList);
                    }

                    triggerList.Add(trigger);
                }
                catch (Exception e)
                {
                    Debug.LogError("Failed to hash CyanTrigger on object: " + VRC.Tools.GetGameObjectPath(trigger.gameObject) +"\n" + e);
                }
            }

            foreach (string key in new List<string>(_programAssets.Keys))
            {
                var programAsset = _programAssets[key];
                if (programAsset == null)
                {
                    _programAssets.Remove(key);
                }

                // Ensure each asset is stored at the proper key
                if (programAsset.triggerHash != key)
                {
                    _programAssets.Remove(key);
                    _programAssets.Remove(programAsset.triggerHash);
                    _programAssets.Add(programAsset.triggerHash, programAsset);
                }
            }
            
            Queue<CyanTriggerProgramAsset> unusedAssets = new Queue<CyanTriggerProgramAsset>();
            foreach (var programAssetPair in _programAssets)
            {
                // Never work with default program
                if (programAssetPair.Value == DefaultProgramAsset)
                {
                    continue;
                }
                
                string hash = programAssetPair.Value.triggerHash;
                if (!hashToTriggers.ContainsKey(hash))
                {
                    unusedAssets.Enqueue(programAssetPair.Value);
                }
            }
            
            foreach (var triggerHash in hashToTriggers.Keys)
            {
                List<CyanTrigger> curTriggers = hashToTriggers[triggerHash];
                if (curTriggers.Count == 0)
                {
                    continue;
                }

                var firstTrigger = curTriggers[0];

                bool recompile = force;
                
                if (!_programAssets.TryGetValue(triggerHash, out var programAsset))
                {
                    // Pull an asset from Unused, or create a new one.
                    if (unusedAssets.Count > 0)
                    {
                        programAsset = unusedAssets.Dequeue();
                        _programAssets.Remove(programAsset.triggerHash);
                    }
                    else
                    {
                        // TODO figure out a better method here since guid is pretty arbitrary
                        programAsset = CreateTriggerProgramAsset(Guid.NewGuid().ToString());
                    }
                    
                    _programAssets.Add(triggerHash, programAsset);
                    recompile = true;
                }
                
                if (programAsset == DefaultProgramAsset)
                {
                    Debug.LogError("Trying to use default program asset for CyanTrigger!");
                    continue;
                }
                
                if (recompile)
                {
                    string prevKey = programAsset.triggerHash;
                    programAsset.SetCyanTriggerData(firstTrigger.triggerInstance.triggerDataInstance, triggerHash);
                    bool success = programAsset.CompileTrigger();
                    if (!success)
                    {
                        PrintError("Failed to compile CyanTrigger on objects: ", curTriggers);
                        
                        if (prevKey != null)
                        {
                            _programAssets.Remove(prevKey);
                        }
                        _programAssets.Remove(programAsset.triggerHash);
                        programAsset.InvalidateData();
                        _programAssets.Add(programAsset.triggerHash, programAsset);
                        
                        foreach (var trigger in curTriggers)
                        {
                            PairTriggerToProgram(trigger, programAsset, false);
                        }
                        
                        continue;
                    }

                    if (programAsset.warningMessages.Count > 0)
                    {
                        PrintWarning("CyanTriggers compiled with warnings: ", curTriggers);
                    }
                }

                if (triggerHash != programAsset.triggerHash)
                {
                    PrintError("CyanTrigger hash was not the expected hash after compiling! \"" + triggerHash +"\" vs \"" + programAsset.triggerHash +"\" for objects: ", curTriggers);
                    continue;
                }
                
                foreach (var trigger in curTriggers)
                {
                    PairTriggerToProgram(trigger, programAsset);
                }
            }
            
            CyanTriggerCompiler.PostBatchCompile();
            
            Profiler.EndSample();
        }

        private static void PairTriggerToProgram(CyanTrigger trigger, CyanTriggerProgramAsset programAsset, bool shouldApply = true)
        {
            try
            {
                var data = trigger.triggerInstance;
                var udon = trigger.triggerInstance.udonBehaviour;

                if (data == null || udon == null || data.triggerDataInstance == null)
                {
                    Debug.LogError("Could not apply program for CyanTrigger: " +
                              VRC.Tools.GetGameObjectPath(trigger.gameObject));
                    return;
                }

                bool dirty = false;
                if (udon.programSource != programAsset)
                {
                    udon.programSource = programAsset;
                    dirty = true;
                }

                if (shouldApply)
                {
                    programAsset.ApplyCyanTriggerToUdon(data, udon, ref dirty);
                }
                else
                {
                    // Clear all public variables on the udon behaviour
                    CyanTriggerProgramAsset.ClearPublicUdonVariables(udon, ref dirty);
                }

                if (dirty)
                {
                    // TODO check for prefab applying?
                    EditorUtility.SetDirty(udon);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to apply program for CyanTrigger: " +
                               VRC.Tools.GetGameObjectPath(trigger.gameObject));
                Debug.LogError(e);
            }
        }

        private static void PrintError(string message, List<CyanTrigger> triggers)
        {
            Debug.LogError(message + ObjectPathsString(triggers));
        }
        
        private static void PrintWarning(string message, List<CyanTrigger> triggers)
        {
            Debug.LogWarning(message + ObjectPathsString(triggers));
        }

        private static string ObjectPathsString(List<CyanTrigger> triggers)
        {
            string objectPaths = "";
            foreach (var trigger in triggers)
            {
                objectPaths += VRC.Tools.GetGameObjectPath(trigger.gameObject) + "\n";
            }

            if (triggers.Count > 1)
            {
                objectPaths = "<View full message to see all objects>\n" + objectPaths;
            }

            return objectPaths;
        }
    }
}

