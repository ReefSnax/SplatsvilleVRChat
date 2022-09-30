
using System;
using System.Collections.Generic;
using VRC.Udon.Graph;

namespace CyanTrigger
{
    public class CyanTriggerCustomNodeType : CyanTriggerCustomUdonActionNodeDefinition
    {
        private readonly Type _type;
        private readonly UdonNodeDefinition _definition;

        public CyanTriggerCustomNodeType(UdonNodeDefinition typeDefinition)
        {
            _definition = typeDefinition;
            _type = CyanTriggerNodeDefinition.GetFixedType(typeDefinition);
        }
        
        public override UdonNodeDefinition GetNodeDefinition()
        {
            return _definition;
        }
        
        public override void AddActionToProgram(CyanTriggerCompileState compileState)
        {
            var actionInstance = compileState.ActionInstance;
            var actionMethod = compileState.ActionMethod;
            var program = compileState.Program;

            var constTypeVar = program.data.GetOrCreateVariableConstant(typeof(Type), _type, false);
            var outputVar = compileState.GetDataFromVariableInstance(-1, 0, actionInstance.inputs[0], _type, true);
            
            actionMethod.AddActions(CyanTriggerAssemblyActionsUtils.CopyVariables(constTypeVar, outputVar));

            var changedVariables = new List<CyanTriggerAssemblyDataType> { outputVar };
            compileState.CheckVariableChanged(actionMethod, changedVariables);
        }
    }
}
