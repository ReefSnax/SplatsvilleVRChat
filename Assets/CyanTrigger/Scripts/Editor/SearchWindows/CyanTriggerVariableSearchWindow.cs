using System;
using System.Collections.Generic;
using UnityEditor;
#if UNITY_2019_3_OR_NEWER
using UnityEditor.Experimental.GraphView;
#else
using UnityEditor.Experimental.UIElements.GraphView;
#endif
using UnityEngine;
using VRC.Udon.Graph;


namespace CyanTrigger
{
    public class CyanTriggerVariableSearchWindow : ScriptableObject, ISearchWindowProvider
    {
        private static Texture2D _blankIcon;
        private static List<SearchTreeEntry> _registryCache;

        public Action<UdonNodeDefinition> OnDefinitionSelected;

        #region ISearchWindowProvider
        
        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            if (entry.userData is CyanTriggerActionInfoHolder actionInfoHolder && OnDefinitionSelected != null)
            {
                if (CyanTriggerSearchWindow.WasEventRightClick)
                {
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Add Variable"), false, () =>
                    {
                        OnDefinitionSelected.Invoke(actionInfoHolder.definition.definition);
                    });
                
                    menu.ShowAsContext();
                    return false;
                }
                OnDefinitionSelected.Invoke(actionInfoHolder.definition.definition);
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

            if (_registryCache != null && _registryCache.Count > 0)
            {
                return _registryCache;
            }
            
            _registryCache = new List<SearchTreeEntry>();
            
            _registryCache.Add(new SearchTreeGroupEntry(new GUIContent("Variable Search"), 0));

            List<CyanTriggerNodeDefinition> definitions = 
                new List<CyanTriggerNodeDefinition>(CyanTriggerNodeDefinitionManager.GetVariableDefinitions());
            
            // Sort so System variables are always first, everything else is alphabetical
            // TODO move to a generic place?
            definitions.Sort((d1, d2) =>
            {
                bool h1System = d1.fullName.StartsWith("CyanTriggerVariable_System");
                bool h2System = d2.fullName.StartsWith("CyanTriggerVariable_System");
                if (h1System == h2System)
                {
                    return d1.fullName.CompareTo(d2.fullName);
                }

                return (!h1System).CompareTo(!h2System);
            });
            
            foreach (var nodeDefinition in definitions)
            {
                _registryCache.Add(new SearchTreeEntry(new GUIContent(nodeDefinition.typeFriendlyName, _blankIcon))
                    {level = 1, userData = CyanTriggerActionInfoHolder.GetActionInfoHolder(nodeDefinition)});
            }
            
            return _registryCache;
        }
        
        
        
        #endregion
    }
}
