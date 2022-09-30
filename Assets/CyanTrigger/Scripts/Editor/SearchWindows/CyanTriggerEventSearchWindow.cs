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
    public class CyanTriggerEventSearchWindow : ScriptableObject, ISearchWindowProvider
    {
        private static List<SearchTreeEntry> _vrcEventDefinitions;
        private static Texture2D _blankIcon;
        
        private static List<SearchTreeEntry> _registryCache;
        private static CyanTriggerSettingsFavoriteItem[] _otherFavoriteEvents;
        
        public Action<CyanTriggerActionInfoHolder> OnDefinitionSelected;


        public static void ResetCache()
        {
            _registryCache = null;
            _otherFavoriteEvents = null;
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
                    actionInfos);
                return true;
            }
            if (entry.userData is CyanTriggerActionInfoHolder actionInfoHolder && OnDefinitionSelected != null)
            {
                if (CyanTriggerSearchWindow.WasEventRightClick)
                {
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Add Event"), false, () =>
                    {
                        OnDefinitionSelected.Invoke(actionInfoHolder);
                    });
                
                    menu.ShowAsContext();
                    return false;
                }
                OnDefinitionSelected.Invoke(actionInfoHolder);
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

            if (_vrcEventDefinitions == null)
            {
                GetVrcEventDefinitions();
            }
        
            if (_registryCache != null && _registryCache.Count > 0) return _registryCache;
            
            _registryCache = new List<SearchTreeEntry>();
            
            _registryCache.Add(new SearchTreeGroupEntry(new GUIContent("Event Search"), 0));
            
            _registryCache.AddRange(_vrcEventDefinitions);
            
            _registryCache.Add(new SearchTreeGroupEntry(new GUIContent("User Events"), 1));
            _registryCache.AddRange(GetUserDefinedEvents());

            return _registryCache;
        }
        
        #endregion

        private static void GetVrcEventDefinitions()
        {
            _vrcEventDefinitions = new List<SearchTreeEntry>();
            _vrcEventDefinitions.Add(new SearchTreeGroupEntry(new GUIContent("VRC Events"), 1));
            
            foreach (var nodeDefinition in CyanTriggerNodeDefinitionManager.GetEventDefinitions())
            {
                _vrcEventDefinitions.Add(new SearchTreeEntry(new GUIContent(nodeDefinition.methodName, _blankIcon))
                    {level = 2, userData = CyanTriggerActionInfoHolder.GetActionInfoHolder(nodeDefinition)});
            }
        }
        
        private static List<SearchTreeEntry> GetUserDefinedEvents()
        {
            List<SearchTreeEntry> results = new List<SearchTreeEntry>();
            HashSet<string> usedNames = new HashSet<string>();

            foreach (var actionInfo in CyanTriggerActionGroupDefinitionUtil.GetEventInfoHolders())
            {
                string actionName = actionInfo.GetActionRenderingDisplayName();
                if (usedNames.Contains(actionName))
                {
                    continue;
                }
                usedNames.Add(actionName);
                
                results.Add(new SearchTreeEntry(
                        new GUIContent(actionName, _blankIcon, actionInfo.action.description))
                    {level = 2, userData = actionInfo});
            }

            return results;
        }

        private static (List<CyanTriggerSettingsFavoriteItem>, List<CyanTriggerSettingsFavoriteItem>)
            GetRemainingEvents(IEnumerable<CyanTriggerSettingsFavoriteItem> favoriteEvents)
        {
            List<CyanTriggerSettingsFavoriteItem> vrcEvents = new List<CyanTriggerSettingsFavoriteItem>();
            List<CyanTriggerSettingsFavoriteItem> customEvents = new List<CyanTriggerSettingsFavoriteItem>();

            HashSet<string> vrcDef = new HashSet<string>();
            HashSet<string> customGuid = new HashSet<string>();

            foreach (var favoriteItem in favoriteEvents)
            {
                var data = favoriteItem.data;
                if (!string.IsNullOrEmpty(data.directEvent))
                {
                    vrcDef.Add(data.directEvent);
                }
                if (!string.IsNullOrEmpty(data.guid))
                {
                    customGuid.Add(data.guid);
                }
            }

            foreach (var nodeDefinition in CyanTriggerNodeDefinitionManager.GetEventDefinitions())
            {
                if (vrcDef.Contains(nodeDefinition.fullName))
                {
                    continue;
                }

                var actionInfo = CyanTriggerActionInfoHolder.GetActionInfoHolder(nodeDefinition);
                vrcEvents.Add(new CyanTriggerSettingsFavoriteItem()
                {
                    item = actionInfo.GetActionRenderingDisplayName(),
                    data = new CyanTriggerActionType {directEvent = nodeDefinition.fullName}
                });
            }
            
            foreach (var actionInfo in CyanTriggerActionGroupDefinitionUtil.GetEventInfoHolders())
            {
                if (customGuid.Contains(actionInfo.action.guid))
                {
                    continue;
                }

                customEvents.Add(new CyanTriggerSettingsFavoriteItem()
                {
                    item = actionInfo.GetActionRenderingDisplayName(),
                    data = new CyanTriggerActionType
                    {
                        guid = actionInfo.action.guid,
                    }
                });
            }

            return (vrcEvents, customEvents);
        }

        public static CyanTriggerSettingsFavoriteItem[] GetAllEventsAsFavorites()
        {
            if (_otherFavoriteEvents != null)
            {
                return _otherFavoriteEvents;
            }

            var favoriteEvents =
                CyanTriggerSettingsFavoriteManager.Instance.FavoriteEvents.FavoriteItems;
            
            List<CyanTriggerSettingsFavoriteItem> favoriteItems = new List<CyanTriggerSettingsFavoriteItem>();
            favoriteItems.AddRange(favoriteEvents);

            var (vrcEvents, customEvents) = 
                GetRemainingEvents(favoriteEvents);
                
            favoriteItems.Add(new CyanTriggerSettingsFavoriteItem{item = "Other VRC Events", scopeDelta = 1});
            favoriteItems.AddRange(vrcEvents);
            favoriteItems.Add(new CyanTriggerSettingsFavoriteItem{item = "End Other VRC Events", scopeDelta = -1});
                
            favoriteItems.Add(new CyanTriggerSettingsFavoriteItem{item = "Other Custom Events", scopeDelta = 1});
            favoriteItems.AddRange(customEvents);
            favoriteItems.Add(new CyanTriggerSettingsFavoriteItem{item = "End Other Custom Events", scopeDelta = -1});

            return _otherFavoriteEvents = favoriteItems.ToArray();
        }
    }
}
