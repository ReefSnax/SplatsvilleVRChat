using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Poiyomi.ModularShaderSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;
using Toggle = UnityEngine.UIElements.Toggle;

namespace PoiyomiPatreon.Scripts.poi_tools.Editor
{
    public class ModularShadersGeneratorElement : VisualElement
    {
        private bool _isSelected;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isErrored) return;
                _isSelected = value;
                _toggle.SetValueWithoutNotify(_isSelected);
            }
        }

        public ModularShader Shader { get; set; }

        private readonly Toggle _toggle;
        private readonly bool _isErrored;
        public ModularShadersGeneratorElement(ModularShader shader)
        {
            Shader = shader;
            style.flexDirection = FlexDirection.Row;
            _toggle = new Toggle();
            _toggle.RegisterValueChangedCallback(evt => IsSelected = evt.newValue);
            Add(_toggle);
            Add(new Label(Shader.Name));
            var issues = ShaderGenerator.CheckShaderIssues(shader);
            if (issues.Count > 0)
            {
                _isErrored = true;
                _toggle.SetEnabled(false);
                VisualElement element = new VisualElement();
                element.AddToClassList("error");
                element.tooltip = "Modular shader has the following errors: \n -" + string.Join("\n -", issues);
                Add(element);
            }
        }
    }
    
    public class ModularShadersGeneratorWindow : EditorWindow
    {
        [MenuItem("Poi/Modular Shaders Generator")]
        private static void ShowWindow()
        {
            var window = GetWindow<ModularShadersGeneratorWindow>();
            window.titleContent = new GUIContent("Modular Shaders Generator");
            window.Show();
        }
        
        private VisualElement _root;
        internal List<ModularShadersGeneratorElement> _elements;
        private string _folderPath = "Assets/_poiyomiShaders/Shaders/8.1/Pro";

        private void CreateGUI()
        {
            _root = rootVisualElement;
            try
            {
                Reload();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private void Reload()
        {
            _root.Clear();

            var styleSheet = Resources.Load<StyleSheet>("Poi/ModularShadersGeneratorStyle");
            _root.styleSheets.Add(styleSheet);

            var view = new ScrollView(ScrollViewMode.Vertical);

            var selectButtons = new VisualElement();
            selectButtons.AddToClassList("buttons-area");

            var selectAll = new Button();
            selectAll.text = "Select all";
            selectAll.style.flexGrow = 1;
            selectAll.clicked += () =>
            {
                foreach (var element in _elements)
                    element.IsSelected = true;
            };
            selectButtons.Add(selectAll);

            var deselectAll = new Button();
            deselectAll.text = "Deselect all";
            deselectAll.style.flexGrow = 1;
            deselectAll.clicked += () =>
            {
                foreach (var element in _elements)
                    element.IsSelected = false;
            };
            selectButtons.Add(deselectAll);

            var toggleSelections = new Button();
            toggleSelections.text = "Toggle selections";
            toggleSelections.style.flexGrow = 1;
            toggleSelections.clicked += () =>
            {
                foreach (var element in _elements)
                    element.IsSelected = !element.IsSelected;
            };
            selectButtons.Add(toggleSelections);
            
            var reloadButton = new Button();
            reloadButton.text = "Refresh";
            reloadButton.style.flexGrow = 1;
            reloadButton.clicked += () =>
            {
                AssetDatabase.Refresh();
                Reload();
            };
            selectButtons.Add(reloadButton);

            view.Add(selectButtons);

            // Load all modular shaders
            _elements = new List<ModularShadersGeneratorElement>();
            foreach (var modularShader in FindAssetsByType<ModularShader>())
            {
                var element = new ModularShadersGeneratorElement(modularShader);
                _elements.Add(element);
                view.Add(element);
            }

            var fileSelector = new VisualElement();
            fileSelector.AddToClassList("folder-selector");

            var folder = new TextField();
            folder.value = _folderPath;
            folder.RegisterValueChangedCallback(evt => _folderPath = evt.newValue);
            folder.style.flexShrink = 1;
            folder.style.flexGrow = 1;
            folder.SetEnabled(false);
            var label = new Label("Destination ");
            label.style.alignSelf = Align.Center;
            fileSelector.Add(label);
            fileSelector.Add(folder);
            var fileButton = new Button();
            fileButton.text = "Open";
            fileButton.clicked += () =>
            {
                string path = folder.value;
                if (Directory.Exists(path))
                {
                    path = Directory.GetParent(path).FullName;
                }
                else
                {
                    path = "Assets";
                }
                path = EditorUtility.OpenFolderPanel("Select folder to use", path, "");
                if (path.Length == 0)
                    return;

                if (!Directory.Exists(path))
                {
                    EditorUtility.DisplayDialog("Error", "The folder does not exist", "Ok");
                    return;
                }

                folder.value = path;
            };
            fileSelector.Add(fileButton);
            view.Add(fileSelector);
            
            var button = new Button();
            button.text = "Generate Shaders";
            button.clicked += GenerateShaders;
            view.Add(button);
            
            _root.Add(view);
        }

        internal void GenerateShaders()
        {
            if (!Directory.Exists(_folderPath))
            {
                EditorUtility.DisplayDialog("Error", "The destination folder does not exist", "Ok");
                return;
            }
            
            foreach (ModularShadersGeneratorElement element in _elements.Where(x => x.IsSelected))
                ShaderGenerator.GenerateShader(_folderPath, element.Shader);
        }

        private static T[] FindAssetsByType<T>() where T : UnityEngine.Object
        {
            List<T> assets = new List<T>();
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).ToString().Replace("UnityEngine.", "")}");
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
                if (asset != null)
                    assets.Add(asset);
            }
            return assets.ToArray();
        }
    }
    public class ModularShadersAutoGen : AssetPostprocessor
    {
#if UNITY_2021_2_OR_NEWER
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
#else
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
#endif
        {
            bool update = false;
            foreach (var importedAssetPath in importedAssets)
            {
                string ext = System.IO.Path.GetExtension(importedAssetPath);
                if (ext == ".poiTemplateCollection" || ext == ".poiTemplate")
                {
                    update = true;
                    break;
                }
            }
            if (update)
            {
                var msgw = Resources.FindObjectsOfTypeAll<ModularShadersGeneratorWindow>().FirstOrDefault();
                if (msgw != null)
                {
                    if (msgw._elements != null && msgw._elements.Count(x => x.IsSelected) > 0)
                    {
                        msgw.GenerateShaders();
                        Thry.ShaderEditor.ReloadActive();
                    }
                }
            }
        }
    }
}