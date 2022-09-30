using VRC.Udon.Graph;

namespace CyanTrigger
{
    public class CyanTriggerCustomNodeStringNewLine : CyanTriggerCustomUdonActionNodeDefinition
    {
        public const string FullName = "SystemString.__get_newLine__SystemString";
        
        public static readonly UdonNodeDefinition NodeDefinition = new UdonNodeDefinition(
            "Get NewLine",
            FullName,
            typeof(string),
            new[]
            {
                new UdonNodeParameter()
                {
                    name = "", 
                    type = typeof(string),
                    parameterType = UdonNodeParameter.ParameterType.OUT
                },
            },
            new string[0],
            new string[0],
            new object[0],
            false
        );
        
        public override UdonNodeDefinition GetNodeDefinition()
        {
            return NodeDefinition;
        }
        
        public override void AddActionToProgram(CyanTriggerCompileState compileState)
        {
            var newLineVariable = compileState.Program.data.GetOrCreateVariableConstant(typeof(string), "\n");
            var variable = compileState.ActionInstance.inputs[0];
            var stringObj = compileState.GetDataFromVariableInstance(-1, 0, variable, typeof(string), true);
            
            compileState.ActionMethod.AddActions(
                CyanTriggerAssemblyActionsUtils.CopyVariables(newLineVariable, stringObj));
        }
    }
}