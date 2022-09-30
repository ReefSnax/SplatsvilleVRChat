using System;
using System.Collections.Generic;
using UnityEditor;
#if UNITY_2019_3_OR_NEWER
using UnityEditor.Experimental.GraphView;
#else
using UnityEditor.Experimental.UIElements.GraphView;
#endif
using UnityEngine;

namespace CyanTrigger
{
    public class CyanTriggerFavoriteSearchWindow : ScriptableObject, ISearchWindowProvider
    {
        public string WindowTitle;
        public IEnumerable<CyanTriggerSettingsFavoriteItem> FavoriteList;
        public Action<CyanTriggerSettingsFavoriteItem> OnDefinitionSelected;

        #region ISearchWindowProvider

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            Texture2D udonTypeIcon = EditorGUIUtility.FindTexture("cs Script Icon");
            
            // TODO find a better icon, preferably something more "custom"
            Texture2D customTypeIcon = EditorGUIUtility.FindTexture("Settings"); 
            
            List<SearchTreeEntry> nodeEntries = new List<SearchTreeEntry>();

            nodeEntries.Add(new SearchTreeGroupEntry(new GUIContent($"{WindowTitle} Search"), 0));

            int level = 1;
            foreach (var item in FavoriteList)
            {
                if (item.scopeDelta == -1)
                {
                    --level;
                    continue;
                }

                if (item.scopeDelta == 0)
                {
                    var icon = udonTypeIcon;
                    if (!string.IsNullOrEmpty(item.data.guid))
                    {
                        icon = customTypeIcon;
                    }
                    nodeEntries.Add(new SearchTreeEntry(new GUIContent(item.item, icon)) {level = level, userData = item});
                }
                else
                {
                    nodeEntries.Add(new SearchTreeGroupEntry(new GUIContent(item.item)) {level = level});
                    ++level;
                }  
            }

            return nodeEntries;
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            if (entry.userData is CyanTriggerSettingsFavoriteItem favoritedItem && OnDefinitionSelected != null)
            {
                if (CyanTriggerSearchWindow.WasEventRightClick)
                {
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Add Item"), false, () =>
                    {
                        OnDefinitionSelected.Invoke(favoritedItem);
                    });
                
                    menu.ShowAsContext();
                    return false;
                }
                
                OnDefinitionSelected.Invoke(favoritedItem);
                return true;
            }

            return false;
        }
        
        #endregion
    }
}