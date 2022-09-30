using System;
using VRC.Udon.Graph;

namespace CyanTrigger
{
    public class CyanTriggerCustomNodeWhile : CyanTriggerCustomUdonActionNodeDefinition, ICyanTriggerCustomNodeLoop
    {
        public static readonly UdonNodeDefinition NodeDefinition = new UdonNodeDefinition(
            "While",
            "CyanTriggerSpecial_While",
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
        
        public override bool CreatesScope()
        {
            return true;
        }

        public override void AddActionToProgram(CyanTriggerCompileState compileState)
        {
            var scopeFrame = compileState.ScopeData.ScopeStack.Peek();
            scopeFrame.EndNop = CyanTriggerAssemblyInstruction.Nop();
            scopeFrame.StartNop = CyanTriggerAssemblyInstruction.Nop();
            
            compileState.ActionMethod.AddAction(scopeFrame.StartNop);
        }
        
        public override void HandleEndScope(CyanTriggerCompileState compileState)
        {
            var actionMethod = compileState.ActionMethod;

            var lastAction = actionMethod.actions[actionMethod.actions.Count - 1];
            if (lastAction.GetInstructionType() != CyanTriggerInstructionType.NOP)
            {
                throw new Exception("While expected last instruction to be of type variable Nop! " + lastAction.GetInstructionType());
            }

            var scopeFrame = compileState.ScopeData.ScopeStack.Peek();
            var jumpToNop = CyanTriggerAssemblyInstruction.Jump(scopeFrame.StartNop);
            actionMethod.actions.Insert(actionMethod.actions.Count - 1, jumpToNop);
            
            actionMethod.AddAction(scopeFrame.EndNop);
        }
        
        public override bool HasDependencyNodes()
        {
            return true;
        }

        public override UdonNodeDefinition[] GetDependentNodes()
        {
            return new[]
            {
                CyanTriggerCustomNodeCondition.NodeDefinition,
                CyanTriggerCustomNodeConditionBody.NodeDefinition,
                CyanTriggerCustomNodeBlockEnd.NodeDefinition
            };
        }
    }
}
