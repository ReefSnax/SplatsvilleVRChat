using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using VRC.SDKBase.Editor.BuildPipeline;
using VRC.Udon;
using Object = UnityEngine.Object;

namespace CyanTrigger
{
    [InitializeOnLoad]
    public class CyanTriggerSerializerManager : UnityEditor.AssetModificationProcessor, IVRCSDKBuildRequestedCallback
    {
        private static readonly List<PrefabStage> OpenedPrefabStages = new List<PrefabStage>();
        
        public int callbackOrder => 1;
        
        static CyanTriggerSerializerManager()
        {
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            EditorSceneManager.sceneOpened += SceneOpened;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            
            PrefabStage.prefabSaving += PrefabStageOnprefabSaving;
            PrefabStage.prefabStageOpened += PrefabStageOnprefabStageOpened;
            PrefabStage.prefabStageClosing += PrefabStageOnprefabStageClosing;
            
            // TODO handle when assemblies reload and OpenedPrefabStages is lost
        }

        public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
        {
            if (requestedBuildType == VRCSDKRequestedBuildType.Avatar)
            {
                // Why are you building an avatar in this project?
                return true;
            }

            return RecompileAllTriggers(true);
        }
        
        public static string[] OnWillSaveAssets(string[] paths)
        {
            Profiler.BeginSample("CyanTrigger.OnWillSaveAssets");
            bool isSavingScene = false;
        
            // TODO check prefab saving?
            foreach (string path in paths)
            {
                if (Path.GetExtension(path).Equals(".unity"))
                {
                    isSavingScene = true;
                    break;
                }
            }
        
            if (isSavingScene)
            {
                RecompileAllTriggers();
            }
            
            Profiler.EndSample();
        
            return paths;
        }

        private static void SceneOpened(Scene scene, OpenSceneMode mode)
        {
            RecompileAllTriggers(true);
        }
        
        private static void PrefabStageOnprefabSaving(GameObject obj)
        {
            var triggers = GetAllOfTypeFromPrefabScenes<CyanTrigger>();
            VerifySceneTriggers(triggers);
            
            // TODO this is not enough to cover all possible prefab situations.
            // Prefab variants and nested prefabs may not get proper compilation. This needs another method for that.
            //CyanTriggerSerializedProgramManager.Instance.ApplyTriggerPrograms(triggers);
        }

        private static void PrefabStageOnprefabStageOpened(PrefabStage prefabStage)
        {
            OpenedPrefabStages.Add(prefabStage);
            
            var triggers = GetAllOfTypeFromPrefabScenes<CyanTrigger>();
            VerifySceneTriggers(triggers);
        }
        
        private static void PrefabStageOnprefabStageClosing(PrefabStage prefabStage)
        {
            OpenedPrefabStages.Remove(prefabStage);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // Exiting edit mode to ensure that everything is compiled before play.
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                RecompileAllTriggers(true);
            }
        }

        private static void OnHierarchyChanged()
        {
            if (IsPlaying())
            {
                return;
            }
            //Debug.Log("Hierarchy Changed!");

            Profiler.BeginSample("CyanTrigger.OnHierarchyChanged");

            VerifySceneTriggers();
            
            Profiler.EndSample();
        }

        public static bool RecompileAllTriggers(bool force = false)
        {
            if (IsPlaying())
            {
                return true;
            }

            bool ret = true;
            Profiler.BeginSample("CyanTrigger.RecompileAllTriggers");

            try
            {
                AssetDatabase.StartAssetEditing();

                VerifyScene();
                
                var triggers = GetAllOfTypeFromAllScenes<CyanTrigger>();
                VerifySceneTriggers(triggers, true);
                
                CyanTriggerSerializedProgramManager.Instance.ApplyTriggerPrograms(triggers, force);
            }
            catch (Exception e)
            {
                ret = false;
                Debug.LogError("Error while compiling all triggers");
                Debug.LogError(e);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            
            Profiler.EndSample();

            return ret;
        }

        private static void VerifyScene()
        {
            // Remove CyanTriggerResources as the are currently not needed. 
            List<CyanTriggerResources> resources = GetAllOfTypeFromAllScenes<CyanTriggerResources>();
            foreach (var resource in resources)
            {
                if (resource != null && resource.gameObject != null)
                {
                    Object.DestroyImmediate(resource.gameObject);
                }
            }
        }
        
        private static void VerifySceneTriggers()
        {
            if (IsPlaying())
            {
                return;
            }

            if (OpenedPrefabStages.Count > 0)
            {
                var triggers = GetAllOfTypeFromPrefabScenes<CyanTrigger>();
                VerifySceneTriggers(triggers);
            }
            else
            {
                var triggers = GetAllOfTypeFromAllScenes<CyanTrigger>();
                VerifySceneTriggers(triggers);
            }
        }

        private static void VerifySceneTriggers(IEnumerable<CyanTrigger> triggers, bool fullVerification = false)
        {
            if (IsPlaying())
            {
                return;
            }

            Profiler.BeginSample("CyanTrigger.VerifySceneTriggers");
            try 
            {
                // AssetDatabase.StartAssetEditing();

                foreach (var trigger in triggers)
                {
                    VerifyTrigger(trigger, fullVerification);
                }

                var sceneUdonBehaviours = GetAllOfTypeFromAllScenes<UdonBehaviour>();
                foreach (var udon in sceneUdonBehaviours)
                {
                    if (!(udon.programSource is CyanTriggerProgramAsset))
                    {
                        continue;
                    }

                    CyanTrigger trigger = udon.GetComponent<CyanTrigger>();
                    if (trigger == null || trigger.triggerInstance.udonBehaviour != udon)
                    {
                        GameObject obj = udon.gameObject;
                        //Debug.Log("Setting object dirty after deleting udon/trigger: " + VRC.Tools.GetGameObjectPath(obj));
                        EditorUtility.SetDirty(obj);
                        Undo.RecordObject(obj, "Removing unused udon");
                        
                        UnityEngine.Object.DestroyImmediate(udon);
                        
                        EditorSceneManager.MarkSceneDirty(obj.scene);
                        if (PrefabUtility.IsPartOfPrefabInstance(obj))
                        {
                            PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
                        }
                    }
                }
            }
            finally
            {
                // TODO resolve issue where inspecting assets causes this to continually fire hierarchy changes.
                // AssetDatabase.StopAssetEditing();
                Profiler.EndSample();
            }
        }

        private static void VerifyTrigger(CyanTrigger trigger, bool fullVerification)
        {
            trigger.Verify();
            trigger.hideFlags = HideFlags.DontSaveInBuild;
            
            if (trigger.triggerInstance == null || trigger.triggerInstance.triggerDataInstance == null)
            {
                Debug.LogError("Trigger data is null!: "
                                 + VRC.Tools.GetGameObjectPath(trigger.gameObject));
                return;
            }
            
            Profiler.BeginSample("CyanTrigger.VerifyTrigger");
            
            Undo.RecordObject(trigger.gameObject, "Verifying Trigger");

            CyanTriggerSerializableInstance triggerInstance = trigger.triggerInstance;
            // Linked Udon is not on the same component
            if (triggerInstance.udonBehaviour != null && trigger.gameObject != triggerInstance.udonBehaviour.gameObject)
            {
                triggerInstance.udonBehaviour = null;
                
                EditorUtility.SetDirty(trigger);
#if CYAN_TRIGGER_DEBUG
                //Debug.Log("Setting trigger dirty with wrong udon: " + VRC.Tools.GetGameObjectPath(trigger.gameObject));
                Debug.LogWarning("Trigger has UdonBehaviour on different object: "
                          + VRC.Tools.GetGameObjectPath(trigger.gameObject));
#endif
            }
            
            // Try getting UdonBehaviour if one already exists
            if (triggerInstance.udonBehaviour == null)
            {
#if CYAN_TRIGGER_DEBUG
                Debug.LogWarning("Trigger missing UdonBehaviour: "
                                 + VRC.Tools.GetGameObjectPath(trigger.gameObject));
#endif
                // find anything that had proper name
                UdonBehaviour[] udonBehaviours = trigger.GetComponents<UdonBehaviour>();
                UdonBehaviour behaviour = null;
                UdonBehaviour nullBehaviour = null;
                bool warnedDuplicate = false;
                foreach (var udonBehaviour in udonBehaviours)
                {
                    AbstractUdonProgramSource abstractProgram = udonBehaviour.programSource;
                    if (abstractProgram is CyanTriggerProgramAsset &&
                        abstractProgram.name.StartsWith(CyanTriggerSerializedProgramManager.SerializedUdonAssetNamePrefix))
                    {
                        if (behaviour == null)
                        {
                            behaviour = udonBehaviour;
                        }
                        else if (!warnedDuplicate)
                        {
#if CYAN_TRIGGER_DEBUG
                            Debug.LogWarning("Multiple UdonBehaviours with CyanTrigger programs. " 
                                             + VRC.Tools.GetGameObjectPath(trigger.gameObject));
#endif
                            warnedDuplicate = true;
                        }
                    }

                    if (abstractProgram == null)
                    {
                        nullBehaviour = udonBehaviour;
                    }
                }

                if (behaviour == null && nullBehaviour != null)
                {
                    behaviour = nullBehaviour;
                }
                
                if (behaviour != null)
                {
                    triggerInstance.udonBehaviour = behaviour;
                    
                    //Debug.Log("Setting trigger dirty with new udon: " + VRC.Tools.GetGameObjectPath(trigger.gameObject));
                    EditorUtility.SetDirty(trigger);
                }
                else
                {
                    trigger.Reset();
                }
            }
            
            Debug.Assert(triggerInstance.udonBehaviour != null, 
                "CyanTrigger UdonBehaviour is still null! "
                + VRC.Tools.GetGameObjectPath(trigger.gameObject));

            // TODO ignore prefabs and log warning suggesting the prefab be opened first!
            int prevVersion = trigger.triggerInstance?.triggerDataInstance?.version ?? 0;
            if (CyanTriggerVersionMigrator.MigrateTrigger(trigger.triggerInstance.triggerDataInstance))
            {
#if CYAN_TRIGGER_DEBUG
                Debug.Log("Migrated object from version " + prevVersion + " to version " +
                          trigger.triggerInstance.triggerDataInstance.version + ", " +
                          VRC.Tools.GetGameObjectPath(trigger.gameObject));
#endif
                // Clear public variable symbol table?
                UdonBehaviour udon = trigger.triggerInstance.udonBehaviour;
                if (udon != null)
                {
                    var publicVariables = udon.publicVariables;
                    if (publicVariables != null)
                    {
                        foreach (var symbol in new List<string>(publicVariables.VariableSymbols))
                        {
                            publicVariables.RemoveVariable(symbol);
                        }
                    }
                }
                
                //Debug.Log("Setting trigger dirty after migration: " + VRC.Tools.GetGameObjectPath(trigger.gameObject));
                EditorUtility.SetDirty(trigger);
            }

            // TODO figure out other things to verify here
            if (fullVerification)
            {
                var dataInstance = trigger.triggerInstance?.triggerDataInstance;
                if (dataInstance?.events != null)
                {
                    bool dirty = false;
                    foreach (var eventData in dataInstance.events)
                    {
                        dirty |= eventData.eventInstance.ValidateVariables();
                        foreach (var actionData in eventData.actionInstances)
                        {
                            dirty |= actionData.ValidateVariables();
                        }
                    }

                    if (dirty)
                    {
                        //Debug.Log("Setting trigger dirty after verification: " + VRC.Tools.GetGameObjectPath(trigger.gameObject));
                        EditorUtility.SetDirty(trigger);
                    }
                }
            }
            
            // 
            if (triggerInstance.udonBehaviour == null)
            {
                Profiler.EndSample();
                return;
            }
            
            if (IsPlaying() || !trigger.gameObject.scene.IsValid())
            {
                Profiler.EndSample();
                return;
            }

            {
                UdonBehaviour udonBehaviour = triggerInstance.udonBehaviour;
                if (udonBehaviour.programSource == null)
                {
                    udonBehaviour.programSource = CyanTriggerSerializedProgramManager.Instance.DefaultProgramAsset;
                    //Debug.Log("Setting udon dirty after setting default program: " + VRC.Tools.GetGameObjectPath(udonBehaviour.gameObject));
                    EditorUtility.SetDirty(udonBehaviour);
                }
            }

            Profiler.EndSample();
        }

        private static bool IsPlaying()
        {
            return EditorApplication.isPlaying;
        }


        private static List<T> GetAllOfTypeFromAllScenes<T>()
        {
            Profiler.BeginSample("CyanTrigger.GetAllOfTypeFromAllScenes");

            List<Scene> scenes = new List<Scene>();
            
            int countLoaded = SceneManager.sceneCount;
            for (int i = 0; i < countLoaded; i++)
            {
                // TODO Verify scene is not prefab scene? 
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid())
                {
                    continue;
                }

                scenes.Add(scene);
            }

            var components = GetAllOfTypeFromScenes<T>(scenes);
            
            Profiler.EndSample();
            
            return components;
        }
        
        private static List<T> GetAllOfTypeFromPrefabScenes<T>()
        {
            Profiler.BeginSample("CyanTrigger.GetAllOfTypeFromPrefabScenes");

            List<Scene> scenes = new List<Scene>();
            
            foreach (var prefabStage in OpenedPrefabStages)
            {
                Scene scene = prefabStage.scene;
                if (!scene.IsValid())
                {
                    continue;
                }

                scenes.Add(scene);
            }

            var components = GetAllOfTypeFromScenes<T>(scenes);
            
            Profiler.EndSample();
            
            return components;
        }

        private static List<T> GetAllOfTypeFromScenes<T>(IEnumerable<Scene> scenes)
        {
            List<T> components = new List<T>();

            foreach (var scene in scenes)
            {
                if (!scene.IsValid())
                {
                    continue;
                }

                GetAllOfTypeFromScene(scene, ref components);
            }
            
            return components;
        }

        private static void GetAllOfTypeFromScene<T>(Scene scene, ref List<T> components)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }
            
            List<GameObject> sceneObjects = new List<GameObject>();
            scene.GetRootGameObjects(sceneObjects);
            
            foreach (var obj in sceneObjects)
            {
                components.AddRange(obj.GetComponentsInChildren<T>(true));
            }
        }
    }
}

