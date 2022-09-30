using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
#if UNITY_2019_3_OR_NEWER
using UnityEditor.Experimental.GraphView;
#else
using UnityEditor.Experimental.UIElements.GraphView;
#endif
using UnityEngine;

namespace CyanTrigger
{
    public class CyanTriggerActionSearchWindow : ScriptableObject, ISearchWindowProvider
    {
        private static List<SearchTreeEntry> _vrcActionDefinitions;
        private static Texture2D _blankIcon;
        
        private static List<SearchTreeEntry> _registryCache;
        
        public Action<CyanTriggerActionInfoHolder> OnDefinitionSelected;

        private static readonly string[] TopLevelEntries =
        {
            "Special",
            "Local Variables",
            "CyanTrigger",
            "UdonBehaviour",
            "System",
            "UnityEngine",
            "VRC",
            "Other",
            //"Recent", // TODO?
        };
        
        private static readonly List<(string, string)> _shortcutRegistries = new List<(string, string)>()
        {
            // ("UnityEngine","Debug"),
            ("VRC", "UdonBehaviour")
        };

        public static void ResetCache()
        {
            _registryCache = null;
        }

        #region ISearchWindowProvider

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            if (entry.userData is List<CyanTriggerActionInfoHolder> actionInfos)
            {
                CyanTriggerSearchWindowManager.Instance.DisplayFocusedSearchWindow(
                    context.screenMousePosition, 
                    OnDefinitionSelected, 
                    entry.name, 
                    actionInfos,
                    (holder => holder.GetActionRenderingDisplayName()));
                return true;
            }
            if (entry.userData is CyanTriggerActionInfoHolder actionInfo && OnDefinitionSelected != null)
            {
                if (CyanTriggerSearchWindow.WasEventRightClick)
                {
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Add Action"), false, () =>
                    {
                        OnDefinitionSelected.Invoke(actionInfo);
                    });
                
                    menu.ShowAsContext();
                    return false;
                }
                OnDefinitionSelected.Invoke(actionInfo);
                return true;
            }

            return false;
        }
        
        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            if (_blankIcon == null)
            {
                _blankIcon = new Texture2D(1, 1);
                _blankIcon.SetPixel(0,0, Color.clear);
                _blankIcon.Apply();
            }

            // if (_vrcActionDefinitions == null)
            // {
            //     GetVrcActionDefinitions();
            // }

            if (_registryCache != null && _registryCache.Count > 0)
            {
                return _registryCache;
            }
            
            GetVrcActionDefinitions();
            
            _registryCache = new List<SearchTreeEntry>();
            _registryCache.Add(new SearchTreeGroupEntry(new GUIContent("Action Search"), 0));
            _registryCache.AddRange(_vrcActionDefinitions);

            // _registryCache.Add(new SearchTreeGroupEntry(new GUIContent("User Actions"), 1));
            // _registryCache.AddRange(GetUserDefinedActions());

            return _registryCache;
        }
        
        #endregion

        private static void GetVrcActionDefinitions()
        {
            _vrcActionDefinitions = new List<SearchTreeEntry>();
            var categorizedNodes = new Dictionary<string, Dictionary<string, List<CyanTriggerActionInfoHolder>>>();
            foreach (string category in TopLevelEntries)
            {
                categorizedNodes.Add(category, new Dictionary<string, List<CyanTriggerActionInfoHolder>>());
            }

            Dictionary<string, string> secondaryToPrimaryLookup = new Dictionary<string, string>();
            
            void AddNodeToSorting(string primaryKey, string secondaryKey, CyanTriggerActionInfoHolder actionInfo)
            {
                if (!categorizedNodes.TryGetValue(primaryKey, out var primaryCategories))
                {
                    primaryCategories = new Dictionary<string, List<CyanTriggerActionInfoHolder>>();
                    categorizedNodes.Add(primaryKey, primaryCategories);
                }
                
                if (!primaryCategories.TryGetValue(secondaryKey, out var nodeList))
                {
                    nodeList = new List<CyanTriggerActionInfoHolder>();
                    primaryCategories.Add(secondaryKey, nodeList);
                }

                if (!secondaryToPrimaryLookup.TryGetValue(secondaryKey, out string curPri))
                {
                    secondaryToPrimaryLookup.Add(secondaryKey, primaryKey);
                } 
#if CYAN_TRIGGER_DEBUG
                // else if (curPri != primaryKey)
                // {
                //     Debug.LogWarning("Secondary Key matches multiple primary! " + secondaryKey +": " + primaryKey +" != " +curPri);
                // }
#endif
                
                nodeList.Add(actionInfo);
            }

            foreach (var nodeDefinition in CyanTriggerNodeDefinitionManager.GetDefinitions())
            {
                if (CyanTriggerNodeDefinitionManager.DefinitionIsHidden(nodeDefinition.fullName))
                {
                    continue;
                }
                
                // Get root sorting
                string[] sortings = GetCategoryForDefinition(nodeDefinition);
                if (sortings == null)
                {
                    continue;
                }
                
                var actionInfo = CyanTriggerActionInfoHolder.GetActionInfoHolder(nodeDefinition);
                if (!actionInfo.IsValid())
                {
#if CYAN_TRIGGER_DEBUG
                    Debug.LogWarning("[CyanTriggerActionSearchWindow] Invalid action info: " + nodeDefinition.fullName);
#endif
                    continue;
                }
                
                string primaryKey = sortings[0];
                string secondaryKey = sortings[1];

                AddNodeToSorting(primaryKey, secondaryKey, actionInfo);
            }

            // Get user defined so they add to the proper categories
            {
                foreach (var actionInfo in CyanTriggerActionGroupDefinitionUtil.GetActionInfoHolders())
                {
                    string displayName = actionInfo.GetDisplayName();

                    if (secondaryToPrimaryLookup.TryGetValue(displayName, out string primaryKey))
                    {
                        // Add in two locations so that you can find custom with the vrc methods.
                        AddNodeToSorting(primaryKey, displayName, actionInfo);
                    }
                    
                    AddNodeToSorting("Custom Actions", displayName + " (Custom)", actionInfo);
                }
            }
            
            // Add shortcuts
            foreach (var shortcut in _shortcutRegistries)
            {
                if (!categorizedNodes.TryGetValue(shortcut.Item1, out var categoryNodeDictionary))
                {
                    continue;
                }

                if (!categoryNodeDictionary.TryGetValue(shortcut.Item2, out var categoryNodeList))
                {
                    continue;
                }

                if (!categorizedNodes.TryGetValue(shortcut.Item2, out var shortcutDictionary))
                {
                    shortcutDictionary = new Dictionary<string, List<CyanTriggerActionInfoHolder>>();
                    categorizedNodes.Add(shortcut.Item2, shortcutDictionary);
                }
                
                if (!shortcutDictionary.TryGetValue(shortcut.Item2, out var nodeList))
                {
                    nodeList = new List<CyanTriggerActionInfoHolder>();
                    shortcutDictionary.Add(shortcut.Item2, nodeList);
                }
                
                nodeList.AddRange(categoryNodeList);
            }

            // Apply custom sorting to categories
            {
                categorizedNodes.TryGetValue("Local Variables", out var category);
                category.TryGetValue("Variable", out var nodes);
                nodes.Sort((h1, h2) =>
                {
                    bool h1System = h1.definition.fullName.StartsWith("CyanTriggerVariable_System");
                    bool h2System = h2.definition.fullName.StartsWith("CyanTriggerVariable_System");
                    if (h1System == h2System)
                    {
                        return h1.definition.fullName.CompareTo(h2.definition.fullName);
                    }

                    return (!h1System).CompareTo(!h2System);
                });
            }

            foreach (var topEntries in categorizedNodes)
            {
                string topName = topEntries.Key;
                var pairs = topEntries.Value.OrderBy(pair => pair.Key).ToList();
                
                // Skip empty lists
                if (pairs.Count == 0)
                {
#if CYAN_TRIGGER_DEBUG
                    Debug.LogWarning("[CyanTriggerActionSearchWindow] Skipping empty pair: "+topName);
#endif
                    continue;
                }
                
                if (pairs.Count == 1)
                {
                    _vrcActionDefinitions.Add(new SearchTreeEntry(new GUIContent(topName, _blankIcon))
                        {level = 1, userData = pairs[0].Value});
                    continue;
                }
                
                _vrcActionDefinitions.Add(new SearchTreeGroupEntry(new GUIContent(topName)) {level = 1});
                
                foreach (var pair in pairs)
                {
                    _vrcActionDefinitions.Add(new SearchTreeEntry(new GUIContent(pair.Key, _blankIcon))
                        {level = 2, userData = pair.Value});
                }
            }
        }

        private static string[] GetCategoryForDefinition(CyanTriggerNodeDefinition nodeDefinition)
        {
            if (nodeDefinition.definitionType == CyanTriggerNodeDefinition.UdonDefinitionType.Event || 
                nodeDefinition.definitionType == CyanTriggerNodeDefinition.UdonDefinitionType.Variable || 
                nodeDefinition.definitionType == CyanTriggerNodeDefinition.UdonDefinitionType.Const)
            {
                return null;
            }

            if (nodeDefinition.typeCategories[0] == "System")
            {
                return new [] {"System", nodeDefinition.typeFriendlyName};
            }

            // Lazy
            if (nodeDefinition.definitionType == CyanTriggerNodeDefinition.UdonDefinitionType.Type)
            {
                return new [] {"Type", "Type"};
                //return new [] {"Type", "Type " + nodeDefinition.typeFriendlyName};
            }
            if (nodeDefinition.definitionType == CyanTriggerNodeDefinition.UdonDefinitionType.CyanTriggerVariable)
            {
                return new [] {"Local Variables", "Variable"};
                //return new [] {"Local Variables", "Variable " + nodeDefinition.typeFriendlyName};
            }

            if (nodeDefinition.typeCategories[0] == "VRC")
            {
                return new [] {"VRC", nodeDefinition.typeFriendlyName};
            }
            if (nodeDefinition.typeCategories[0] == "CyanTrigger") // TODO?
            {
                return new [] {"CyanTrigger", "CyanTrigger"};
            }
            if (nodeDefinition.typeCategories[0] == CyanTriggerNodeDefinition.UdonDefinitionType.CyanTriggerSpecial.ToString())
            {
                return new [] {"Special", "Special"};
            }
            
            
            if (nodeDefinition.typeCategories[0] == "UnityEngine")
            {
                return new [] {"UnityEngine", nodeDefinition.typeFriendlyName};
            }

            // TODO find these and figure out if they need to be implemented.
            if (nodeDefinition.typeFriendlyName == "Void")
            {
                return null;
            }
            
            // Cinemachine, TMPro, and ??? go under Other
            return new [] {"Other", nodeDefinition.typeFriendlyName};
        }

        private static List<SearchTreeEntry> GetUserDefinedActions()
        {
            List<SearchTreeEntry> results = new List<SearchTreeEntry>();

            Dictionary<string, List<CyanTriggerActionInfoHolder>> categorized =
                new Dictionary<string, List<CyanTriggerActionInfoHolder>>();
            
            foreach (var actionInfo in CyanTriggerActionGroupDefinitionUtil.GetActionInfoHolders())
            {
                string displayName = actionInfo.GetDisplayName();
                if (!categorized.TryGetValue(displayName, out var list))
                {
                    list = new List<CyanTriggerActionInfoHolder>();
                    categorized.Add(displayName, list);
                }
                
                list.Add(actionInfo);
            }

            foreach (var item in categorized)
            {
                results.Add(new SearchTreeEntry(
                        new GUIContent("*" +item.Key, _blankIcon)) {level = 2, userData = item.Value});
            }

            return results;
        }
    }
}

