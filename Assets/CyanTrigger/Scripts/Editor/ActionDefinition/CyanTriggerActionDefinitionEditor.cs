using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace CyanTrigger
{
    [CustomEditor(typeof(CyanTriggerActionGroupDefinition), true)]
    public class CyanTriggerActionDefinitionEditor : Editor
    {
        private CyanTriggerActionGroupDefinition _definition;

        private SerializedProperty _exposedActionsProperty;
        
        private ReorderableList _exposedEventsList;
        private ReorderableList _variableList;
        private int _selectedEvent = -1;
        private int _selectedVariable = -1;

        private ReorderableList _variableArrayList;
        private bool _variableArrayExpand;

        private bool _isEditable;
        
        private void OnEnable()
        {
            _definition = (CyanTriggerActionGroupDefinition)target;
            _definition.Initialize();
            
            _exposedActionsProperty = serializedObject.FindProperty(nameof(CyanTriggerActionGroupDefinition.exposedActions));
            
            CreateActionList();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            CyanTriggerActionGroupDefinition programAsset = (CyanTriggerActionGroupDefinition)target;
            
            bool displayOptions = programAsset.DisplayExtraEditorOptions(serializedObject);

            _isEditable = _definition.IsEditorModifiable();
            
            _exposedEventsList.draggable = _exposedEventsList.displayAdd = _exposedEventsList.displayRemove = _isEditable;
            if (_variableList != null)
            {
                _variableList.draggable = _variableList.displayAdd = _variableList.displayRemove = _isEditable;
            }

            if (GUILayout.Button("Refresh Data"))
            {
                _definition.Initialize();
                CreateActionList();
            }
            
            if (displayOptions)
            {
                DrawEvents();
            }

            serializedObject.ApplyModifiedProperties();
            
            if (GUI.changed)
            {
                // TODO update only the group action and not everything every time
                //CyanTriggerActionGroupDefinitionUtil.CollectAllCyanTriggerActionDefinitions();
            }
        }

        public void DrawEvents()
        {
            SerializedProperty exposedActionsProperty = serializedObject.FindProperty(nameof(CyanTriggerActionGroupDefinition.exposedActions));

            GUIStyle style = new GUIStyle(EditorStyles.helpBox);
            EditorGUILayout.BeginVertical(style);
            
            _exposedEventsList.DoLayoutList();

            if (_selectedEvent >= _exposedEventsList.count)
            {
                _selectedEvent = -1;
            }

            if (_selectedEvent == -1 && _selectedVariable != -1)
            {
                _selectedVariable = -1;
                _variableList = null;

                _variableArrayExpand = true;
                _variableArrayList = null;
            }
            
            if (_selectedEvent != -1)
            {
                EditorGUILayout.Space();
                
                EditorGUILayout.BeginVertical(style);
                EditorGUI.BeginDisabledGroup(!_isEditable);
                
                SerializedProperty actionProperty = exposedActionsProperty.GetArrayElementAtIndex(_selectedEvent);
                SerializedProperty nameProperty =
                    actionProperty.FindPropertyRelative(nameof(CyanTriggerActionDefinition.actionName));
                SerializedProperty variantNameProperty =
                    actionProperty.FindPropertyRelative(nameof(CyanTriggerActionDefinition.actionVariantName));
                SerializedProperty descriptionProperty =
                    actionProperty.FindPropertyRelative(nameof(CyanTriggerActionDefinition.description));
                SerializedProperty baseEventProperty =
                    actionProperty.FindPropertyRelative(nameof(CyanTriggerActionDefinition.baseEventName));
                SerializedProperty entryEventProperty =
                    actionProperty.FindPropertyRelative(nameof(CyanTriggerActionDefinition.eventEntry));
                SerializedProperty variablesProperty =
                    actionProperty.FindPropertyRelative(nameof(CyanTriggerActionDefinition.variables));
                        
                EditorGUILayout.PropertyField(nameProperty);
                EditorGUILayout.PropertyField(variantNameProperty);
                EditorGUILayout.PropertyField(descriptionProperty);
                
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.PropertyField(baseEventProperty);
                EditorGUILayout.PropertyField(entryEventProperty);
                EditorGUI.EndDisabledGroup();
                
                EditorGUI.EndDisabledGroup();
                
                if (_variableList == null)
                {
                    CreateVariableList(variablesProperty);
                }
                
                EditorGUILayout.Space();
                _variableList.DoLayoutList();
                
                if (_selectedVariable >= _variableList.count)
                {
                    _selectedVariable = -1;
                }
                
                if (_selectedVariable != -1)
                {
                    EditorGUILayout.Space();
                    
                    EditorGUILayout.BeginVertical(style);
                    EditorGUI.BeginDisabledGroup(!_isEditable);
                    
                    SerializedProperty variableProperty = variablesProperty.GetArrayElementAtIndex(_selectedVariable);
                    
                    SerializedProperty udonNameProperty =
                        variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableDefinition.udonName));
                    SerializedProperty displayNameProperty =
                        variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableDefinition.displayName));
                    SerializedProperty varDescriptionProperty =
                        variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableDefinition.description));
                    SerializedProperty varTypeProperty =
                        variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableDefinition.variableType));
                    SerializedProperty typeProperty =
                        variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableDefinition.type));
                    SerializedProperty typeDefProperty =
                        typeProperty.FindPropertyRelative(nameof(CyanTriggerSerializableType.typeDef));
                    SerializedProperty defaultValueProperty =
                        variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableDefinition.defaultValue));
                    
                    Type type = Type.GetType(typeDefProperty.stringValue);
                    
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.PropertyField(udonNameProperty);
                    EditorGUI.EndDisabledGroup();
                    
                    EditorGUILayout.PropertyField(displayNameProperty);
                    EditorGUILayout.PropertyField(varDescriptionProperty);

                    
                    CyanTriggerActionVariableTypeDefinition varTypes =
                        (CyanTriggerActionVariableTypeDefinition) varTypeProperty.intValue;

                    bool constant = varTypes.HasFlag(CyanTriggerActionVariableTypeDefinition.Constant);
                    bool variableInput = varTypes.HasFlag(CyanTriggerActionVariableTypeDefinition.VariableInput);
                    bool variableOutput = varTypes.HasFlag(CyanTriggerActionVariableTypeDefinition.VariableOutput);
                    bool hidden = varTypes.HasFlag(CyanTriggerActionVariableTypeDefinition.Hidden);
                    bool allowsMultiple = varTypes.HasFlag(CyanTriggerActionVariableTypeDefinition.AllowsMultiple);
                    
                    varTypes = CyanTriggerActionVariableTypeDefinition.None;

                    EditorGUI.BeginDisabledGroup(_selectedVariable != 0 || type.IsArray);

                    allowsMultiple = EditorGUILayout.Toggle(
                        new GUIContent("Repeat for Multiple Objects",
                            "This variable will be displayed as a list and the action will repeat itself for each " +
                            "item. This option is only available for the first variable slot and cannot be hidden. " +
                            "Array types are not supported at this time"),
                        allowsMultiple && _selectedVariable == 0);

                    EditorGUI.EndDisabledGroup();
                    
                    EditorGUI.BeginDisabledGroup(allowsMultiple);
                    
                    hidden = EditorGUILayout.Toggle(
                        new GUIContent("Hidden in inspector",
                            "Check this if this variable will only ever use the default value. " +
                            "This is useful for making variants of an action using only different input parameters."), 
                        hidden && !allowsMultiple);

                    EditorGUI.EndDisabledGroup();
                    
                    EditorGUI.BeginDisabledGroup(hidden);
                    
                    constant = EditorGUILayout.Toggle(
                        new GUIContent("Allows Constants",
                            "Check this if constant values provided from the inspector can be used for this variable."), 
                        constant);

                    variableInput = EditorGUILayout.Toggle(
                        new GUIContent("Allows Variable Input",
                            "Check this if user defined variables can be used for this variable."), variableInput);

                    EditorGUI.BeginDisabledGroup(constant || !variableInput);
                    
                    variableOutput = EditorGUILayout.Toggle(
                        new GUIContent("Modifies Variable",
                            "Check this if this variable will be modified in the action and the " +
                            "value stored into a user defined variable."), 
                        variableOutput);

                    EditorGUI.EndDisabledGroup();
                    EditorGUI.EndDisabledGroup();

                    if (hidden)
                    {
                        constant = true;
                        variableInput = false;
                        variableOutput = false;
                    }
                    if (constant && variableOutput)
                    {
                        variableOutput = false;
                    }
                    if (!variableInput && variableOutput)
                    {
                        variableOutput = false;
                    }

                    if (constant)
                    {
                        varTypes |= CyanTriggerActionVariableTypeDefinition.Constant;
                    }
                    if (variableInput)
                    {
                        varTypes |= CyanTriggerActionVariableTypeDefinition.VariableInput;
                    }
                    if (variableOutput)
                    {
                        varTypes |= CyanTriggerActionVariableTypeDefinition.VariableOutput;
                    }
                    if (hidden)
                    {
                        varTypes |= CyanTriggerActionVariableTypeDefinition.Hidden;
                    }
                    if (allowsMultiple)
                    {
                        varTypes |= CyanTriggerActionVariableTypeDefinition.AllowsMultiple;
                    }

                    varTypeProperty.intValue = (int) varTypes;
                    
                    EditorGUI.BeginDisabledGroup(!constant);
                    
                    if (type.IsArray)
                    {
                        CyanTriggerPropertyEditor.DrawArrayEditor(
                            defaultValueProperty, 
                            new GUIContent("Default Value"),
                            type, 
                            ref _variableArrayExpand, 
                            ref _variableArrayList);
                    }
                    else
                    {
                        CyanTriggerPropertyEditor.DrawEditor(
                            defaultValueProperty, 
                            Rect.zero, 
                            new GUIContent("Default Value"), 
                            type, 
                            true);
                    }
                    
                    
                    EditorGUI.EndDisabledGroup();
                    
                    EditorGUI.EndDisabledGroup();
                    EditorGUILayout.EndVertical();
                }
                
                
                EditorGUILayout.EndVertical();
                
                serializedObject.ApplyModifiedProperties();
            }
            
            EditorGUILayout.EndVertical();
        }

        private void CreateActionList()
        {
            ReorderableList newList = new ReorderableList(serializedObject, _exposedActionsProperty, _isEditable, true, _isEditable, _isEditable);
            newList.drawHeaderCallback = (Rect rect) => EditorGUI.LabelField(rect, "Actions");
            newList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                SerializedProperty actionProperty = _exposedActionsProperty.GetArrayElementAtIndex(index);
                SerializedProperty nameProperty =
                    actionProperty.FindPropertyRelative(nameof(CyanTriggerActionDefinition.actionName));
                SerializedProperty variantNameProperty =
                    actionProperty.FindPropertyRelative(nameof(CyanTriggerActionDefinition.actionVariantName));
                SerializedProperty baseEventProperty =
                    actionProperty.FindPropertyRelative(nameof(CyanTriggerActionDefinition.baseEventName));
                
                string actionLabel = "Action";
                if (!baseEventProperty.stringValue.Equals("Event_Custom"))
                {
                    actionLabel = "Event";
                }
                
                string actionName =
                    $"{actionLabel}/{nameProperty.stringValue}/{variantNameProperty.stringValue}";
                
                
                // TODO get variables and add params here?
                EditorGUI.LabelField(rect, new GUIContent(actionName));
                
                
                if (isActive)
                {
                    if (_selectedEvent != index)
                    {
                        _selectedEvent = index;
                        _selectedVariable = -1;
                        _variableList = null;

                        _variableArrayExpand = true;
                        _variableArrayList = null;
                    }
                }
                else if (_selectedEvent == index)
                {
                    _selectedEvent = -1;
                    
                    _selectedVariable = -1;
                    _variableList = null;

                    _variableArrayExpand = true;
                    _variableArrayList = null;
                }
            };
            newList.onAddCallback = (ReorderableList list) =>
            {
                _definition.AddNewEvent(_exposedActionsProperty);
                
                serializedObject.ApplyModifiedProperties();
            };
            _exposedEventsList = newList;
        }
        
        private void CreateVariableList(SerializedProperty variablesProperty)
        {
            ReorderableList newList = new ReorderableList(serializedObject, variablesProperty, _isEditable, true, _isEditable, _isEditable);
            newList.drawHeaderCallback = (Rect rect) => EditorGUI.LabelField(rect, "Variables");
            newList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                SerializedProperty variableProperty = variablesProperty.GetArrayElementAtIndex(index);
                
                SerializedProperty typeProperty =
                    variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableDefinition.type));
                SerializedProperty typeDefProperty =
                    typeProperty.FindPropertyRelative(nameof(CyanTriggerSerializableType.typeDef));
                SerializedProperty nameProperty =
                    variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableDefinition.displayName));
                
                Type type = Type.GetType(typeDefProperty.stringValue);

                string actionName =
                    $"{CyanTriggerNameHelpers.GetTypeFriendlyName(type)} {nameProperty.stringValue}";
                EditorGUI.LabelField(rect, new GUIContent(actionName));
                
                if (isActive)
                {
                    if (_selectedVariable != index)
                    {
                        _selectedVariable = index;
                        _variableArrayExpand = true;
                        _variableArrayList = null;
                    }
                }
                else if (_selectedVariable == index)
                {
                    _selectedVariable = -1;
                    _variableArrayExpand = true;
                    _variableArrayList = null;
                }
            };
            newList.onAddCallback = (ReorderableList list) =>
            {
                _definition.AddNewVariable(variablesProperty);
                
                serializedObject.ApplyModifiedProperties();
            };
            _variableList = newList;
        }
    }
}
