using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEngine;
using VRC.Udon.Graph;

namespace CyanTrigger
{
    public class CyanTriggerSettingsWindow : EditorWindow
    {
        private const string VersionFileName = "version";
        private const string WikiURL = "https://github.com/CyanLaser/CyanTrigger/wiki";
        private const string DiscordURL = "https://discord.gg/stPkhM2T6C";
        private const string PatreonURL = "https://www.patreon.com/CyanLaser";
        
        private static readonly GUIContent FavoriteVariablesLabel = new GUIContent("Favorite Variables");
        private static readonly GUIContent FavoriteEventsLabel = new GUIContent("Favorite Events");
        private static readonly GUIContent FavoriteActionsLabel = new GUIContent("Favorite Actions");
        
        private static readonly GUIContent AddVariablesLabel = new GUIContent("Add Variable");
        private static readonly GUIContent AddEventsLabel = new GUIContent("Add Event");
        private static readonly GUIContent AddActionsLabel = new GUIContent("Add Action");

        private static readonly GUIContent ActionDetailedViewLabel = new GUIContent("Show Action Parameters",
            "When enabled, all parameters in each CyanTrigger action will be displayed with the action type. This can clutter the UI, but gives enough detail to fully understand the action without expanding it.");

        private static string _version;

        private static GUIStyle _style;
        
        private CyanTriggerSettingsData _settingsData;
        private SerializedObject _settingsSerializedObject;
        
        private SerializedObject _variablesSerializedObject;
        private SerializedObject _eventsSerializedObject;
        private SerializedObject _actionsSerializedObject;

        private CyanTriggerSettingsFavoritesTreeView _variableFavorites;
        private CyanTriggerSettingsFavoritesTreeView _eventFavorites;
        private CyanTriggerSettingsFavoritesTreeView _actionFavorites;

        private SerializedProperty _variablesProperty;
        private SerializedProperty _eventsProperty;
        private SerializedProperty _actionsProperty;

        private AnimBool _showVariables;
        private AnimBool _showEvents;
        private AnimBool _showActions;
        
        // Other settings
        private SerializedProperty _actionDetailedViewProperty;

        private Vector2 _scrollPosition;

        [MenuItem ("Window/CyanTrigger/CyanTrigger Settings")]
        public static void ShowWindow()
        {
            var window = GetWindow<CyanTriggerSettingsWindow> ();
            window.titleContent = new GUIContent ("CyanTrigger Settings");
            window.Show ();
        }
        
        void OnEnable()
        {
            _settingsData = CyanTriggerSettings.Instance;
            _settingsSerializedObject = new SerializedObject(_settingsData);

            // TODO allow users to create and select which favorite type they are editing
            CyanTriggerSettingsFavoriteManager favoritesManager = CyanTriggerSettingsFavoriteManager.Instance;
            _variablesSerializedObject = new SerializedObject(favoritesManager.FavoriteVariables);
            _eventsSerializedObject = new SerializedObject(favoritesManager.FavoriteEvents);
            _actionsSerializedObject = new SerializedObject(favoritesManager.FavoriteActions);
            
            _variablesProperty = _variablesSerializedObject.FindProperty(nameof(CyanTriggerSettingsFavoriteList.FavoriteItems));
            _eventsProperty = _eventsSerializedObject.FindProperty(nameof(CyanTriggerSettingsFavoriteList.FavoriteItems));
            _actionsProperty = _actionsSerializedObject.FindProperty(nameof(CyanTriggerSettingsFavoriteList.FavoriteItems));

            _actionDetailedViewProperty = _settingsSerializedObject.FindProperty(nameof(CyanTriggerSettingsData.actionDetailedView));
            
            _variableFavorites = new CyanTriggerSettingsFavoritesTreeView(_variablesProperty);
            _eventFavorites = new CyanTriggerSettingsFavoritesTreeView(_eventsProperty);
            _actionFavorites = new CyanTriggerSettingsFavoritesTreeView(_actionsProperty);
            UpdateIdStartIndexes();

            // This is actually annoying with large lists...
            // _variableFavorites.ExpandAll();
            // _eventFavorites.ExpandAll();
            // _actionFavorites.ExpandAll();

            _showVariables = new AnimBool(true);
            _showEvents = new AnimBool(true);
            _showActions = new AnimBool(true);

            _showVariables.valueChanged.AddListener(Repaint);
            _showEvents.valueChanged.AddListener(Repaint);
            _showActions.valueChanged.AddListener(Repaint);
        }

        private void UpdateIdStartIndexes()
        {
            _variableFavorites.IdStartIndex = 0;
            _eventFavorites.IdStartIndex = _variableFavorites.Size;
            _actionFavorites.IdStartIndex = _variableFavorites.Size + _eventFavorites.Size;
        }

        private void ReloadAllTrees()
        {
            _variableFavorites.Reload();
            _eventFavorites.Reload();
            _actionFavorites.Reload();
        }
        
        void OnGUI()
        {
            _style = EditorStyles.helpBox;
            
            _settingsSerializedObject.Update();
            _variablesSerializedObject.Update();
            _eventsSerializedObject.Update();
            _actionsSerializedObject.Update();
            
            UpdateIdStartIndexes();
            
            if (Event.current.type == EventType.ValidateCommand &&
                Event.current.commandName == "UndoRedoPerformed")
            {
                ReloadAllTrees();
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(15);
            EditorGUILayout.BeginVertical(GUILayout.MaxWidth(EditorGUIUtility.currentViewWidth - 30));
            
            EditorGUILayout.Space();
            DrawHeader("CyanTrigger Settings");
            EditorGUILayout.Space();

            DrawActionSection();
            EditorGUILayout.Space();

            bool uiShouldUpdate = DrawUISettings();
            EditorGUILayout.Space();
            
            DrawTree(_variableFavorites, _variablesProperty, FavoriteVariablesLabel, _showVariables, AddVariablesLabel, AddVariableMenu);
            EditorGUILayout.Space();
            
            DrawTree(_eventFavorites, _eventsProperty, FavoriteEventsLabel, _showEvents, AddEventsLabel, AddEvent);
            EditorGUILayout.Space();
            
            DrawTree(_actionFavorites, _actionsProperty, FavoriteActionsLabel, _showActions, AddActionsLabel, AddAction);

            
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();

            _settingsSerializedObject.ApplyModifiedProperties();
            _variablesSerializedObject.ApplyModifiedProperties();
            _actionsSerializedObject.ApplyModifiedProperties();
            bool eventsChanged = _eventsSerializedObject.ApplyModifiedProperties();
            
            if (eventsChanged)
            {
                CyanTriggerEventSearchWindow.ResetCache();
            }
            

            if (uiShouldUpdate)
            {
                CyanTriggerSerializableInstanceEditor.UpdateAllOpenSerializers();
            }
        }

        public static void DrawHeader(string title)
        {
            if (string.IsNullOrEmpty(_version))
            {
                _version = Resources.Load<TextAsset>(VersionFileName).text.Trim();
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            GUILayout.Label(" Version: " + _version);
            GUILayout.Label(" Created by CyanLaser");

            DrawLinks();
            
            EditorGUILayout.EndVertical();
        }
        
        private static void DrawLinks()
        {
            Rect buttonAreaRect = EditorGUILayout.BeginHorizontal();
            buttonAreaRect.height = 16;
            const float spaceBetween = 5;
            float width = (buttonAreaRect.width - spaceBetween * 2) / 3;

            Rect button1 = new Rect(buttonAreaRect.x, buttonAreaRect.y, width, buttonAreaRect.height);
            Rect button2 = new Rect(button1.xMax + spaceBetween, buttonAreaRect.y, width, buttonAreaRect.height);
            Rect button3 = new Rect(button2.xMax + spaceBetween, buttonAreaRect.y, width, buttonAreaRect.height);
            
            if (GUI.Button(button1, "Wiki"))
            {
                Application.OpenURL(WikiURL);
            }

            if (GUI.Button(button2, "Discord"))
            {
                Application.OpenURL(DiscordURL);
            }

            if (GUI.Button(button3, "Patreon"))
            {
                Application.OpenURL(PatreonURL);
            }
            
            // TODO GitHub or Youtube tutorials link

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(buttonAreaRect.height);
        }

        private static void DrawActionSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField("Actions");
            
            if (GUILayout.Button(new GUIContent("Compile Triggers", "Compile all CyanTriggers in the scene.")))
            {
                CyanTriggerSerializerManager.RecompileAllTriggers(true);
            }
            
            if (GUILayout.Button(new GUIContent("Clear Serialized Data", "Clear all the serialized CyanTrigger data. the serialized data is reused between scenes, but sometimes more can be generated than needed.")))
            {
                CyanTriggerSerializedProgramManager.Instance.ClearSerializedData();
            }
            
            EditorGUILayout.EndVertical();
        }

        private bool DrawUISettings()
        {
            EditorGUILayout.BeginVertical(_style);
            EditorGUILayout.LabelField("UI Settings");

            // TODO ensure all open editors update their view
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_actionDetailedViewProperty, ActionDetailedViewLabel);

            bool changed = EditorGUI.EndChangeCheck();
            
            EditorGUILayout.EndVertical();
            return changed;
        }

        private static void DrawTree(
            CyanTriggerSettingsFavoritesTreeView treeView, 
            SerializedProperty property,
            GUIContent label,
            AnimBool showFields,
            GUIContent addLabel,
            Action addAction)
        {
            EditorGUILayout.BeginVertical(_style);
            
            showFields.target = EditorGUILayout.Foldout(showFields.target, label, true);
            
            if (!EditorGUILayout.BeginFadeGroup(showFields.faded))
            {
                EditorGUILayout.EndFadeGroup();
                EditorGUILayout.EndVertical();
                return;
            }

            // Catch Undo/redo case
            if (treeView.Size != property.arraySize)
            {
                treeView.Reload();
            }

            Rect treeRect = EditorGUILayout.BeginVertical();
            treeRect.height = treeView.totalHeight + EditorGUIUtility.singleLineHeight;//200;
            GUILayout.Space(treeRect.height);
            treeView.OnGUI(treeRect);
            EditorGUILayout.EndVertical();
            
            
            EditorGUILayout.Space();
            
            
            Rect buttonAreaRect = EditorGUILayout.BeginHorizontal();
            buttonAreaRect.height = 16;
            const float spaceBetween = 5;
            float width = (buttonAreaRect.width - spaceBetween * 2) / 3;

            Rect button1 = new Rect(buttonAreaRect.x, buttonAreaRect.y, width, buttonAreaRect.height);
            Rect button2 = new Rect(button1.xMax + spaceBetween, buttonAreaRect.y, width, buttonAreaRect.height);
            Rect button3 = new Rect(button2.xMax + spaceBetween, buttonAreaRect.y, width, buttonAreaRect.height);

            if (GUI.Button(button1, addLabel))
            {
                addAction.Invoke();
                treeView.Reload();
            }
            
            if (GUI.Button(button2, "Add Folder"))
            {
                int index = property.arraySize;
                AddItem(property, "New Folder", true);
                treeView.Reload();
            
                // Ensure new folder starts expanded
                List<int> expanded = new List<int>(treeView.GetExpanded());
                expanded.Add(index + treeView.IdStartIndex); // expected folder start index
                treeView.SetExpanded(expanded);
                treeView.BeginRename(treeView.GetItem(index));
            }
            
            EditorGUI.BeginDisabledGroup(!treeView.HasSelection());
            if (GUI.Button(button3, "Remove"))
            {
                treeView.RemoveSelected();
            }
            EditorGUI.EndDisabledGroup();
        
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(buttonAreaRect.height);

            EditorGUILayout.EndFadeGroup();
            EditorGUILayout.EndVertical();
        }

        
        private void AddVariableMenu()
        {
            CyanTriggerSearchWindowManager.Instance.DisplayVariableSearchWindow(AddVariable);
        }

        private void AddVariable(UdonNodeDefinition selectedItem)
        {
            AddDataItem(_variablesProperty, 
                CyanTriggerNameHelpers.GetTypeFriendlyName(selectedItem.type), 
                CyanTriggerActionInfoHolder.GetActionInfoHolder(selectedItem));
            
            _variablesProperty.serializedObject.ApplyModifiedProperties();
            _variableFavorites.Reload();
        }

        private void AddEvent()
        {
            CyanTriggerSearchWindowManager.Instance.DisplayEventSearchWindow(AddEvent);
        }
        
        private void AddEvent(CyanTriggerActionInfoHolder selectedItem)
        {
            AddDataItem(_eventsProperty, selectedItem.GetActionRenderingDisplayName(), selectedItem);
            
            _eventsProperty.serializedObject.ApplyModifiedProperties();
            _eventFavorites.Reload();
        }
        
        private void AddAction()
        {
            CyanTriggerSearchWindowManager.Instance.DisplayActionSearchWindow(AddAction);
        }
        
        private void AddAction(CyanTriggerActionInfoHolder selectedItem)
        {
            AddDataItem(_actionsProperty, selectedItem.GetActionRenderingDisplayName(), selectedItem);
            
            _actionsProperty.serializedObject.ApplyModifiedProperties();
            _actionFavorites.Reload();
        }
        
        
        private static void AddDataItem(SerializedProperty property, string itemName, CyanTriggerActionInfoHolder infoHolder)
        {
            string def = infoHolder.definition?.fullName;
            string guid = infoHolder.action?.guid;
            AddDataItem(property, itemName, def, guid);
        }
        
        private static void AddDataItem(SerializedProperty property, string itemName, string directDefinition, string guid)
        {
            var element = AddItem(property, itemName, false);
            SetDataForItem(element, directDefinition, guid);
        }

        private static void SetDataForItem(SerializedProperty property, string direct, string guid)
        {
            var dataProp = property.FindPropertyRelative(nameof(CyanTriggerSettingsFavoriteItem.data));
            dataProp.FindPropertyRelative(nameof(CyanTriggerActionType.directEvent)).stringValue = direct;
            dataProp.FindPropertyRelative(nameof(CyanTriggerActionType.guid)).stringValue = guid;
        }
        
        private static SerializedProperty AddItem(SerializedProperty property, string itemName, bool hasScope)
        {
            ++property.arraySize;
            var element = property.GetArrayElementAtIndex(property.arraySize - 1);
            element.FindPropertyRelative(nameof(CyanTriggerSettingsFavoriteItem.item)).stringValue = itemName;
            element.FindPropertyRelative(nameof(CyanTriggerSettingsFavoriteItem.scopeDelta)).intValue = hasScope ? 1 : 0;
            SetDataForItem(element, "", "");
                
            if (hasScope)
            {
                ++property.arraySize;
                var scopeElement = property.GetArrayElementAtIndex(property.arraySize - 1);
                scopeElement.FindPropertyRelative(nameof(CyanTriggerSettingsFavoriteItem.item)).stringValue = 
                    "_ScopeEnd " + itemName;
                scopeElement.FindPropertyRelative(nameof(CyanTriggerSettingsFavoriteItem.scopeDelta)).intValue = -1;
                SetDataForItem(scopeElement, "", "");
            }

            return element;
        }
    }
}
