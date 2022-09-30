using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;
using VRC.Udon.Graph;

namespace CyanTrigger
{
    public class CyanTriggerActionInstanceRenderData
    {
        private SerializedProperty _property;
        public SerializedProperty Property
        {
            get => _property;
            set
            {
                _property = value;
                Initialize();
            }
        }

        private int _cachedExpanded = -1;
        private SerializedProperty _expandedProperty;
        public bool IsExpanded
        {
            get
            {
                if (_cachedExpanded == -1)
                {
                    _cachedExpanded = (_expandedProperty != null && _expandedProperty.boolValue) ? 1 : 0;
                }
                return _cachedExpanded == 1;
            }
            set
            {
                int valueInt = value ? 1 : 0;
                if (_expandedProperty != null && _cachedExpanded != valueInt)
                {
                    _expandedProperty.boolValue = value;
                    _expandedProperty.serializedObject.ApplyModifiedProperties();
                    _cachedExpanded = valueInt;
                }
            }
        }

        private string _cachedComment = null;
        private SerializedProperty _commentProperty;
        public string Comment
        {
            get
            {
                if (_cachedComment == null)
                {
                    _cachedComment = _commentProperty != null ? _commentProperty.stringValue : string.Empty;
                }
                return _cachedComment;
            }
            set
            {
                if (_commentProperty != null && _cachedComment != value)
                {
                    _commentProperty.stringValue = value;
                    _commentProperty.serializedObject.ApplyModifiedProperties();
                    _cachedComment = value;
                }
            }
        }

        public CyanTriggerActionInfoHolder ActionInfo;
        public bool[] ExpandedInputs = new bool[0];
        public ReorderableList[] InputLists = new ReorderableList[0];
        public bool NeedsRedraws;
        public bool ContainsNull;

        private void Initialize()
        {
            if (_property != null)
            {
                ActionInfo = CyanTriggerActionInfoHolder.GetActionInfoHolderFromProperties(_property);
                _expandedProperty = _property.FindPropertyRelative(nameof(CyanTriggerActionInstance.expanded));
                    
                var commentBaseProperty = _property.FindPropertyRelative(nameof(CyanTriggerActionInstance.comment));
                _commentProperty = commentBaseProperty.FindPropertyRelative(nameof(CyanTriggerComment.comment));
                
                var variableDefinitions = ActionInfo.GetVariables();

                if (InputLists.Length != variableDefinitions.Length)
                {
                    InputLists = new ReorderableList[variableDefinitions.Length];
                    ExpandedInputs = new bool[variableDefinitions.Length];
                    for (int cur = 0; cur < variableDefinitions.Length; ++cur)
                    {
                        // TODO turn these into serialized properties when persisted
                        ExpandedInputs[cur] = true;
                    }
                }
            }
            else
            {
                ActionInfo = null;
                _expandedProperty = null;
                _commentProperty = null;
                
                ExpandedInputs = new bool[0];
                InputLists = new ReorderableList[0];
            }
        }

        public void UpdateExpandCache()
        {
            if (_expandedProperty != null)
            {
                IsExpanded = _expandedProperty.boolValue;
            }
        }
        
        public void ClearInputLists()
        {
            if (InputLists == null)
            {
                return;
            }

            for (int i = 0; i < InputLists.Length; ++i)
            {
                InputLists[i] = null;
            }
        }
    }
    
    public class CyanTriggerActionTreeView : CyanTriggerScopedDataTreeView<CyanTriggerActionInstanceRenderData>
    {
        private const float DefaultRowHeight = 20;
        private const float SpaceAboveRowEditor = 2;
        private const float SpaceBetweenRowEditor = 6;
        private const float SpaceBetweenRowEditorSides = 6;
        private const float CellVerticalMargin = 3;
        private const float CellHorizontalMargin = 6;
        private const float ExpandButtonSize = 16;

        private const string CommentControlName = "Action Editor Comment Control";

        public Action OnActionChanged;
        public Func<Type, int, List<CyanTriggerEditorVariableOption>> GetVariableOptions;
        public Func<int, string, string, bool> IsVariableValid;
        
        private readonly AnimBool _showActions;
        private bool _initialized;
        
        private bool _delayRefreshRowHeight = false;
        private bool _delayActionsChanged = false;


        private int _mouseOverId = -1;
        private int _editingCommentId = -1;
        private bool _focusedCommentEditor = false;
        private float _lastRectWidth = -1;

        private int _lastKeyboardControl = -1;
        private int _focusedActionId = -1;
        private bool _itemHasChanges = false;
        
        private static MultiColumnHeader CreateColumnHeader()
        {
            MultiColumnHeaderState.Column[] columns =
            {
                new MultiColumnHeaderState.Column
                {
                    minWidth = 50f, width = 100f, headerTextAlignment = TextAlignment.Center, canSort = false
                }
            };
            MultiColumnHeader multiColumnHeader = new MultiColumnHeader(new MultiColumnHeaderState(columns))
            {
                height = 0,
            };
            multiColumnHeader.ResizeToFit();
            
            return multiColumnHeader;
        }
        
        private static string GetElementDisplayName(SerializedProperty actionProperty)
        {
            CyanTriggerActionInfoHolder actionInfo = 
                CyanTriggerActionInfoHolder.GetActionInfoHolderFromProperties(actionProperty);
            return actionInfo.GetActionRenderingDisplayName(actionProperty);
        }
        
        private static int GetElementScopeDelta(SerializedProperty actionProperty)
        {
            CyanTriggerActionInfoHolder actionInfo = 
                CyanTriggerActionInfoHolder.GetActionInfoHolderFromProperties(actionProperty);
            return actionInfo.GetScopeDelta();
        }
        
        public CyanTriggerActionTreeView(
            SerializedProperty elements, 
            Action onActionChanged,
            Func<Type, int, List<CyanTriggerEditorVariableOption>> getVariableOptions,
            Func<int, string, string, bool> isVariableValid) 
            : base(elements, CreateColumnHeader(), GetElementScopeDelta, GetElementDisplayName)
        {
            showBorder = true;
            rowHeight = DefaultRowHeight;
            showAlternatingRowBackgrounds = true;
            useScrollView = false;

            OnActionChanged = onActionChanged;

            _showActions = new AnimBool(true);
            _showActions.valueChanged.AddListener(Repaint);

            GetVariableOptions = getVariableOptions;
            IsVariableValid = isVariableValid;

            // Proxy so that each element can draw their own, even if they don't currently have children.
            // This does remove the nice animation though. 
            // TODO fix the nice animation by persisting foldout state
            foldoutOverride = (position, expandedState, style) => expandedState;
        }

        public void Dispose()
        {
            if (_focusedActionId != -1 && _itemHasChanges)
            {
                UpdateVariableNamesFromProvider(_focusedActionId, Undo.GetCurrentGroup());
                _itemHasChanges = false;
            }

            Elements.serializedObject.ApplyModifiedProperties();
        }

        protected override void OnBuildRoot(CyanTriggerScopedTreeItem root)
        {
            // On rebuild, assume lists need to be recreated.
            foreach (var data in GetData())
            {
                data.Item1.ClearInputLists();
            }

            // Force reverify
            _initialized = false;
        }

        private void VerifyActions()
        {
            // Go through all actions and verify that the listed variable is valid.
            // TODO add other per action checks here
            
            bool VerifyActionInput(
                int actionIndex, 
                SerializedProperty varProp, 
                CyanTriggerActionInstanceRenderData data)
            {
                var isVariable = varProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
                
                if (!isVariable.boolValue)
                {
                    data.ContainsNull |= CyanTriggerPropertyEditor.InputContainsNullVariableOrValue(varProp);
                    return false;
                }
            
                var varId = varProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID));
                var varName = varProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.name));
                var curGuid = varId.stringValue;
                var curName = varName.stringValue;

                bool modified = false;
                if (!IsVariableValid(actionIndex, curGuid, curName))
                {
                    varName.stringValue = "";
                    varId.stringValue = "";
                    modified = true;
                }

                data.ContainsNull |= CyanTriggerPropertyEditor.InputContainsNullVariableOrValue(varProp);

                return modified;
            }

            bool VerifyAction(int actionIndex,
                bool beforeInputs,
                SerializedProperty actionProp,
                CyanTriggerActionInstanceRenderData data)
            {
                if (!beforeInputs)
                {
                    return false;
                }

                // Initializing so nothing has nulls
                data.ContainsNull = false;
                
                // Check if allows multi input and list is zero
                var variableDefs = data.ActionInfo.GetVariables();
                if (variableDefs.Length > 0)
                {
                    var varDef = variableDefs[0];
                    if ((varDef.variableType & CyanTriggerActionVariableTypeDefinition.AllowsMultiple) != 0)
                    {
                        var multiInputs = actionProp.FindPropertyRelative(nameof(CyanTriggerActionInstance.multiInput));
                        int multiSize = multiInputs.arraySize;
                        data.ContainsNull = multiSize == 0;
                    }
                }

                return false;
            }

            UpdateActionInputs(VerifyActionInput, VerifyAction);
        }

        private CyanTriggerActionInstanceRenderData GetOrCreateExpandData(int id, bool forceCreate = false)
        {
            var data = GetData(id);
            int index = GetItemIndex(id);
            if (forceCreate || data == null)
            {
                data = new CyanTriggerActionInstanceRenderData();
                SetData(id, data);
            }

            data.Property = index < ItemElements.Length ? 
                ItemElements[index] : 
                Elements.GetArrayElementAtIndex(index);
            
            return data;
        }

        private bool ShouldShowVariantSelector(CyanTriggerActionInstanceRenderData actionData)
        {
            var definition = actionData.ActionInfo.definition;
            return definition == null ||
                !(definition.definitionType == CyanTriggerNodeDefinition.UdonDefinitionType.CyanTriggerSpecial ||
                  definition.definitionType == CyanTriggerNodeDefinition.UdonDefinitionType.CyanTriggerVariable ||
                  definition.definitionType == CyanTriggerNodeDefinition.UdonDefinitionType.Type);
        }
        
        private bool ItemCanExpand(CyanTriggerActionInstanceRenderData actionData)
        {
            return actionData.ActionInfo.IsValid() && 
                   (ShouldShowVariantSelector(actionData) || actionData.ActionInfo.GetVariables().Length > 0);
        }

        private bool ItemHasComment(CyanTriggerActionInstanceRenderData actionData)
        {
            string comment = actionData.Comment;
            if (!string.IsNullOrEmpty(comment))
            {
                return true;
            }
            
            var definition = actionData.ActionInfo.definition;
            return definition?.definition == CyanTriggerCustomNodeComment.NodeDefinition;
        }

        public void UpdateAllItemDisplayNames()
        {
            for (int i = 0; i < Items.Length; ++i)
            {
                if (Items[i] != null)
                {
                    Items[i].displayName = GetElementDisplayName(ItemElements[i]);
                }
            }
        }

        private void UpdateActionInputs(
            Func<int, SerializedProperty, CyanTriggerActionInstanceRenderData, bool> inputUpdateMethod, 
            Func<int, bool, SerializedProperty, CyanTriggerActionInstanceRenderData, bool> actionUpdateMethod)
        {
            bool anyChanges = false;
            for (int curAction = 0; curAction < Items.Length; ++curAction)
            {
                var item = Items[curAction];
                if (item == null)
                {
                    continue;
                }

                var data = GetData(item.id);
                if (data?.ActionInfo == null)
                {
                    continue;
                }
                
                var variableDefs = data.ActionInfo.GetVariables();
                var property = data.Property;
                var inputs = property.FindPropertyRelative(nameof(CyanTriggerActionInstance.inputs));
                int size = inputs.arraySize;

                bool modified = false;
                
                if (actionUpdateMethod != null)
                {
                    modified |= actionUpdateMethod(curAction, true, property, data);
                }
                
                if (inputUpdateMethod != null)
                {
                    for (int curInput = 0; curInput < variableDefs.Length; ++curInput)
                    {
                        var varDef = variableDefs[curInput];
                        if (curInput == 0 && 
                            (varDef.variableType & CyanTriggerActionVariableTypeDefinition.AllowsMultiple) != 0)
                        {
                            var multiInputs = property.FindPropertyRelative(nameof(CyanTriggerActionInstance.multiInput));
                            int multiSize = multiInputs.arraySize;
                        
                            for (int curMultiInput = 0; curMultiInput < multiSize; ++curMultiInput)
                            {
                                var multiInputProp = multiInputs.GetArrayElementAtIndex(curMultiInput);
                                modified |= inputUpdateMethod(curAction, multiInputProp, data);
                            }
                        
                            continue;
                        }

                        if (curInput >= size)
                        {
                            break;
                        }
                    
                        var inputProp = inputs.GetArrayElementAtIndex(curInput);
                        modified |= inputUpdateMethod(curAction, inputProp, data);
                    }
                }
                
                // Ensure inputs are verified first.                
                if (actionUpdateMethod != null)
                {
                    modified |= actionUpdateMethod(curAction, false, property, data);
                }

                if (modified)
                {
                    item.displayName = GetElementDisplayName(data.Property);
                    data.ClearInputLists();
                    anyChanges = true;
                }
            }

            if (anyChanges)
            {
                _delayRefreshRowHeight = true;
            }
        }

        private void UpdateVariableNamesFromProvider(int actionId, int curUndo)
        {
            var data = GetData(actionId);
            if (data == null)
            {
                return;
            }
            
            var variables = data.ActionInfo.GetCustomEditorVariableOptions(data.Property);
            if (variables == null || variables.Length == 0)
            {
                return;
            }
                
            Dictionary<string, string> updatedGuids = new Dictionary<string, string>();
            foreach (var variable in variables)
            {
                updatedGuids.Add(variable.ID,variable.Name);
            }
            
            UpdateVariableNames(updatedGuids);
            Elements.serializedObject.ApplyModifiedProperties();
            
            OnActionChanged?.Invoke();
            
            Undo.CollapseUndoOperations(curUndo-1);
        }
        
        public void UpdateVariableNames(Dictionary<string, string> guidToNames)
        {
            if (guidToNames == null)
            {
                return;
            }
            
            bool UpdateVariableProperty(
                int actionIndex, 
                SerializedProperty varProp, 
                CyanTriggerActionInstanceRenderData data)
            {
                var isVariable = varProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
                if (!isVariable.boolValue)
                {
                    return false;
                }
                
                var varId = varProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID));
                var varName = varProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.name));

                var curGuid = varId.stringValue;
                if (guidToNames.TryGetValue(curGuid, out string newName))
                {
                    varName.stringValue = newName;
                    return true;
                }

                // Handle when changed variable had a callback and this variable is using the previous version of it.
                if (CyanTriggerCustomNodeOnVariableChanged.IsPrevVariable(varName.stringValue, curGuid))
                {
                    string varMainId = CyanTriggerCustomNodeOnVariableChanged.GetMainVariableId(curGuid);
                    if (!string.IsNullOrEmpty(varMainId) && guidToNames.TryGetValue(varMainId, out newName))
                    {
                        varId.stringValue = CyanTriggerCustomNodeOnVariableChanged.GetPrevVariableGuid(
                            CyanTriggerCustomNodeOnVariableChanged.GetOldVariableName(newName),
                            varMainId);
                        return true;
                    }
                }

                return false;
            }

            UpdateActionInputs(UpdateVariableProperty, null);
        }

        public void DeleteVariables(HashSet<string> guids, HashSet<string> names)
        {
            if (guids.Count == 0 && names.Count == 0)
            {
                return;
            }
            
            bool ClearDeletedVariableProperty(
                int actionIndex, 
                SerializedProperty varProp, 
                CyanTriggerActionInstanceRenderData data)
            {
                var isVariable = varProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
                if (!isVariable.boolValue)
                {
                    return false;
                }
                
                var varId = varProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID));
                var varName = varProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.name));
                var curGuid = varId.stringValue;
                var curName = varName.stringValue;
                
                if (guids.Contains(curGuid) || (string.IsNullOrEmpty(curGuid) && names.Contains(curName)))
                {
                    varName.stringValue = "";
                    varId.stringValue = "";
                    return true;
                }
                
                return false;
            }

            UpdateActionInputs(ClearDeletedVariableProperty, null);
        }

        public void UndoReset()
        {
            ClearData();
            Reload();
            OnActionChanged?.Invoke();
        }
        
        public void DoLayoutTree()
        {
            _mouseOverId = -1;

            bool showView = _showActions.target;
            
            CyanTriggerPropertyEditor.DrawFoldoutListHeader(
                new GUIContent("Actions (" + VisualSize +")"),
                ref showView,
                false,
                0,
                null,
                false,
                null,
                false,
                true
                );
            _showActions.target = showView;
            
            if (!EditorGUILayout.BeginFadeGroup(_showActions.faded))
            {
                EditorGUILayout.EndFadeGroup();
                return;
            }
            
            if (Size != Elements.arraySize)
            {
                ClearData();
                Reload();
                _delayActionsChanged = true;
            }
            
            if (_delayActionsChanged)
            {
                _delayActionsChanged = false;
                OnActionChanged?.Invoke();
            }

            
            if (!_initialized)
            {
                _initialized = true;
                VerifyActions();
            }


            // Remove selection when treeview is not focused. I may have to remove this later if it is annoying.
            if (!HasFocus() && HasSelection())
            {
                SetSelection(new List<int>(), TreeViewSelectionOptions.FireSelectionChanged);
            }
            
            
            // Action has a comment being edited. Check if we it needs to be closed. 
            if (_editingCommentId != -1)
            {
                // Started editing a comment, refresh row heights
                if (!_focusedCommentEditor)
                {
                    _delayRefreshRowHeight = true;
                }

                // Detect if we should close the comment editor
                Event cur = Event.current;
                bool enterPressed = cur.type == EventType.KeyDown &&
                                    cur.keyCode == KeyCode.Return &&
                                    (cur.shift || cur.alt || cur.command || cur.control);
                if ((_focusedCommentEditor && GUI.GetNameOfFocusedControl() != CommentControlName) ||
                    enterPressed)
                {
                    _editingCommentId = -1;
                    GUI.FocusControl(null);
                    _delayRefreshRowHeight = true;

                    if (enterPressed)
                    {
                        cur.Use();
                    }
                }
            }
            
            
            Rect treeRect = EditorGUILayout.BeginVertical();
#if !UNITY_2019_3_OR_NEWER
            treeRect.x += 1;
            treeRect.width -= 2;
#endif
            
            // Calculate if we need to update row heights before getting tree's height for layout purposes.
            if (treeRect.width > 0)
            {
                if (_lastRectWidth <= 0 || treeRect.width != _lastRectWidth)
                {
                    _delayRefreshRowHeight = true;
                }
                _lastRectWidth = treeRect.width;
            }
            
            if (_delayRefreshRowHeight)
            {
                _delayRefreshRowHeight = false;
                RefreshCustomRowHeights();
            }
            
            treeRect.height = totalHeight + (Size == 0 ? DefaultRowHeight : 0);
            GUILayout.Space(treeRect.height+1);
            
            var listActionFooterIcons = new[]
            {
                new GUIContent("SDK2", "Add action from list of SDK2 actions"),
                EditorGUIUtility.TrIconContent("Favorite", "Add action from favorites actions"),
                EditorGUIUtility.TrIconContent("Toolbar Plus", "Add action from all actions"),
                EditorGUIUtility.TrIconContent("FilterByType", "Add Local Variable"),
                EditorGUIUtility.TrIconContent("TreeEditor.Duplicate", "Duplicate selected item"),
                EditorGUIUtility.TrIconContent("Toolbar Minus", "Remove selection from list")
            };
            
            bool hasSelection = HasSelection();
            CyanTriggerPropertyEditor.DrawButtonFooter(
                listActionFooterIcons, 
                new Action[]
                {
                    AddNewActionFromSDK2List,
                    AddNewActionFromFavoriteList,
                    AddNewActionFromAllList,
                    AddLocalVariable,
                    DuplicateSelectedItems,
                    RemoveSelected
                },
                new []
                {
                    false, false, false, false, !hasSelection, !hasSelection
                });

            
            // Draw the treeview!
            OnGUI(treeRect);
            
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndFadeGroup();
            
            if (_delayRefreshRowHeight)
            {
                _delayRefreshRowHeight = false;
                RefreshCustomRowHeights();
                Repaint();
            }
        }

        public void RefreshHeight()
        {
            foreach (var data in GetData())
            {
                data.Item1.UpdateExpandCache();
            }
            
            _delayRefreshRowHeight = false;
            RefreshCustomRowHeights();
        }

        private float GetLabelHeight(CyanTriggerScopedTreeItem item, CyanTriggerActionInstanceRenderData data, float indent)
        {
            if (data.ActionInfo?.definition?.definition == CyanTriggerCustomNodeComment.NodeDefinition)
            {
                return 0;
            }
            
            float width = _lastRectWidth - CellHorizontalMargin * 2 - ExpandButtonSize - 2 - indent;
            float height = CyanTriggerEditorGUIUtil.TreeViewLabelStyle.CalcHeight(new GUIContent(item.displayName), width);

            return height;
        }
        
        private float GetCommentHeight(int id, CyanTriggerActionInstanceRenderData expandData, float indent)
        {
            if (!ItemHasComment(expandData) && _editingCommentId != id)
            {
                return 0;
            }

            float width = _lastRectWidth - CellHorizontalMargin * 2 - ExpandButtonSize - 2 - indent;
            float height = CyanTriggerEditorGUIUtil.CommentStyle.CalcHeight(new GUIContent("// "+expandData.Comment), width);
            return height;
        }

        protected override float GetCustomRowHeight(int row, TreeViewItem item)
        {
            var scopedItem = (CyanTriggerScopedTreeItem) item;
            CyanTriggerActionInstanceRenderData data = GetOrCreateExpandData(item.id);
            float indent = GetContentIndent(item);
            bool canExpand = ItemCanExpand(data);

            float height = 2 * CellVerticalMargin; // top and bottom margin for labels
            height += GetLabelHeight(scopedItem, data, indent);
            height += GetCommentHeight(scopedItem.id, data, indent);
            
            if (canExpand && data.IsExpanded)
            {
                int index = scopedItem.Index;
                // Should show drop down
                bool shouldShowVariants = ShouldShowVariantSelector(data);
                if (shouldShowVariants)
                {
                    height += DefaultRowHeight;
                }

                float inputHeight = CyanTriggerPropertyEditor.GetHeightForActionInstanceInputEditors(
                    data, 
                    type => GetVariableOptions(type, index));

                height += inputHeight;
                
                // Add separator spacing
                if (shouldShowVariants && inputHeight > 5)
                {
                    height += SpaceBetweenRowEditor * 2;
                }
                height += SpaceBetweenRowEditor + SpaceAboveRowEditor;
            }

            return height;
        }
        
        protected override void OnRowGUI(RowGUIArgs args)
        {
            var item = (CyanTriggerScopedTreeItem)args.item;
            var data = GetOrCreateExpandData(item.id);
            bool canExpand = ItemCanExpand(data);
            
            Rect rowRect = args.rowRect;
            if (rowRect.Contains(Event.current.mousePosition))
            {
                _mouseOverId = item.id;
            }
            
            
            float itemIndent = GetContentIndent(item);
            float actionLabelHeight = GetLabelHeight(item, data, itemIndent);
            float commentHeight = GetCommentHeight(item.id, data, itemIndent);
            
            // Draw Row comment and label
            float labelHeight = DrawRowLabel(
                rowRect, 
                itemIndent,
                actionLabelHeight,
                commentHeight,
                item, 
                data, 
                args.label, 
                canExpand, 
                args.selected, 
                args.focused);
            
            
            var boxStyle = new GUIStyle
            {
                border = new RectOffset(1, 1, 1, 1), 
                normal =
                {
                    background = data.ContainsNull
                        ? CyanTriggerImageResources.ActionTreeWarningOutline
                        : CyanTriggerImageResources.ActionTreeOutlineTop
                }
            };

            // This is the last visible row, draw the bottom border so that it is obvious it ends here.
            GetFirstAndLastVisibleRows(out _, out var last);
            if (last == args.row)
            {
                GUI.Box(new Rect(rowRect.x, rowRect.yMax-1, rowRect.width, 1), GUIContent.none, boxStyle);
            } 
            
            // Move action editor to the left to make it more obvious about scope
            float foldoutIndent = GetFoldoutIndent(item) - 2;
            if (item.depth > 0)
            {
                var lineStyle = new GUIStyle
                {
                    normal = { background = CyanTriggerImageResources.ActionTreeGrayBox }
                };
                float space = foldoutIndent / item.depth;
                Rect lineRect = new Rect(rowRect.x, rowRect.y, 1, rowRect.height);
                for (int i = 0; i < item.depth; ++i)
                {
                    GUI.Box(lineRect, GUIContent.none, lineStyle);
                    lineRect.x += space;
                }
                
                rowRect.xMin += foldoutIndent;
            }
            
            // Draw an outline around the element to emphasize what you are editing.
            GUI.Box(rowRect, GUIContent.none, boxStyle);
            
            if (!canExpand || !data.IsExpanded || _delayRefreshRowHeight)
            {
                return;
            }
            
            bool isUndo = (Event.current.commandName == "UndoRedoPerformed");
            if (isUndo)
            {
                data.NeedsRedraws = true;
                ClearInputList(data);
            }

            // Move usable area over by the size of the foldout icon. This is to make it more obvious what scope is included.
            rowRect.xMin += CyanTriggerEditorGUIUtil.FoldoutStyle.fixedWidth;

            // Remove top area from Row
            rowRect.yMin += labelHeight;


            // Draw rect around area to separate it from everything else
            rowRect.x += SpaceBetweenRowEditorSides;
            rowRect.width -= SpaceBetweenRowEditorSides * 2;
            rowRect.yMin += SpaceAboveRowEditor;
            rowRect.height -= SpaceBetweenRowEditor;
            
            if (Event.current.type == EventType.Repaint)
            {
                // Draw background to overwrite the selection blue
                Rect minimalRect = new Rect(rowRect.x + 1, rowRect.y + 1, rowRect.width - 2, rowRect.height - 2);
                DefaultStyles.backgroundEven.Draw(minimalRect, false, false, false, false);
            
                // Draw the rounded rectangle to contain all inputs
                CyanTriggerEditorGUIUtil.HelpBoxStyle.Draw(rowRect, false, false, false, false); 
            }

            rowRect.yMin += SpaceBetweenRowEditor;

            rowRect.x += SpaceBetweenRowEditorSides;
            rowRect.width -= SpaceBetweenRowEditorSides * 2;

            if (ShouldShowVariantSelector(data))
            {
                Rect variantRect = new Rect(rowRect);
                variantRect.height = DefaultRowHeight;
                
                DrawVariantSelector(variantRect, args, data);
            
                rowRect.yMin += DefaultRowHeight + SpaceBetweenRowEditor * 2;

                if (rowRect.height > EditorGUIUtility.singleLineHeight)
                {
                    float sideSpace = 5;
                    float lift = SpaceBetweenRowEditor * 1.5f;
                    Rect separatorRect = new Rect(rowRect.x + sideSpace, rowRect.y - lift, rowRect.width - sideSpace * 2, 1);
                    EditorGUI.DrawRect(separatorRect, CyanTriggerImageResources.LineColorDark);
                }
            }

            int undoGroup = Undo.GetCurrentGroup();
            EditorGUI.BeginChangeCheck();
            int index = item.Index;
            CyanTriggerPropertyEditor.DrawActionInstanceInputEditors(
                data, 
                type => GetVariableOptions(type, index), 
                rowRect, 
                false);
            
            if (_lastKeyboardControl != GUIUtility.keyboardControl)
            {
                // Previous item had modifications before keyboard focus changed. Try update variable names.
                if (_itemHasChanges)
                {
                    UpdateVariableNamesFromProvider(_focusedActionId, undoGroup);
                }
                
                _lastKeyboardControl = GUIUtility.keyboardControl;
                _focusedActionId = item.id;
                _itemHasChanges = false;
            }
            
            if (undoGroup != Undo.GetCurrentGroup() && _itemHasChanges)
            {
                UpdateVariableNamesFromProvider(item.id, undoGroup);
            }

            // TODO try to minimize the number of times this gets called...
            if (EditorGUI.EndChangeCheck() || data.NeedsRedraws || isUndo)
            {
                item.displayName = GetElementDisplayName(data.Property);
                
                float newActionLabelHeight = GetLabelHeight(item, data, itemIndent);
                if (newActionLabelHeight != actionLabelHeight)
                {
                    _delayRefreshRowHeight = true;
                }

                if (_focusedActionId == item.id)
                {
                    _itemHasChanges = true;
                }
            }

            if (data.NeedsRedraws)
            {
                data.NeedsRedraws = false;
                _delayRefreshRowHeight = true;
            }
        }

        private float DrawRowLabel(
            Rect rowRect, 
            float itemIndent,
            float actionLabelHeight,
            float commentHeight,
            CyanTriggerScopedTreeItem item, 
            CyanTriggerActionInstanceRenderData data, 
            string label, 
            bool canExpand, 
            bool selected, 
            bool focused)
        {
            float labelHeight = actionLabelHeight + commentHeight + CellVerticalMargin * 2;

            Rect cellRect = new Rect(rowRect);
            cellRect.yMin += CellVerticalMargin;
            
            Rect commentRect = new Rect(cellRect);
            commentRect.height = commentHeight;
            
            cellRect.yMin += commentHeight;

            if (canExpand)
            {
                Rect expandButtonRect = new Rect(rowRect.xMax - CellHorizontalMargin - ExpandButtonSize, cellRect.y - 1,
                    ExpandButtonSize, ExpandButtonSize);
                
                // Draw expand button on right of the row.
                GUIContent expandIcon = data.IsExpanded
                    ? CyanTriggerEditorGUIUtil.CloseActionEditorIcon
                    : CyanTriggerEditorGUIUtil.OpenActionEditorIcon;
                if (GUI.Button(expandButtonRect, expandIcon, new GUIStyle()))
                {
                    data.IsExpanded = !data.IsExpanded;
                    _delayRefreshRowHeight = true;
                }
            }

            // Draw foldout for items with scope, even if they have no children
            if (item.HasScope)
            {
                int id = item.id;
                
                float foldoutIndent = GetFoldoutIndent(item);
                GUIStyle foldoutStyle = CyanTriggerEditorGUIUtil.FoldoutStyle;
                Rect foldoutRect = new Rect(cellRect.x + foldoutIndent, cellRect.y, foldoutStyle.fixedWidth, foldoutStyle.lineHeight);
                bool isExpanded = IsExpanded(id);
                bool newExpand = GUI.Toggle(foldoutRect, isExpanded, GUIContent.none, foldoutStyle);
                if (isExpanded != newExpand)
                {
                    if (Event.current.alt)
                    {
                        SetExpandedRecursive(id, newExpand);
                    }
                    else
                    {
                        SetExpanded(id, newExpand);
                    }
                }
            }

            float rightMargin = CellHorizontalMargin * 2 + ExpandButtonSize;
            cellRect.width -= rightMargin;
            commentRect.width -= rightMargin;
                
            cellRect.xMin += itemIndent;
            commentRect.xMin += itemIndent;
            
            
            // Draw comment and action labels
            if (Event.current.rawType == EventType.Repaint)
            {
                if (commentHeight > 0)
                {
                    CyanTriggerEditorGUIUtil.CommentStyle.Draw(commentRect, "// "+data.Comment, false, false, selected, false);
                }
                CyanTriggerEditorGUIUtil.TreeViewLabelStyle.Draw(cellRect, label, false, false, selected, focused);
            }
            
            // Draw comment editor
            if (_editingCommentId == item.id)
            {
                Event cur = Event.current;
                bool wasEscape = (cur.type == EventType.KeyDown && cur.keyCode == KeyCode.Escape);

                GUI.SetNextControlName(CommentControlName);
                string comment = EditorGUI.TextArea(commentRect, data.Comment, EditorStyles.textArea);
                data.Comment = comment;

                if (!_focusedCommentEditor)
                {
                    _focusedCommentEditor = true;
                    EditorGUI.FocusTextInControl(CommentControlName);
                }
                
                float newHeight = GetCommentHeight(item.id, data, itemIndent);
                if (newHeight != commentHeight)
                {
                    _delayRefreshRowHeight = true;
                }
                
                Rect completeButtonRect = new Rect(rowRect.xMax - CellHorizontalMargin - ExpandButtonSize, rowRect.y,
                    ExpandButtonSize, EditorGUIUtility.singleLineHeight);
                
                
                bool completeButton = GUI.Button(completeButtonRect, CyanTriggerEditorGUIUtil.CommentCompleteIcon, new GUIStyle());
                
                if (wasEscape || completeButton)
                {
                    if (comment.Length > 0 && comment.Trim().Length == 0)
                    {
                        data.Comment = "";
                    }
                    
                    _editingCommentId = -1;
                    GUI.FocusControl(null);
                    _delayRefreshRowHeight = true;
                }
            }
            
            return labelHeight;
        }
        

        private void DrawVariantSelector(Rect rect, RowGUIArgs args, CyanTriggerActionInstanceRenderData data)
        {
            int variantCount = CyanTriggerActionGroupDefinitionUtil.GetActionVariantCount(data.ActionInfo);
            
            float spaceBetween = 5;
            float width = (rect.width - spaceBetween * 2) / 3f;

            Rect labelRect = new Rect(rect.x, rect.y, width, rect.height);
            GUI.Label(labelRect, new GUIContent($"Action Variants ({variantCount})"));
            Rect buttonRect = new Rect(labelRect.xMax + spaceBetween, rect.y, rect.width - spaceBetween - labelRect.width, rect.height);
            
            EditorGUI.BeginDisabledGroup(variantCount <= 1);
            if (GUI.Button(buttonRect, data.ActionInfo.GetMethodSignature(), new GUIStyle(EditorStyles.popup)))
            {
                GenericMenu menu = new GenericMenu();
                
                foreach (var actionVariant in CyanTriggerActionGroupDefinitionUtil.GetActionVariantInfoHolders(data.ActionInfo))
                {
                    menu.AddItem(new GUIContent(actionVariant.GetMethodSignature()), false, (t) =>
                    {
                        var actionInfo = (CyanTriggerActionInfoHolder) t;
                        if (actionInfo == data.ActionInfo)
                        {
                            return;
                        }
                        
                        CyanTriggerSerializedPropertyUtils.SetActionData(actionInfo, ItemElements[GetItemIndex(args.item.id)]);
                        data = GetOrCreateExpandData(args.item.id, true);
                        data.IsExpanded = true;

                        _delayActionsChanged = true;
                        _delayRefreshRowHeight = true;
                    }, actionVariant);
                }
                
                menu.ShowAsContext();
            }
            EditorGUI.EndDisabledGroup();
        }
        
        protected override bool CanDuplicate(IEnumerable<int> items)
        {
            foreach (int id in items)
            {
                int index = GetItemIndex(id);
                if (!CanItemBeMoved(Items[index]))
                {
                    return false;
                }
            }
            
            return true;
        }

        protected override List<int> DuplicateItems(IEnumerable<int> items)
        {
            List<int> newIds = new List<int>();
            HashSet<int> duplicatedInd = new HashSet<int>();
            List<int> sortedItems = new List<int>(items);
            sortedItems.Sort();

            Dictionary<string, string> variableGuidMap = new Dictionary<string, string>();
            
            foreach (int id in sortedItems)
            {
                int index = GetItemIndex(id);
                if (duplicatedInd.Contains(index))
                {
                    continue;
                }
                
                var item = Items[index];
                for (int i = item.Index; i <= item.ScopeEndIndex; ++i)
                {
                    DuplicateAction(ItemElements[i], variableGuidMap);
                    newIds.Add(Elements.arraySize - 1 + IdStartIndex);
                    duplicatedInd.Add(i);
                }
            }

            return newIds;
        }
        
        protected override List<SerializedProperty> GetProperties(IEnumerable<int> items)
        {
            List<SerializedProperty> res = new List<SerializedProperty>();
            HashSet<int> duplicatedInd = new HashSet<int>();
            List<int> sortedItems = new List<int>(items);
            sortedItems.Sort();

            foreach (int id in sortedItems)
            {
                int index = GetItemIndex(id);
                if (duplicatedInd.Contains(index))
                {
                    continue;
                }
                
                var item = Items[index];
                for (int i = item.Index; i <= item.ScopeEndIndex; ++i)
                {
                    res.Add(ItemElements[i]);
                    duplicatedInd.Add(i);
                }
            }

            return res;
        }
        
        protected override List<SerializedProperty> DuplicateProperties(IEnumerable<SerializedProperty> items)
        {
            Dictionary<string, string> variableGuidMap = new Dictionary<string, string>();
            List<SerializedProperty> ret = new List<SerializedProperty>();
            
            foreach (var property in items)
            {
                ret.Add(DuplicateAction(property, variableGuidMap));
            }

            return ret;
        }

        protected override bool AllowRenameOption()
        {
            return false;
        }

        protected override void GetRightClickMenuOptions(GenericMenu menu, Event currentEvent)
        {
            base.GetRightClickMenuOptions(menu, currentEvent);

            if (_mouseOverId != -1)
            {
                var data = GetData(_mouseOverId);
                
                menu.AddSeparator("");
                menu.AddItem(new GUIContent(ItemHasComment(data) ? "Edit Comment" : "Add Comment"), false, () =>
                {
                    _editingCommentId = _mouseOverId;
                    _focusedCommentEditor = false;
                    SetSelection(new List<int>(), TreeViewSelectionOptions.FireSelectionChanged);
                });
                
                if (ItemCanExpand(data))
                {
                    GUIContent expandContent = data.IsExpanded
                        ? new GUIContent("Close Action Editor")
                        : new GUIContent("Open Action Editor");
                    menu.AddItem(expandContent, false, () =>
                    {
                        _delayRefreshRowHeight = true;
                        data.IsExpanded = !data.IsExpanded;
                    });
                }
            }
            
            menu.AddSeparator("");
            int curMouseOver = _mouseOverId;
            
            // Add new actions at the parent of the selected
            menu.AddItem(new GUIContent("Add Local Variable"), false, () =>
            {
                CyanTriggerSearchWindowManager.Instance.DisplayVariableSearchWindow(
                    GUIUtility.GUIToScreenPoint(currentEvent.mousePosition),
                    definition => AddNewActionAtSelected(definition, curMouseOver));
            });
            menu.AddItem(new GUIContent("Add Favorite Action"), false, () =>
            {
                CyanTriggerSearchWindowManager.Instance.DisplayActionFavoritesSearchWindow(
                    GUIUtility.GUIToScreenPoint(currentEvent.mousePosition),
                    item => AddNewActionAtSelected(item, curMouseOver));
            });
            menu.AddItem(new GUIContent("Add Action"), false, () =>
            {
                CyanTriggerSearchWindowManager.Instance.DisplayActionSearchWindow(
                    GUIUtility.GUIToScreenPoint(currentEvent.mousePosition),
                    holder => AddNewActionDirectAtSelected(holder, curMouseOver));
            });
        }
        
        protected override void DoubleClickedItem(int id)
        {
            var data = GetData(id);
            if (data == null || !ItemCanExpand(data))
            {
                return;
            }

            data.IsExpanded = !data.IsExpanded;
            _delayRefreshRowHeight = true;
        }
        
        protected override bool CanItemBeRemoved(CyanTriggerScopedTreeItem item)
        {
            return !IsItemHidden(item);
        }
        
        protected override bool CanItemBeMoved(CyanTriggerScopedTreeItem item)
        {
            return !IsItemHidden(item);
        }

        // TODO create a better method for this instead of implicitly using hidden
        private bool IsItemHidden(CyanTriggerScopedTreeItem item)
        {
            var data = GetOrCreateExpandData(item.id);
            var definition = data.ActionInfo.definition;
            return definition != null &&
                   CyanTriggerNodeDefinitionManager.DefinitionIsHidden(definition.fullName);
        }

        // TODO allow force setting the parent if it doesn't allow direct children.
        // Ex: if should drop item into condition. This requires modifying both args and parent.
        protected override bool ShouldRejectDragAndDrop(DragAndDropArgs args, CyanTriggerScopedTreeItem parent)
        {
            if (parent == rootItem)
            {
                return false;
            }

            var data = GetOrCreateExpandData(parent.id);
            var definition = data.ActionInfo.definition;
            return definition != null &&
                   CyanTriggerNodeDefinitionManager.DefinitionPreventsDirectChildren(definition.fullName);
        }
        
        private void AddLocalVariable()
        {
            CyanTriggerSearchWindowManager.Instance.DisplayVariableSearchWindow(AddNewAction);
        }
        
        private void AddNewActionFromAllList()
        {
            CyanTriggerSearchWindowManager.Instance.DisplayActionSearchWindow(AddNewActionDirect);
        }

        private void AddNewActionFromFavoriteList()
        {
            CyanTriggerSearchWindowManager.Instance.DisplayActionFavoritesSearchWindow(AddNewAction);
        }
        
        private void AddNewActionFromSDK2List()
        {
            CyanTriggerSearchWindowManager.Instance.DisplaySDK2ActionFavoritesSearchWindow(AddNewAction);
        }

        private void AddNewAction(UdonNodeDefinition udonNode)
        {
            AddNewActionDirect(CyanTriggerActionInfoHolder.GetActionInfoHolder(udonNode));
        }
        
        private void AddNewAction(CyanTriggerSettingsFavoriteItem favorite)
        {
            AddNewActionDirect(CyanTriggerActionInfoHolder.GetActionInfoHolder(favorite));
        }

        private void AddNewActionDirect(CyanTriggerActionInfoHolder actionInfoHolder)
        {
            AddNewAction(actionInfoHolder);
        }
        
        private void AddNewActionAtSelected(UdonNodeDefinition udonNode, int insertNodeIndex)
        {
            AddNewActionDirectAtSelected(CyanTriggerActionInfoHolder.GetActionInfoHolder(udonNode), insertNodeIndex);
        }
        
        private void AddNewActionAtSelected(CyanTriggerSettingsFavoriteItem favorite, int insertNodeIndex)
        {
            AddNewActionDirectAtSelected(CyanTriggerActionInfoHolder.GetActionInfoHolder(favorite), insertNodeIndex);
        }

        private void AddNewActionDirectAtSelected(CyanTriggerActionInfoHolder actionInfoHolder, int insertNodeIndex)
        {
            int size = Elements.arraySize;
            AddNewAction(actionInfoHolder);
            Reload();

            CyanTriggerScopedTreeItem insertNode = null;
            if (insertNodeIndex != -1)
            {
                insertNode = Items[GetItemIndex(insertNodeIndex)];
            }
            
            var itemsToMove = new List<int> { IdStartIndex + size };
            
            CyanTriggerScopedTreeItem insertParent = insertNode;
            DragAndDropPosition dragPosition;
            int insertIndex = 0;
            if (insertParent == null)
            {
                insertParent = (CyanTriggerScopedTreeItem)rootItem;
                dragPosition = DragAndDropPosition.BetweenItems;
            }
            else if (insertParent.HasScope)
            {
                // if, elseif, while. Try putting it in the condition body
                if (ShouldRejectDragAndDrop(new DragAndDropArgs(), insertParent))
                {
                    SetExpanded(insertParent.id, true);
                    insertParent = (CyanTriggerScopedTreeItem)insertParent.children[1];
                    Debug.Assert(GetData(insertParent.id).ActionInfo.definition.fullName == CyanTriggerCustomNodeConditionBody.NodeDefinition.fullName);
                }
                dragPosition = DragAndDropPosition.UponItem;
            } 
            else 
            {
                insertParent = (CyanTriggerScopedTreeItem) insertNode.parent;
                insertIndex = insertParent.children.IndexOf(insertNode) + 1;
                dragPosition = DragAndDropPosition.BetweenItems;
            }

            MoveElements(itemsToMove, insertParent, dragPosition, insertIndex);
        }
        
        private List<SerializedProperty> AddNewAction(
            CyanTriggerActionInfoHolder actionInfoHolder, 
            bool includeDependencies = true)
        {
            int startIndex = Elements.arraySize;
            var newProperties = actionInfoHolder.AddActionToEndOfPropertyList(Elements, includeDependencies);
            OnActionChanged?.Invoke();

            for (int i = startIndex; i < Elements.arraySize; ++i)
            {
                int id = i + IdStartIndex;
                SetExpanded(id, true);

                if (includeDependencies)
                {
                    GetOrCreateExpandData(id).IsExpanded = true;
                }
            }

            return newProperties;
        }

        private SerializedProperty DuplicateAction(
            SerializedProperty actionProperty,
            Dictionary<string, string> variableGuidMap)
        {
            var actionInfo = CyanTriggerActionInfoHolder.GetActionInfoHolderFromProperties(actionProperty);
            var dupedPropertyList = AddNewAction(actionInfo, false);
            Debug.Assert(dupedPropertyList.Count == 1,
                "Duplicating a property returned unexpected size! " + dupedPropertyList.Count);
            var dupedProperty = dupedPropertyList[0];
            
            CyanTriggerSerializedPropertyUtils.CopyDataAndRemapVariables(actionInfo, actionProperty, dupedProperty, variableGuidMap);

            return dupedProperty;
        }
        
        protected override void OnItemsRemoved(List<CyanTriggerScopedTreeItem> removedItems)
        {
            base.OnItemsRemoved(removedItems);
            
            HashSet<string> removedGuids = new HashSet<string>();
            foreach (var item in removedItems)
            {
                var data = GetData(item.id);
                if (data == null)
                {
                    continue;
                }

                var info = data.ActionInfo;
                if (info == null)
                {
                    continue;
                }

                var variables = info.GetCustomEditorVariableOptions(ItemElements[item.Index]);
                if (variables == null || variables.Length == 0)
                {
                    continue;
                }

                foreach (var variable in variables)
                {
                    removedGuids.Add(variable.ID);
                }
            }
            DeleteVariables(removedGuids, new HashSet<string>());
            
            _delayActionsChanged = true;
        }

        protected override void OnElementsRemapped(int[] mapping, int prevIdStart)
        {
            base.OnElementsRemapped(mapping, prevIdStart);
            _delayActionsChanged = true;
        }

        protected override void OnElementRemapped(CyanTriggerActionInstanceRenderData element, int prevIndex, int newIndex)
        {
            ClearInputList(element);
        }

        private void ClearInputList(CyanTriggerActionInstanceRenderData element)
        {
            element.ClearInputLists();
        }
    }
}
