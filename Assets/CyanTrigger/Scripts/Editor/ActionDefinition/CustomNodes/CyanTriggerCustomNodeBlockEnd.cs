using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRC.Udon.Graph;

namespace CyanTrigger
{
    public class CyanTriggerCustomNodeBlockEnd : CyanTriggerCustomUdonActionNodeDefinition
    {
        public static readonly UdonNodeDefinition NodeDefinition = new UdonNodeDefinition(
            "BlockEnd",
            "CyanTriggerSpecial_BlockEnd",
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
            // Do nothing!
        }
    }
}
