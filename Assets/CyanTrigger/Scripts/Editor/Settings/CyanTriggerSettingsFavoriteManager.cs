
using UnityEditor;
using UnityEngine;

namespace CyanTrigger
{
    public class CyanTriggerSettingsFavoriteManager
    {
        private const string FavoriteVariablesName = "Variables";
        private const string FavoriteEventsName = "Events";
        private const string FavoriteActionsName = "Actions";
        private const string SDK2ActionsName = "SDK2_Actions";
        private const string FavoritesPrefix = "Settings/CyanTriggerFavorite_";
        private const string ResourcesPath = "Assets/CyanTrigger/Resources/";
        
        private static CyanTriggerSettingsFavoriteManager _instance;
        public static CyanTriggerSettingsFavoriteManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new CyanTriggerSettingsFavoriteManager();
                }
                return _instance;
            }
        }
        
        // TODO search all items in the directory to find favorite items and organize them.
        public CyanTriggerSettingsFavoriteList FavoriteVariables;
        public CyanTriggerSettingsFavoriteList FavoriteEvents;
        public CyanTriggerSettingsFavoriteList FavoriteActions;
        public CyanTriggerSettingsFavoriteList Sdk2Actions;
        
        private CyanTriggerSettingsFavoriteManager()
        {
            FavoriteVariables = LoadOrCreateFavoriteList(FavoritesPrefix + FavoriteVariablesName);
            FavoriteEvents = LoadOrCreateFavoriteList(FavoritesPrefix + FavoriteEventsName);
            FavoriteActions = LoadOrCreateFavoriteList(FavoritesPrefix + FavoriteActionsName);
            Sdk2Actions = LoadOrCreateFavoriteList(FavoritesPrefix + SDK2ActionsName);
        }

        private static CyanTriggerSettingsFavoriteList LoadOrCreateFavoriteList(string path)
        {
            var favoriteList = Resources.Load<CyanTriggerSettingsFavoriteList>(path);
            if (favoriteList == null)
            {
                Debug.Log($"Favorite List at {path} was null! Creating a new one.");
                favoriteList = CreateFavoriteList(path);
            }

            return favoriteList;
        }
        
        private static CyanTriggerSettingsFavoriteList CreateFavoriteList(string path)
        {
            path = ResourcesPath + path + ".asset";
            CyanTriggerSettingsFavoriteList favoriteList =
                ScriptableObject.CreateInstance<CyanTriggerSettingsFavoriteList>();
            AssetDatabase.CreateAsset(favoriteList, path);
            AssetDatabase.ImportAsset(path);
            return favoriteList;
        }

        private void CopyFavorites(CyanTriggerSettingsFavoriteItem[] src, ref CyanTriggerSettingsFavoriteItem[] dest)
        {
            if (dest == null || dest.Length != src.Length)
            {
                dest = new CyanTriggerSettingsFavoriteItem[src.Length];
            }

            for (int cur = 0; cur < src.Length; ++cur)
            {
                dest[cur] = src[cur];
            }
        }
    }
}
