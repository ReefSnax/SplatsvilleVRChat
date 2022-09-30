using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CyanTrigger
{
    public class CyanTriggerActionGroupDefinitionUtil : AssetPostprocessor
    {
        private static CyanTriggerActionGroupDefinition[] _definitions;

        private static CyanTriggerActionInfoHolder[] _eventInfoHolders;
        private static CyanTriggerActionInfoHolder[] _actionInfoHolders;

        private static Dictionary<string, CyanTriggerActionDefinition> _actionGuidsToActions;
        private static Dictionary<CyanTriggerActionDefinition, CyanTriggerActionGroupDefinition> _actionToActionGroups;

        private static Dictionary<string, List<CyanTriggerActionInfoHolder>> _eventMethodNameToVariants;
        private static Dictionary<string, List<CyanTriggerActionInfoHolder>> _actionMethodNameToVariants;

        static CyanTriggerActionGroupDefinitionUtil()
        {
            CollectAllCyanTriggerActionDefinitions();
        }
        
        // TODO optimize this
        private static void OnPostprocessAllAssets(
            string[] importedAssets, 
            string[] deletedAssets, 
            string[] movedAssets, 
            string[] movedFromAssetPaths)
        {
            foreach (var path in importedAssets)
            {
                if (AssetDatabase.LoadAssetAtPath<CyanTriggerActionGroupDefinition>(path) != null)
                {
                    CollectAllCyanTriggerActionDefinitions();
                    break;
                }
            }
        }

        public static bool TryGetActionDefinition(string guid, out CyanTriggerActionDefinition actionDefinition)
        {
            return _actionGuidsToActions.TryGetValue(guid, out actionDefinition);
        }

        public static bool TryGetActionGroupDefinition(
            string guid,
            out CyanTriggerActionGroupDefinition groupDefinition)
        {
            if (!TryGetActionDefinition(guid, out var actionDefinition))
            {
                groupDefinition = null;
                return false;
            }

            return TryGetActionGroupDefinition(actionDefinition, out groupDefinition);
        }
        
        public static bool TryGetActionGroupDefinition(
            CyanTriggerActionDefinition actionDefinition,
            out CyanTriggerActionGroupDefinition groupDefinition)
        {
            return _actionToActionGroups.TryGetValue(actionDefinition, out groupDefinition);
        }
        
        public static bool ContainsActionDefinition(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return false;
            }
            return _actionGuidsToActions.ContainsKey(guid);
        }

        public static IEnumerable<CyanTriggerActionInfoHolder> GetEventInfoHolders()
        {
            for (int i = 0; i < _eventInfoHolders.Length; ++i)
            {
                yield return _eventInfoHolders[i];
            }
        }
        
        public static IEnumerable<CyanTriggerActionInfoHolder> GetActionInfoHolders()
        {
            for (int i = 0; i < _actionInfoHolders.Length; ++i)
            {
                yield return _actionInfoHolders[i];
            }
        }

        public static int GetEventVariantCount(CyanTriggerActionInfoHolder eventInfoHolder)
        {
            if (_eventMethodNameToVariants.TryGetValue(eventInfoHolder.GetDisplayName(), out var eventList))
            {
                return eventList.Count;
            }
            return 0;
        }
        
        public static IEnumerable<CyanTriggerActionInfoHolder> GetEventVariantInfoHolders(
            CyanTriggerActionInfoHolder eventInfoHolder)
        {
            if (_eventMethodNameToVariants.TryGetValue(eventInfoHolder.GetDisplayName(), out var eventList))
            {
                for (int i = 0; i < eventList.Count; ++i)
                {
                    yield return eventList[i];
                }
            }
        }
        
        public static int GetActionVariantCount(CyanTriggerActionInfoHolder actionInfoHolder)
        {
            if (_actionMethodNameToVariants.TryGetValue(actionInfoHolder.GetActionRenderingDisplayName(), out var actionList))
            {
                return actionList.Count;
            }
            return 0;
        }
        
        public static IEnumerable<CyanTriggerActionInfoHolder> GetActionVariantInfoHolders(
            CyanTriggerActionInfoHolder actionInfoHolder)
        {
            if (_actionMethodNameToVariants.TryGetValue(actionInfoHolder.GetActionRenderingDisplayName(), out var actionList))
            {
                for (int i = 0; i < actionList.Count; ++i)
                {
                    yield return actionList[i];
                }
            }
        }
        
        // TODO optimize this to take in the definitions already loaded
        private static void CollectAllCyanTriggerActionDefinitions()
        {
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(CyanTriggerActionGroupDefinition));
            _definitions = new CyanTriggerActionGroupDefinition[guids.Length];
            _actionGuidsToActions = new Dictionary<string, CyanTriggerActionDefinition>();
            _actionToActionGroups = new Dictionary<CyanTriggerActionDefinition, CyanTriggerActionGroupDefinition>();

            List<CyanTriggerActionInfoHolder> eventInfoHolders = new List<CyanTriggerActionInfoHolder>();
            List<CyanTriggerActionInfoHolder> actionInfoHolders = new List<CyanTriggerActionInfoHolder>();

            for (int cur = 0; cur < guids.Length; ++cur)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[cur]);
                var definition = AssetDatabase.LoadAssetAtPath<CyanTriggerActionGroupDefinition>(path);
                if (definition == null || definition.exposedActions == null)
                {
                    continue;
                }
                
                _definitions[cur] = definition;

                foreach (var action in definition.exposedActions)
                {
                    _actionGuidsToActions.Add(action.guid, action);
                    _actionToActionGroups.Add(action, definition);

                    var actionInfoHolder = CyanTriggerActionInfoHolder.GetActionInfoHolder(action, definition);

                    if (action.IsEvent())
                    {
                        eventInfoHolders.Add(actionInfoHolder);
                    }
                    else
                    {
                        actionInfoHolders.Add(actionInfoHolder);
                    }
                }
            }

            _eventInfoHolders = eventInfoHolders.ToArray();
            _actionInfoHolders = actionInfoHolders.ToArray();

            UpdateMethodVariants();
            
            CyanTriggerActionSearchWindow.ResetCache();
            CyanTriggerEventSearchWindow.ResetCache();
        }

        private static void UpdateMethodVariants()
        {
            _eventMethodNameToVariants = new Dictionary<string, List<CyanTriggerActionInfoHolder>>();
            _actionMethodNameToVariants = new Dictionary<string, List<CyanTriggerActionInfoHolder>>();

            void AddToEventList(string eventName, CyanTriggerActionInfoHolder infoHolder)
            {
                if (!_eventMethodNameToVariants.TryGetValue(eventName, out var eventList))
                {
                    eventList = new List<CyanTriggerActionInfoHolder>();
                    _eventMethodNameToVariants.Add(eventName, eventList);
                }
                    
                eventList.Add(infoHolder);
            }
            
            void AddToActionList(string actionName, CyanTriggerActionInfoHolder infoHolder)
            {
                if (!_actionMethodNameToVariants.TryGetValue(actionName, out var actionList))
                {
                    actionList = new List<CyanTriggerActionInfoHolder>();
                    _actionMethodNameToVariants.Add(actionName, actionList);
                }
                    
                actionList.Add(infoHolder);
            }
            
            foreach (var udonDef in CyanTriggerNodeDefinitionManager.GetDefinitions())
            {
                var infoHolder = CyanTriggerActionInfoHolder.GetActionInfoHolder(udonDef);
                if (udonDef.definitionType == CyanTriggerNodeDefinition.UdonDefinitionType.Event)
                {
                    AddToEventList(udonDef.methodName, infoHolder);
                }
                else
                {
                    AddToActionList(udonDef.GetActionDisplayName(), infoHolder);
                }
            }
            
            foreach (var eventInfo in _eventInfoHolders)
            {
                AddToEventList(eventInfo.GetDisplayName(), eventInfo);
            }
            foreach (var actionInfo in _actionInfoHolders)
            {
                AddToActionList(actionInfo.GetActionRenderingDisplayName(), actionInfo);
            }
        }
    }
}