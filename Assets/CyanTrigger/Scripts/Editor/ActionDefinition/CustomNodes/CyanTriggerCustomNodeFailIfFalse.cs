using System;
using VRC.Udon.Graph;

namespace CyanTrigger
{
    public class CyanTriggerCustomNodeFailIfFalse : CyanTriggerCustomUdonActionNodeDefinition
    {
        public static readonly UdonNodeDefinition NodeDefinition = new UdonNodeDefinition(
            "ConditionFailIfFalse",
            "CyanTriggerSpecial_ConditionFailIfFalse",
            typeof(CyanTrigger),
            new []
            {
                new UdonNodeParameter
                {
                    name = "bool",
                    type = typeof(bool),
                    parameterType = UdonNodeParameter.ParameterType.IN,
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
                type = new CyanTriggerSerializableType(typeof(bool)),
                udonName = "bool",
                displayName = "Should fail", 
                description = "If the input provided is false, then the entire condition will fail, skipping the rest of the actions in the Condition and skipping the ConditionBody.",
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
            var actionInstance = compileState.ActionInstance;
            var actionMethod = compileState.ActionMethod;
            var program = compileState.Program;
            var scopeData = compileState.ScopeData;

            foreach (var scopeFrame in scopeData.ScopeStack)
            {
                if (scopeFrame.Definition is CyanTriggerCustomNodeCondition)
                {
                    var variable = actionInstance.inputs[0];
                    
                    // Push constant false for if the jump is successful.
                    actionMethod.AddAction(CyanTriggerAssemblyInstruction.PushVariable(
                        program.data.GetOrCreateVariableConstant(typeof(bool), false)));
                    
                    // Check if the value was false and we should jump to the end of the condition
                    actionMethod.AddAction(CyanTriggerAssemblyInstruction.PushVariable(
                        compileState.GetDataFromVariableInstance(-1, 0, variable, typeof(bool), false)));
                    actionMethod.AddAction(CyanTriggerAssemblyInstruction.JumpIfFalse(scopeFrame.EndNop));
                    
                    // Pop off the constant false since we did not jump.
                    actionMethod.AddAction(CyanTriggerAssemblyInstruction.Pop());
                    return;
                }
            }
            
            throw new Exception("FailIfFalse statement not included in a condition!");
        }
    }
}
