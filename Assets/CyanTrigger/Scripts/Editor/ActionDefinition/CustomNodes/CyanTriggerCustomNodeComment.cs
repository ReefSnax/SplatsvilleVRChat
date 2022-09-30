
using VRC.Udon.Graph;

namespace CyanTrigger
{
    public class CyanTriggerCustomNodeComment : CyanTriggerCustomUdonActionNodeDefinition
    {
        public static readonly UdonNodeDefinition NodeDefinition = new UdonNodeDefinition(
            "Comment",
            "CyanTriggerSpecial_Comment",
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