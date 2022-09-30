using UnityEngine;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Graph;

namespace CyanTrigger
{
    public class CyanTriggerCustomNodeReturnIfDisabled : CyanTriggerCustomUdonActionNodeDefinition
    {
        public static readonly UdonNodeDefinition NodeDefinition = new UdonNodeDefinition(
            "ReturnIfDisabled",
            "CyanTriggerSpecial_ReturnIfDisabled",
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
            var data = compileState.Program.data;

            var thisGameObject = data.GetThisConst(typeof(GameObject));
            var thisUdon = data.GetThisConst(typeof(IUdonEventReceiver));

            var tempBool = data.RequestTempVariable(typeof(bool));
            var pushTempBool = CyanTriggerAssemblyInstruction.PushVariable(tempBool);
            
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.PushVariable(thisGameObject));
            actionMethod.AddAction(pushTempBool);
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.CreateExtern(
                CyanTriggerDefinitionResolver.GetMethodSignature(
                    typeof(GameObject).GetProperty(nameof(GameObject.activeInHierarchy)).GetGetMethod())));
            
            actionMethod.AddAction(pushTempBool);
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.JumpIfFalse(actionMethod.endNop));
            
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.PushVariable(thisUdon));
            actionMethod.AddAction(pushTempBool);
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.CreateExtern(
                CyanTriggerDefinitionResolver.GetMethodSignature(
                    typeof(UdonBehaviour).GetProperty(nameof(UdonBehaviour.enabled)).GetGetMethod())));
            
            actionMethod.AddAction(pushTempBool);
            actionMethod.AddAction(CyanTriggerAssemblyInstruction.JumpIfFalse(actionMethod.endNop));
        }
    }
}

