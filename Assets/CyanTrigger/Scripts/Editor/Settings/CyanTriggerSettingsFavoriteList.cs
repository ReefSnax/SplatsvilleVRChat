using System;
using UnityEngine;

namespace CyanTrigger
{
    public enum CyanTriggerFavoriteType 
    {
        None = 0,
        Variables = 1,
        Events = 2,
        Actions = 3,
    }
    
    [Serializable]
    public class CyanTriggerSettingsFavoriteItem
    {
        public string item;
        public CyanTriggerActionType data;
        public int scopeDelta;
    }

    public class CyanTriggerSettingsFavoriteList : ScriptableObject
    {
        public CyanTriggerFavoriteType FavoriteType;
        public CyanTriggerSettingsFavoriteItem[] FavoriteItems = new CyanTriggerSettingsFavoriteItem[0];
    }
}