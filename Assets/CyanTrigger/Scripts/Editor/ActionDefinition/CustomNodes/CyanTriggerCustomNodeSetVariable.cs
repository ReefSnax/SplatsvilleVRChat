using System;
using System.Collections.Generic;
using VRC.Udon.Graph;

namespace CyanTrigger
{
    public class CyanTriggerCustomNodeSetVariable : CyanTriggerCustomUdonActionNodeDefinition
    {
        private readonly Type _type;
        private readonly UdonNodeDefinition _definition;

        public CyanTriggerCustomNodeSetVariable(Type type)
        {
            _type = type;
            string friendlyName = CyanTriggerNameHelpers.GetTypeFriendlyName(_type);
            string fullName = GetFullnameForType(_type);
            
            _definition = new UdonNodeDefinition(
                "Set " + friendlyName,
                fullName,
                _type,
                new []
                {
                    new UdonNodeParameter
                    {
                        name = "input",
                        parameterType = UdonNodeParameter.ParameterType.IN,
                        type = _type
                    },
                    new UdonNodeParameter
                    {
                        name = "output",
                        parameterType = UdonNodeParameter.ParameterType.OUT,
                        type = _type
                    }
                },
                new string[0],
                new string[0],
                new object[0],
                true
            );
        }
        
        public static string GetFullnameForType(Type type)
        {
            string fullName = CyanTriggerNameHelpers.SanitizeName(type.FullName);
            if (type.IsArray)
            {
                fullName += "Array";
            }

            return fullName+"__.Set__"+fullName +"__" +fullName;
        }
        
        public override UdonNodeDefinition GetNodeDefinition()
        {
            return _definition;
        }
        
        public override void AddActionToProgram(CyanTriggerCompileState compileState)
        {
            var actionInstance = compileState.ActionInstance;
            var actionMethod = compileState.ActionMethod;
            
            var dataVar =
                compileState.GetDataFromVariableInstance(-1, 0, actionInstance.inputs[0], _type, false);
            var outputVar =
                compileState.GetDataFromVariableInstance(-1, 1, actionInstance.inputs[1], _type, true);
            
            actionMethod.AddActions(CyanTriggerAssemblyActionsUtils.CopyVariables(dataVar, outputVar));
            
            var changedVariables = new List<CyanTriggerAssemblyDataType> { outputVar };
            compileState.CheckVariableChanged(actionMethod, changedVariables);
        }
    }
}
