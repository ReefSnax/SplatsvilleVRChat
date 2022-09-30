using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Graph;

namespace CyanTrigger
{
    public abstract class CyanTriggerCustomUdonActionNodeDefinition : CyanTriggerCustomUdonNodeDefinition
    {
        public override bool GetBaseMethod(
            CyanTriggerAssemblyProgram program, 
            CyanTriggerActionInstance actionInstance,
            out CyanTriggerAssemblyMethod method)
        {
            throw new System.NotImplementedException();
        }

        public override void AddEventToProgram(CyanTriggerCompileState compileState)
        {
            throw new System.NotImplementedException();
        }
    }
}
