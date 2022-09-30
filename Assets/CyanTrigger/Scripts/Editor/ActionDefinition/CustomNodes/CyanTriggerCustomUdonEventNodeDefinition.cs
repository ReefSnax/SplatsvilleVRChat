using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Graph;

namespace CyanTrigger
{
    public abstract class CyanTriggerCustomUdonEventNodeDefinition : CyanTriggerCustomUdonNodeDefinition
    {
        public override void AddActionToProgram(CyanTriggerCompileState compileState)
        {
            throw new NotImplementedException();
        }
    }
}
