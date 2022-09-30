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
    public class CyanTriggerSearchWindowManager
    {
        private static CyanTriggerSearchWindowManager _instance;
        public static CyanTriggerSearchWindowManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new CyanTriggerSearchWindowManager();
                }
                return _instance;
            }
        }

        private readonly CyanTriggerVariableSearchWindow _variableSearchWindow;
        private readonly CyanTriggerActionSearchWindow _actionSearchWindow;
        private readonly CyanTriggerEventSearchWindow _eventSearchWindow;
        private readonly CyanTriggerFocusedSearchWindow _focusedSearchWindow;
        private readonly CyanTriggerFavoriteSearchWindow _favoritesSearchWindow;
        
        // TODO make generic and take in the favorites list and auto populate every time.
        
        
        private Vector2 _searchWindowPosition;

        private CyanTriggerSearchWindowManager()
        {
            _variableSearchWindow = ScriptableObject.CreateInstance<CyanTriggerVariableSearchWindow>();
            _actionSearchWindow = ScriptableObject.CreateInstance<CyanTriggerActionSearchWindow>();
            _eventSearchWindow = ScriptableObject.CreateInstance<CyanTriggerEventSearchWindow>();
            _focusedSearchWindow = ScriptableObject.CreateInstance<CyanTriggerFocusedSearchWindow>();
            _favoritesSearchWindow = ScriptableObject.CreateInstance<CyanTriggerFavoriteSearchWindow>();
        }

        private Vector2 GetMousePos()
        {
            Vector2 pos = Vector2.zero;
            if (Event.current != null)
            {
                pos = Event.current.mousePosition;
            }

            return GUIUtility.GUIToScreenPoint(pos);
        }
        
        public void DisplayVariableSearchWindow(Action<UdonNodeDefinition> onSelect)
        {
            DisplayVariableSearchWindow(GetMousePos(), onSelect);
        }
        
        public void DisplayVariableSearchWindow(Vector2 pos, Action<UdonNodeDefinition> onSelect)
        {
            _variableSearchWindow.OnDefinitionSelected = onSelect;
            CyanTriggerSearchWindow.Open(new SearchWindowContext(pos), _variableSearchWindow);
        }
        
        public void DisplayActionSearchWindow(Action<CyanTriggerActionInfoHolder> onSelect)
        {
            DisplayActionSearchWindow(GetMousePos(), onSelect);
        }
        
        public void DisplayActionSearchWindow(Vector2 pos, Action<CyanTriggerActionInfoHolder> onSelect)
        {
            _actionSearchWindow.OnDefinitionSelected = onSelect;
            CyanTriggerSearchWindow.Open(new SearchWindowContext(pos), _actionSearchWindow);
        }
        
        public void DisplayEventSearchWindow(Action<CyanTriggerActionInfoHolder> onSelect)
        {
            DisplayEventSearchWindow(GetMousePos(), onSelect);
        }
        
        public void DisplayEventSearchWindow(Vector2 pos, Action<CyanTriggerActionInfoHolder> onSelect)
        {
            _eventSearchWindow.OnDefinitionSelected = onSelect;
            CyanTriggerSearchWindow.Open(new SearchWindowContext(pos), _eventSearchWindow);
        }
        
        public void DisplayFocusedSearchWindow(
            Vector2 pos, 
            Action<CyanTriggerActionInfoHolder> onSelect, 
            string title, 
            List<CyanTriggerActionInfoHolder> entries,
            Func<CyanTriggerActionInfoHolder, string> displayMethod = null)
        {
            _searchWindowPosition = pos;
            _focusedSearchWindow.OnDefinitionSelected = onSelect;
            _focusedSearchWindow.WindowTitle = title;
            _focusedSearchWindow.FocusedNodeDefinitions = entries;
            
            if (displayMethod == null)
            {
                _focusedSearchWindow.ResetDisplayMethod();
            }
            else
            {
                _focusedSearchWindow.GetDisplayString = displayMethod;
            }
            
            EditorApplication.update += TryOpenFocusedSearch;
        }
        
        private void TryOpenFocusedSearch()
        {
            if (CyanTriggerSearchWindow.Open(new SearchWindowContext(_searchWindowPosition, 400f), _focusedSearchWindow))
            {
                EditorApplication.update -= TryOpenFocusedSearch;
            }
        }
        
        
        public void DisplayVariableFavoritesSearchWindow(Action<CyanTriggerSettingsFavoriteItem> onSelect)
        {
            DisplayVariableFavoritesSearchWindow(GetMousePos(), onSelect);
        }

        public void DisplayVariableFavoritesSearchWindow(Vector2 pos, Action<CyanTriggerSettingsFavoriteItem> onSelect)
        {
            _favoritesSearchWindow.OnDefinitionSelected = onSelect;
            _favoritesSearchWindow.WindowTitle = "Favorite Variables";
            _favoritesSearchWindow.FavoriteList =
                CyanTriggerSettingsFavoriteManager.Instance.FavoriteVariables.FavoriteItems;
            
            CyanTriggerSearchWindow.Open(new SearchWindowContext(pos), _favoritesSearchWindow);
        }
        
        public void DisplayEventsFavoritesSearchWindow(Action<CyanTriggerSettingsFavoriteItem> onSelect, bool displayAll = false)
        {
            DisplayEventsFavoritesSearchWindow(GetMousePos(), onSelect, displayAll);
        }
        
        public void DisplayEventsFavoritesSearchWindow(Vector2 pos, Action<CyanTriggerSettingsFavoriteItem> onSelect, bool displayAll = false)
        {
            _favoritesSearchWindow.OnDefinitionSelected = onSelect;
            _favoritesSearchWindow.WindowTitle = "Favorite Events";

            if (displayAll)
            {
                _favoritesSearchWindow.FavoriteList = CyanTriggerEventSearchWindow.GetAllEventsAsFavorites();
            }
            else
            {
                _favoritesSearchWindow.FavoriteList =
                    CyanTriggerSettingsFavoriteManager.Instance.FavoriteEvents.FavoriteItems;
            }
            
            CyanTriggerSearchWindow.Open(new SearchWindowContext(pos), _favoritesSearchWindow);
        }
        
        public void DisplayActionFavoritesSearchWindow(Action<CyanTriggerSettingsFavoriteItem> onSelect)
        {
            DisplayActionFavoritesSearchWindow(GetMousePos(), onSelect);
        }
        
        public void DisplayActionFavoritesSearchWindow(Vector2 pos, Action<CyanTriggerSettingsFavoriteItem> onSelect)
        {
            _favoritesSearchWindow.OnDefinitionSelected = onSelect;
            _favoritesSearchWindow.WindowTitle = "Favorite Actions";
            _favoritesSearchWindow.FavoriteList = 
                CyanTriggerSettingsFavoriteManager.Instance.FavoriteActions.FavoriteItems;
            
            CyanTriggerSearchWindow.Open(new SearchWindowContext(pos), _favoritesSearchWindow);
        }
        
        public void DisplaySDK2ActionFavoritesSearchWindow(Action<CyanTriggerSettingsFavoriteItem> onSelect)
        {
            DisplaySDK2ActionFavoritesSearchWindow(GetMousePos(), onSelect);
        }
        
        public void DisplaySDK2ActionFavoritesSearchWindow(Vector2 pos, Action<CyanTriggerSettingsFavoriteItem> onSelect)
        {
            _favoritesSearchWindow.OnDefinitionSelected = onSelect;
            _favoritesSearchWindow.WindowTitle = "SDK2 Actions";
            _favoritesSearchWindow.FavoriteList = 
                CyanTriggerSettingsFavoriteManager.Instance.Sdk2Actions.FavoriteItems;
            
            CyanTriggerSearchWindow.Open(new SearchWindowContext(pos), _favoritesSearchWindow);
        }
    }
}
