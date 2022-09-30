using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRC.Udon.Graph;

namespace CyanTrigger
{
    public class CyanTriggerCustomNodeElseIf : CyanTriggerCustomNodeElse
    {
        public new static readonly UdonNodeDefinition NodeDefinition = new UdonNodeDefinition(
            "Else If",
            "CyanTriggerSpecial_ElseIf",
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
        
        // All logic is handled in the else code
        public override void HandleEndScope(CyanTriggerCompileState compileState)
        {
            var actionMethod = compileState.ActionMethod;

            var scopeFrame = compileState.ScopeData.ScopeStack.Peek();
            actionMethod.actions.Insert(actionMethod.actions.Count - 1, scopeFrame.EndNop);
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
