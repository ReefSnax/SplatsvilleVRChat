using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRC.Udon.Graph;

namespace CyanTrigger
{
    public class CyanTriggerCustomNodeContinue : CyanTriggerCustomUdonActionNodeDefinition
    {
        public static readonly UdonNodeDefinition NodeDefinition = new UdonNodeDefinition(
            "Continue",
            "CyanTriggerSpecial_Continue",
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

        public override void AddActionToProgram(CyanTriggerCompileState compileState)
        {
            foreach (var scopeFrame in compileState.ScopeData.ScopeStack)
            {
                if (scopeFrame.IsLoop)
                {
                    compileState.ActionMethod.AddAction(CyanTriggerAssemblyInstruction.Jump(scopeFrame.StartNop));
                    return;
                }
            }

            throw new Exception("Continue statement not included in a loop!");
        }
    }
}
