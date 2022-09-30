using System;
using UnityEngine;
using VRC.Udon.Graph;

namespace CyanTrigger
{
    public class CyanTriggerCustomNodeConditionBody : CyanTriggerCustomUdonActionNodeDefinition
    {
        public static readonly UdonNodeDefinition NodeDefinition = new UdonNodeDefinition(
            "ConditionBody",
            "CyanTriggerSpecial_ConditionBody",
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
            var actionMethod = compileState.ActionMethod;
            var scopeData = compileState.ScopeData;

            var scopeFrame = scopeData.ScopeStack.Peek();
            scopeFrame.EndNop = CyanTriggerAssemblyInstruction.Nop();
            
            // Verify previous node is Condition
            if (!(scopeData.PreviousScopeDefinition is CyanTriggerCustomNodeCondition))
            {
                throw new Exception("Condition body did not come after a Condition! " + scopeData.PreviousScopeDefinition);
            }

            var actions = actionMethod.actions;
            if (actions.Count < 2)
            {
                throw new Exception("Condition body expected at least two instructions.");
            }

            var pushAction = actions[actions.Count - 2];
            var nopAction = actions[actions.Count - 1];
            
            if (pushAction.GetInstructionType() != CyanTriggerInstructionType.PUSH)
            {
                throw new Exception("Condition body expected last instruction to be of type variable push! " + pushAction.GetInstructionType());
            }
            
            if (nopAction.GetInstructionType() != CyanTriggerInstructionType.NOP)
            {
                throw new Exception("Condition body expected last instruction to be of type variable Nop! " + nopAction.GetInstructionType());
            }
            
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.JumpIfFalse(scopeFrame.EndNop));
        }
        
        public override void HandleEndScope(CyanTriggerCompileState compileState)
        {
            var scopeFrame = compileState.ScopeData.ScopeStack.Peek();
            compileState.ActionMethod.AddAction(scopeFrame.EndNop);
        }
        
        public override bool HasDependencyNodes()
        {
            return true;
        }

        public override UdonNodeDefinition[] GetDependentNodes()
        {
            return new[] {CyanTriggerCustomNodeBlockEnd.NodeDefinition};
        }
    }
}