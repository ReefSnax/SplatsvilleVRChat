using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;

namespace CyanTrigger
{
    [Serializable]
    public class CyanTriggerActionDataReferenceIndex
    {
        public string symbolName;
        public Type symbolType;
        public int eventIndex;
        public int actionIndex;
        public int variableIndex;
        public int multiVariableIndex;

        public override string ToString()
        {
            return $"Symbol: {symbolType} {symbolName} event[{eventIndex}].action[{actionIndex}].var[{multiVariableIndex}, {variableIndex}]";
        }
    }
    
    [Serializable]
    public class CyanTriggerDataReferences
    {
        public readonly List<CyanTriggerActionDataReferenceIndex> ActionDataIndices =
            new List<CyanTriggerActionDataReferenceIndex>();

        public string animatorSymbol;
        public readonly Dictionary<string, Type> userVariables = new Dictionary<string, Type>();
        
        public void ApplyPublicVariableData(
            CyanTriggerDataInstance cyanTriggerDataInstance,
            UdonBehaviour udonBehaviour,
            IUdonSymbolTable symbolTable,
            ref bool dirty) 
        {
            IUdonVariableTable publicVariables = udonBehaviour.publicVariables;
            if (publicVariables == null)
            {
                Debug.LogError("Cannot set public variables when VariableTable is null");
                return;
            }
            
            // Remove non-exported public variables
            foreach(string publicVariableSymbol in new List<string>(publicVariables.VariableSymbols))
            {
                // Symbol table doesn't have the variable name 
                // The type for the symbol doesn't match the type currently in the public variable table.
                if(!symbolTable.HasExportedSymbol(publicVariableSymbol) || 
                   (publicVariables.TryGetVariableType(publicVariableSymbol, out var type) && 
                    type != symbolTable.GetSymbolType(publicVariableSymbol)))
                {
                    //Debug.Log("Removing Reference: " + publicVariableSymbol);
                    publicVariables.RemoveVariable(publicVariableSymbol);
                }
            }
            

            HashSet<string> usedVariables = new HashSet<string>();
            usedVariables.Add(CyanTriggerAssemblyData.ThisGameObjectGUID);
            usedVariables.Add(CyanTriggerAssemblyData.ThisTransformGUID);
            usedVariables.Add(CyanTriggerAssemblyData.ThisUdonBehaviourGUID);

            SetUdonVariable(
                udonBehaviour, 
                publicVariables, 
                CyanTriggerAssemblyData.ThisGameObjectGUID, 
                typeof(GameObject), 
                udonBehaviour.gameObject,
                ref dirty);
            
            SetUdonVariable(
                udonBehaviour, 
                publicVariables, 
                CyanTriggerAssemblyData.ThisTransformGUID, 
                typeof(Transform), 
                udonBehaviour.transform,
                ref dirty);
            
            SetUdonVariable(
                udonBehaviour, 
                publicVariables, 
                CyanTriggerAssemblyData.ThisUdonBehaviourGUID, 
                typeof(IUdonEventReceiver), 
                udonBehaviour,
                ref dirty);

            // TODO figure out a more generic way to handle data like animator move and timerqueue
            
            // Add TimerQueue if in the code
            string timerQueueSymbolName =
                CyanTriggerAssemblyData.GetSpecialVariableName(CyanTriggerAssemblyData.CyanTriggerSpecialVariableName
                    .TimerQueue);
            if (symbolTable.HasExportedSymbol(timerQueueSymbolName))
            {
                usedVariables.Add(timerQueueSymbolName);
                SetUdonVariable(
                    udonBehaviour, 
                    publicVariables, 
                    timerQueueSymbolName, 
                    typeof(IUdonEventReceiver), 
                    CyanTriggerResourceManager.CyanTriggerResources.timerQueueUdonBehaviour,
                    ref dirty);
            }
            
            if (!string.IsNullOrEmpty(animatorSymbol))
            {
                usedVariables.Add(animatorSymbol);
                
                SetUdonVariable(
                    udonBehaviour, 
                    publicVariables, 
                    animatorSymbol, 
                    typeof(Animator), 
                    udonBehaviour.GetComponent<Animator>(),
                    ref dirty);
            }
            
            foreach (var variable in cyanTriggerDataInstance.variables)
            {
                // Check if variable was valid based on last compile
                if (!userVariables.TryGetValue(variable.name, out var type) || type != variable.type.type)
                {
                    continue;   
                }
                
                usedVariables.Add(variable.name);

                object value = variable.data.obj;
                value = VerifyVariableData(type, value, () => $"Global variable \"{variable.name}\" contains invalid data for type {type.Name}. Data: {value}. Replacing with default value. Please check the CyanTrigger on object {VRC.Tools.GetGameObjectPath(udonBehaviour.gameObject)}");
                
                SetUdonVariable(
                    udonBehaviour, 
                    publicVariables, 
                    variable.name,
                    type, 
                    value,
                    ref dirty);

                // Variable had a callback. Ensure that previous value is equal to default value.
                string prevVarName = CyanTriggerCustomNodeOnVariableChanged.GetOldVariableName(variable.name);
                if (symbolTable.HasExportedSymbol(prevVarName))
                {
                    usedVariables.Add(prevVarName);
                    SetUdonVariable(
                        udonBehaviour, 
                        publicVariables, 
                        prevVarName,
                        type, 
                        value,
                        ref dirty);
                }
            }
            
            foreach (var publicVar in ActionDataIndices)
            {
                object data = null;
                Type type = publicVar.symbolType;

                var eventInstance = cyanTriggerDataInstance
                    .events[publicVar.eventIndex];
                CyanTriggerActionInstance actionInstance;

                string message = $"Event[{publicVar.eventIndex}]";
                
                if (publicVar.actionIndex < 0)
                {
                    // TODO figure out event organization here
                    actionInstance = eventInstance.eventInstance;
                }
                else
                {
                    actionInstance = eventInstance.actionInstances[publicVar.actionIndex];
                    message += $".Action[{publicVar.actionIndex}]";
                }

                // TODO figure out a way to get modified data from custom udon node definitions.
                if (actionInstance != null)
                {
                    CyanTriggerActionVariableInstance variableInstance;
                    if (publicVar.multiVariableIndex != -1)
                    {
                        variableInstance = actionInstance.multiInput[publicVar.multiVariableIndex];
                        message += $".Input[0][{publicVar.multiVariableIndex}]";
                    }
                    else
                    {
                        variableInstance = actionInstance.inputs[publicVar.variableIndex];
                        message += $".Input[{publicVar.variableIndex}]";
                    }

                    data = variableInstance.data.obj;
                }
                
                // TODO fix this. This is too hacky.
                if (type == typeof(CyanTrigger))
                {
                    type = typeof(IUdonEventReceiver);
                    if (data is CyanTrigger trigger)
                    {
                        data = trigger.triggerInstance.udonBehaviour;
                    }
                }

                // TODO find a better way here...
                if (publicVar.variableIndex == 1 && 
                    actionInstance.actionType.directEvent == CyanTriggerCustomNodeSetComponentActive.FullName )
                {
                    string varType = data as string;
                    
                    if (CyanTriggerNodeDefinitionManager.TryGetComponentType(varType, out var componentType))
                    {
                        data = componentType.AssemblyQualifiedName;
                    }
                    else
                    {
                        Debug.Log("Could not find type for SetComponentActive: " + varType);
                    }
                }
                
#if CYAN_TRIGGER_DEBUG
                Type expectedType = symbolTable.GetSymbolType(publicVar.symbolName);
                if (expectedType != type)
                {
                    Debug.LogWarning("Type for symbol does not match public variable type. " + expectedType +", " + type);
                }
#endif
                
                usedVariables.Add(publicVar.symbolName);
                
                data = VerifyVariableData(type, data, () => $"{message} contains invalid data for type {type.Name}. Data: {data}. Replacing with default value. Please check the CyanTrigger on object {VRC.Tools.GetGameObjectPath(udonBehaviour.gameObject)}");

                SetUdonVariable(
                    udonBehaviour, 
                    publicVariables, 
                    publicVar.symbolName, 
                    type, 
                    data,
                    ref dirty);
            }
            
#if CYAN_TRIGGER_DEBUG
            // Used for debug purposes to see if a public variable was missed.
            foreach (string publicVariableSymbol in new List<string>(publicVariables.VariableSymbols))
            {
                if(!usedVariables.Contains(publicVariableSymbol))
                {
                    Debug.LogWarning("[Internal] Variable was unused: " + publicVariableSymbol);
                }
            }
#endif
        }

        private static object VerifyVariableData(
            Type symbolType,
            object value,
            Func<string> getMessage)
        {
            bool badData = false;
            object other = CyanTriggerPropertyEditor.CreateInitialValueForType(symbolType, value, ref badData);
            if (badData)
            {
                Debug.LogError(getMessage());
                value = other;
            }

            return value;
        }

        private static void SetUdonVariable(
            UdonBehaviour udonBehaviour, 
            IUdonVariableTable publicVariables, 
            string exportedSymbol, 
            Type symbolType, 
            object value, 
            ref bool dirty)
        {
            value = VerifyVariableData(symbolType, value, () => $"Variable \"{exportedSymbol}\" contains invalid data for type {symbolType.Name}. Data: {value}. Replacing with default value. Please check the CyanTrigger on object {VRC.Tools.GetGameObjectPath(udonBehaviour.gameObject)}");
            
            bool hasVariable = publicVariables.TryGetVariableValue(exportedSymbol, out object variableValue);

            if (value == null || (value is UnityEngine.Object unityValue && unityValue == null))
            {
                if (hasVariable && variableValue != null)
                {
                    dirty = true;
                    //Debug.Log(exportedSymbol +" was changed! " + variableValue +" to " +value);

                    //Debug.Log("Setting object dirty after removing variable: " + VRC.Tools.GetGameObjectPath(udonBehaviour.gameObject) +" " +exportedSymbol);
                    EditorUtility.SetDirty(udonBehaviour);
 
                    Undo.RecordObject(udonBehaviour, "Modify Public Variable");

                    publicVariables.RemoveVariable(exportedSymbol);

                    EditorSceneManager.MarkSceneDirty(udonBehaviour.gameObject.scene);

                    if (PrefabUtility.IsPartOfPrefabInstance(udonBehaviour))
                    {
                        PrefabUtility.RecordPrefabInstancePropertyModifications(udonBehaviour);
                    }
                }
                
                return;
            }
            
            if (!hasVariable || !value.Equals(variableValue))
            {
                dirty = true;
                //Debug.Log(exportedSymbol +" was changed! " + variableValue +" to " +value);

                //Debug.Log("Setting object dirty after updating variable: " + VRC.Tools.GetGameObjectPath(udonBehaviour.gameObject));
                EditorUtility.SetDirty(udonBehaviour);
 
                Undo.RecordObject(udonBehaviour, "Modify Public Variable");

                if (!publicVariables.TrySetVariableValue(exportedSymbol, value))
                {
                    if (!publicVariables.TryAddVariable(CreateUdonVariable(exportedSymbol, value, symbolType)))
                    {
                        Debug.LogError($"Failed to set public variable '{exportedSymbol}' value.");
                    }
                }

                EditorSceneManager.MarkSceneDirty(udonBehaviour.gameObject.scene);

                if (PrefabUtility.IsPartOfPrefabInstance(udonBehaviour))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(udonBehaviour);
                }
            }
        }
        
        public static IUdonVariable CreateUdonVariable(string symbolName, object value, Type declaredType)
        {
            try
            {
                Type udonVariableType = typeof(UdonVariable<>).MakeGenericType(declaredType);
                return (IUdonVariable) Activator.CreateInstance(udonVariableType, symbolName, value);
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to create UdonVariable for symbol: " + symbolName +", type: " +declaredType +", object: " +value);
                Debug.LogError(e);
                throw e;
            }
        }
    }
}
