using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRC.Udon.Graph;

namespace CyanTrigger
{
    public class CyanTriggerCustomNodeElse : CyanTriggerCustomUdonActionNodeDefinition
    {
        public static readonly UdonNodeDefinition NodeDefinition = new UdonNodeDefinition(
            "Else",
            "CyanTriggerSpecial_Else",
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

            // Verify previous node is If or Else_If
            if (!(scopeData.PreviousScopeDefinition is CyanTriggerCustomNodeIf) && 
                !(scopeData.PreviousScopeDefinition is CyanTriggerCustomNodeElseIf))
            {
                throw new Exception("Condition body did not come after a Condition! " + scopeData.PreviousScopeDefinition);
            }

            var endNop = CyanTriggerAssemblyInstruction.Nop();
            scopeData.ScopeStack.Peek().EndNop = endNop;
            
            var lastAction = actionMethod.actions[actionMethod.actions.Count - 1];
            if (lastAction.GetInstructionType() != CyanTriggerInstructionType.NOP)
            {
                throw new Exception("Else expected last instruction to be of type variable Nop! " + lastAction.GetInstructionType());
            }

            var jumpToNop = CyanTriggerAssemblyInstruction.Jump(endNop);
            actionMethod.actions.Insert(actionMethod.actions.Count - 1, jumpToNop);
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
            return new[]
            {
                CyanTriggerCustomNodeBlockEnd.NodeDefinition
            };
        }
    }
}