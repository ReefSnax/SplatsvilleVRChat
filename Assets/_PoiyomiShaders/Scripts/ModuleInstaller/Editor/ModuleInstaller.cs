#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.IO;
using UnityEngine.UI;
using System;
using System.Text;
using UnityEngine.Assertions;
using System.Linq;
using System.Text.RegularExpressions;
using Poiyomi.ModularShaderSystem;

//using Poiyomi.ModularShaderSystem;

// PoiyomiPatreon.Scripts.poi_tools.Editor

namespace Poiyomi.AngryLabs
{
    [ExecuteInEditMode]
    public class ModuleInstaller : EditorWindow
    {
        [MenuItem("Poi/Module Installer")]
        static void Init()
        {
            //var window =  GetWindowWithRect<BlendshapeToTextures>(new Rect(0, 0, 200, 500));
            var window = GetWindow(typeof(ModuleInstaller));
            window.minSize = new Vector2(250, 300);
            window.Show();
        }

        private static void ShowWindow()
        {
            var window = GetWindow<ModuleInstaller>();
            window.titleContent = new GUIContent("Module Installer");
            window.Show();

            RefreshShaderList();
        }

        string CopyName { get; set; }
        string NewID { get; set; } = ".poiyomi/User Modules/";

        string OutputPath;

        static List<ModularShader> _shaders;
        static List<Shader> _compiledShaders;

        static IEnumerable<T> FindAssetsByType<T>(string searchFolder = null)
        {
            AssetDatabase.Refresh();
            string[] guids;
            if(searchFolder != null)
            {
                guids  = AssetDatabase.FindAssets($"t:{typeof(T).ToString().Replace("UnityEngine.", "")}", new string[] { searchFolder });
            }
            else
            {
                guids = AssetDatabase.FindAssets($"t:{typeof(T).ToString().Replace("UnityEngine.", "")}");
            }

            var paths = guids.Select(x => AssetDatabase.GUIDToAssetPath(x)).ToList();

            var oassets = paths
                .Select(path => AssetDatabase.LoadMainAssetAtPath(path))
                .Where(x => x != null)
                .ToList()
                ;

            List<T> assets = new List<T>(oassets.Cast<T>());
            return assets;
        }

        ModularShader _activeShader;
        ModularShader ActiveShader
        {
            get
            {
                return _activeShader;
            }
            set
            {
                if(ActiveShader == value)
                {
                    return;
                }

                _activeShader = value;
            }
        }

        /// <summary>
        /// List of modules to install
        /// </summary>
        List<ShaderModule> ToInstall = new List<ShaderModule>();

        bool AlreadyInstalled
        {
            get
            {
                if (ToInstall == null) return false;

                var modules = ActiveShader.BaseModules;

                var hash = new HashSet<string>(ToInstall.Select(x=>x?.Name).Where(x=>x!=null));

                return modules.Any(x => hash.Contains(x?.Name));
            }
        }

        bool ValidCopyName
        {
            get
            {
                if (_shaders == null) RefreshShaderList();
                return !_shaders.Any(x => x.Name == CopyName) && ! string.IsNullOrEmpty(CopyName);
            }
        }

        bool ValidNewID
        {
            get
            {
                if (_compiledShaders == null) RefreshShaderList();

                if (NewID.EndsWith("/"))
                {
                    return false;
                }

                Regex alreadyExists = new Regex($@".*{NewID}$");
                return !_compiledShaders.Any(x => alreadyExists.IsMatch(x.name)) && ! string.IsNullOrEmpty(NewID) ;
            }
        }

        Vector2 ScrollPosition { get; set; }

        bool _refreshing;
        private void OnFocus()
        {
            RefreshShaderList();
        }

        void OnGUI()
        {
            GUILayout.ExpandWidth(true);

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField($"By: AngriestSCV");


                var sty = new GUIStyle(GUI.skin.button)
                {
                    padding = new RectOffset()
                    {
                        bottom = 8,
                        top = 8,
                        left = 25,
                        right = 25,
                    },
                    
                };

                if (GUILayout.Button("angriestscv.gumroad.com", sty))
                {
                    Application.OpenURL("https://angriestscv.gumroad.com");
                }
            }

            using(var sv = new GUILayout.ScrollViewScope(ScrollPosition))
            using (new GUILayout.VerticalScope())
            {
                ScrollPosition = sv.scrollPosition;

                if (_shaders == null || _compiledShaders == null)
                {
                    RefreshShaderList();
                }

                EditorGUILayout.LabelField($""); // provides the perfect amount of vertical space
                ActiveShader = (ModularShader) EditorGUILayout.ObjectField("Shader to install to", ActiveShader, typeof(ModularShader), true);

                if (ActiveShader == null)
                {
                    this.ShowNotification(new GUIContent("A Modular Shader target is required"));
                    return;
                }
                else
                {
                    RemoveNotification();
                }

                GUILayout.Space(20);

                CopyName = EditorGUILayout.TextField("Modular Shader Name", CopyName);
                NewID = EditorGUILayout.TextField("Output Shader Name", NewID);

                OutputPath = EditorGUILayout.TextField("Output Path", OutputPath);
                //EditorGUILayout.LabelField("Output Path", OutputPath);
                if (GUILayout.Button("Set Output Path"))
                {
                    string startPath = OutputPath;
                    if (!Directory.Exists(startPath??""))
                    {
                        startPath = "Assets";
                    }
                    OutputPath = EditorUtility.OpenFolderPanel("Output Path Picker", startPath, "");
                    if (OutputPath.StartsWith(Application.dataPath))
                    {
                        OutputPath = "Assets" + OutputPath.Substring(Application.dataPath.Length);
                    }
                    else
                    {
                        OutputPath = "Assets/";
                    }

                    if(!Directory.Exists(OutputPath))
                    {
                        OutputPath = "Assets/";
                    }

                }

                GUILayout.Space(20);
                using (new GUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Modules to install");

                    if (GUILayout.Button("+"))
                    {
                        ToInstall.Add(null);
                    }
                    else if(GUILayout.Button("-"))
                    {
                        if(ToInstall.Count != 0)
                        {
                            ToInstall.RemoveAt(ToInstall.Count - 1);
                        }
                        else
                        {
                            Debug.Log("You are module-less.");
                        }
                    }
                }

                for(int i=0; i<ToInstall.Count; i++)
                {
                    ToInstall[i] = (ShaderModule) EditorGUILayout.ObjectField("", ToInstall[i], typeof(ShaderModule), true);
                }

                GUILayout.Space(20);
                if (!ValidCopyName || !ValidNewID)
                {
                    if (string.IsNullOrEmpty(CopyName))
                    {
                        GUILayout.Label("The output Modular Shader name is required.");
                    }
                    else if (!ValidCopyName)
                    {
                        GUILayout.Label("The Modular Shader asset already exists.");
                        if (GUILayout.Button("Delete it/them"))
                        {
                            DeleteModularShaderByName(CopyName);
                            RefreshShaderList();
                        }
                    }

                    if (string.IsNullOrEmpty(NewID))
                    {
                        GUILayout.Label("A new shader name is required");
                    }
                    else if (NewID.EndsWith("/"))
                    {
                        GUILayout.Label("'/' is not a valid final character");
                    }
                    else if (!ValidNewID)
                    {
                        GUILayout.Label("The shader name is already used.");

                        if (GUILayout.Button("Delete it"))
                        {
                            DeleteShaderByName(NewID);
                            RefreshShaderList();
                        }
                    }
                    else
                    {
                        GUILayout.Label("Valid NewID");
                    }
                }
                else if(ToInstall.All(x=>x==null))
                {
                    GUILayout.Label("There are no modules to install");
                }
                else if(string.IsNullOrEmpty(OutputPath))
                {
                    GUILayout.Label("An output path is required");
                }
                else if (!Directory.Exists(OutputPath))
                {
                    GUILayout.Label("The given output directory does not exist");
                }
                else 
                {

                    if (AlreadyInstalled)
                    {
                        GUILayout.Label("At least one of the modules to install is already in the active shader. Duplicates will not be reinstalled");
                    }

                    if (GUILayout.Button("Install Modules"))
                    {
                        Install();
                    }
                }
            }
        }

        private void DeleteModularShaderByName(string name)
        {
            AssetDatabase.Refresh();
            string[] guids;
            guids = AssetDatabase.FindAssets($"t:{typeof(ModularShader).ToString().Replace("UnityEngine.", "")}");
            
            IEnumerable<string> paths = guids.Select(x => AssetDatabase.GUIDToAssetPath(x)).ToList();
            var assets = paths
                .Select(path => new { Asset = AssetDatabase.LoadMainAssetAtPath(path) as ModularShader, Path = path })
                .Where(x => x.Asset != null)
                .Where(x => x.Asset.Name == name)
                .ToList();

            var toDel = assets
                .Select(x => x.Path)
                .ToList()
                ;

            foreach(string path in toDel)
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        private void DeleteShaderByName(string name)
        {
            AssetDatabase.Refresh();
            string[] guids;
            guids = AssetDatabase.FindAssets($"t:{typeof(Shader).ToString().Replace("UnityEngine.", "")}");
            
            IEnumerable<string> paths = guids.Select(x => AssetDatabase.GUIDToAssetPath(x)).ToList();
            var assets = paths
                .Select(path => new { Asset = AssetDatabase.LoadMainAssetAtPath(path) as Shader, Path = path })
                .Where(x => x.Asset != null)
                .ToList();

            Regex re = new Regex($@".*{name}$");

            string delAsset = assets
                .Where(x => re.IsMatch(x.Asset.name))
                .Select(x => x.Path)
                .FirstOrDefault()
                ;

            AssetDatabase.DeleteAsset(delAsset);
        }

        private void Install()
        {
            RefreshShaderList();
            if(!ValidCopyName || !ValidNewID)
            {
                return;
            }

            ModularShader shader = new ModularShader()
            {
                AdditionalModules = ActiveShader.AdditionalModules.ToList(),
                AdditionalSerializedData = ActiveShader.AdditionalSerializedData,
                Author = ActiveShader.Author,
                BaseModules = ActiveShader.BaseModules.Where(x=>x!=null).ToList(),
                CustomEditor = ActiveShader.CustomEditor,
                Description = ActiveShader.Description,
                hideFlags = ActiveShader.hideFlags,
                Name = CopyName,
                Properties = ActiveShader.Properties.ToList(),
                ShaderTemplate = ActiveShader.ShaderTemplate,
                ShaderPropertiesTemplate = ActiveShader.ShaderPropertiesTemplate,
                UseTemplatesForProperties = ActiveShader.UseTemplatesForProperties,
                Version = ActiveShader.Version,
                Id = ActiveShader.Id + "AudioBump",
                ShaderPath = NewID,
                LockBaseModules = ActiveShader.LockBaseModules,
                LastGeneratedShaders = new List<Shader>(),
            };

            string savePath = $"{OutputPath}/{CopyName}.asset";
            Debug.Log($"Creating new asset at [{savePath}]");
            AssetDatabase.CreateAsset(shader, savePath);

            var installedNames = new HashSet<string>(shader.BaseModules.Select(x => x.Name).Where(x => x != null));

            foreach(ShaderModule mod in ToInstall)
            {
                if (mod == null) continue;
                if (installedNames.Contains(mod.Name)) continue;

                installedNames.Add(mod.Name);
                shader.BaseModules.Add(mod);
            }

            AssetDatabase.SaveAssets();

            RefreshShaderList();
        }

        ShaderModule FindModuleById(string name, string searchPath)
        {
            var modules = FindAssetsByType<ShaderModule>(searchPath);

            return modules.FirstOrDefault(x => x?.Id == name);
        }
        

        private static void RefreshShaderList()
        {
            _shaders = FindAssetsByType<ModularShader>().ToList();
            _compiledShaders = FindAssetsByType<Shader>().ToList();
        }

    }
}
#endif
