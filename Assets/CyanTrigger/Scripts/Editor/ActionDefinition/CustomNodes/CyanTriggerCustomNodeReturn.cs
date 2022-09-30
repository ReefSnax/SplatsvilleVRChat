using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRC.Udon.Graph;

namespace CyanTrigger
{
    public class CyanTriggerCustomNodeReturn : CyanTriggerCustomUdonActionNodeDefinition
    {
        public static readonly UdonNodeDefinition NodeDefinition = new UdonNodeDefinition(
            "Return",
            "CyanTriggerSpecial_Return",
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
            var actionMethod = compileState.ActionMethod;
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.Jump(actionMethod.endNop));
        }
    }
}