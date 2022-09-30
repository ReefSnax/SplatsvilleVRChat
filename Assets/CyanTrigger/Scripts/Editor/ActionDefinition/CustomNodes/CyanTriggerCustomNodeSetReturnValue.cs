using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRC.Udon.Graph;

namespace CyanTrigger
{
    public class CyanTriggerCustomNodeSetReturnValue : CyanTriggerCustomUdonActionNodeDefinition
    {
        public static readonly UdonNodeDefinition NodeDefinition = new UdonNodeDefinition(
            "SetReturnValue",
            "CyanTriggerSpecial_SetReturnValue",
            typeof(CyanTrigger),
            new []
            {
                new UdonNodeParameter
                {
                    name = "value",
                    type = typeof(object),
                    parameterType = UdonNodeParameter.ParameterType.IN
                },
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
                type = new CyanTriggerSerializableType(typeof(object)),
                udonName = "value",
                displayName = "return value", 
                description = "Set the event return value to this object",
                variableType = CyanTriggerActionVariableTypeDefinition.VariableInput
            },
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
        
        public override void AddActionToProgram(CyanTriggerCompileState compileState)
        {
            var returnVariable = compileState.Program.data.GetSpecialVariable(
                CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.ReturnValue);

            var variable = compileState.ActionInstance.inputs[0];
            var valueObject = compileState.GetDataFromVariableInstance(-1, 0, variable, typeof(object), false);
            
            compileState.ActionMethod.AddActions(
                CyanTriggerAssemblyActionsUtils.CopyVariables(valueObject,returnVariable));
        }
    }
}