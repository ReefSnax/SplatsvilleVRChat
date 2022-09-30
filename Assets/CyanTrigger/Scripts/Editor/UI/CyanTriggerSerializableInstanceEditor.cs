using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace CyanTrigger
{
    public class CyanTriggerSerializableInstanceEditor
    {
        private static readonly GUIContent SelectToEditContent = new GUIContent("Select to Edit");
        private static readonly GUIContent UpdateOrderContent = new GUIContent("Update Order",
            "Set the value this CyanTrigger should be sorted by when calling Start, Update, LateUpdate, FixedUpdate, and PostLateUpdate. Lower values will be executed earlier.");
        private static readonly GUIContent ProgramSyncModeContent = new GUIContent("Sync Method",
            "How will synced variables within this CyanTrigger be handled?");
        private static readonly GUIContent InteractTextContent = new GUIContent("Interaction Text",
            "Text that will be shown to the user when they highlight an object to interact.");
        private static readonly GUIContent ProximityContent = new GUIContent("Proximity",
            "How close the user needs to be before the object can be interacted with. Note that this is not in unity units and the distance depends on the avatar scale.");

        private const string UnnamedCustomName = "Unnamed";
        
        private const string CommentControlName = "Event Editor Comment Control";

        private const int InvalidCommentId = -1;
        private const int CyanTriggerCommentId = -2;
        
        public static readonly CyanTriggerActionVariableDefinition AllowedUserGateVariableDefinition =
            new CyanTriggerActionVariableDefinition
            {
                type = new CyanTriggerSerializableType(typeof(string)),
                variableType = CyanTriggerActionVariableTypeDefinition.Constant |
                               CyanTriggerActionVariableTypeDefinition.VariableInput,
                displayName = "Allowed Users",
                description = "If the local user's name is in this list, they will be allowed to initiate this event."
            };
        public static readonly CyanTriggerActionVariableDefinition DeniedUserGateVariableDefinition =
            new CyanTriggerActionVariableDefinition
            {
                type = new CyanTriggerSerializableType(typeof(string)),
                variableType = CyanTriggerActionVariableTypeDefinition.Constant |
                               CyanTriggerActionVariableTypeDefinition.VariableInput,
                displayName = "Denied Users",
                description = "If the local user's name is in this list, they will be denied from initiating this event."
            };

        private static readonly HashSet<CyanTriggerSerializableInstanceEditor> OpenSerializers =
            new HashSet<CyanTriggerSerializableInstanceEditor>();

        private readonly SerializedObject _serializedObject;
        private readonly SerializedProperty _serializedProperty;
        private readonly SerializedProperty _dataInstanceProperty;
        private readonly CyanTriggerSerializableInstance _cyanTriggerSerializableInstance;
        private readonly CyanTriggerDataInstance _cyanTriggerDataInstance;
        

        private bool _showOtherSettings = true;
        private bool _showVariablesSection = true;
        
        private readonly CyanTriggerVariableTreeView _variableTreeView;
        private readonly SerializedProperty _variableDataProperty;

        private readonly Dictionary<Type, CyanTriggerEditorVariableOptionList> _userVariableOptions =
            new Dictionary<Type, CyanTriggerEditorVariableOptionList>();
        private readonly Dictionary<string, string> _userVariables = new Dictionary<string, string>();

        private CyanTriggerActionInstanceRenderData[] _eventInstanceRenderData = 
            new CyanTriggerActionInstanceRenderData[0];
        private CyanTriggerActionInstanceRenderData[] _eventOptionRenderData = 
            new CyanTriggerActionInstanceRenderData[0];

        private int _eventListSize;
        private readonly SerializedProperty _eventsProperty;
        private ReorderableList[] _eventActionUserGateLists = new ReorderableList[0];
        private Dictionary<int, ReorderableList>[] _eventActionInputLists = new Dictionary<int, ReorderableList>[0];
        private Dictionary<int, ReorderableList>[] _eventInputLists = new Dictionary<int, ReorderableList>[0];
        
        private int _editingCommentId = InvalidCommentId;
        private bool _editCommentButtonPressed = false;
        private bool _focusedCommentEditor = false;

        private CyanTriggerActionTreeView[] _eventActionTrees = new CyanTriggerActionTreeView[0];
        
        private bool _resetVariableInputs = false;

        private Editor _baseEditor;

        private CyanTriggerEditorScopeTree[] _scopeTreeRoot = new CyanTriggerEditorScopeTree[0];
        
        
        public CyanTriggerSerializableInstanceEditor( 
            SerializedProperty serializedProperty, 
            CyanTriggerSerializableInstance cyanTriggerSerializableInstance,
            Editor baseEditor)
        {
            OpenSerializers.Add(this);
            
            _serializedProperty = serializedProperty;
            _serializedObject = serializedProperty.serializedObject;
            _cyanTriggerSerializableInstance = cyanTriggerSerializableInstance;
            _baseEditor = baseEditor;
            
            _cyanTriggerDataInstance = cyanTriggerSerializableInstance?.triggerDataInstance;
            _dataInstanceProperty =
                serializedProperty.FindPropertyRelative(nameof(CyanTriggerSerializableInstance.triggerDataInstance));

            _variableDataProperty = _dataInstanceProperty.FindPropertyRelative(nameof(CyanTriggerDataInstance.variables));
            _eventsProperty = _dataInstanceProperty.FindPropertyRelative(nameof(CyanTriggerDataInstance.events));

            _variableTreeView = new CyanTriggerVariableTreeView(
                _variableDataProperty,
                OnVariableAddedOrRemoved,
                (varName, varGuid) =>
                {
                    varName = CyanTriggerNameHelpers.SanitizeName(varName);
                    return GetUniqueVariableName(varName, varGuid, _cyanTriggerDataInstance.variables);
                },
                OnGlobalVariableRenamed);

            UpdateUserVariableOptions();
        }
        
        public void Dispose()
        {
            OpenSerializers.Remove(this);
            
            var target = _baseEditor.target;
            _baseEditor = null;
            
            if (target == null)
            {
                return;
            }

            for (int index = 0; index < _eventActionTrees.Length; ++index)
            {
                if (_eventActionTrees[index] != null)
                {
                    _eventActionTrees[index].Dispose();
                }
            }
        }

        public static void UpdateAllOpenSerializers()
        {
            foreach (var serializer in OpenSerializers)
            {
                serializer.UpdateActionTreeDisplayNames();
                serializer._baseEditor.Repaint();
            }
        }

        public void OnInspectorGUI()
        {
            Profiler.BeginSample("CyanTriggerEditor");

            _serializedObject.UpdateIfRequiredOrScript();

            if (Event.current.commandName == "UndoRedoPerformed")
            {
                ResetValues();
                ResetAllActionTrees();
                _resetVariableInputs = true;
            }

            HandleCommentEvents();

            UpdateVariableScope();

            EditorGUILayout.BeginVertical(GUILayout.MaxWidth(EditorGUIUtility.currentViewWidth - 30));
            
            EditorGUILayout.Space();

            RenderWarningsAndErrors();
            
            RenderMainComment();
            
            RenderExtraOptions();
            
            RenderVariables();

            RenderEvents();

            EditorGUILayout.EndVertical();

            HandleTriggerRightClick(GUILayoutUtility.GetLastRect());

            // if ((GUI.changed && _serializedObject.hasModifiedProperties) ||
            //     (Event.current.type == EventType.ValidateCommand &&
            //      Event.current.commandName == "UndoRedoPerformed"))
            // {
            //     MarkDirty();
            // }

            _serializedObject.ApplyModifiedProperties();

            CheckResetVariableInputs();
            
            Profiler.EndSample();
        }

        private void CheckResetVariableInputs()
        {
            if (_resetVariableInputs)
            {
                _resetVariableInputs = false;
                UpdateUserVariableOptions();
                UpdateActionTreeDisplayNames();
            }
        }

        private void HandleCommentEvents()
        {
            // Event has a comment being edited. Check if we it needs to be closed. 
            if (_editingCommentId != InvalidCommentId)
            {
                // Detect if we should close the comment editor
                Event cur = Event.current;
                bool enterPressed = cur.type == EventType.KeyDown &&
                                    cur.keyCode == KeyCode.Return &&
                                    (cur.shift || cur.alt || cur.command || cur.control);
                bool isCommentControlFocused = GUI.GetNameOfFocusedControl() == CommentControlName;
                if ((!_editCommentButtonPressed && _focusedCommentEditor && !isCommentControlFocused) || enterPressed)
                {
                    _editingCommentId = InvalidCommentId;
                    GUI.FocusControl(null);
                    if (enterPressed)
                    {
                        cur.Use();
                    }
                }
            }
            _editCommentButtonPressed = false;
        }
        
        private void HandleTriggerRightClick(Rect triggerRect)
        {
            Event current = Event.current;
            if(current.type == EventType.ContextClick && triggerRect.Contains(current.mousePosition))
            {
                GenericMenu menu = new GenericMenu();
                
                menu.AddItem(new GUIContent("Edit CyanTrigger Comment"), false, () =>
                {
                    _editingCommentId = CyanTriggerCommentId;
                    _focusedCommentEditor = false;
                });
                
                void SetAllEventsExpandState(bool value)
                {
                    for (int i = 0; i < _eventsProperty.arraySize; ++i)
                    {
                        SerializedProperty action = _eventsProperty.GetArrayElementAtIndex(i);
                        SerializedProperty expanded = action.FindPropertyRelative(nameof(CyanTriggerEvent.expanded));
                        expanded.boolValue = value;
                    }
                    _eventsProperty.serializedObject.ApplyModifiedProperties();
                }
            
                menu.AddItem(new GUIContent("Maximize All Events"), false, () => SetAllEventsExpandState(true));
                menu.AddItem(new GUIContent("Minimize All Events"), false, () => SetAllEventsExpandState(false));
                
                menu.AddSeparator("");
                
                menu.AddItem(new GUIContent("Compile Triggers"), false, () =>
                {
                    CyanTriggerSerializerManager.RecompileAllTriggers(true);
                });
                menu.AddItem(new GUIContent("Show CyanTrigger Settings"), false, CyanTriggerSettingsWindow.ShowWindow);

                menu.ShowAsContext();
 
                current.Use(); 
            }
        }

        private void ResetValues()
        {
            UpdateUserVariableOptions();

            ResizeEventArrays(_eventsProperty.arraySize);

            for (int i = 0; i < _eventListSize; ++i)
            {
                _eventActionUserGateLists[i] = null;
                
                if (_eventInputLists[i] == null)
                {
                    _eventInputLists[i] = new Dictionary<int, ReorderableList>();
                }
                else
                {
                    _eventInputLists[i].Clear();
                }

                if (_eventActionInputLists[i] == null)
                {
                    _eventActionInputLists[i] = new Dictionary<int, ReorderableList>();
                }
                else
                {
                    _eventActionInputLists[i].Clear();
                }

                var eventData = _eventInstanceRenderData[i];
                if (eventData != null)
                {
                    eventData.ClearInputLists();
                    eventData.Property = _eventsProperty.GetArrayElementAtIndex(i)
                        .FindPropertyRelative(nameof(CyanTriggerEvent.eventInstance));
                }
                
                _eventOptionRenderData[i]?.ClearInputLists();

                // Force recalculate all variable scopes
                _scopeTreeRoot[i] = null;
            }

            UpdateActionTreeViewProperties();
        }

        private void ResetAllActionTrees()
        {
            int eventLength = _eventsProperty.arraySize;
            for (int index = 0; index < eventLength; ++index)
            {
                if (_eventActionTrees[index] == null)
                {
                    continue;
                }
                _eventActionTrees[index].UndoReset();
            }
        }

        private void UpdateVariableScope()
        {
            if (_scopeTreeRoot.Length != _eventsProperty.arraySize)
            {
                _scopeTreeRoot = new CyanTriggerEditorScopeTree[_eventsProperty.arraySize];
            }

            for (int eventIndex = 0; eventIndex < _eventsProperty.arraySize; ++eventIndex)
            {
                if (_scopeTreeRoot[eventIndex] != null)
                {
                    continue;
                }
                
                _scopeTreeRoot[eventIndex] = new CyanTriggerEditorScopeTree();
                var actionListProperty = _eventsProperty.GetArrayElementAtIndex(eventIndex)
                    .FindPropertyRelative(nameof(CyanTriggerEvent.actionInstances));
                _scopeTreeRoot[eventIndex].CreateStructure(actionListProperty);
            }
        }

        private void UpdateActionTreeDisplayNames()
        {
            for (int index = 0; index < _eventActionTrees.Length; ++index)
            {
                if (_eventActionTrees[index] != null)
                {
                    _eventActionTrees[index].UpdateAllItemDisplayNames();
                }
            }
        }

        private void RemoveEvents(List<int> toRemove)
        {
            int eventLength = _eventsProperty.arraySize;
            int newCount = eventLength - toRemove.Count;
            toRemove.Sort();
            
            // TODO update all other arrays here too :eyes:
            CyanTriggerActionInstanceRenderData[] tempRenderData =
                new CyanTriggerActionInstanceRenderData[newCount];
            CyanTriggerActionInstanceRenderData[] tempOptionData =
                new CyanTriggerActionInstanceRenderData[newCount];

            CyanTriggerActionTreeView[] tempActionTrees = new CyanTriggerActionTreeView[newCount];
            
            Dictionary<int, ReorderableList>[] tempEventActionInputLists = 
                new Dictionary<int, ReorderableList>[newCount];
            Dictionary<int, ReorderableList>[] tempEventInputLists = 
                new Dictionary<int, ReorderableList>[newCount];

            ReorderableList[] tempEventActionUserGateLists = new ReorderableList[newCount];
            var tempScopeTrees = new CyanTriggerEditorScopeTree[newCount];
            
            int itr = 0;
            for (int index = 0; index < eventLength; ++index)
            {
                if (itr < toRemove.Count && toRemove[itr] == index)
                {
                    _eventsProperty.DeleteArrayElementAtIndex(toRemove[itr]);
                    ++itr;
                    continue;
                }

                int nIndex = index - itr;
                tempRenderData[nIndex] = _eventInstanceRenderData[index];
                tempRenderData[nIndex].Property = _eventsProperty.GetArrayElementAtIndex(nIndex)
                    .FindPropertyRelative(nameof(CyanTriggerEvent.eventInstance));
                tempOptionData[nIndex] = _eventOptionRenderData[index];
                
                tempActionTrees[nIndex] = _eventActionTrees[index];
                
                tempEventActionInputLists[nIndex] = _eventActionInputLists[index];
                tempEventInputLists[nIndex] = _eventInputLists[index];

                tempEventActionUserGateLists[nIndex] = _eventActionUserGateLists[index];
                
                tempScopeTrees[nIndex] = _scopeTreeRoot[index];

                if (itr > 0)
                {
                    tempRenderData[nIndex]?.ClearInputLists();
                    tempOptionData[nIndex]?.ClearInputLists();
                    tempEventActionUserGateLists[nIndex] = null;
                    tempEventInputLists[nIndex]?.Clear();
                    tempEventActionInputLists[nIndex]?.Clear();
                }
            }
            
            _eventInstanceRenderData = tempRenderData;
            _eventOptionRenderData = tempOptionData;
            _eventActionTrees = tempActionTrees;
            _eventActionInputLists = tempEventActionInputLists;
            _eventInputLists = tempEventInputLists;
            _eventActionUserGateLists = tempEventActionUserGateLists;
            _scopeTreeRoot = tempScopeTrees;

            _eventListSize = newCount;
            
            UpdateActionTreeViewProperties();
        }

        private void UpdateActionTreeViewProperties()
        {
            int eventLength = _eventsProperty.arraySize;
            for (int index = 0; index < eventLength; ++index)
            {
                UpdateOrCreateActionTreeForEvent(index);
            }

            UpdateAllTreeIndexCounts();
        }

        private void UpdateAllTreeIndexCounts()
        {
            int eventLength = _eventsProperty.arraySize;
            _variableTreeView.IdStartIndex = 0;
            int treeIndexCount = _variableTreeView.Size;
            for (int index = 0; index < eventLength; ++index)
            {
                if (_eventActionTrees[index] == null)
                {
                    Debug.LogWarning($"[CyanTrigger] Action tree [{index}] is null and cannot set start id!");
                    continue;
                }

                if (_eventActionTrees[index].IdStartIndex != treeIndexCount)
                {
                    _eventActionTrees[index].IdStartIndex = treeIndexCount;
                }
                treeIndexCount += _eventActionTrees[index].Size;
            }
        }

        private void ResizeEventArrays(int newSize)
        {
            _eventListSize = newSize;
            Array.Resize(ref _eventInstanceRenderData, newSize);
            Array.Resize(ref _eventOptionRenderData, newSize);
            Array.Resize(ref _eventActionTrees, newSize);
            Array.Resize(ref _eventActionInputLists, newSize);
            Array.Resize(ref _eventInputLists, newSize);
            Array.Resize(ref _eventActionUserGateLists, newSize);
            Array.Resize(ref _scopeTreeRoot, newSize);

            UpdateActionTreeViewProperties();
        }

        private void SwapEventElements(List<int> toMoveUp)
        {
            foreach (int index in toMoveUp)
            {
                int prev = index - 1;
                _eventsProperty.MoveArrayElement(index, prev);

                SwapElements(_eventInstanceRenderData, index, prev);
                SwapElements(_eventOptionRenderData, index, prev);
                SwapElements(_eventActionTrees, index, prev);
                SwapElements(_eventActionInputLists, index, prev);
                SwapElements(_eventInputLists, index, prev);
                SwapElements(_scopeTreeRoot, index, prev);

                if (_eventInstanceRenderData[index] != null)
                {
                    _eventInstanceRenderData[index].Property = _eventsProperty.GetArrayElementAtIndex(index)
                        .FindPropertyRelative(nameof(CyanTriggerEvent.eventInstance));
                }
                if (_eventInstanceRenderData[prev] != null)
                {
                    _eventInstanceRenderData[prev].Property = _eventsProperty.GetArrayElementAtIndex(prev)
                        .FindPropertyRelative(nameof(CyanTriggerEvent.eventInstance));
                }

                _eventActionUserGateLists[index] = null;
                _eventActionUserGateLists[prev] = null;
                
                _eventInstanceRenderData[index]?.ClearInputLists();
                _eventInstanceRenderData[prev]?.ClearInputLists();
                
                _eventOptionRenderData[index]?.ClearInputLists();
                _eventOptionRenderData[prev]?.ClearInputLists();
                
                _eventActionInputLists[index]?.Clear();
                _eventActionInputLists[prev]?.Clear();
                
                _eventInputLists[index]?.Clear();
                _eventInputLists[prev]?.Clear();
            }

            UpdateActionTreeViewProperties();
        }

        private void MoveEventToIndex(int srcIndex, int dstIndex)
        {
            if (srcIndex == dstIndex)
            {
                return;
            }
            int delta = (int)Mathf.Sign(dstIndex - srcIndex);
            bool isDown = delta > 0;
            List<int> moveOrder = new List<int>();
            for (int cur = srcIndex; cur != dstIndex; cur += delta)
            {
                // When moving elements down, we actually move the one below it up.
                int index = cur + (isDown ? 1 : 0);
                moveOrder.Add(index);
            }
            SwapEventElements(moveOrder);
        }

        private static void SwapElements<T>(IList<T> array, int index1, int index2)
        {
            var temp = array[index1];
            array[index1] = array[index2];
            array[index2] = temp;
        }

        private void UpdateOrCreateActionTreeForEvent(int index)
        {
            var actionListProperty = _eventsProperty.GetArrayElementAtIndex(index)
                .FindPropertyRelative(nameof(CyanTriggerEvent.actionInstances));
                
            List<CyanTriggerEditorVariableOption> GetVariableOptionsForEvent(Type type, int actionIndex)
            {
                return GetVariableOptions(type, index, actionIndex);
            }

            bool IsVariableValidForEvent(int actionIndex, string guid, string name)
            {
                return IsVariableValid(index, actionIndex, guid, name);
            }

            void OnEventActionsChanged()
            {
                OnActionsChanged(index);
            }
            
            if (_eventActionTrees[index] == null)
            {
                _eventActionTrees[index] = new CyanTriggerActionTreeView(
                    actionListProperty, 
                    OnEventActionsChanged, 
                    GetVariableOptionsForEvent,
                    IsVariableValidForEvent);
                _eventActionTrees[index].ExpandAll();
            }
            else
            {
                var actionTree = _eventActionTrees[index];
                actionTree.Elements = actionListProperty;
                actionTree.GetVariableOptions = GetVariableOptionsForEvent;
                actionTree.OnActionChanged = OnEventActionsChanged;
                actionTree.IsVariableValid = IsVariableValidForEvent;
            }
        }

        private void OnActionsChanged(int eventIndex)
        {
            if (eventIndex >= _scopeTreeRoot.Length || _scopeTreeRoot[eventIndex] == null)
            {
                return;
            }
            
            // Recalculate action variable inds
            var actionListProperty = _eventsProperty.GetArrayElementAtIndex(eventIndex)
                .FindPropertyRelative(nameof(CyanTriggerEvent.actionInstances));
            _scopeTreeRoot[eventIndex].CreateStructure(actionListProperty);
        }

        private void OnVariableAddedOrRemoved(List<string> removedIds)
        {
            if (removedIds != null && removedIds.Count > 0)
            {
                var removedHash = new HashSet<string>(removedIds);
                var removedNames = new HashSet<string>();
                for (int index = 0; index < _eventActionTrees.Length; ++index)
                {
                    if (_eventActionTrees[index] != null)
                    {
                        _eventActionTrees[index].DeleteVariables(removedHash, removedNames);
                    }
                }
            }

            _serializedObject.ApplyModifiedProperties();

            _resetVariableInputs = true;
        }

        private void OnGlobalVariableRenamed(string oldName, string newName, string guid)
        {
            Dictionary<string, string> updatedGuids = new Dictionary<string, string>
            {
                {guid, newName},
            };
            
            for (int index = 0; index < _eventActionTrees.Length; ++index)
            {
                if (_eventActionTrees[index] != null)
                {
                    _eventActionTrees[index].UpdateVariableNames(updatedGuids);
                }
            }
            
            _serializedObject.ApplyModifiedProperties();
            
            _resetVariableInputs = true;
        }

        private SerializedProperty AddEvent(CyanTriggerActionInfoHolder actionInfo)
        {
            _eventsProperty.arraySize++;
            SerializedProperty newEvent = _eventsProperty.GetArrayElementAtIndex(_eventsProperty.arraySize - 1);
            CyanTriggerSerializedPropertyUtils.InitializeEventProperties(actionInfo, newEvent);

            ResizeEventArrays(_eventsProperty.arraySize);

            return newEvent;
        }
        
        private void DuplicateEvent(int eventIndex)
        {
            if (eventIndex < 0 || eventIndex >= _eventInstanceRenderData.Length ||
                _eventInstanceRenderData[eventIndex] == null)
            {
                return;
            }
            
            CyanTriggerActionInfoHolder actionInfo = _eventInstanceRenderData[eventIndex].ActionInfo;
            int lastElement = _eventsProperty.arraySize++;
            var srcEvent = _eventsProperty.GetArrayElementAtIndex(eventIndex);
            var newEvent = _eventsProperty.GetArrayElementAtIndex(lastElement);
            
            // Clear info before creating data for the property.
            CyanTriggerSerializedPropertyUtils.InitializeEventProperties(actionInfo, newEvent);
            
            // Resize arrays, which also creates the action tree for this new event.
            ResizeEventArrays(_eventsProperty.arraySize);

            var srcActionTree = _eventActionTrees[eventIndex];
            var dstActionTree = _eventActionTrees[lastElement];
            
            CyanTriggerSerializedPropertyUtils.CopyEventProperties(actionInfo, srcEvent, newEvent);
           
            MoveEventToIndex(_eventsProperty.arraySize - 1, eventIndex + 1);
            
            // Copy tree's expanded values after elements have been moved and trees rebuilt.
            dstActionTree.SetExpandedApplyingStartId(srcActionTree.GetExpandedWithoutStartId());
            
            _serializedObject.ApplyModifiedProperties();
            
            ResetValues();
            
            UpdateVariableScope();
        }
        
        

        private void UpdateUserVariableOptions()
        {
            _userVariableOptions.Clear();
            _userVariables.Clear();

            CyanTriggerEditorVariableOptionList allVariables = new CyanTriggerEditorVariableOptionList(typeof(CyanTriggerVariable));
            _userVariableOptions.Add(allVariables.Type, allVariables);

            void AddOptionToAllTypes(CyanTriggerEditorVariableOption option)
            {
                _userVariables.Add(option.ID, option.Name);
                
                // Todo, figure if this breaks anything
                if (!option.IsReadOnly)
                {
                    allVariables.VariableOptions.Add(option);
                }

                HashSet<Type> foundTypes = new HashSet<Type>();
                Queue<Type> typeQueue = new Queue<Type>();

                Type type = option.Type;
                if (type == typeof(IUdonEventReceiver))
                {
                    type = typeof(UdonBehaviour);
                }
                typeQueue.Enqueue(type);
                foundTypes.Add(type);

                while (typeQueue.Count > 0)
                {
                    Type t = typeQueue.Dequeue();
                    if (!_userVariableOptions.TryGetValue(t, out CyanTriggerEditorVariableOptionList options))
                    {
                        options = new CyanTriggerEditorVariableOptionList(t);
                        _userVariableOptions.Add(t, options);
                    }

                    options.VariableOptions.Add(option);
                    
                    foreach (var interfaceType in t.GetInterfaces())
                    {
                        if (!foundTypes.Contains(interfaceType))
                        {
                            typeQueue.Enqueue(interfaceType);
                            foundTypes.Add(interfaceType);
                        }
                    }

                    Type baseType = t.BaseType;
                    if (baseType != null && !foundTypes.Contains(baseType))
                    {
                        typeQueue.Enqueue(baseType);
                        foundTypes.Add(baseType);
                    }
                }
            }
            
            for (int var = 0; var < _variableDataProperty.arraySize; ++var)
            {
                SerializedProperty variableProperty = _variableDataProperty.GetArrayElementAtIndex(var);
                
                string name = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.name)).stringValue;
                string id = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.variableID)).stringValue;
                
                SerializedProperty typeProperty =
                    variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.type));
                SerializedProperty typeDefProperty =
                    typeProperty.FindPropertyRelative(nameof(CyanTriggerSerializableType.typeDef));
                Type varType = Type.GetType(typeDefProperty.stringValue);
                
                CyanTriggerEditorVariableOption option = new CyanTriggerEditorVariableOption
                    {ID = id, Name = name, Type = varType};
                AddOptionToAllTypes(option);
            }
            
            CyanTriggerEditorVariableOption thisGameObject = new CyanTriggerEditorVariableOption
            {
                ID = CyanTriggerAssemblyData.ThisGameObjectGUID, 
                Name = CyanTriggerAssemblyData.ThisGameObjectName, 
                Type = typeof(GameObject), 
                IsReadOnly = true
            };
            AddOptionToAllTypes(thisGameObject);
            
            CyanTriggerEditorVariableOption thisTransform = new CyanTriggerEditorVariableOption
            {
                ID = CyanTriggerAssemblyData.ThisTransformGUID, 
                Name = CyanTriggerAssemblyData.ThisTransformName, 
                Type = typeof(Transform), 
                IsReadOnly = true
            };
            AddOptionToAllTypes(thisTransform);
            
            CyanTriggerEditorVariableOption thisUdonBehaviour = new CyanTriggerEditorVariableOption
            {
                ID = CyanTriggerAssemblyData.ThisUdonBehaviourGUID, 
                Name = CyanTriggerAssemblyData.ThisUdonBehaviourName,
                Type = typeof(IUdonEventReceiver), 
                IsReadOnly = true
            };
            AddOptionToAllTypes(thisUdonBehaviour);
            
            CyanTriggerEditorVariableOption thisCyanTrigger = new CyanTriggerEditorVariableOption
            {
                ID = CyanTriggerAssemblyData.ThisCyanTriggerGUID, 
                Name = CyanTriggerAssemblyData.ThisCyanTriggerName, 
                Type = typeof(CyanTrigger), 
                IsReadOnly = true
            };
            AddOptionToAllTypes(thisCyanTrigger);
            
            CyanTriggerEditorVariableOption localPlayer = new CyanTriggerEditorVariableOption
            {
                ID = CyanTriggerAssemblyData.LocalPlayerGUID, 
                Name = CyanTriggerAssemblyData.LocalPlayerName, 
                Type = typeof(VRCPlayerApi), 
                IsReadOnly = true
            };
            AddOptionToAllTypes(localPlayer);
        }

        private bool IsVariableValid(int eventIndex, int actionIndex, string guid, string name)
        {
            // TODO verify expected type

            bool isPrevVariable = CyanTriggerCustomNodeOnVariableChanged.IsPrevVariable(name, guid);
            // event variables
            if (string.IsNullOrEmpty(guid) || isPrevVariable)
            {
                CyanTriggerActionInfoHolder curActionInfo = _eventInstanceRenderData[eventIndex].ActionInfo;
                
                SerializedProperty eventInstance = _eventInstanceRenderData[eventIndex].Property;
                foreach (var def in curActionInfo.GetVariableOptions(eventInstance))
                {
                    if (def.Name == name || (isPrevVariable && def.ID == guid))
                    {
                        return true;
                    }
                }

                return false;
            }
            
            if (_userVariables.ContainsKey(guid))
            {
                return true;
            }

            // Go through action provided variables
            return _scopeTreeRoot[eventIndex].IsVariableValid(actionIndex, guid);
        }

        private List<CyanTriggerEditorVariableOption> GetVariableOptions(Type varType, int eventIndex, int actionIndex)
        {
            // TODO cache this better
            List<CyanTriggerEditorVariableOption> options = new List<CyanTriggerEditorVariableOption>();

            // Get event variables
            CyanTriggerActionInfoHolder curActionInfo = _eventInstanceRenderData[eventIndex].ActionInfo;
            
            SerializedProperty eventInstance = _eventInstanceRenderData[eventIndex].Property;
            foreach (var def in curActionInfo.GetVariableOptions(eventInstance))
            {
                if (varType.IsAssignableFrom(def.Type))
                {
                    options.Add(def);
                }
            }
            
            // Get user variables of this type
            if (_userVariableOptions.TryGetValue(varType, out var list))
            {
                options.AddRange(list.VariableOptions);
            }

            if (_scopeTreeRoot[eventIndex] != null)
            {
                options.AddRange(_scopeTreeRoot[eventIndex].GetVariableOptions(varType, actionIndex).Reverse());
            }

            // TODO add items that can be casted or tostring'ed

            return options;
        }

        public static string GetUniqueVariableName(string varName, string id, CyanTriggerVariable[] variables)
        {
            bool match;
            int count = 0;
            string varMatchName = varName;
            do
            {
                match = false;
                foreach (var variable in variables)
                {
                    if (variable.name == varMatchName && id != variable.variableID)
                    {
                        match = true;
                        ++count;
                        varMatchName = varName + count;
                        break;
                    }
                }
            } while (match);

            return varMatchName;
        }

        private void RenderWarningsAndErrors()
        {
            UdonBehaviour udonBehaviour = _cyanTriggerSerializableInstance?.udonBehaviour;
            if (udonBehaviour == null || !(udonBehaviour.programSource is CyanTriggerProgramAsset programAsset))
            {
                return;
            }

            var warnings = programAsset.warningMessages;

            if (warnings == null)
            {
                warnings = new List<string>();
            }

            var errors = programAsset.errorMessages;
            if (errors == null)
            {
                errors = new List<string>();
            }
            
            if (warnings.Count > 0 || errors.Count > 0)
            {
                EditorGUILayout.BeginVertical(CyanTriggerEditorGUIUtil.HelpBoxStyle);

                if (warnings.Count > 0)
                {
                    EditorGUILayout.LabelField("Warnings:");
                    EditorGUILayout.LabelField(string.Join("\n", warnings), CyanTriggerEditorGUIUtil.WarningTextStyle);
                }
                
                if (errors.Count > 0)
                {
                    EditorGUILayout.LabelField("Errors:");
                    EditorGUILayout.LabelField(string.Join("\n", errors), CyanTriggerEditorGUIUtil.ErrorTextStyle);
                }
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }
        }
        
        private void RenderMainComment()
        {
            SerializedProperty commentProperty =
                _dataInstanceProperty.FindPropertyRelative(nameof(CyanTriggerDataInstance.comment));
            SerializedProperty commentTextProperty =
                commentProperty.FindPropertyRelative(nameof(CyanTriggerComment.comment));

            if (!string.IsNullOrEmpty(commentTextProperty.stringValue) || _editingCommentId == CyanTriggerCommentId)
            {
                EditorGUILayout.BeginVertical(CyanTriggerEditorGUIUtil.HelpBoxStyle);
            
                RenderCommentSection(commentTextProperty, CyanTriggerCommentId);
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }
        }

        private void RenderCommentSection(SerializedProperty commentProperty, int index)
        {
            string comment = commentProperty.stringValue;
            // Draw comment editor
            if (_editingCommentId == index && !_editCommentButtonPressed)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(5);
                
                Event cur = Event.current;
                bool wasEscape = (cur.type == EventType.KeyDown && cur.keyCode == KeyCode.Escape);

                GUI.SetNextControlName(CommentControlName);
                string newComment = EditorGUILayout.TextArea(comment, EditorStyles.textArea);
                if (newComment != comment)
                {
                    commentProperty.stringValue = newComment;
                }

                bool completeButton = GUILayout.Button(CyanTriggerEditorGUIUtil.CommentCompleteIcon, new GUIStyle(), GUILayout.Width(16));
                
                GUILayout.Space(5);
                EditorGUILayout.EndHorizontal();
                
                if (!_focusedCommentEditor)
                {
                    _focusedCommentEditor = true;
                    EditorGUI.FocusTextInControl(CommentControlName);
                }

                if (wasEscape || completeButton)
                {
                    if (newComment.Length > 0 && newComment.Trim().Length == 0)
                    {
                        commentProperty.stringValue = "";
                    }
                
                    _editingCommentId = InvalidCommentId;
                    GUI.FocusControl(null);
                }
            }
            else if (!string.IsNullOrEmpty(comment))
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(5);

                EditorGUILayout.LabelField("// " +comment, CyanTriggerEditorGUIUtil.CommentStyle);

                GUILayout.Space(5);
                EditorGUILayout.EndHorizontal();
            }
        }

        private void RenderExtraOptions()
        {
            // Check for all settings that should be shown
            bool renderInteractSettings = false;
            bool renderUpdateOrderSetting = false;
            if (_cyanTriggerDataInstance?.events != null)
            {
                foreach (var eventType in _cyanTriggerDataInstance.events)
                {
                    string directEvent = eventType.eventInstance.actionType.directEvent;
                    if (string.IsNullOrEmpty(directEvent))
                    {
                        continue;
                    }
                    
                    if (directEvent.Equals("Event_Interact"))
                    {
                        renderInteractSettings = true;
                    }

                    if (directEvent.Equals("Event_Start") || 
                        directEvent.Equals("Event_Update") || 
                        directEvent.Equals("Event_LateUpdate") || 
                        directEvent.Equals("Event_FixedUpdate") ||
                        directEvent.Equals("Event_PostLateUpdate"))
                    {
                        renderUpdateOrderSetting = true;
                    }
                    
                    // TODO check for other event types
                }
            }

            // If no settings should show, don't show this section at all.
            if (!(renderInteractSettings || renderUpdateOrderSetting))
            {
                return;
            }
            
            EditorGUILayout.BeginVertical(CyanTriggerEditorGUIUtil.HelpBoxStyle);
            
            // TODO persist showing other settings
            
            CyanTriggerPropertyEditor.DrawFoldoutListHeader(
                new GUIContent("Other Settings"),
                ref _showOtherSettings,
                false,
                0,
                null,
                false,
                null,
                false,
                false
            );

            if (!_showOtherSettings)
            {
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
                return;
            }
            
            // Update order
            {
                if (renderUpdateOrderSetting)
                {
                    SerializedProperty updateOrderProperty =
                        _dataInstanceProperty.FindPropertyRelative(nameof(CyanTriggerDataInstance.updateOrder));
                
                    EditorGUILayout.PropertyField(updateOrderProperty, UpdateOrderContent);
                }
            }

            // Add interact options
            if (renderInteractSettings)
            {
                SerializedProperty interactTextProperty =
                    _serializedProperty.FindPropertyRelative(
                        nameof(CyanTriggerSerializableInstance.interactText));
                SerializedProperty interactProximityProperty =
                    _serializedProperty.FindPropertyRelative(
                        nameof(CyanTriggerSerializableInstance.proximity));
                    
                EditorGUILayout.PropertyField(interactTextProperty, InteractTextContent);

                interactProximityProperty.floatValue = EditorGUILayout.Slider(ProximityContent,
                    interactProximityProperty.floatValue, 0f, 100f);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }
        
        private void RenderVariables()
        {
            EditorGUILayout.BeginVertical(CyanTriggerEditorGUIUtil.HelpBoxStyle);

            // TODO allow dragging objects/components here to add them as variables
            CyanTriggerPropertyEditor.DrawFoldoutListHeader(
                new GUIContent("Variables"),
                ref _showVariablesSection,
                false,
                0,
                null,
                false,
                null,
                false,
                false
                );
            
            if (!_showVariablesSection)
            {
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
                
                return;
            }
            
            _variableTreeView.DoLayoutTree();
            
            if (_variableDataProperty.arraySize != _variableTreeView.Size)
            {
                _resetVariableInputs = true;
            }

            bool shouldShowSyncMode = _cyanTriggerSerializableInstance.triggerDataInstance.HasSyncedVariables();
            
            // Sync mode
            if (shouldShowSyncMode)
            {
                UdonBehaviour udonBehaviour = _cyanTriggerSerializableInstance?.udonBehaviour;
                
                SerializedProperty programSyncModeProperty =
                    _dataInstanceProperty.FindPropertyRelative(nameof(CyanTriggerDataInstance.programSyncMode));

                EditorGUILayout.PropertyField(programSyncModeProperty, ProgramSyncModeContent);
                CyanTriggerProgramSyncMode syncMode = (CyanTriggerProgramSyncMode) programSyncModeProperty.intValue;

                if (syncMode == CyanTriggerProgramSyncMode.Continuous)
                {
                    EditorGUILayout.HelpBox("Synced variables will automatically be synced with users in the room periodically. This will happen multiple times per second, even if no data changes.", MessageType.Info);
                }
                else if (syncMode == CyanTriggerProgramSyncMode.Manual)
                {
                    EditorGUILayout.HelpBox("Synced variables will only be synced with users in the room after UdonBehaviour.RequestSerialization is called.", MessageType.Info);
                }
                else if (syncMode == CyanTriggerProgramSyncMode.ManualWithAutoRequest)
                {
                    EditorGUILayout.HelpBox("Modifying a synced variable will automatically request serialization at the end of the event. Note that with fast changing values, only the latest value will be synced with users in the room.", MessageType.Info);
                }

                if (udonBehaviour != null)
                {
                    bool requiresContinuous = CyanTriggerUtil.GameObjectRequiresContinuousSync(udonBehaviour);
                    bool isManual = syncMode != CyanTriggerProgramSyncMode.Continuous;

                    if (isManual && requiresContinuous)
                    {
                        EditorGUILayout.HelpBox("This CyanTrigger is set to Manual sync and the object has the VRCObjectSync Component. These are incompatible and the object's position will not sync. If an object has ObjectSync, all Udon need to be on Continuous Sync. It is recommended to use multiple objects with the position sync'ed object using Continuous Sync and another object with Manual Sync to sync variables.", MessageType.Error);
                    }
                }
            }
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space();

            CheckResetVariableInputs();
        }

        private void RenderEvents()
        {
            int eventLength = _eventsProperty.arraySize;

            if (_eventListSize != eventLength)
            {
                if (_eventListSize < eventLength)
                {
                    ResizeEventArrays(eventLength);
                }
                else
                {
                    Debug.LogWarning("Event size does not match!" + _eventListSize +" " +eventLength);
                    ResetValues();
                }
            }

            UpdateAllTreeIndexCounts();

            List<int> toRemove = new List<int>();
            List<int> toMoveUp = new List<int>();

            for (int curEvent = 0; curEvent < eventLength; ++curEvent)
            {
                EditorGUILayout.BeginVertical(CyanTriggerEditorGUIUtil.HelpBoxStyle);

                SerializedProperty eventProperty = _eventsProperty.GetArrayElementAtIndex(curEvent);
                SerializedProperty eventInfo = eventProperty.FindPropertyRelative(nameof(CyanTriggerEvent.eventInstance));
                SerializedProperty expandedProperty = eventProperty.FindPropertyRelative(nameof(CyanTriggerEvent.expanded));
                
                if (_eventInstanceRenderData[curEvent] == null)
                {
                    _eventInstanceRenderData[curEvent] = new CyanTriggerActionInstanceRenderData
                    {
                        Property = eventInfo,
                    };
                    
                    // TODO, do this better?
                    _eventOptionRenderData[curEvent] = new CyanTriggerActionInstanceRenderData()
                    {
                        // Currently only for user gate
                        InputLists = new ReorderableList[1],
                        ExpandedInputs = new [] {true},
                    };
                }

                CyanTriggerActionInfoHolder curActionInfo = _eventInstanceRenderData[curEvent].ActionInfo;
                TriggerModifyAction modifyAction = RenderEventHeader(curEvent, eventProperty, curActionInfo);

                if (expandedProperty.boolValue)
                {
                    RenderEventOptions(curEvent, eventProperty, curActionInfo);

                    EditorGUILayout.Space();

                    RenderEventActions(curEvent);
                }

                EditorGUILayout.EndVertical();

                Rect eventRect = GUILayoutUtility.GetLastRect();

                if (modifyAction == TriggerModifyAction.None)
                {
                    HandleEventRightClick(eventRect, curEvent);
                }
                else if (modifyAction == TriggerModifyAction.Delete)
                {
                    toRemove.Add(curEvent);
                }
                else if (modifyAction == TriggerModifyAction.MoveUp)
                {
                    toMoveUp.Add(curEvent);
                }
                else if (modifyAction == TriggerModifyAction.MoveDown)
                {
                    toMoveUp.Add(curEvent + 1);
                }

                EditorGUILayout.Space();
            }

            if (toRemove.Count > 0)
            {
                RemoveEvents(toRemove);
            }

            if (toMoveUp.Count > 0)
            {
                SwapEventElements(toMoveUp);
            }
            
            RenderAddEventButton();
        }

        private TriggerModifyAction RenderEventHeader(
            int index, 
            SerializedProperty eventProperty,
            CyanTriggerActionInfoHolder actionInfo)
        {
            SerializedProperty eventInfo = eventProperty.FindPropertyRelative(nameof(CyanTriggerEvent.eventInstance));
            SerializedProperty expandedProperty = eventProperty.FindPropertyRelative(nameof(CyanTriggerEvent.expanded));

            float headerRowHeight = 16f;
            Rect rect = EditorGUILayout.BeginHorizontal(GUILayout.Height(headerRowHeight));

            Rect foldoutRect = new Rect(rect.x + 14, rect.y, 6, rect.height);
            bool expanded = expandedProperty.boolValue;
            bool newExpand = EditorGUI.Foldout(foldoutRect, expanded, GUIContent.none, true);
            if (newExpand != expanded)
            {
                expandedProperty.boolValue = newExpand;
                expanded = newExpand;
            }
            
            float spaceBetween = 5;
            float initialSpace = foldoutRect.width + 14;
            float initialOffset = foldoutRect.xMax;

            float baseWidth = (rect.width - initialSpace - spaceBetween * 2) / 3.0f;
            float opButtonWidth = (baseWidth - 2 * spaceBetween) / 3.0f;

            Rect removeRect = new Rect(rect.xMax - opButtonWidth, rect.y, opButtonWidth, rect.height);
            Rect downRect = new Rect(removeRect.x - spaceBetween - opButtonWidth, rect.y, opButtonWidth,
                rect.height);
            Rect upRect = new Rect(downRect.x - spaceBetween - opButtonWidth, rect.y, opButtonWidth, rect.height);

            void DrawContentInCenterOfRect(Rect contentRect, GUIContent content, GUIStyle style = null)
            {
                if (style == null)
                {
                    style = GUI.skin.label;
                }
                
                Vector2 size = style.CalcSize(content);
                GUI.Label(new Rect(contentRect.center.x - size.x * 0.5f, 
                    contentRect.center.y - size.y * 0.5f, size.x, size.y), content, style);
            }
            
            // Draw modify buttons (move up, down, delete)
            TriggerModifyAction modifyAction = TriggerModifyAction.None;
            {
                EditorGUI.BeginDisabledGroup(index == 0);
                if (GUI.Button(upRect, GUIContent.none))
                {
                    modifyAction = TriggerModifyAction.MoveUp;
                }
                DrawContentInCenterOfRect(upRect, new GUIContent("▲", "Move Event Up"));

                EditorGUI.EndDisabledGroup();

                EditorGUI.BeginDisabledGroup(index == _eventsProperty.arraySize - 1);
                if (GUI.Button(downRect, GUIContent.none))
                {
                    modifyAction = TriggerModifyAction.MoveDown;
                }
                DrawContentInCenterOfRect(downRect, new GUIContent("▼", "Move Event Down"));

                EditorGUI.EndDisabledGroup();

                if (GUI.Button(removeRect, GUIContent.none))
                {
                    modifyAction = TriggerModifyAction.Delete;
                }
                DrawContentInCenterOfRect(removeRect, new GUIContent("✖", "Delete Event"));
            }

            // Draw hidden event header
            if (!expanded)
            {
                GUILayout.Space(EditorGUIUtility.singleLineHeight);

                float eventWidth = rect.width - initialOffset - opButtonWidth * 3 - spaceBetween * 2;
                Rect eventLabelRect = new Rect(initialOffset, rect.y, eventWidth, rect.height);

                string actionDisplayName = actionInfo.GetDisplayName();
                if (actionInfo.definition != null && actionInfo.definition.fullName.Equals("Event_Custom"))
                {
                    SerializedProperty nameProperty = eventProperty.FindPropertyRelative(nameof(CyanTriggerEvent.name));
                    string customName = string.IsNullOrEmpty(nameProperty.stringValue)
                        ? UnnamedCustomName
                        : nameProperty.stringValue;
                    actionDisplayName += $" \"{customName}\"";
                }
                
                EditorGUI.LabelField(eventLabelRect, actionDisplayName);
                
                EditorGUILayout.EndHorizontal();
                return modifyAction;
            }
            GUILayout.Space(EditorGUIUtility.singleLineHeight * 2);
            
            
            Rect typeRect = new Rect(initialOffset, rect.y, baseWidth, rect.height);
            Rect typeVariantRect = new Rect(typeRect.xMax + spaceBetween, rect.y, baseWidth, rect.height);

            bool valid = actionInfo.IsValid();

            void UpdateEventActionInfo(CyanTriggerActionInfoHolder newActionInfo)
            {
                if (actionInfo.Equals(newActionInfo))
                {
                    return;
                }
                
                var oldVariables = actionInfo.GetVariables();
                var newVariables = actionInfo.GetVariables();
                var nameHash = new HashSet<string>();
                foreach (var variable in oldVariables)
                {
                    nameHash.Add(variable.displayName);
                }
                foreach (var variable in newVariables)
                {
                    nameHash.Remove(variable.displayName);
                }
                
                _eventActionTrees[index].DeleteVariables(new HashSet<string>(), nameHash);
                
                CyanTriggerSerializedPropertyUtils.SetActionData(newActionInfo, eventInfo, false);
                _eventInstanceRenderData[index].ActionInfo = newActionInfo;
            }
            
            if (GUI.Button(typeRect, actionInfo.GetDisplayName(), EditorStyles.popup))
            {
                void UpdateEvent(CyanTriggerSettingsFavoriteItem newEventInfo)
                {
                    var data = newEventInfo.data;
                    var newActionInfo =
                        CyanTriggerActionInfoHolder.GetActionInfoHolder(data.guid, data.directEvent);
                    
                    UpdateEventActionInfo(newActionInfo);
                }

                CyanTriggerSearchWindowManager.Instance.DisplayEventsFavoritesSearchWindow(UpdateEvent, true);
            }

            int variantCount = CyanTriggerActionGroupDefinitionUtil.GetEventVariantCount(actionInfo);
            EditorGUI.BeginDisabledGroup(!valid || variantCount <= 1);
            if (GUI.Button(typeVariantRect, actionInfo.GetVariantName(), EditorStyles.popup))
            {
                GenericMenu menu = new GenericMenu();
                
                foreach (var actionVariant in CyanTriggerActionGroupDefinitionUtil.GetEventVariantInfoHolders(actionInfo))
                {
                    menu.AddItem(new GUIContent(actionVariant.GetVariantName()), false, (t) =>
                    {
                        UpdateEventActionInfo((CyanTriggerActionInfoHolder) t);
                    }, actionVariant);
                }
                
                menu.ShowAsContext();
            }
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            
            // Render gate, networking
            SerializedProperty eventOptions = eventProperty.FindPropertyRelative(nameof(CyanTriggerEvent.eventOptions));
            SerializedProperty userGateProperty =
                eventOptions.FindPropertyRelative(nameof(CyanTriggerEventOptions.userGate));
            SerializedProperty broadcastProperty =
                eventOptions.FindPropertyRelative(nameof(CyanTriggerEventOptions.broadcast));
           

            Rect subHeaderRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(20f));
            GUILayout.Space(20f);

            subHeaderRect.x = initialOffset;

            Rect gateRect = new Rect(subHeaderRect.x, subHeaderRect.y, baseWidth, subHeaderRect.height);
            Rect broadcastRect = new Rect(gateRect.xMax + spaceBetween, subHeaderRect.y, baseWidth, subHeaderRect.height);

            EditorGUI.PropertyField(gateRect, userGateProperty, GUIContent.none);
            
            string[] broadcastOptions = {"Local", "Send To Owner", "Send To All"};
            int broadcastIndex = broadcastProperty.intValue;
            int newBroadcastIndex = EditorGUI.Popup(broadcastRect, broadcastIndex, broadcastOptions);
            if (broadcastIndex != newBroadcastIndex)
            {
                broadcastProperty.intValue = newBroadcastIndex;
            }

            {
                Rect menuRect = new Rect(removeRect.x, subHeaderRect.y, opButtonWidth, headerRowHeight);
                Rect duplicateRect = new Rect(menuRect.x - spaceBetween - opButtonWidth, subHeaderRect.y, opButtonWidth,
                    headerRowHeight);
                Rect commentRect = new Rect(duplicateRect.x - spaceBetween - opButtonWidth, subHeaderRect.y, opButtonWidth, 
                    headerRowHeight);

                if (GUI.Button(menuRect, GUIContent.none))
                {
                    ShowEventOptionsMenu(index);
                }

#if !UNITY_2019_3_OR_NEWER               
                menuRect.yMin += 4;
#endif
                DrawContentInCenterOfRect(menuRect, CyanTriggerEditorGUIUtil.EventMenuIcon);
                
                if (GUI.Button(duplicateRect, GUIContent.none))
                {
                    DuplicateEvent(index);
                }
                DrawContentInCenterOfRect(duplicateRect, CyanTriggerEditorGUIUtil.EventDuplicateIcon);

                EditorGUI.BeginDisabledGroup(_editingCommentId != InvalidCommentId);
                if (GUI.Button(commentRect, GUIContent.none))
                {
                    _editCommentButtonPressed = true;
                    _editingCommentId = index;
                    _focusedCommentEditor = false;
                }
                EditorGUI.EndDisabledGroup();
                
                DrawContentInCenterOfRect(commentRect, CyanTriggerEditorGUIUtil.EventCommentContent);
            }
            
            EditorGUILayout.EndHorizontal();
            
            return modifyAction;
        }

        private void RenderEventOptions(
            int eventIndex, 
            SerializedProperty eventProperty, 
            CyanTriggerActionInfoHolder actionInfo)
        {
            SerializedProperty eventOptions = eventProperty.FindPropertyRelative(nameof(CyanTriggerEvent.eventOptions));
            SerializedProperty delayProperty = eventOptions.FindPropertyRelative(nameof(CyanTriggerEventOptions.delay));
            SerializedProperty nameProperty = eventProperty.FindPropertyRelative(nameof(CyanTriggerEvent.name));
            SerializedProperty userGateProperty =
                eventOptions.FindPropertyRelative(nameof(CyanTriggerEventOptions.userGate));

            List<CyanTriggerEditorVariableOption> GetThisEventVariables(Type type)
            {
                return GetVariableOptions(type, eventIndex, -1);
            }
            
            if (userGateProperty.intValue == (int) CyanTriggerUserGate.UserAllowList ||
                userGateProperty.intValue == (int) CyanTriggerUserGate.UserDenyList)
            {
                SerializedProperty specificUserGateProperty =
                    eventOptions.FindPropertyRelative(nameof(CyanTriggerEventOptions.userGateExtraData));

                var definition = userGateProperty.intValue == (int) CyanTriggerUserGate.UserAllowList ?
                    AllowedUserGateVariableDefinition :
                    DeniedUserGateVariableDefinition;

                Rect rectRef = Rect.zero;
                CyanTriggerPropertyEditor.DrawActionVariableInstanceMultiInputEditor(
                    _eventOptionRenderData[eventIndex],
                    0,
                    specificUserGateProperty,
                    definition,
                    GetThisEventVariables,
                    ref rectRef,
                    true
                );
            }

            // TODO variable or const delay value
            EditorGUILayout.PropertyField(delayProperty,
                new GUIContent("Delay in Seconds",
                    "This event will be delayed for the given seconds before performing any actions."));

            // Handle "Event_Custom" specially to display the name parameter here
            if (actionInfo.definition != null && actionInfo.definition.fullName.Equals("Event_Custom"))
            {
                EditorGUILayout.PropertyField(nameProperty, new GUIContent("Name", "The name of this event."));
                string name = nameProperty.stringValue;
                string sanitizedName = CyanTriggerNameHelpers.SanitizeName(name);
                
                if (string.IsNullOrEmpty(sanitizedName))
                {
                    sanitizedName = UnnamedCustomName;
                }

                if (name != sanitizedName)
                {
                    nameProperty.stringValue = sanitizedName;
                }
            }

            SerializedProperty eventInstance =
                eventProperty.FindPropertyRelative(nameof(CyanTriggerEvent.eventInstance));
            // Render event comment 
            {
                SerializedProperty eventComment =
                    eventInstance.FindPropertyRelative(nameof(CyanTriggerActionInstance.comment));
                SerializedProperty eventCommentProperty =
                    eventComment.FindPropertyRelative(nameof(CyanTriggerComment.comment));
                
                RenderCommentSection(eventCommentProperty, eventIndex);
            }

            CyanTriggerActionVariableDefinition[] variableDefinitions = actionInfo.GetVariables();
            CyanTriggerEditorVariableOption[] eventVariableOptions = actionInfo.GetVariableOptions(eventInstance);
            if (variableDefinitions.Length + eventVariableOptions.Length > 0)
            {
                GUILayout.Space(4);

                var eventRenderData = _eventInstanceRenderData[eventIndex];
                bool expanded = eventRenderData.IsExpanded;
                CyanTriggerPropertyEditor.DrawFoldoutListHeader(
                    new GUIContent(actionInfo.GetActionRenderingDisplayName() + " Inputs"),
                    ref expanded,
                    false,
                    0,
                    null,
                    false,
                    null,
                    false,
                    true
                );

                eventRenderData.IsExpanded = expanded;

                if (expanded)
                {
                    // Draw an outline around the element to emphasize what you are editing.
                    var boxStyle = new GUIStyle
                    {
                        border = new RectOffset(2, 2, 2, 2), 
                        normal = { background = CyanTriggerImageResources.EventInputBackground},
                        padding = new RectOffset(8,8,2,5),
#if UNITY_2019_3_OR_NEWER
                        margin = new RectOffset(4, 4, 0, 0),
#else
                        margin = new RectOffset(5, 5, 0, 0)
#endif
                    };
                    
                    EditorGUILayout.BeginVertical(boxStyle);
                    
                    // Show variable inputs
                    if (variableDefinitions.Length > 0)
                    {
                        CyanTriggerPropertyEditor.DrawActionInstanceInputEditors(
                            eventRenderData,
                            GetThisEventVariables, 
                            Rect.zero, 
                            true);
                        
                        // TODO figure out a better method here. This is hacky and I hate it.
                        if (eventRenderData.ActionInfo.definition?.definition ==
                            CyanTriggerCustomNodeOnVariableChanged.NodeDefinition)
                        {
                            CyanTriggerCustomNodeOnVariableChanged.SetVariableExtraData(
                                eventInstance, 
                                _cyanTriggerDataInstance.variables);
                        }
                    }
                    
                    // Display variables provided by the event.
                    if (eventVariableOptions.Length > 0)
                    {
                        GUILayout.Space(2);
                        
                        // TODO clean up visuals here. This is kind of ugly
                        EditorGUILayout.BeginVertical(CyanTriggerEditorGUIUtil.HelpBoxStyle);
                        EditorGUILayout.LabelField("Event Variables");

                        foreach (var variable in eventVariableOptions)
                        {
                            Rect variableRect = EditorGUILayout.BeginHorizontal();
                            GUIContent variableLabel = new GUIContent(
                                CyanTriggerNameHelpers.GetTypeFriendlyName(variable.Type) + " " + variable.Name);
                            Vector2 dim = GUI.skin.label.CalcSize(variableLabel);
                            variableRect.height = 16;
                            variableRect.x = variableRect.xMax - dim.x;
                            variableRect.width = dim.x;
                            EditorGUI.LabelField(variableRect, variableLabel);
                            EditorGUILayout.EndHorizontal();
                            GUILayout.Space(variableRect.height);
                        }

                        EditorGUILayout.EndVertical();
                    }
                    
                    EditorGUILayout.EndVertical();
                }
                GUILayout.Space(7);
            }
            
            // TODO if CustomAction or Custom Event, add option for defining event input variables
            // (General CyanTrigger Inline variables should be defined in the code rather than at the top...)
        }

        private void RenderEventActions(int eventIndex)
        {
            if (_eventActionTrees[eventIndex] == null)
            {
                Debug.LogWarning("Event action tree is null for event "+eventIndex);
                UpdateOrCreateActionTreeForEvent(eventIndex);
                _eventActionTrees[eventIndex].ExpandAll();
                UpdateActionTreeViewProperties();
            }
            _eventActionTrees[eventIndex].DoLayoutTree();
        }

        private void HandleEventRightClick(Rect eventRect, int eventIndex)
        {
            Event current = Event.current;
            if(current.type == EventType.ContextClick && eventRect.Contains(current.mousePosition))
            {
                ShowEventOptionsMenu(eventIndex);
                current.Use(); 
            }
        }

        private void ShowEventOptionsMenu(int eventIndex)
        {
            /*
            Move Event to Top
            Move Event to Bottom
            Open All Scope
            Close All Scope
             */


            GenericMenu menu = new GenericMenu();
                
            GUIContent moveEventUpContent = new GUIContent("Move Event Up");
            GUIContent moveEventDownContent = new GUIContent("Move Event Down");
            if (eventIndex > 0)
            {
                menu.AddItem(moveEventUpContent, false, () =>
                {
                    SwapEventElements(new List<int> {eventIndex});
                    _serializedObject.ApplyModifiedProperties();
                });
                // TODO Move to top, 
            }
            else
            {
                menu.AddDisabledItem(moveEventUpContent, false);
            }
            if (eventIndex + 1 < _eventsProperty.arraySize)
            {
                menu.AddItem(moveEventDownContent, false, () =>
                {
                    SwapEventElements(new List<int> {eventIndex + 1});
                    _serializedObject.ApplyModifiedProperties();
                });
                // TODO move to Bottom
            }
            else
            {
                menu.AddDisabledItem(moveEventDownContent, false);
            }

            SerializedProperty eventProperty = _eventsProperty.GetArrayElementAtIndex(eventIndex);
            SerializedProperty expandedProperty = eventProperty.FindPropertyRelative(nameof(CyanTriggerEvent.expanded));
            void ToggleEventExpanded()
            {
                expandedProperty.boolValue = !expandedProperty.boolValue;
                expandedProperty.serializedObject.ApplyModifiedProperties();
            }

            GUIContent eventExpandOption = expandedProperty.boolValue
                ? new GUIContent("Minimize Event", "")
                : new GUIContent("Maximize Event", "");
            menu.AddItem(eventExpandOption, false, ToggleEventExpanded);

            void SetActionEditorExpandState(bool value)
            {
                SerializedProperty actions = eventProperty.FindPropertyRelative(nameof(CyanTriggerEvent.actionInstances));

                for (int i = 0; i < actions.arraySize; ++i)
                {
                    SerializedProperty action = actions.GetArrayElementAtIndex(i);
                    SerializedProperty expanded = action.FindPropertyRelative(nameof(CyanTriggerEvent.expanded));
                    expanded.boolValue = value;
                }
                eventProperty.serializedObject.ApplyModifiedProperties();
                _eventActionTrees[eventIndex].RefreshHeight();
            }
            
            menu.AddItem(new GUIContent("Open all Action Editors"), false, () => SetActionEditorExpandState(true));
            menu.AddItem(new GUIContent("Close all Action Editors"), false, () => SetActionEditorExpandState(false));

            
            // Add or edit comment for the event
            SerializedProperty eventInstance =
                eventProperty.FindPropertyRelative(nameof(CyanTriggerEvent.eventInstance));
            SerializedProperty eventComment =
                eventInstance.FindPropertyRelative(nameof(CyanTriggerActionInstance.comment));
            SerializedProperty eventCommentProperty =
                eventComment.FindPropertyRelative(nameof(CyanTriggerComment.comment));
            GUIContent commentContent = new GUIContent(string.IsNullOrEmpty(eventCommentProperty.stringValue)
                ? "Add Comment"
                : "Edit Comment");
            menu.AddItem(commentContent, false, () =>
            {
                _editingCommentId = eventIndex;
                _focusedCommentEditor = false;
            });
            
                
            menu.AddSeparator("");
            
            menu.AddItem(new GUIContent("Duplicate Event"), false, () =>
            {
                DuplicateEvent(eventIndex);
            });
            
            menu.AddSeparator("");
            
            menu.AddItem(new GUIContent("Delete Event"), false, () =>
            {
                RemoveEvents(new List<int> {eventIndex});
                _serializedObject.ApplyModifiedProperties();
            });

            if (_eventActionTrees[eventIndex] != null)
            {
                GUIContent clearAllActionsContent = new GUIContent("Clear All Actions");
                if (_eventActionTrees[eventIndex].Elements.arraySize > 0)
                {
                    menu.AddItem(clearAllActionsContent, false, () =>
                    {
                        _eventActionTrees[eventIndex].Elements.ClearArray();
                        _serializedObject.ApplyModifiedProperties();
                    });
                }
                else
                {
                    menu.AddDisabledItem(clearAllActionsContent, false);
                }
            }

            menu.ShowAsContext();
        }
        
        private void RenderAddEventButton()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Add Event"))
            {
                void AddFavoriteEvent(CyanTriggerSettingsFavoriteItem newEventInfo)
                {
                    var data = newEventInfo.data;
                    AddEvent(CyanTriggerActionInfoHolder.GetActionInfoHolder(data.guid, data.directEvent));
                    _serializedObject.ApplyModifiedProperties();
                }

                CyanTriggerSearchWindowManager.Instance.DisplayEventsFavoritesSearchWindow(AddFavoriteEvent, true);
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
        }

        private enum TriggerModifyAction
        {
            None,
            Delete,
            MoveUp,
            MoveDown,
        }
    }
    
    public class CyanTriggerEditorVariableOption
    {
        public Type Type;
        public string Name;
        public string ID;
        public bool IsReadOnly;
    }

    public class CyanTriggerEditorVariableOptionList
    {
        public readonly Type Type;
        public readonly List<CyanTriggerEditorVariableOption> 
            VariableOptions = new List<CyanTriggerEditorVariableOption>();

        public CyanTriggerEditorVariableOptionList(Type t)
        {
            Type = t;
        }
    }

    public class CyanTriggerEditorScopeTree
    {
        private readonly List<CyanTriggerEditorVariableOption> _variableOptions =
            new List<CyanTriggerEditorVariableOption>();
        private readonly List<int> _prevIndex = new List<int>();
        private readonly List<int> _startIndex = new List<int>();

        public IEnumerable<CyanTriggerEditorVariableOption> GetVariableOptions(Type varType, int index)
        {
            if (index < 0 || index >= _startIndex.Count)
            {
                yield break;
            }
            int ind = _startIndex[index];

            while (ind != -1)
            {
                var variable = _variableOptions[ind];
                if (varType.IsAssignableFrom(variable.Type))
                {
                    yield return variable;
                }
                
                ind = _prevIndex[ind];
            }
        }

        // TODO validate expected name or type
        public bool IsVariableValid(int index, string guid)
        {
            if (index < 0 || index >= _startIndex.Count)
            {
                return false;
            }
            int ind = _startIndex[index];

            while (ind != -1)
            {
                var variable = _variableOptions[ind];
                if (variable.ID == guid)
                {
                    return true;
                }
                
                ind = _prevIndex[ind];
            }

            return false;
        }
        

        public void CreateStructure(SerializedProperty actionList)
        {
            _variableOptions.Clear();
            _prevIndex.Clear();
            _startIndex.Clear();

            Stack<int> lastScopes = new Stack<int>();
            int lastScopeIndex = -1;
            for (int i = 0; i < actionList.arraySize; ++i)
            {
                SerializedProperty actionProperty = actionList.GetArrayElementAtIndex(i);
                CyanTriggerActionInfoHolder actionInfo = 
                    CyanTriggerActionInfoHolder.GetActionInfoHolderFromProperties(actionProperty);
                int scopeDelta = actionInfo.GetScopeDelta();

                if (scopeDelta > 0)
                {
                    lastScopes.Push(lastScopeIndex);
                }
                else if (scopeDelta < 0)
                {
                    lastScopeIndex = lastScopes.Pop();
                }
                
                _startIndex.Add(lastScopeIndex);
                
                var variables = actionInfo.GetCustomEditorVariableOptions(actionProperty);
                if (variables != null)
                {
                    foreach (var variable in variables)
                    {
                        _prevIndex.Add(lastScopeIndex);
                        lastScopeIndex = _variableOptions.Count;
                        
                        _variableOptions.Add(variable);
                    }
                }
            }
        }
    }

    // TODO move this to its own file
    public static class CyanTriggerImageResources
    {
        public static readonly Color LineColorDark = EditorGUIUtility.isProSkin ? 
            new Color(0, 0, 0, 0.5f) : 
            new Color(0.5f, 0.5f, 0.5f, 0.5f);

        public static readonly Color LineColor = EditorGUIUtility.isProSkin ?
            new Color(0.1f, 0.1f, 0.1f, 0.5f) :
            new Color(0.5f, 0.5f, 0.5f, 0.5f);

        public static readonly Color BackgroundColor = EditorGUIUtility.isProSkin ? 
            new Color(0.25f, 0.25f, 0.25f, 0.5f) : 
#if UNITY_2019_3_OR_NEWER
            new Color(0.75f, 0.75f, 0.75f, 0.25f);
#else
            new Color(1f, 1f, 1f, 0.25f);
#endif

        private static Texture2D _actionTreeOutlineTop;
        public static Texture2D ActionTreeOutlineTop
        {
            get
            {
                if (_actionTreeOutlineTop == null)
                {
                    _actionTreeOutlineTop = CreateTexture(3, 3, (x, y) => x == 1 && y <= 1 ? Color.clear : LineColor);
                }
                return _actionTreeOutlineTop;
            }
        }
        
        private static Texture2D _actionTreeWarningOutline;
        public static Texture2D ActionTreeWarningOutline
        {
            get
            {
                if (_actionTreeWarningOutline == null)
                {
                    _actionTreeWarningOutline = CreateTexture(3, 3, (x, y) => x == 1 && y == 1 ? Color.clear : CyanTriggerEditorGUIUtil.WarningColor);
                }
                return _actionTreeWarningOutline;
            }
        }
        
        private static Texture2D _actionTreeErrorOutline;
        public static Texture2D ActionTreeErrorOutline
        {
            get
            {
                if (_actionTreeErrorOutline == null)
                {
                    _actionTreeErrorOutline = CreateTexture(3, 3, (x, y) => x == 1 && y == 1 ? Color.clear : CyanTriggerEditorGUIUtil.ErrorColor);
                }
                return _actionTreeErrorOutline;
            }
        }
        
        private static Texture2D _actionTreeGrayBox;
        public static Texture2D ActionTreeGrayBox
        {
            get
            {
                if (_actionTreeGrayBox == null)
                {
                    _actionTreeGrayBox = CreateTexture(1,1, (x, y) => LineColor);
                }
                return _actionTreeGrayBox;
            }
        }
        
        private static Texture2D _eventInputBackground;
        public static Texture2D EventInputBackground
        {
            get
            {
                if (_eventInputBackground == null)
                {
                    _eventInputBackground = CreateTexture(5,5, (x, y) =>
                    {
                        if (x == 4 || y == 4 || x == 0 || y == 0)
                        {
                            return LineColorDark;
                        }

                        if (x == 3 || y == 3 || x == 1 || y == 1)
                        {
                            return LineColor;
                        }

                        return BackgroundColor;
                    });
                }
                return _eventInputBackground;
            }
        }
            
        private static Texture2D CreateTexture(int width, int height, Func<int, int, Color> getColor)
        {
            Texture2D ret = new Texture2D(width, height)
            {
                alphaIsTransparency = true,
                filterMode = FilterMode.Point
            };
            for (int y = 0; y < ret.height; ++y)
            {
                for (int x = 0; x < ret.width; ++x)
                {
                    ret.SetPixel(x, y, getColor(x, y));
                }
            }
            ret.Apply();
            return ret;
        }
    }
    
    // TODO move this to its own file
    public static class CyanTriggerEditorGUIUtil
    {
        public static readonly Color ErrorColor = EditorGUIUtility.isProSkin 
            ? new Color(1, 0.337f, 0.278f)
            : new Color(0.851f, 0.078f, 0);

        public static readonly Color WarningColor = new Color(244f / 255f, 152f / 255f, 16f / 255f);
        public static readonly Color WarningTextColor = new Color(244f / 255f, 102f / 255f, 0f);

        public static readonly Color CommentColor = EditorGUIUtility.isProSkin 
            ? new Color(133f / 255f, 196f / 255f, 108f / 255f)
            : new Color(36f / 255f, 135f / 255f, 0);

        public readonly static GUIStyle HelpBoxStyle = EditorStyles.helpBox;
        
        private static GUIStyle _foldoutStyle;
        public static GUIStyle FoldoutStyle
        {
            get
            {
                if (_foldoutStyle == null)
                {
                    _foldoutStyle = "IN Foldout";
                }
                return _foldoutStyle;
            }
        }
        
        private static GUIStyle _treeViewLabelStyle;
        public static GUIStyle TreeViewLabelStyle
        {
            get
            {
                if (_treeViewLabelStyle == null)
                {
                    _treeViewLabelStyle = new GUIStyle("TV Line");
                    _treeViewLabelStyle.wordWrap = true;
                }
                return _treeViewLabelStyle;
            }
        }
        
        private static GUIStyle _commentStyle;
        public static GUIStyle CommentStyle
        {
            get
            {
                if (_commentStyle == null)
                {
                    _commentStyle = new GUIStyle(TreeViewLabelStyle);
                    _commentStyle.normal.textColor = CommentColor;

                }
                return _commentStyle;
            }
        }
        
        private static GUIStyle _warningTextStyle;
        public static GUIStyle WarningTextStyle
        {
            get
            {
                if (_warningTextStyle == null)
                {
                    _warningTextStyle = new GUIStyle(EditorStyles.textArea);
                    _warningTextStyle.normal.textColor = WarningTextColor;
                }
                return _warningTextStyle;
            }
        }
        
        private static GUIStyle _errorTextStyle;
        public static GUIStyle ErrorTextStyle
        {
            get
            {
                if (_errorTextStyle == null)
                {
                    _errorTextStyle = new GUIStyle(EditorStyles.textArea);
                    _errorTextStyle.normal.textColor = ErrorColor;
                }
                return _errorTextStyle;
            }
        }


        private static GUIContent _openActionEditorIcon;
        public static GUIContent OpenActionEditorIcon
        {
            get
            {
                if (_openActionEditorIcon == null)
                {
                    _openActionEditorIcon = EditorGUIUtility.TrIconContent("winbtn_win_max_h", "Open Action Editor");
                }
                return _openActionEditorIcon;
            }
        }
        
        private static GUIContent _closeActionEditorIcon;
        public static GUIContent CloseActionEditorIcon
        {
            get
            {
                if (_closeActionEditorIcon == null)
                {
                    _closeActionEditorIcon = EditorGUIUtility.TrIconContent("winbtn_win_min_h", "Close Action Editor");
                }
                return _closeActionEditorIcon;
            }
        }
        
        private static GUIContent _commentCompleteIcon;
        public static GUIContent CommentCompleteIcon
        {
            get
            {
                if (_commentCompleteIcon == null)
                {
                    _commentCompleteIcon = EditorGUIUtility.TrIconContent("FilterSelectedOnly", "Close comment editor");
                }
                return _commentCompleteIcon;
            }
        }

        private static GUIContent _eventMenuIcon;
        public static GUIContent EventMenuIcon
        {
            get
            {
                if (_eventMenuIcon == null)
                {
#if UNITY_2019_3_OR_NEWER
                    _eventMenuIcon = EditorGUIUtility.TrIconContent("_Menu", "View Event Options");
#else
                    _eventMenuIcon = EditorGUIUtility.TrIconContent("LookDevPaneOption", "View Event Options");
#endif
                }
                return _eventMenuIcon;
            }
        }
        
        private static GUIContent _eventDuplicateIcon;
        public static GUIContent EventDuplicateIcon
        {
            get
            {
                if (_eventDuplicateIcon == null)
                {
                    _eventDuplicateIcon = EditorGUIUtility.TrIconContent("TreeEditor.Duplicate", "Duplicate Event");
                }
                return _eventDuplicateIcon;
            }
        }
        
        private static GUIContent _eventCommentContent;
        public static GUIContent EventCommentContent
        {
            get
            {
                if (_eventCommentContent == null)
                {
                    _eventCommentContent = new GUIContent("//", "Edit Comment");
                }
                return _eventCommentContent;
            }
        }
    }
}
