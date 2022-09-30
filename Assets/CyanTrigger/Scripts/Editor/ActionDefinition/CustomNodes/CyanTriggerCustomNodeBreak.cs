using System;
using VRC.Udon.Graph;

namespace CyanTrigger
{
    public class CyanTriggerCustomNodeBreak : CyanTriggerCustomUdonActionNodeDefinition
    {
        public static readonly UdonNodeDefinition NodeDefinition = new UdonNodeDefinition(
            "Break",
            "CyanTriggerSpecial_Break",
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
                    compileState.ActionMethod.AddAction(CyanTriggerAssemblyInstruction.Jump(scopeFrame.EndNop));
                    return;
                }
            }

            throw new Exception("Break statement not included in a loop!");
        }
    }
}