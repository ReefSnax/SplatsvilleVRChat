using System;
using VRC.Udon.Graph;

namespace CyanTrigger
{
    public class CyanTriggerCustomNodeCondition : CyanTriggerCustomNodeVariableProvider
    {
        public static readonly UdonNodeDefinition NodeDefinition = new UdonNodeDefinition(
            "Condition",
            "CyanTriggerSpecial_Condition",
            typeof(CyanTrigger),
            new UdonNodeParameter[0],
            new string[0],
            new string[0],
            new object[0],
            true
        );

        public override UdonNodeDefinition GetNodeDefinition()
        {
            return NodeDefinition;
        }

        protected override (string, Type)[] GetVariables()
        {
            return new[]
            {
                ("Condition Variable", typeof(bool))
            };
        }
        
        protected override bool ShowDefinedVariablesAtBeginning()
        {
            return true;
        }
        
        public override bool CreatesScope()
        {
            return true;
        }

        public override void AddActionToProgram(CyanTriggerCompileState compileState)
        {
            var actionMethod = compileState.ActionMethod;
            var program = compileState.Program;

            var scopeFrame = compileState.ScopeData.ScopeStack.Peek();
            scopeFrame.EndNop = CyanTriggerAssemblyInstruction.Nop();
            
            string variableGuid = GetVariableGuid(compileState.ActionInstance, 0);

            var constFalse = program.data.GetOrCreateVariableConstant(typeof(bool), false);
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.PushVariable(constFalse));
            var userVariable = program.data.GetUserDefinedVariable(variableGuid);
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.PushVariable(userVariable));
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.Copy());
        }
        
        public override void HandleEndScope(CyanTriggerCompileState compileState)
        {
            var actionInstance = compileState.ActionInstance;
            var actionMethod = compileState.ActionMethod;

            string guid = GetVariableGuid(actionInstance, 0);
            var userVariable = compileState.Program.data.GetUserDefinedVariable(guid);
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.PushVariable(userVariable));
            
            // Push jump point for Pass or Fail checks. 
            var scopeFrame = compileState.ScopeData.ScopeStack.Peek();
            actionMethod.AddAction(scopeFrame.EndNop);
        }
        
        public override bool HasDependencyNodes()
        {
            return true;
        }

        public override UdonNodeDefinition[] GetDependentNodes()
        {
            return new[] {CyanTriggerCustomNodeBlockEnd.NodeDefinition};
        }

        protected override string GetVariableName(CyanTriggerAssemblyProgram program, Type type)
        {
            return program.data.CreateVariableName("cond", type);
        }
    }
}