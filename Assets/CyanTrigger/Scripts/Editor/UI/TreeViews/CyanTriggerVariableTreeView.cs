using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Graph;

namespace CyanTrigger
{
    public class CyanTriggerVariableTreeView : CyanTriggerScopedDataTreeView<CyanTriggerVariableTreeView.VariableExpandData>
    {
        public class VariableExpandData
        {
            public bool IsExpanded;
            public ReorderableList List;
        }
        
        private const float DefaultRowHeight = 20;
        private const float SpaceBetweenRowEditor = 6;
        private const float SpaceBetweenRowEditorSides = 6;
        
        private readonly Action<List<string>> _onVariableAddedOrRemoved;
        private readonly Action<string, string, string> _onVariableRenamed;
        private readonly Func<string, string, string> _getUniqueVariableName;

        private bool _delayRefreshRowHeight = false;

        private bool _shouldVerifyVariables;
        
        private static MultiColumnHeader CreateColumnHeader()
        {
            string[] columnHeaders = {"Name", "Type", "Value", "Sync"};
            MultiColumnHeaderState.Column[] columns = new MultiColumnHeaderState.Column[4];
            for (int cur = 0; cur < columns.Length; ++cur)
            {
                columns[cur] = new MultiColumnHeaderState.Column
                {
                    minWidth = 50f,
                    width = 100f, 
                    headerTextAlignment = TextAlignment.Center, 
                    canSort = false,
                    headerContent = new GUIContent(columnHeaders[cur]),
                };
            }
            
            MultiColumnHeader multiColumnHeader = new MultiColumnHeader(new MultiColumnHeaderState(columns))
            {
                height = 18,
            };
            multiColumnHeader.ResizeToFit();
            
            return multiColumnHeader;
        }
        
        private static string GetElementDisplayName(SerializedProperty property)
        {
            return property.FindPropertyRelative(nameof(CyanTriggerVariable.name)).stringValue;
        }
        
        private static int GetElementScopeDelta(SerializedProperty property)
        {
            return 0;
        }

        public CyanTriggerVariableTreeView(
            SerializedProperty elements, 
            Action<List<string>> onVariableAddedOrRemoved,
            Func<string, string, string> getUniqueVariableName,
            Action<string, string, string> onVariableRenamed) 
            : base (elements, CreateColumnHeader(), GetElementScopeDelta, GetElementDisplayName)
        {
            showBorder = true;
            rowHeight = DefaultRowHeight;
            showAlternatingRowBackgrounds = true;
            useScrollView = false;
            _onVariableAddedOrRemoved = onVariableAddedOrRemoved;
            _getUniqueVariableName = getUniqueVariableName;
            _onVariableRenamed = onVariableRenamed;
            
            Reload();
        }

        protected override void OnBuildRoot(CyanTriggerScopedTreeItem root)
        {
            // On rebuild, assume lists need to be recreated.
            foreach (var data in GetData())
            {
                data.Item1.List = null;
            }
            
            _shouldVerifyVariables = true;
        }

        private void VerifyVariables()
        {
            int size = Elements.arraySize;
            for (int cur = 0; cur < size; ++cur)
            {
                SerializedProperty variableProperty = Elements.GetArrayElementAtIndex(cur);
                SerializedProperty typeProperty =
                    variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.type));
                SerializedProperty typeDefProperty =
                    typeProperty.FindPropertyRelative(nameof(CyanTriggerSerializableType.typeDef));
                Type type = Type.GetType(typeDefProperty.stringValue);
                SerializedProperty dataProperty = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.data));
                
                object obj = CyanTriggerSerializableObject.ObjectFromSerializedProperty(dataProperty);
                bool dirty = false;
                obj = CyanTriggerPropertyEditor.CreateInitialValueForType(type, obj, ref dirty);

                if (dirty)
                {
                    if(type.IsArray && typeof(UnityEngine.Object).IsAssignableFrom(type.GetElementType()))
                    {
                        var array = (Array) obj;
                    
                        Array destinationArray = Array.CreateInstance(type.GetElementType(), array.Length);
                        Array.Copy(array, destinationArray, array.Length);
                
                        obj = destinationArray;
                    }
                
                    CyanTriggerSerializableObject.UpdateSerializedProperty(dataProperty, obj);
                }
            }
        }

        private VariableExpandData GetOrCreateExpandData(int id)
        {
            var data = GetData(id);
            if (data == null)
            {
                data = new VariableExpandData();
                SetData(id, data);
            }

            return data;
        }

        public void DoLayoutTree()
        {
            bool isUndo = (Event.current.commandName == "UndoRedoPerformed");
            
            if (Size != Elements.arraySize || isUndo)
            {
                _onVariableAddedOrRemoved?.Invoke(null);
                Reload();
            }

            if (_shouldVerifyVariables)
            {
                _shouldVerifyVariables = false;
                VerifyVariables();
            }
            
            Rect treeRect = EditorGUILayout.BeginVertical();
            treeRect.height = totalHeight + (Size == 0 ? DefaultRowHeight : 0);
            treeRect.x += 1;
            treeRect.width -= 2;
            GUILayout.Space(treeRect.height + 1);
            
            var listActionFooterIcons = new[]
            {
                EditorGUIUtility.TrIconContent("Favorite", "Choose to add to list"),
                EditorGUIUtility.TrIconContent("Toolbar Plus", "Choose to add to list"),
                EditorGUIUtility.TrIconContent("TreeEditor.Duplicate", "Duplicate selected item"),
                EditorGUIUtility.TrIconContent("Toolbar Minus", "Remove selection from list")
            };
            
            bool hasSelection = HasSelection();
            CyanTriggerPropertyEditor.DrawButtonFooter(
                listActionFooterIcons, 
                new Action[]
                {
                    AddNewVariableFromFavoriteList,
                    AddNewVariableFromAllList,
                    DuplicateSelectedItems,
                    RemoveSelected
                },
                new []
                {
                    false, false, !hasSelection, !hasSelection
                });
            
            OnGUI(treeRect);
            
            EditorGUILayout.EndVertical();
            
            if (_delayRefreshRowHeight)
            {
                _delayRefreshRowHeight = false;
                RefreshCustomRowHeights();
            }
        }

        protected override float GetCustomRowHeight(int row, TreeViewItem item)
        {
            float height = DefaultRowHeight;
            var scopedItem = (CyanTriggerScopedTreeItem) item;
            
            SerializedProperty variableProperty = Elements.GetArrayElementAtIndex(scopedItem.Index);
            SerializedProperty typeProperty = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.type));
            SerializedProperty typeDefProperty =
                typeProperty.FindPropertyRelative(nameof(CyanTriggerSerializableType.typeDef));
            Type type = Type.GetType(typeDefProperty.stringValue);

            if (CyanTriggerPropertyEditor.TypeHasSingleLineEditor(type))
            {
                return height;
            }

            VariableExpandData expandData = GetOrCreateExpandData(item.id);
            if (!expandData.IsExpanded)
            {
                return height;
            }
            
            // Calculate multi line height for the property
            SerializedProperty dataProperty = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.data));
            var data = CyanTriggerSerializableObject.ObjectFromSerializedProperty(dataProperty);
            
            height = height + SpaceBetweenRowEditor * 2 + 
                   CyanTriggerPropertyEditor.HeightForEditor(type, data, true, ref expandData.List);

            return height;
        }

        protected override bool CanRename(TreeViewItem item)
        {
            return true;
        }
        
        protected override void RenameEnded(RenameEndedArgs args)
        {
            if (!args.acceptedRename || args.newName.Equals(args.originalName))
            {
                return;
            }
            
            int index = GetItemIndex(args.itemID);
            var variableProperty = Elements.GetArrayElementAtIndex(index);
            
            var guid = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.variableID)).stringValue;
            string newName = _getUniqueVariableName(args.newName, guid);
            
            if (newName.Equals(args.originalName))
            {
                return;
            }
            
            variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.name)).stringValue = newName;
            Items[index].displayName = newName;
            _onVariableRenamed?.Invoke(args.originalName, newName, guid);
        }

        protected override bool CanDuplicate(IEnumerable<int> items)
        {
            return true;
        }

        protected override List<int> DuplicateItems(IEnumerable<int> items)
        {
            List<int> newIds = new List<int>();
            foreach (int id in GetSelection())
            {
                int index = GetItemIndex(id);
                DuplicateVariable(Elements.GetArrayElementAtIndex(index));
                newIds.Add(id + IdStartIndex);
            }

            return newIds;
        }

        protected override void GetRightClickMenuOptions(GenericMenu menu, Event currentEvent)
        {
            base.GetRightClickMenuOptions(menu, currentEvent);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Add Variable"), false, () =>
            {
                CyanTriggerSearchWindowManager.Instance.DisplayVariableSearchWindow(
                    GUIUtility.GUIToScreenPoint(currentEvent.mousePosition), AddNewVariable);
            });
            menu.AddItem(new GUIContent("Add Favorite Variable"), false, () =>
            {
                CyanTriggerSearchWindowManager.Instance.DisplayVariableFavoritesSearchWindow(
                    GUIUtility.GUIToScreenPoint(currentEvent.mousePosition), AddNewVariable);
            });
        }

        protected override void DoubleClickedItem(int id)
        {
            var data = GetData(id);
            if (data == null)
            {
                return;
            }

            data.IsExpanded = !data.IsExpanded;
            _delayRefreshRowHeight = true;
        }

        protected override void OnItemsRemoved(List<CyanTriggerScopedTreeItem> removedItems)
        {
            base.OnItemsRemoved(removedItems);
            List<string> guids = new List<string>();
            foreach (var item in removedItems)
            {
                if (item.Index >= ItemElements.Length || ItemElements[item.Index] == null)
                {
                    continue;
                }

                var guidProp = ItemElements[item.Index].FindPropertyRelative(nameof(CyanTriggerVariable.variableID));
                guids.Add(guidProp.stringValue);
            }
            
            _onVariableAddedOrRemoved?.Invoke(guids);
        }

        protected override void OnRowGUI(RowGUIArgs args)
        {
            var item = (CyanTriggerScopedTreeItem) args.item;

            // Only draw variable fields when not renaming the variable
            if (!args.isRenaming)
            {
                SerializedProperty variableProperty = Elements.GetArrayElementAtIndex(item.Index);
                SerializedProperty typeProperty =
                    variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.type));
                SerializedProperty typeDefProperty =
                    typeProperty.FindPropertyRelative(nameof(CyanTriggerSerializableType.typeDef));
                Type type = Type.GetType(typeDefProperty.stringValue);
            
                for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
                {
                    Rect cellRect = args.GetCellRect(i);
                    cellRect.height = DefaultRowHeight;
                    CellGUI(cellRect, args.GetColumn(i), item, variableProperty, type);
                }
            }

            Rect editorRect = new Rect(args.rowRect);
            editorRect.y += DefaultRowHeight + SpaceBetweenRowEditor;
            editorRect.height -= DefaultRowHeight + SpaceBetweenRowEditor * 2;
            editorRect.x += SpaceBetweenRowEditorSides;
            editorRect.width -= SpaceBetweenRowEditorSides * 2;
            DrawMultilineVariableEditor(item.Index, editorRect);
        }

        void CellGUI(Rect cellRect, int column, CyanTriggerScopedTreeItem item, SerializedProperty variableProperty, Type type)
        {
            CenterRectUsingSingleLineHeight(ref cellRect);
            
            switch (column)
            {
                case 0: // Name
                {
                    GUIContent content = new GUIContent(item.displayName, item.displayName);
                    CyanTriggerNameHelpers.TruncateContent(content, cellRect);
                    EditorGUI.LabelField(cellRect, content);
                    break;
                }
                case 1: // Type
                {
                    string typeName = CyanTriggerNameHelpers.GetTypeFriendlyName(type);
                    GUIContent content = new GUIContent(typeName, typeName);
                    CyanTriggerNameHelpers.TruncateContent(content, cellRect);
                    EditorGUI.LabelField(cellRect, content);
                    break;
                }
                case 2: // Value
                {
                    // TODO check for application playing and update displayed value.
                    SerializedProperty dataProperty = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.data));
                    
                    if (!CyanTriggerPropertyEditor.TypeHasSingleLineEditor(type))
                    {
                        VariableExpandData expandData = GetOrCreateExpandData(item.id);
                        if (GUI.Button(cellRect, new GUIContent(expandData.IsExpanded ? "Hide" : "Edit")))
                        {
                            expandData.IsExpanded = !expandData.IsExpanded;
                            _delayRefreshRowHeight = true;
                        }
                    }
                    else
                    {
                        CyanTriggerPropertyEditor.DrawEditor(dataProperty, cellRect, GUIContent.none, type, false);
                    }
                    break;
                }
                case 3: // Sync
                {
                    SerializedProperty syncProperty = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.sync));
                    
                    // New UI using the Sync beta
                    bool canSync = UdonNetworkTypes.CanSync(type);
                    EditorGUI.BeginDisabledGroup(!canSync);

                    int selected = 0;
                    CyanTriggerVariableSyncMode cur = (CyanTriggerVariableSyncMode)syncProperty.intValue;
                    
                    List<CyanTriggerVariableSyncMode> syncOptions = new List<CyanTriggerVariableSyncMode>();
                    syncOptions.Add(CyanTriggerVariableSyncMode.NotSynced);
                    if (canSync)
                    {
                        syncOptions.Add(CyanTriggerVariableSyncMode.Synced);
                    }
                    if (UdonNetworkTypes.CanSyncLinear(type))
                    {
                        syncOptions.Add(CyanTriggerVariableSyncMode.SyncedLinear);
                    }
                    if (UdonNetworkTypes.CanSyncSmooth(type))
                    {
                        syncOptions.Add(CyanTriggerVariableSyncMode.SyncedSmooth);
                    }

                    string[] options = new string[syncOptions.Count];
                    for (int option = 0; option < options.Length; ++option)
                    {
                        options[option] = syncOptions[option].ToString();
                        if (cur == syncOptions[option])
                        {
                            selected = option;
                        }
                    }

                    int newSelected = EditorGUI.Popup(cellRect, selected, options);
                    if (newSelected != selected)
                    {
                        syncProperty.intValue = (int) syncOptions[newSelected];
                    }
                    EditorGUI.EndDisabledGroup();
                    
                    break;
                }
            }
        }
        
        private void DrawMultilineVariableEditor(int index, Rect rect)
        {
            SerializedProperty variableProperty = Elements.GetArrayElementAtIndex(index);
            SerializedProperty typeProperty =
                variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.type));
            SerializedProperty typeDefProperty =
                typeProperty.FindPropertyRelative(nameof(CyanTriggerSerializableType.typeDef));
            Type type = Type.GetType(typeDefProperty.stringValue);

            int id = Items[index].id;
            VariableExpandData expandData = GetOrCreateExpandData(id);
            
            if (!CyanTriggerPropertyEditor.TypeHasSingleLineEditor(type) && expandData.IsExpanded)
            {
                SerializedProperty nameProperty =
                    variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.name));
                SerializedProperty dataProperty =
                    variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.data));
                
                if (type.IsArray)
                {
                    int size = expandData.List == null ? 0 : expandData.List.count;
                    bool showArray = expandData.IsExpanded;
                    
                    string display = CyanTriggerNameHelpers.GetTypeFriendlyName(type) + " " + nameProperty.stringValue;
                    CyanTriggerPropertyEditor.DrawArrayEditor(
                        dataProperty,
                        new GUIContent(display),
                        type,
                        ref expandData.IsExpanded,
                        ref expandData.List,
                        false,
                        rect);

                    int newSize = expandData.List == null ? 0 : expandData.List.count;
                    if (size != newSize || showArray != expandData.IsExpanded)
                    {
                        _delayRefreshRowHeight = true;
                    }
                }
                else
                {
                    CyanTriggerPropertyEditor.DrawEditor(dataProperty, rect, GUIContent.none, type, false);
                }
            }
        }

        private void AddNewVariableFromAllList()
        {
            CyanTriggerSearchWindowManager.Instance.DisplayVariableSearchWindow(AddNewVariable);
        }

        private void AddNewVariableFromFavoriteList()
        {
            CyanTriggerSearchWindowManager.Instance.DisplayVariableFavoritesSearchWindow(AddNewVariable);
        }
        
        private void AddNewVariable(UdonNodeDefinition def)
        {
            AddNewVariable(CyanTriggerNameHelpers.GetTypeFriendlyName(def.type), def.type);
        }

        private void AddNewVariable(CyanTriggerSettingsFavoriteItem favorite)
        {
            if (string.IsNullOrEmpty(favorite.data.directEvent))
            {
                Debug.LogWarning("Cannot create a new variable without a proper definition!");
                return;
            }

            var def = CyanTriggerNodeDefinitionManager.GetDefinition(favorite.data.directEvent);
            if (def == null)
            {
                Debug.LogWarning("Cannot create a new variable without a proper definition!");
                return;
            }

            AddNewVariable(CyanTriggerNameHelpers.GetTypeFriendlyName(def.baseType), def.baseType);
        }

        private void AddNewVariable(string variableName, Type type, object data = default, bool rename = true)
        {
            Elements.arraySize++;
            SerializedProperty newVariableProperty = Elements.GetArrayElementAtIndex(Elements.arraySize - 1);

            string id = Guid.NewGuid().ToString();
            variableName = _getUniqueVariableName(variableName, id);
            CyanTriggerSerializedPropertyUtils.SetVariableData(newVariableProperty, variableName, type, data, id);
            
            _onVariableAddedOrRemoved?.Invoke(null);

            if (rename)
            {
                Reload();
                BeginRename(Items[Elements.arraySize - 1]);
            }
        }

        private void DuplicateVariable(SerializedProperty variableProperty)
        {
            SerializedProperty nameProperty = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.name));
            SerializedProperty typeProperty = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.type));
            SerializedProperty typeDefProperty =
                typeProperty.FindPropertyRelative(nameof(CyanTriggerSerializableType.typeDef));
            Type type = Type.GetType(typeDefProperty.stringValue);
            SerializedProperty dataProperty = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.data));
            var data = CyanTriggerSerializableObject.ObjectFromSerializedProperty(dataProperty);
            
            AddNewVariable(nameProperty.stringValue, type, data, false);
        }
        
        protected override void OnElementRemapped(VariableExpandData element, int prevIndex, int newIndex)
        {
            element.List = null;
            _onVariableAddedOrRemoved?.Invoke(null);
        }
    }
}
