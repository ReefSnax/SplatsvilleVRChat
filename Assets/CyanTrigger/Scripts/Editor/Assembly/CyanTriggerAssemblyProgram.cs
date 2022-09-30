
using System.Collections.Generic;

namespace CyanTrigger
{
    public class CyanTriggerAssemblyProgram
    {
        public readonly CyanTriggerAssemblyCode code;
        public readonly CyanTriggerAssemblyData data;

        public CyanTriggerAssemblyProgram(CyanTriggerAssemblyCode code, CyanTriggerAssemblyData data)
        {
            this.data = data;
            this.code = code;
        }

        public string FinishAndExport()
        {
            Finish();
            ApplyAddresses();
            return Export();
        }

        public virtual void Finish()
        {
            // Ensure that all event variables are added.
            foreach (var method in code.GetMethods())
            {
                data.GetEventVariables(method.name);
            }
            
            code.Finish();
        }

        public void ApplyAddresses()
        {
            data.ApplyAddresses();
            code.ApplyAddresses();
            data.FinalizeJumpVariableAddresses();
        }

        public string Export()
        {
            return data.Export() + "\n" + code.Export();
        }

        public CyanTriggerAssemblyProgram Clone()
        {
            Dictionary<CyanTriggerAssemblyInstruction, CyanTriggerAssemblyInstruction> instructionMapping =
                new Dictionary<CyanTriggerAssemblyInstruction, CyanTriggerAssemblyInstruction>();
            Dictionary<CyanTriggerAssemblyDataType, CyanTriggerAssemblyDataType> variableMapping =
                new Dictionary<CyanTriggerAssemblyDataType, CyanTriggerAssemblyDataType>();

            CyanTriggerAssemblyProgram program =
                new CyanTriggerAssemblyProgram(code.Clone(instructionMapping), data.Clone(variableMapping));

            program.code.UpdateMapping(instructionMapping, variableMapping);
            program.data.UpdateJumpInstructions(instructionMapping);
            
            return program;
        }
        
        public void MergeProgram(CyanTriggerAssemblyProgram program)
        {
            foreach (var method in program.code.GetMethods())
            {
                code.AddMethod(method);
            }
            CyanTriggerAssemblyData.MergeData(data, program.data);
        }
    }
}
