

using System;
using VRC.Udon.Graph;

namespace CyanTrigger
{
    public class CyanTriggerCustomNodeLoopFor : CyanTriggerCustomNodeVariableProvider, ICyanTriggerCustomNodeLoop
    {
        public static readonly UdonNodeDefinition NodeDefinition = new UdonNodeDefinition(
            "For",
            "CyanTriggerSpecial_For",
            typeof(CyanTrigger),
            new []
            {
                new UdonNodeParameter
                {
                    name = "start",
                    type = typeof(int),
                    parameterType = UdonNodeParameter.ParameterType.IN
                },
                new UdonNodeParameter
                {
                    name = "end",
                    type = typeof(int),
                    parameterType = UdonNodeParameter.ParameterType.IN
                },
                new UdonNodeParameter
                {
                name = "step",
                type = typeof(int),
                parameterType = UdonNodeParameter.ParameterType.IN
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
        
        public override bool CreatesScope()
        {
            return true;
        }

        public override void AddActionToProgram(CyanTriggerCompileState compileState)
        {
            var actionInstance = compileState.ActionInstance;
            var actionMethod = compileState.ActionMethod;
            var program = compileState.Program;
            var data = program.data;
            
            var scopeFrame = compileState.ScopeData.ScopeStack.Peek();
            scopeFrame.EndNop = CyanTriggerAssemblyInstruction.Nop();
            scopeFrame.StartNop = CyanTriggerAssemblyInstruction.Nop();
            var conditionStartNop = CyanTriggerAssemblyInstruction.Nop();
            
            var conditionEndNop = CyanTriggerAssemblyInstruction.Nop();
            var conditionNegativeNop = CyanTriggerAssemblyInstruction.Nop();

            Type intType = typeof(int);
            Type boolType = typeof(bool);
            var step = data.RequestTempVariable(intType);
            var end = data.RequestTempVariable(intType);
            var stepIsPositive = data.RequestTempVariable(boolType);
            var conditionBool = data.RequestTempVariable(boolType);
            
            var startInput = compileState.GetDataFromVariableInstance(-1, 0, actionInstance.inputs[0], intType, false);
            var endInput = compileState.GetDataFromVariableInstance(-1, 1, actionInstance.inputs[1], intType, false);
            var stepInput = compileState.GetDataFromVariableInstance(-1, 2, actionInstance.inputs[2], intType, false);

            if (!actionInstance.inputs[2].isVariable && ((int) actionInstance.inputs[2].data.obj) == 0)
            {
                compileState.LogError("For loop has step value of 0!");
            }
            if (actionInstance.inputs[2].isVariable && 
                string.IsNullOrEmpty(actionInstance.inputs[2].name) && 
                string.IsNullOrEmpty(actionInstance.inputs[2].variableID))
            {
                compileState.LogError("For loop has empty variable for step value!");
            }

            string variableGuid = GetVariableGuid(actionInstance, 0);
            var userVariable = program.data.GetUserDefinedVariable(variableGuid);

            var pushIndex = CyanTriggerAssemblyInstruction.PushVariable(userVariable);
            var pushStep = CyanTriggerAssemblyInstruction.PushVariable(step);
            var pushEnd = CyanTriggerAssemblyInstruction.PushVariable(end);
            var pushStepIsPositive = CyanTriggerAssemblyInstruction.PushVariable(stepIsPositive);
            var pushConditionBool = CyanTriggerAssemblyInstruction.PushVariable(conditionBool);

            var copyInstruction = CyanTriggerAssemblyInstruction.Copy();
            
            // Initialize the index to the start value
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.PushVariable(startInput));
            actionMethod.AddAction(pushIndex);
            actionMethod.AddAction(copyInstruction);
            
            // Copy end value
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.PushVariable(endInput));
            actionMethod.AddAction(pushEnd);
            actionMethod.AddAction(copyInstruction);
            
            // Copy step value
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.PushVariable(stepInput));
            actionMethod.AddAction(pushStep);
            actionMethod.AddAction(copyInstruction);
            
            // Check if step is positive. This will be used for comparing index with end.
            actionMethod.AddAction(pushStep);
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.PushVariable(data.GetOrCreateVariableConstant(intType, 0, false)));
            actionMethod.AddAction(pushStepIsPositive);
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.CreateExtern(
                CyanTriggerDefinitionResolver.GetPrimitiveOperationSignature(intType,
                    PrimitiveOperation.GreaterThanOrEqual)));

            // Jump to condition start
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.Jump(conditionStartNop));
            
            // Start of for loop. Update value and check condition again.
            compileState.ActionMethod.AddAction(scopeFrame.StartNop);
            
            // Update index
            actionMethod.AddAction(pushIndex);
            actionMethod.AddAction(pushStep);
            actionMethod.AddAction(pushIndex);
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.CreateExtern(
                CyanTriggerDefinitionResolver.GetPrimitiveOperationSignature(intType,
                    PrimitiveOperation.Addition)));
            
            // Check if the condition is valid
            compileState.ActionMethod.AddAction(conditionStartNop);
            
            // push comparison variables early
            actionMethod.AddAction(pushIndex);
            actionMethod.AddAction(pushEnd);
            actionMethod.AddAction(pushConditionBool);
            
            // Jump to negative compare if not positive
            actionMethod.AddAction(pushStepIsPositive);
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.JumpIfFalse(conditionNegativeNop));
            
            // Step is positive, check if index is still than end
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.CreateExtern(
                CyanTriggerDefinitionResolver.GetPrimitiveOperationSignature(intType,
                    PrimitiveOperation.LessThan)));

            actionMethod.AddAction(CyanTriggerAssemblyInstruction.Jump(conditionEndNop));
            actionMethod.AddAction(conditionNegativeNop);
            
            // Step is negative, check if index is still greater than end
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.CreateExtern(
                CyanTriggerDefinitionResolver.GetPrimitiveOperationSignature(intType,
                    PrimitiveOperation.GreaterThan)));
            
            actionMethod.AddAction(conditionEndNop);
            
            // Push condition variable and jump to end if false
            actionMethod.AddAction(pushConditionBool);
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.JumpIfFalse(scopeFrame.EndNop));
        }
        
        public override void HandleEndScope(CyanTriggerCompileState compileState)
        {
            var actionMethod = compileState.ActionMethod;
            
            var scopeFrame = compileState.ScopeData.ScopeStack.Peek();
            var jumpToNop = CyanTriggerAssemblyInstruction.Jump(scopeFrame.StartNop);
            
            actionMethod.AddAction(jumpToNop);
            actionMethod.AddAction(scopeFrame.EndNop);
        }

        protected override (string, Type)[] GetVariables()
        {
            return new [] { ("index", typeof(int)) };
        }

        protected override bool ShowDefinedVariablesAtBeginning()
        {
            return true;
        }
        
        protected override string GetVariableName(CyanTriggerAssemblyProgram program, Type type)
        {
            return program.data.CreateVariableName("for_index", type);
        }
        
        public override bool HasDependencyNodes()
        {
            return true;
        }

        public override UdonNodeDefinition[] GetDependentNodes()
        {
            return new[]
            {
                CyanTriggerCustomNodeBlockEnd.NodeDefinition
            };
        }
    }
}
