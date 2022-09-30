using System;
using System.Collections.Generic;
using UnityEditor;
using VRC.Udon.Compiler.Compilers;
using VRC.Udon.Graph;

namespace CyanTrigger
{
    public class CyanTriggerCustomNodeOnVariableChanged : CyanTriggerCustomUdonEventNodeDefinition
    {
        public static string OnVariableChangedEventName = "Event_OnVariableChanged";

        private static string OldVariableDisplayName = "oldValue";

        public static UdonNodeDefinition NodeDefinition = new UdonNodeDefinition(
            "OnVariableChanged",
            OnVariableChangedEventName,
            typeof(void),
            new[]
            {
                new UdonNodeParameter()
                {
                    name = "variable",
                    type = typeof(CyanTriggerVariable),
                    parameterType = UdonNodeParameter.ParameterType.IN
                },
                new UdonNodeParameter()
                {
                    name = OldVariableDisplayName,
                    type = typeof(object),
                    parameterType = UdonNodeParameter.ParameterType.OUT
                }
            },
            new string[0],
            new string[0],
            new object[0],
            true
        );

        public override UdonNodeDefinition GetNodeDefinition()
        {
            return NodeDefinition;
        }
        
        public override bool DefinesCustomEditorVariableOptions()
        {
            return true;
        }
        
        public override CyanTriggerEditorVariableOption[] GetCustomEditorVariableOptions(
            SerializedProperty variableProperties)
        {
            if (variableProperties.arraySize == 0)
            {
                return new CyanTriggerEditorVariableOption[0];
            }

            CyanTriggerEditorVariableOption ret = 
                GetPrevVariableOptionFromData(variableProperties.GetArrayElementAtIndex(0));

            if (ret == null)
            {
                return new CyanTriggerEditorVariableOption[0];
            }

            return new[]
            {
                ret
            };
        }

        public override bool GetBaseMethod(
            CyanTriggerAssemblyProgram program,
            CyanTriggerActionInstance actionInstance,
            out CyanTriggerAssemblyMethod method)
        {
            var variable = program.data.GetUserDefinedVariable(actionInstance.inputs[0].variableID);
            string methodName = GetVariableChangeEventName(variable.name);
            bool created = program.code.GetOrCreateMethod(methodName, true, out method);
            if (created)
            {
                method.AddEndAction(
                    CyanTriggerAssemblyActionsUtils.CopyVariables(variable, variable.previousVariable));
            }
            return created;
        }

        public override void AddEventToProgram(CyanTriggerCompileState compileState)
        {
            AddDefaultEventToProgram(compileState.Program, compileState.EventMethod, compileState.ActionMethod);
        }
        
        public static HashSet<string> GetVariablesWithOnChangedCallback(CyanTriggerEvent[] events, ref bool allValid)
        {
            allValid = true;
            HashSet<string> variablesWithCallbacks = new HashSet<string>();
            foreach (var trigEvent in events)
            {
                var eventInstance = trigEvent.eventInstance;
                if (eventInstance.actionType.directEvent == OnVariableChangedEventName)
                {
                    string varId = eventInstance.inputs[0].variableID;
                    if (string.IsNullOrEmpty(varId))
                    {
                        allValid = false;
                    }
                    variablesWithCallbacks.Add(varId);
                }
            }

            return variablesWithCallbacks;
        }

        public static string GetOldVariableName(string varName)
        {
            return UdonGraphCompiler.GetOldVariableName(varName);
        }

        public static string GetVariableChangeEventName(string varName)
        {
            return UdonGraphCompiler.GetVariableChangeEventName(varName);
        }

        
        // TODO fix all of this extra data, as it is way too hacky...
        public static void SetVariableExtraData(SerializedProperty eventInstance, CyanTriggerVariable[] variables)
        {
            SerializedProperty eventInputs =
                eventInstance.FindPropertyRelative(nameof(CyanTriggerActionInstance.inputs));
            SerializedProperty variableInstance = eventInputs.GetArrayElementAtIndex(0);

            if (variableInstance == null)
            {
                return;
            }

            SerializedProperty dataProperty =
                variableInstance.FindPropertyRelative(nameof(CyanTriggerVariable.data));
            
            string varName = 
                variableInstance.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.name)).stringValue;
            
            string varId = 
                variableInstance.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID)).stringValue;
            
            string[] data = GetDataForVariable(varName, varId, variables);
            CyanTriggerSerializableObject.UpdateSerializedProperty(dataProperty, data);
        }

        private static string[] GetDataForVariable(string varName, string varId, CyanTriggerVariable[] variables)
        {
            string[] data = null;
            foreach (var variable in variables)
            {
                if (variable.name == varName)
                {
                    string prevVar = GetOldVariableName(varName);
                    data = new[] {prevVar, variable.type.typeDef, varId};
                    break;
                }
            }

            return data;
        }

        public static CyanTriggerEditorVariableOption GetPrevVariableOptionFromData(SerializedProperty variableInstance)
        {
            SerializedProperty dataProperty =
                variableInstance.FindPropertyRelative(nameof(CyanTriggerVariable.data));

            string[] data = (string[]) CyanTriggerSerializableObject.ObjectFromSerializedProperty(dataProperty);

            if (data == null)
            {
                return null;
            }

            Type varType = Type.GetType(data[1]);
            string oldVarName = data[0];
            string actualId = data[2];

            string guid = GetPrevVariableGuid(oldVarName, actualId);
            
            return new CyanTriggerEditorVariableOption
            {
                Name = OldVariableDisplayName,
                ID = guid,
                Type = varType,
                IsReadOnly = true,
            };
        }

        private const string IsPrevVariableTag = "IsPrev";
        public static string GetPrevVariableGuid(string oldVarName, string actualId)
        {
            string guid = CyanTriggerAssemblyDataGuidTags.AddVariableNameTag(oldVarName);
            guid = CyanTriggerAssemblyDataGuidTags.AddVariableIdTag(actualId, guid);
            guid = CyanTriggerAssemblyDataGuidTags.AddVariableGuidTag(IsPrevVariableTag, "true", guid);
            return guid;
        }
        
        public static bool IsPrevVariable(string name, string guid)
        {
            return name == OldVariableDisplayName && 
                   !string.IsNullOrEmpty(CyanTriggerAssemblyDataGuidTags.GetVariableGuidTag(guid, IsPrevVariableTag));
        }

        public static string GetMainVariableId(string guid)
        {
            return CyanTriggerAssemblyDataGuidTags.GetVariableId(guid);
        }

        public static void MigrateEvent(CyanTriggerActionInstance eventAction, CyanTriggerVariable[] variables)
        {
            if (eventAction.actionType.directEvent != OnVariableChangedEventName)
            {
                return;
            }

            if (eventAction.inputs.Length == 0)
            {
                return;
            }

            var input = eventAction.inputs[0];
            string[] data = GetDataForVariable(input.name, input.variableID, variables);
            eventAction.inputs[0].data = new CyanTriggerSerializableObject(data);
        }
    }
}
