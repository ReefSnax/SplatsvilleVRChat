using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase;

namespace CyanTrigger
{
    public static class CyanTriggerSerializedPropertyUtils
    {
        public static void SetVariableData(SerializedProperty variableProperty, string variableName, Type type, object data = default, string id = null)
        {
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString();
            }
            
            SerializedProperty idProperty =
                variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.variableID));
            idProperty.stringValue = id;

            SerializedProperty nameProperty = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.name));
            nameProperty.stringValue = variableName;

            SerializedProperty syncProperty = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.sync));
            syncProperty.enumValueIndex = (int) CyanTriggerVariableSyncMode.NotSynced;

            SerializedProperty typeProperty = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.type));
            SerializedProperty typeDefProperty =
                typeProperty.FindPropertyRelative(nameof(CyanTriggerSerializableType.typeDef));
            typeDefProperty.stringValue = type.AssemblyQualifiedName;

            SerializedProperty dataProperty = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.data));

            if (data == null)
            {
                CyanTriggerSerializableObject.UpdateSerializedProperty(dataProperty,
                    type.IsValueType ? Activator.CreateInstance(type) : null);
            }
            else
            {
                CyanTriggerSerializableObject.UpdateSerializedProperty(dataProperty, data);
            }
        }
    
        public static void SetActionData(
            CyanTriggerActionInfoHolder actionInfo, 
            SerializedProperty actionProperty, 
            bool clearComment = true)
        {
            SerializedProperty actionTypeProperty =
                actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.actionType));
            SerializedProperty directEvent =
                actionTypeProperty.FindPropertyRelative(nameof(CyanTriggerActionType.directEvent));
            SerializedProperty guidProperty =
                actionTypeProperty.FindPropertyRelative(nameof(CyanTriggerActionType.guid));
            SerializedProperty inputsProperty =
                actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.inputs));
            SerializedProperty multiInputsProperty =
                actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.multiInput));
            multiInputsProperty.ClearArray();
            
            SerializedProperty expandedProperty =
                actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.expanded));
            expandedProperty.boolValue = false;

            if (clearComment)
            {
                SerializedProperty commentProperty =
                    actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.comment));
                SerializedProperty commentStringProperty =
                    commentProperty.FindPropertyRelative(nameof(CyanTriggerComment.comment));
                commentStringProperty.stringValue = "";
            }

            if (actionInfo.action != null)
            {
                guidProperty.stringValue = actionInfo.action.guid;
                directEvent.stringValue = null;
                SetPropertyInputDefaults(inputsProperty, multiInputsProperty, actionInfo.action.variables);
            }

            if (actionInfo.definition != null)
            {
                guidProperty.stringValue = null;
                directEvent.stringValue = actionInfo.definition.fullName;

                SetPropertyInputDefaults(inputsProperty, multiInputsProperty, actionInfo.definition.variableDefinitions);
                
                if (actionInfo.customDefinition != null && actionInfo.customDefinition.HasCustomVariableInitialization())
                {
                    actionInfo.customDefinition.InitializeVariableProperties(inputsProperty, multiInputsProperty);
                }
            }
            
            actionProperty.serializedObject.ApplyModifiedProperties();
        }

        public static void SetPropertyInputDefaults(
            SerializedProperty inputsProperty, 
            SerializedProperty multiInputsProperty,
            CyanTriggerActionVariableDefinition[] variableDefinitions)
        {
            inputsProperty.ClearArray();
            inputsProperty.arraySize = variableDefinitions.Length;
            
            for (int cur = 0; cur < variableDefinitions.Length; ++cur)
            {
                var variableDef = variableDefinitions[cur];
                Type type = variableDef.type.type;
                
                SerializedProperty inputProperty;
                if (cur == 0 && (variableDef.variableType & CyanTriggerActionVariableTypeDefinition.AllowsMultiple) != 0)
                {
                    if (type == typeof(VRCPlayerApi))
                    {
                        multiInputsProperty.arraySize = 1;
                        inputProperty = multiInputsProperty.GetArrayElementAtIndex(cur);
                    }
                    else
                    {
                        continue;
                    }
                }
                else
                {
                    inputProperty = inputsProperty.GetArrayElementAtIndex(cur);
                }
                    
                SerializedProperty dataProperty =
                    inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
                SerializedProperty isVariableProperty =
                    inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
                    
                var data = variableDef.defaultValue?.obj;
                if (data == null)
                {
                    if(type.IsValueType)
                    {
                        data = Activator.CreateInstance(type);
                    }
                    else if (type.IsArray)
                    {
                        data = Array.CreateInstance(type, 0);
                    } 
                    else if (type == typeof(string))
                    {
                        data = "";
                    }
                }

                if ((variableDef.variableType & CyanTriggerActionVariableTypeDefinition.Constant) == 0)
                {
                    isVariableProperty.boolValue = true;
                }

                if (type == typeof(VRCPlayerApi))
                {
                    isVariableProperty.boolValue = true;
                    SerializedProperty nameProperty =
                        inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.name));
                    SerializedProperty idProperty =
                        inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID));

                    nameProperty.stringValue = CyanTriggerAssemblyData.LocalPlayerName;
                    idProperty.stringValue = CyanTriggerAssemblyData.LocalPlayerGUID;
                }
                
                CyanTriggerSerializableObject.UpdateSerializedProperty(dataProperty, data);
            }
        }

        public static void InitializeEventProperties(CyanTriggerActionInfoHolder actionInfo, SerializedProperty newEvent)
        {
            SerializedProperty eventInstance = newEvent.FindPropertyRelative(nameof(CyanTriggerEvent.eventInstance));
            SetActionData(actionInfo, eventInstance);
            
            SerializedProperty actionExpandedProperty =
                eventInstance.FindPropertyRelative(nameof(CyanTriggerActionInstance.expanded));
            actionExpandedProperty.boolValue = true;
            
            SerializedProperty expandedProperty = newEvent.FindPropertyRelative(nameof(CyanTriggerEvent.expanded));
            expandedProperty.boolValue = true;

            SerializedProperty actionInstances = newEvent.FindPropertyRelative(nameof(CyanTriggerEvent.actionInstances));
            actionInstances.ClearArray();
            
            SerializedProperty eventOptions = newEvent.FindPropertyRelative(nameof(CyanTriggerEvent.eventOptions));
            SerializedProperty userGate = eventOptions.FindPropertyRelative(nameof(CyanTriggerEventOptions.userGate));
            userGate.intValue = 0;
            SerializedProperty userGateExtraData = 
                eventOptions.FindPropertyRelative(nameof(CyanTriggerEventOptions.userGateExtraData));
            userGateExtraData.ClearArray();
            SerializedProperty broadcast = eventOptions.FindPropertyRelative(nameof(CyanTriggerEventOptions.broadcast));
            broadcast.intValue = 0;
            SerializedProperty delay = eventOptions.FindPropertyRelative(nameof(CyanTriggerEventOptions.delay));
            delay.floatValue = 0;
            
            SerializedProperty eventName = newEvent.FindPropertyRelative(nameof(CyanTriggerEvent.name));
            eventName.stringValue = "_Unnamed";
        }
        
        public static void CopyEventProperties(
            CyanTriggerActionInfoHolder actionInfo, 
            SerializedProperty srcEvent, 
            SerializedProperty dstEvent)
        {
            Dictionary<string, string> variableGuidMap = new Dictionary<string, string>();
            
            SerializedProperty dstEventInstance = dstEvent.FindPropertyRelative(nameof(CyanTriggerEvent.eventInstance));
            SerializedProperty srcEventInstance = srcEvent.FindPropertyRelative(nameof(CyanTriggerEvent.eventInstance));
            SetActionData(actionInfo, dstEventInstance);
            
            // Copy event type info
            CopyDataAndRemapVariables(actionInfo, srcEventInstance, dstEventInstance, variableGuidMap);
            
            SerializedProperty dstExpanded = dstEvent.FindPropertyRelative(nameof(CyanTriggerEvent.expanded));
            SerializedProperty srcExpanded = srcEvent.FindPropertyRelative(nameof(CyanTriggerEvent.expanded));
            dstExpanded.boolValue = srcExpanded.boolValue;
            
            SerializedProperty dstActionInstances = dstEvent.FindPropertyRelative(nameof(CyanTriggerEvent.actionInstances));
            SerializedProperty srcActionInstances = srcEvent.FindPropertyRelative(nameof(CyanTriggerEvent.actionInstances));
            dstActionInstances.ClearArray();
            dstActionInstances.arraySize = srcActionInstances.arraySize;
            for (int curAction = 0; curAction < srcActionInstances.arraySize; ++curAction)
            {
                SerializedProperty dstAction = dstActionInstances.GetArrayElementAtIndex(curAction);
                SerializedProperty srcAction = srcActionInstances.GetArrayElementAtIndex(curAction);

                // Copy all actions
                var curActionInfo = CyanTriggerActionInfoHolder.GetActionInfoHolderFromProperties(srcAction);
                SetActionData(curActionInfo, dstAction);
                CopyDataAndRemapVariables(curActionInfo, srcAction, dstAction, variableGuidMap);
            }
            
            SerializedProperty dstEventOptions = dstEvent.FindPropertyRelative(nameof(CyanTriggerEvent.eventOptions));
            SerializedProperty srcEventOptions = srcEvent.FindPropertyRelative(nameof(CyanTriggerEvent.eventOptions));
            
            SerializedProperty dstUserGate = dstEventOptions.FindPropertyRelative(nameof(CyanTriggerEventOptions.userGate));
            SerializedProperty srcUserGate = srcEventOptions.FindPropertyRelative(nameof(CyanTriggerEventOptions.userGate));
            dstUserGate.intValue = srcUserGate.intValue;
            
            SerializedProperty dstUserGateExtraData = 
                dstEventOptions.FindPropertyRelative(nameof(CyanTriggerEventOptions.userGateExtraData));
            SerializedProperty srcUserGateExtraData = 
                srcEventOptions.FindPropertyRelative(nameof(CyanTriggerEventOptions.userGateExtraData));
            dstUserGateExtraData.ClearArray();
            dstUserGateExtraData.arraySize = srcUserGateExtraData.arraySize;
            for (int curData = 0; curData < srcUserGateExtraData.arraySize; ++curData)
            {
                SerializedProperty dstDataElement = dstUserGateExtraData.GetArrayElementAtIndex(curData);
                SerializedProperty srcDataElement = srcUserGateExtraData.GetArrayElementAtIndex(curData);
                
                CopyVariableInstanceProperty(srcDataElement, dstDataElement, variableGuidMap);
            }
            
            SerializedProperty dstBroadcast = dstEventOptions.FindPropertyRelative(nameof(CyanTriggerEventOptions.broadcast));
            SerializedProperty srcBroadcast = srcEventOptions.FindPropertyRelative(nameof(CyanTriggerEventOptions.broadcast));
            dstBroadcast.intValue = srcBroadcast.intValue;
            
            SerializedProperty dstDelay = dstEventOptions.FindPropertyRelative(nameof(CyanTriggerEventOptions.delay));
            SerializedProperty srcDelay = srcEventOptions.FindPropertyRelative(nameof(CyanTriggerEventOptions.delay));
            dstDelay.floatValue = srcDelay.floatValue;

            SerializedProperty dstEventName = dstEvent.FindPropertyRelative(nameof(CyanTriggerEvent.name));
            SerializedProperty srcEventName = srcEvent.FindPropertyRelative(nameof(CyanTriggerEvent.name));
            dstEventName.stringValue = srcEventName.stringValue;
        }

        public static void CopyDataAndRemapVariables(
            CyanTriggerActionInfoHolder infoHolder,
            SerializedProperty srcProperty,
            SerializedProperty dstProperty, 
            Dictionary<string, string> variableGuidMap)
        {
            SerializedProperty srcInputsProperty =
                srcProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.inputs));
            SerializedProperty dstInputsProperty =
                dstProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.inputs));
            SerializedProperty srcMultiInputsProperty =
                srcProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.multiInput));
            SerializedProperty dstMultiInputsProperty =
                dstProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.multiInput));
            
            SerializedProperty dstExpanded = 
                dstProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.expanded));
            SerializedProperty srcExpanded = 
                srcProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.expanded));
            dstExpanded.boolValue = srcExpanded.boolValue;
            
            SerializedProperty dstComment = 
                dstProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.comment));
            SerializedProperty srcComment = 
                srcProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.comment));
            SerializedProperty dstCommentString = 
                dstComment.FindPropertyRelative(nameof(CyanTriggerComment.comment));
            SerializedProperty srcCommentString = 
                srcComment.FindPropertyRelative(nameof(CyanTriggerComment.comment));
            dstCommentString.stringValue = srcCommentString.stringValue;

            int startIndex = 0;
            int endIndex = srcInputsProperty.arraySize;
            
            // If defines custom variables, get old guid and add mapping
            if (infoHolder.customDefinition != null && infoHolder.customDefinition.DefinesCustomEditorVariableOptions())
            {
                // Ensure that variable data is not overwritten when copied
                if (infoHolder.customDefinition is CyanTriggerCustomNodeVariableProvider variableProvider)
                {
                    (startIndex, endIndex) = variableProvider.GetDefinitionVariableRange();
                    variableProvider.CopyVariableName(srcInputsProperty, dstInputsProperty);
                }
                
                var srcOptions = infoHolder.customDefinition.GetCustomEditorVariableOptions(srcInputsProperty);
                var dstOptions = infoHolder.customDefinition.GetCustomEditorVariableOptions(dstInputsProperty);

                if (srcOptions.Length != dstOptions.Length)
                {
                    // Try setting properties first and then pull again.
                    SetPropertyInputDefaults(
                        srcInputsProperty, 
                        dstInputsProperty, 
                        srcMultiInputsProperty,
                        dstMultiInputsProperty,
                        variableGuidMap, 
                        startIndex, 
                        endIndex);
                    
                    dstOptions = infoHolder.customDefinition.GetCustomEditorVariableOptions(dstInputsProperty);
                }
                
                Debug.Assert(srcOptions.Length == dstOptions.Length,
                    "Duplicated property has different custom variable option sizes! src: " + srcOptions.Length +
                    ", dst: " + dstOptions.Length);
                
                for (int cur = 0; cur < srcOptions.Length; ++cur)
                {
                    string srcGuid = srcOptions[cur].ID;
                    string dstGuid = dstOptions[cur].ID;
                    variableGuidMap.Add(srcGuid, dstGuid);
                }
            }

            // Copy all data from the source property to the destination.
            // Ensure that variable guids are remapped properly.
            SetPropertyInputDefaults(
                srcInputsProperty, 
                dstInputsProperty, 
                srcMultiInputsProperty,
                dstMultiInputsProperty,
                variableGuidMap, 
                startIndex, 
                endIndex);
        }

        public static void SetPropertyInputDefaults(
            SerializedProperty srcInputProperties,
            SerializedProperty dstInputProperties,
            SerializedProperty srcMultiInputProperties,
            SerializedProperty dstMultiInputProperties,
            Dictionary<string, string> variableGuidMap,
            int startIndex,
            int endIndex)
        {
            dstInputProperties.arraySize = srcInputProperties.arraySize;
            for (int cur = startIndex; cur < endIndex; ++cur)
            {
                SerializedProperty srcInputProperty = srcInputProperties.GetArrayElementAtIndex(cur);
                SerializedProperty dstInputProperty = dstInputProperties.GetArrayElementAtIndex(cur);
                CopyVariableInstanceProperty(srcInputProperty, dstInputProperty, variableGuidMap);
            }
            
            dstMultiInputProperties.arraySize = srcMultiInputProperties.arraySize;
            for (int cur = 0; cur < srcMultiInputProperties.arraySize; ++cur)
            {
                SerializedProperty srcInputProperty = srcMultiInputProperties.GetArrayElementAtIndex(cur);
                SerializedProperty dstInputProperty = dstMultiInputProperties.GetArrayElementAtIndex(cur);
                CopyVariableInstanceProperty(srcInputProperty, dstInputProperty, variableGuidMap);
            }
        }

        public static void CopyVariableInstanceProperty(
            SerializedProperty srcInputProperty, 
            SerializedProperty dstInputProperty,
            Dictionary<string, string> variableGuidMap)
        {
            SerializedProperty srcDataProperty =
                srcInputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
            SerializedProperty srcIsVariableProperty =
                srcInputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
            SerializedProperty srcGuidProperty =
                srcInputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID));
            SerializedProperty srcNameProperty =
                srcInputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.name));
            
            SerializedProperty dstDataProperty =
                dstInputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
            SerializedProperty dstIsVariableProperty =
                dstInputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
            SerializedProperty dstGuidProperty =
                dstInputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID));
            SerializedProperty dstNameProperty =
                dstInputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.name));

            dstIsVariableProperty.boolValue = srcIsVariableProperty.boolValue;
            dstNameProperty.stringValue = srcNameProperty.stringValue;
            
            CyanTriggerSerializableObject.CopySerializedProperty(srcDataProperty, dstDataProperty);
            
            // Remap the variable if it was duplicated
            string srcGuid = srcGuidProperty.stringValue;
            if (!variableGuidMap.TryGetValue(srcGuid, out var dstGuid))
            {
                dstGuid = srcGuid;
            }
            dstGuidProperty.stringValue = dstGuid;
        }
    }
}
