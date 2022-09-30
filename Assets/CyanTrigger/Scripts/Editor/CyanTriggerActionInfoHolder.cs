using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Graph;

namespace CyanTrigger
{
    public class CyanTriggerActionInfoHolder
    {
        private const string InvalidString = "<Invalid>";
        
        private static readonly CyanTriggerActionInfoHolder InvalidAction;
        private static readonly Dictionary<string, CyanTriggerActionInfoHolder> CustomActions =
            new Dictionary<string, CyanTriggerActionInfoHolder>();
        private static readonly Dictionary<string, CyanTriggerActionInfoHolder> DefinitionActions =
            new Dictionary<string, CyanTriggerActionInfoHolder>();
        
        public readonly CyanTriggerNodeDefinition definition;
        public readonly CyanTriggerCustomUdonNodeDefinition customDefinition;
        public readonly CyanTriggerActionDefinition action;
        public readonly CyanTriggerActionGroupDefinition actionGroup;

        
        static CyanTriggerActionInfoHolder()
        {
            InvalidAction = new CyanTriggerActionInfoHolder();
        }

        public static CyanTriggerActionInfoHolder GetActionInfoHolder(
            string guid,
            string directEvent)
        {
            if (!string.IsNullOrEmpty(guid))
            {
                return GetActionInfoHolderFromGuid(guid);
            }
            if (!string.IsNullOrEmpty(directEvent))
            {
                return GetActionInfoHolderFromDefinition(directEvent);
            }

            return InvalidAction;
        }

        public static CyanTriggerActionInfoHolder GetActionInfoHolder(CyanTriggerSettingsFavoriteItem favoriteItem)
        {
            var actionType = favoriteItem.data;
            return GetActionInfoHolder(actionType.guid, actionType.directEvent);
        }
        
        public static CyanTriggerActionInfoHolder GetActionInfoHolderFromGuid(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return InvalidAction;
            }

            if (CustomActions.TryGetValue(guid, out var actionInfo))
            {
                if (actionInfo.IsValid())
                {
                    return actionInfo;
                }
                CustomActions.Remove(guid);
            }

            if (!CyanTriggerActionGroupDefinitionUtil.TryGetActionDefinition(guid,
                out CyanTriggerActionDefinition actionDef))
            {
                return InvalidAction;
            }
            
            if (!CyanTriggerActionGroupDefinitionUtil.TryGetActionGroupDefinition(actionDef, out var actionGroup))
            {
                return InvalidAction;
            }
            
            actionInfo = new CyanTriggerActionInfoHolder(actionDef, actionGroup);
            CustomActions.Add(guid, actionInfo);
            
            return actionInfo;
        }

        public static CyanTriggerActionInfoHolder GetActionInfoHolder(CyanTriggerActionDefinition actionDef)
        {
            if (actionDef == null || string.IsNullOrEmpty(actionDef.guid))
            {
                return InvalidAction;
            }
            
            if (CustomActions.TryGetValue(actionDef.guid, out var actionInfo))
            {
                if (actionInfo.IsValid())
                {
                    return actionInfo;
                }
                CustomActions.Remove(actionDef.guid);
            }
            
            if (!CyanTriggerActionGroupDefinitionUtil.TryGetActionGroupDefinition(actionDef, out var actionGroup))
            {
                return InvalidAction;
            }
            
            actionInfo = new CyanTriggerActionInfoHolder(actionDef, actionGroup);
            CustomActions.Add(actionDef.guid, actionInfo);
            
            return actionInfo;
        }
        
        public static CyanTriggerActionInfoHolder GetActionInfoHolder(
            CyanTriggerActionDefinition actionDef, 
            CyanTriggerActionGroupDefinition actionGroup)
        {
            if (actionDef == null || string.IsNullOrEmpty(actionDef.guid))
            {
                return InvalidAction;
            }
            
            if (CustomActions.TryGetValue(actionDef.guid, out var actionInfo))
            {
                if (actionInfo.IsValid())
                {
                    return actionInfo;
                }
                CustomActions.Remove(actionDef.guid);
            }
            
            if (actionGroup == null && 
                !CyanTriggerActionGroupDefinitionUtil.TryGetActionGroupDefinition(actionDef, out actionGroup))
            {
                return InvalidAction;
            }
            
            actionInfo = new CyanTriggerActionInfoHolder(actionDef, actionGroup);
            CustomActions.Add(actionDef.guid, actionInfo);
            
            return actionInfo;
        }
        
        public static CyanTriggerActionInfoHolder GetActionInfoHolder(UdonNodeDefinition definition)
        {
            if (definition == null)
            {
                return InvalidAction;
            }
            
            return GetActionInfoHolderFromDefinition(definition.fullName);
        }
        
        public static CyanTriggerActionInfoHolder GetActionInfoHolder(CyanTriggerNodeDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.fullName))
            {
                return InvalidAction;
            }

            string key = definition.fullName;
            if (DefinitionActions.TryGetValue(key, out var actionInfo))
            {
                if (actionInfo.IsValid())
                {
                    return actionInfo;
                }
                DefinitionActions.Remove(key);
            }

            actionInfo = new CyanTriggerActionInfoHolder(definition);
            DefinitionActions.Add(key, actionInfo);
            
            return actionInfo;
        }
        
        public static CyanTriggerActionInfoHolder GetActionInfoHolderFromDefinition(string definition)
        {
            if (string.IsNullOrEmpty(definition))
            {
                return InvalidAction;
            }

            if (DefinitionActions.TryGetValue(definition, out var actionInfo))
            {
                if (actionInfo.IsValid())
                {
                    return actionInfo;
                }
                DefinitionActions.Remove(definition);
            }

            var def = CyanTriggerNodeDefinitionManager.GetDefinition(definition);
            if (def == null)
            {
                return InvalidAction;
            }
            
            actionInfo = new CyanTriggerActionInfoHolder();
            DefinitionActions.Add(definition, actionInfo);
            
            return actionInfo;
        }
        
        public static CyanTriggerActionInfoHolder GetActionInfoHolderFromProperties(SerializedProperty actionProperty)
        {
            SerializedProperty actionTypeProperty =
                actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.actionType));
            SerializedProperty directEvent =
                actionTypeProperty.FindPropertyRelative(nameof(CyanTriggerActionType.directEvent));
            SerializedProperty guidProperty =
                actionTypeProperty.FindPropertyRelative(nameof(CyanTriggerActionType.guid));

            return GetActionInfoHolder(
                guidProperty.stringValue,
                directEvent.stringValue);
        }
        
        private CyanTriggerActionInfoHolder() { }

        private CyanTriggerActionInfoHolder(CyanTriggerNodeDefinition definition)
        {
            this.definition = definition;
            CyanTriggerNodeDefinitionManager.TryGetCustomDefinition(definition.fullName, out customDefinition);
        }

        private CyanTriggerActionInfoHolder(
            CyanTriggerActionDefinition action,
            CyanTriggerActionGroupDefinition actionGroup)
        {
            this.action = action;
            this.actionGroup = actionGroup;
            
            if (this.actionGroup == null)
            {
                CyanTriggerActionGroupDefinitionUtil.TryGetActionGroupDefinition(action, out this.actionGroup);
            }
        }

        public CyanTriggerActionType GetActionType()
        {
            return new CyanTriggerActionType
            {
                guid = action?.guid,
                directEvent = definition?.fullName
            };
        }

        public string GetDisplayName()
        {
            if (definition != null)
            {
                return definition.GetActionDisplayName();
            }

            if (action != null)
            {
                return action.actionName;
            }

            return InvalidString;
        }

        public string GetVariantName()
        {
            if (definition != null)
            {
                return "VRC_Direct";
            }

            if (action != null)
            {
                return action.actionVariantName;
            }

            return InvalidString;
        }

        public string GetActionRenderingDisplayName()
        {
            string displayName = GetDisplayName();

            if (definition != null)
            {
                return displayName;
            }

            return displayName + "." + GetVariantName();
        }

        public string GetMethodSignature()
        {
            if (definition != null)
            {
                return definition.GetMethodDisplayName();
            }
            
            if (action != null)
            {
                return action.GetMethodSignature();
            }
            
            return InvalidString;
        }

        public bool IsValid()
        {
            return IsDefinition() || IsAction();
        }

        public bool IsAction()
        {
            return action != null && actionGroup != null;
        }

        public bool IsDefinition()
        {
            return definition != null;
        }

        public bool Equals(CyanTriggerActionInfoHolder o)
        {
            return 
                o != null 
                && definition == o.definition 
                && action == o.action 
                && actionGroup == o.actionGroup;
        }

        public CyanTriggerActionVariableDefinition[] GetVariables()
        {
            if (action != null)
            {
                return action.variables;
            }
            
            if (definition == null || definition.fullName == "Event_Custom")
            {
                return new CyanTriggerActionVariableDefinition[0];
            }

            return definition.variableDefinitions;

            /*
            // This would only be used for custom to provide the name field, but it isn't labeled.
            // Keeping the code, but ignoring it here. 
            List<CyanTriggerActionVariableDefinition> variables = new List<CyanTriggerActionVariableDefinition>();
            foreach (var inputs in definition.GetEventVariables(1 /* inputs only UdonNodeParameter.ParameterType * /))
            {
                variables.Add(new CyanTriggerActionVariableDefinition
                {
                    type = new SerializableType(inputs.Item2), 
                    udonName = inputs.Item1, 
                    displayName = inputs.Item1, 
                    variableType = inputs.Item2 == typeof(CyanTriggerVariable) ? 
                        CyanTriggerActionVariableTypeDefinition.VariableInput :
                        CyanTriggerActionVariableTypeDefinition.Constant
                });
            }
            
            return variables.ToArray();
            */
        }

        public CyanTriggerEditorVariableOption[] GetVariableOptions(SerializedProperty eventProperty)
        {
            var def = definition;
            if (action != null)
            {
                def = CyanTriggerNodeDefinitionManager.GetDefinition(action.baseEventName);
            }
            
            if (def == null)
            {
                return new CyanTriggerEditorVariableOption[0];
            }

            List<CyanTriggerEditorVariableOption> variables = new List<CyanTriggerEditorVariableOption>();
            var options = GetCustomEditorVariableOptions(eventProperty);
            if (options != null)
            {
                return options;
            }
            
            foreach (var (varName, varType) in def.GetEventVariables(/* output only UdonNodeParameter.ParameterType */))
            {
                variables.Add(new CyanTriggerEditorVariableOption 
                    {Type = varType, Name = varName, IsReadOnly = true});
            }
            
            return variables.ToArray();
        }

        public CyanTriggerEditorVariableOption[] GetCustomEditorVariableOptions(SerializedProperty actionProperty)
        {
            if (definition != null &&
                customDefinition != null &&
                customDefinition.DefinesCustomEditorVariableOptions())
            {
                SerializedProperty inputsProperty =
                    actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.inputs));
                
                return customDefinition.GetCustomEditorVariableOptions(inputsProperty);
            }
            return null;
        }

        public int GetScopeDelta()
        {
            // Check if item has scope (block, blockend, while, for, if, else if, else)
            if (definition != null && CyanTriggerNodeDefinitionManager.DefinitionHasScope(definition.fullName))
            {
                return 1;
            }

            if (definition != null && 
                definition.definition == CyanTriggerCustomNodeBlockEnd.NodeDefinition)
            {
                return -1;
            }
            return 0;
        }

        public List<SerializedProperty> AddActionToEndOfPropertyList(
            SerializedProperty actionListProperty, 
            bool includeDependencies)
        {
            List<SerializedProperty> properties = new List<SerializedProperty>();
            actionListProperty.arraySize++;
            SerializedProperty newActionProperty =
                actionListProperty.GetArrayElementAtIndex(actionListProperty.arraySize - 1);
            properties.Add(newActionProperty);
            
            CyanTriggerSerializedPropertyUtils.SetActionData(this, newActionProperty);
            
            // If scope, add appropriate end point
            if (includeDependencies &&
                definition != null && 
                customDefinition != null &&
                customDefinition.HasDependencyNodes())
            {
                foreach (var dependency in customDefinition.GetDependentNodes())
                {
                    properties.AddRange(
                        GetActionInfoHolder(dependency).AddActionToEndOfPropertyList(actionListProperty, true));
                }
            }

            return properties;
        }

        public string GetActionRenderingDisplayName(SerializedProperty actionProperty)
        {
            if (!CyanTriggerSettings.Instance.actionDetailedView)
            {
                return GetActionRenderingDisplayName();
            }
            
            string signature = GetActionRenderingDisplayName();
            if (customDefinition is CyanTriggerCustomNodeVariable)
            {
                var variableDefinitions = GetVariables();
                SerializedProperty inputsProperty =
                    actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.inputs));
                SerializedProperty inputNameProperty = inputsProperty.GetArrayElementAtIndex(0);
                SerializedProperty inputDataProperty = inputsProperty.GetArrayElementAtIndex(1);
                
                string name = GetTextForProperty(inputNameProperty, variableDefinitions[0]);
                name = name.Substring(1, name.Length - 2);
            
                return "var " + name +" " + signature + 
                       ".Set(" + GetTextForProperty(inputDataProperty, variableDefinitions[1]) + ")";
            }
            
            return signature + GetMethodArgumentDisplay(actionProperty);
        }
        
        public string GetMethodArgumentDisplay(SerializedProperty actionProperty)
        {
            SerializedProperty inputsProperty =
                actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.inputs));
            
            StringBuilder sb = new StringBuilder();

            var variableDefinitions = GetVariables();
            int displayCount = 0;
            for (int input = 0; input < inputsProperty.arraySize && input < variableDefinitions.Length; ++input)
            {
                var variableDef = variableDefinitions[input];
                if ((variableDef.variableType & CyanTriggerActionVariableTypeDefinition.Hidden) != 0)
                {
                    continue;
                }

                if (displayCount > 0)
                {
                    sb.Append(", ");
                }
                ++displayCount;
                
                Type varType = variableDef.type.type;
                
                if (input == 0 &&
                    (variableDef.variableType & CyanTriggerActionVariableTypeDefinition.AllowsMultiple) != 0)
                {
                    SerializedProperty multiInputsProperty =
                        actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.multiInput));
                    
                    if (multiInputsProperty.arraySize == 0)
                    {
                        sb.Append("null");
                    }
                    else if (multiInputsProperty.arraySize == 1)
                    {
                        SerializedProperty multiInputProperty = multiInputsProperty.GetArrayElementAtIndex(0);
                        sb.Append(GetTextForProperty(multiInputProperty, variableDef));
                    }
                    else
                    {
                        sb.Append(CyanTriggerNameHelpers.GetTypeFriendlyName(varType) +"Array");
                    }
                    
                    continue;
                }
                
                SerializedProperty inputProperty = inputsProperty.GetArrayElementAtIndex(input);
                sb.Append(GetTextForProperty(inputProperty, variableDef));
            }

            if (sb.Length > 0)
            {
                return "(" + sb + ")";
            }
            
            return sb.ToString();
        }
        
        public static string GetTextForProperty(
            SerializedProperty inputProperty, 
            CyanTriggerActionVariableDefinition variableDef)
            {
                SerializedProperty isVariableProperty =
                    inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
                
                if (isVariableProperty.boolValue)
                {
                    SerializedProperty nameProperty =
                        inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.name));

                    bool isOutput =
                        (variableDef.variableType & CyanTriggerActionVariableTypeDefinition.VariableOutput) != 0;
                   
                    string displayName = nameProperty.stringValue;
                    if (string.IsNullOrEmpty(displayName))
                    {
                        displayName = "null";
                    }
                    
                    // TODO verify that name is always filled :eyes:
                    return (isOutput ? "out " : "") + "var " + displayName;
                }
                
                SerializedProperty dataProperty =
                    inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
                var data = CyanTriggerSerializableObject.ObjectFromSerializedProperty(dataProperty);

                if (data == null)
                {
                    return "null";
                }

                Type varType = variableDef.type.type;

                
                if (varType == typeof(string))
                {
                    return "\"" + data +"\"";
                }

                if (data is GameObject gameObject)
                {
                    return VRC.Tools.GetGameObjectPath(gameObject);
                }
                if (data is Component component)
                {
                    return VRC.Tools.GetGameObjectPath(component.gameObject);
                }

                if (data is UnityEngine.Object obj)
                {
                    return obj.name;
                }
                
                string ret = data.ToString();
                if (ret == varType.FullName ||
                    (varType.IsValueType 
                     && !varType.IsPrimitive && 
                     varType.GetMethod("ToString", new Type[0]).DeclaringType == typeof(ValueType)))
                {
                    return "const " + CyanTriggerNameHelpers.GetTypeFriendlyName(varType);
                }

                return ret;
            }
    }
  
}