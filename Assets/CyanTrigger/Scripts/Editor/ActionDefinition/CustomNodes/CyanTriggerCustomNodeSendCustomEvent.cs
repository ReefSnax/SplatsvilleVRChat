using UnityEditor;
using VRC.Udon.Graph;

namespace CyanTrigger
{
    public class CyanTriggerCustomNodeSendCustomEvent : CyanTriggerCustomUdonActionNodeDefinition
    {
        public static readonly UdonNodeDefinition NodeDefinition = new UdonNodeDefinition(
            "SendCustomEvent",
            "CyanTrigger.__SendCustomEvent__CyanTrigger__SystemString",
            typeof(CyanTrigger),
            new[]
            {
                new UdonNodeParameter()
                {
                    name = "instance", 
                    type = typeof(CyanTrigger),
                    parameterType = UdonNodeParameter.ParameterType.IN
                },
                new UdonNodeParameter()
                {
                    name = "name", 
                    type = typeof(string),
                    parameterType = UdonNodeParameter.ParameterType.IN
                }
            },
            new string[0],
            new string[0],
            new object[0],
            true
        );
        
        public static readonly CyanTriggerActionVariableDefinition[] VariableDefinitions =
        {
            new CyanTriggerActionVariableDefinition
            {
                type = new CyanTriggerSerializableType(typeof(CyanTrigger)),
                udonName = "instance",
                displayName = "CyanTrigger", 
                variableType = CyanTriggerActionVariableTypeDefinition.Constant |
                               CyanTriggerActionVariableTypeDefinition.VariableInput |
                               CyanTriggerActionVariableTypeDefinition.AllowsMultiple
            },
            new CyanTriggerActionVariableDefinition
            {
                type = new CyanTriggerSerializableType(typeof(string)),
                udonName = "name",
                displayName = "Custom Name", 
                variableType = CyanTriggerActionVariableTypeDefinition.Constant
            }
        };
        
        public override UdonNodeDefinition GetNodeDefinition()
        {
            return NodeDefinition;
        }
        
        public override bool HasCustomVariableSettings()
        {
            return true;
        }
        
        public override CyanTriggerActionVariableDefinition[] GetCustomVariableSettings()
        {
            return VariableDefinitions;
        }
        
        public override bool HasCustomVariableInitialization()
        {
            return true;
        }
        
        public override void InitializeVariableProperties(
            SerializedProperty inputsProperty, 
            SerializedProperty multiInputsProperty)
        {
            multiInputsProperty.arraySize = 1;
            SerializedProperty inputNameProperty = multiInputsProperty.GetArrayElementAtIndex(0);
            SerializedProperty nameProperty =
                inputNameProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.name));
            nameProperty.stringValue = CyanTriggerAssemblyData.ThisCyanTriggerName;
            SerializedProperty guidProperty =
                inputNameProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID));
            guidProperty.stringValue = CyanTriggerAssemblyData.ThisCyanTriggerGUID;
            SerializedProperty isVariableProperty =
                inputNameProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
            isVariableProperty.boolValue = true;
                
            multiInputsProperty.serializedObject.ApplyModifiedProperties();
        }

        public override void AddActionToProgram(CyanTriggerCompileState compileState)
        {
            var actionInstance = compileState.ActionInstance;
            var actionMethod = compileState.ActionMethod;
            var program = compileState.Program;
            
            string eventName = actionInstance.inputs[1].data?.obj as string;
            if (string.IsNullOrEmpty(eventName))
            {
                compileState.LogError("CyanTrigger.SendCustomEvent cannot have an empty event!");
                return;
            }
            
            var eventNameVariable =
                compileState.GetDataFromVariableInstance(-1, 1, actionInstance.inputs[1], typeof(string), false);
            
            for (int curMulti = 0; curMulti < actionInstance.multiInput.Length; ++curMulti)
            {
                var variable = actionInstance.multiInput[curMulti];

                // Jump to self. Optimize and jump directly to the method
                if (variable.isVariable && variable.variableID == CyanTriggerAssemblyData.ThisCyanTriggerGUID)
                {
                    actionMethod.AddActions(CyanTriggerAssemblyActionsUtils.JumpToFunction(
                        program, 
                        eventName));
                    continue;
                }
                
                actionMethod.AddActions(CyanTriggerAssemblyActionsUtils.SendCustomEvent(
                    program,
                    compileState.GetDataFromVariableInstance(curMulti, 0, variable, typeof(CyanTrigger), false),
                    eventNameVariable));
            }
        }
    }
}

