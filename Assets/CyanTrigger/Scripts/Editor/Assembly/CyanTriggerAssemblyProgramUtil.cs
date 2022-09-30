using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Editor;

namespace CyanTrigger
{
    public static class CyanTriggerAssemblyProgramUtil
    {

        public static CyanTriggerAssemblyProgram MergePrograms(params CyanTriggerAssemblyProgram[] programs)
        {
            CyanTriggerAssemblyCode code = new CyanTriggerAssemblyCode();
            CyanTriggerAssemblyData data = new CyanTriggerAssemblyData();
            CyanTriggerAssemblyProgram program = new CyanTriggerAssemblyProgram(code, data);

            foreach (var programToMerge in programs)
            {
                program.MergeProgram(programToMerge);
            }

            return program;
        }
        
        
        public static CyanTriggerAssemblyProgram CreateProgram(IUdonProgram udonProgram)
        {
            CyanTriggerAssemblyCode code = new CyanTriggerAssemblyCode();
            CyanTriggerAssemblyData data = new CyanTriggerAssemblyData();
            CyanTriggerAssemblyProgram program = new CyanTriggerAssemblyProgram(code, data);

            Dictionary<uint, string> variableLocs = new Dictionary<uint, string>();
            Dictionary<uint, string> methodLocs = new Dictionary<uint, string>();
            Dictionary<uint, CyanTriggerAssemblyInstruction> instructionLocs = new Dictionary<uint, CyanTriggerAssemblyInstruction>();

            List<(CyanTriggerAssemblyInstruction, CyanTriggerAssemblyInstruction)> unresolvedJumps = new List<(CyanTriggerAssemblyInstruction, CyanTriggerAssemblyInstruction)>();

            // Get variables
            {
                foreach (var symbol in udonProgram.SymbolTable.GetSymbols())
                {
                    uint i = udonProgram.SymbolTable.GetAddressFromSymbol(symbol);
                    Type type = udonProgram.SymbolTable.GetSymbolType(symbol);

                    CyanTriggerAssemblyDataType variable = data.AddNamedVariable(symbol, type);
                    variable.defaultValue = udonProgram.Heap.GetHeapVariable(i);

                    variableLocs.Add(i, symbol);
                }
            }

            // Get assembly
            {
                foreach (var symbol in udonProgram.EntryPoints.GetSymbols())
                {
                    uint i = udonProgram.EntryPoints.GetAddressFromSymbol(symbol);
                    methodLocs.Add(i, symbol);
                }

                string[] assembly_ = UdonEditorManager.Instance.DisassembleProgram(udonProgram);

                CyanTriggerAssemblyMethod method = null;

                for (int i = 0; i < assembly_.Length; ++i)
                {
                    CyanTriggerAssemblyInstruction instruction = null;

                    int index = assembly_[i].IndexOf(':');
                    string addressString = assembly_[i].Substring(0, index).Trim();
                    uint address = Convert.ToUInt32(addressString, 16);

                    assembly_[i] = assembly_[i].Substring(index + 2);

                    string[] split = assembly_[i].Split(',');
                    if (split[0].StartsWith("PUSH"))
                    {
                        CyanTriggerAssemblyDataType variable = null;
                        uint variableAddress = Convert.ToUInt32(split[1].Trim(), 16);
                        if (variableLocs.TryGetValue(variableAddress, out string varName))
                        {
                            variable = data.GetVariableNamed(varName);
                            Debug.Assert(variable != null, "Could not find variable named " + varName);
                        }
                        else
                        {
                            Debug.LogError("unknown variable? " + variableAddress);
                            break;
                        }

                        instruction = CyanTriggerAssemblyInstruction.PushVariable(variable);
                    }
                    else if (split[0].StartsWith("JUMP_INDIRECT"))
                    {
                        string varName = split[1].Trim();
                        CyanTriggerAssemblyDataType variable = data.GetVariableNamed(varName);
                        Debug.Assert(variable != null, "Could not find variable named " + varName);
                        instruction = CyanTriggerAssemblyInstruction.JumpIndirect(variable);
                    }
                    else if (split[0].StartsWith("JUMP_IF_FALSE") || split[0].Equals("JUMP"))
                    {
                        string add = split[1].Trim();
                        if (add.StartsWith("0x"))
                        {
                            uint methodAddress = Convert.ToUInt32(add, 16);

                            CyanTriggerAssemblyInstruction nop = CyanTriggerAssemblyInstruction.Nop();
                            nop.SetAddress(methodAddress);

                            if (split[0].StartsWith("JUMP_IF_FALSE"))
                            {
                                instruction = CyanTriggerAssemblyInstruction.JumpIfFalse(nop);
                            }
                            else if (split[0].Equals("JUMP"))
                            {
                                instruction = CyanTriggerAssemblyInstruction.Jump(nop);
                            }

                            unresolvedJumps.Add((instruction, nop));
                        }
                        else
                        {
                            Debug.LogError("unknown jump? " + add);
                            break;
                        }
                    }
                    else if (split[0].StartsWith("COPY"))
                    {
                        instruction = CyanTriggerAssemblyInstruction.Copy();
                    }
                    else if (split[0].StartsWith("NOP"))
                    {
                        instruction = CyanTriggerAssemblyInstruction.Nop();
                    }
                    else if (split[0].StartsWith("EXTERN"))
                    {
                        instruction = CyanTriggerAssemblyInstruction.CreateExtern(split[1].Replace("\"", "").Trim());
                    }

                    // Get new method
                    if (methodLocs.TryGetValue(address, out string methodName))
                    {
                        if (method != null)
                        {
                            code.AddMethod(method);
                        }

                        method = new CyanTriggerAssemblyMethod(methodName, false);
                    }

                    Debug.Assert(instruction != null, "Did not create instruction for assembly: " + assembly_[i]);

                    instruction.SetAddress(address);
                    instructionLocs.Add(address, instruction);
                    method.AddAction(instruction);
                }

                if (method != null)
                {
                    code.AddMethod(method);
                }
                else
                {
                    Debug.LogWarning("No methods after reading program!");
                }
                
                // foreach (var exportedMethods in udonProgram.EntryPoints.GetExportedSymbols())
                // {
                //     code.GetMethod(exportedMethods).export = true;
                // }
            }

            // Update instruction locs based on nops
            foreach (var instructionPair in unresolvedJumps)
            {
                var instruction = instructionPair.Item1;
                var nop = instructionPair.Item2;

                if (instructionLocs.TryGetValue(nop.GetAddress(), out CyanTriggerAssemblyInstruction jumpInstruction))
                {
                    instruction.SetJumpInstruction(jumpInstruction);
                }
                else
                {
                    //Debug.LogWarning("Could not get instruction for address: " + nop.GetAddress());
                    instruction.SetJumpInstruction(null);
                }
            }

            // Update variables that might be jumps
            {
                foreach (var symbol in udonProgram.SymbolTable.GetSymbols())
                {
                    Type type = udonProgram.SymbolTable.GetSymbolType(symbol);
                    if (type != typeof(uint))
                    {
                        continue;
                    }

                    uint i = udonProgram.SymbolTable.GetAddressFromSymbol(symbol);
                    uint value = (uint)udonProgram.Heap.GetHeapVariable(i);

                    // U# style method jumps where you skip the first instruction
                    uint methodStartAddress = value - CyanTriggerAssemblyInstruction.GetUdonInstructionSize(CyanTriggerInstructionType.PUSH);
                    if (!methodLocs.TryGetValue(methodStartAddress, out string methodName))
                    {
                        continue;
                    }

                    CyanTriggerAssemblyDataType variable = data.GetVariableNamed(symbol);
                    if (!instructionLocs.TryGetValue(methodStartAddress, out CyanTriggerAssemblyInstruction instruction))
                    {
                        continue;
                    }

                    data.AddJumpReturnVariable(instruction, variable);
                }
            }

            return program;
        }

        public static void ProcessProgramForCyanTriggers(CyanTriggerAssemblyProgram program) // Add asset here to know for prefixing and method signatures
        {
            ConvertFunctionEpilogues(program);
            ConvertBlankCustomEvents(program);
        }


        // This function will go through all methods in the program and convert the 
        // function epilogues to CyanTrigger style. This is based on UdonSharp's
        // format, but using different variable names.
        
        // U# 
        // - Pushes variable with max end jump location at method starts. This is used to know where to return.
        // - On calling directly, Pushes address of next line, jumps to method address + 1.
        /* 
         _methodName:
            PUSH, __0_const_intnl_SystemUInt32

            [Method] <- Jump here for local method calls

            PUSH, __0_intnl_returnTarget_UInt32 #Function epilogue
            COPY
            JUMP_INDIRECT, __0_intnl_returnTarget_UInt32
        */

        // UdonGraph
        // - Nothing at beginning, never have jump indirect to methods...
        // - End jump to infinity! 
        /*
         _methodName:
            [Method]
            JUMP, 0xFFFFFFFC
        */
        public static void ConvertFunctionEpilogues(CyanTriggerAssemblyProgram program)
        {
            CyanTriggerAssemblyCode code = program.code;
            CyanTriggerAssemblyData data = program.data;

            uint maxAddress = 0;
            foreach (var method in code.GetMethods())
            {
                foreach (var instruction in method.actions)
                {
                    maxAddress = Math.Max(maxAddress, instruction.GetAddress());
                }
            }

            // Assumed based on CyanTrigger and UdonSharp assembly generation
            bool hasFunctionEpilogue = data.GetVariableNamed(CyanTriggerAssemblyData.GetSpecialVariableName(CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.ReturnAddress)) != null;

            // UdonSharp program
            if (!hasFunctionEpilogue && data.GetVariableNamed("__refl_const_intnl_udonTypeID") != null)
            {
                // This seems risky 
                CyanTriggerAssemblyDataType udonSharpReturnTarget = data.GetVariableNamed("__0_intnl_returnTarget_UInt32");
                Debug.Assert(udonSharpReturnTarget != null, "Could not find variable named \"__0_intnl_returnTarget_UInt32\" in UdonSharp program.");
                CyanTriggerAssemblyDataType udonSharpEndAddress = data.GetVariableNamed("__0_const_intnl_SystemUInt32");
                Debug.Assert(udonSharpEndAddress != null, "Could not find variable named \"__0_const_intnl_SystemUInt32\" in UdonSharp program.");

                data.RenameVariable(CyanTriggerAssemblyData.GetSpecialVariableName(CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.ReturnAddress), udonSharpReturnTarget);
                data.RenameVariable(CyanTriggerAssemblyData.GetSpecialVariableName(CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.EndAddress), udonSharpEndAddress);

                hasFunctionEpilogue = true;
            }

            if (!hasFunctionEpilogue)
            {
                // Unknown program type, assume no function epilogue. 
                // Convert all jump to end to jump to method nop, and convert jump to last address to nop. 
                // Add initial address push and function epilogue.

                foreach (var method in code.GetMethods())
                {
                    for (int cur = 0; cur < method.actions.Count; ++cur)
                    {
                        var instruction = method.actions[cur];
                        bool isLast = cur + 1 == method.actions.Count;

                        CyanTriggerInstructionType instructionType = instruction.GetInstructionType();

                        if (isLast)
                        {
                            Debug.Assert(instructionType == CyanTriggerInstructionType.JUMP, "Last method instruction is not a JUMP instruction! ");
                            // convert to Nop as a way to delete, but not remove jump references to this instruction
                            instruction.ConvertToNOP();
                        }
                        else if (instructionType == CyanTriggerInstructionType.JUMP || instructionType == CyanTriggerInstructionType.JUMP_IF_FALSE)
                        {
                            var jumpInstruction = instruction.GetJumpInstruction();
                            if (jumpInstruction.GetInstructionType() == CyanTriggerInstructionType.NOP && jumpInstruction.GetAddress() > maxAddress)
                            {
                                jumpInstruction.SetJumpInstruction(method.endNop);
                                //instruction.SetJumpInstruction(method.endNop);
                            }
                        }
                    }

                    method.PushInitialEndVariable(data);
                    method.PushMethodEndReturnJump(data);
                }
            }

            // Ensure variables have proper initial values
            data.CreateSpecialAddressVariables();
        }


        // Find and convert SendCustomEvent(this, "") to jump methods
        public static void ConvertBlankCustomEvents(CyanTriggerAssemblyProgram program)
        {
            CyanTriggerAssemblyCode code = program.code;
            CyanTriggerAssemblyData data = program.data;
            
            string sendCustomEventName = CyanTriggerDefinitionResolver.GetMethodSignature(
                    typeof(IUdonEventReceiver).GetMethod(nameof(IUdonEventReceiver.SendCustomEvent), new [] { typeof(string) }));
            sendCustomEventName = "\"" + sendCustomEventName + "\"";
            // TODO check the udon version of the event

            // add a new variable special jump location
            CyanTriggerAssemblyDataType actionJumpVariable = 
                data.GetSpecialVariable(CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.ActionJumpAddress);

            // TODO process all copy's first to ensure that other methods don't modify the variable.
            HashSet<string> copyVariables = new HashSet<string>();
            
            foreach (var method in code.GetMethods())
            {
                // Starting at 2 since you always need 2 inputs into the custom event
                for (int cur = 2; cur < method.actions.Count; ++cur)
                {
                    var instruction = method.actions[cur];
                    if (instruction.GetInstructionType() == CyanTriggerInstructionType.COPY)
                    {
                        var pushEvent = method.actions[cur-1];
                        copyVariables.Add(pushEvent.GetVariableName());
                    }
                    
                    if (instruction.GetInstructionType() == CyanTriggerInstructionType.EXTERN &&
                        instruction.GetSignature() == sendCustomEventName)
                    {
                        var pushUdon = method.actions[cur-2];
                        var pushEvent = method.actions[cur-1];
                        string eventVarName = pushEvent.GetVariableName();
                        
                        //bool udonNull = ((UdonGameObjectComponentHeapReference)data.GetVariableNamed(pushUdon.GetVariableName()).defaultValue is;
                        bool eventEmpty = 
                            string.IsNullOrEmpty((string)data.GetVariableNamed(eventVarName).defaultValue) &&
                            !copyVariables.Contains(eventVarName);
                        
                        
                        if (eventEmpty)
                        {
                            instruction.ConvertToNOP();
                            pushUdon.ConvertToNOP();
                            pushEvent.ConvertToNOP();
                            
                            method.actions.InsertRange(
                                cur - 2, 
                                CyanTriggerAssemblyActionsUtils.JumpIndirect(data, actionJumpVariable));
                        }
                    }
                }
            }
        }
        
        
        public static CyanTriggerProgramTranslation AddNamespace(CyanTriggerAssemblyProgram program, string prefixNamespace)
        {
            CyanTriggerItemTranslation[] methodTranslations = program.code.AddPrefixToAllMethods(prefixNamespace);
            CyanTriggerItemTranslation[] variableTranslations = program.data.AddPrefixToAllVariables(prefixNamespace);

            Dictionary<string, string> methodMap = new Dictionary<string, string>();
            Dictionary<string, string> variableMap = new Dictionary<string, string>();

            foreach (var method in methodTranslations)
            {
                methodMap.Add(method.BaseName, method.TranslatedName);
            }
            
            foreach (var variable in variableTranslations)
            {
                variableMap.Add(variable.BaseName, variable.TranslatedName);
            }
            
            CyanTriggerAssemblyCode code = program.code;
            CyanTriggerAssemblyData data = program.data;
            
            string sendCustomEventName = CyanTriggerDefinitionResolver.GetMethodSignature(
                typeof(IUdonEventReceiver).GetMethod(nameof(IUdonEventReceiver.SendCustomEvent)));
            sendCustomEventName = "\"" + sendCustomEventName + "\"";

            string sendCustomNetworkedEventName = CyanTriggerDefinitionResolver.GetMethodSignature(
                typeof(IUdonEventReceiver).GetMethod(nameof(IUdonEventReceiver.SendCustomNetworkEvent)));
            sendCustomNetworkedEventName = "\"" + sendCustomNetworkedEventName + "\"";
            
            foreach (var method in code.GetMethods())
            {
                for (int cur = 0; cur < method.actions.Count; ++cur)
                {
                    var instruction = method.actions[cur];
                    if (instruction.GetInstructionType() == CyanTriggerInstructionType.EXTERN &&
                        (instruction.GetSignature() == sendCustomEventName ||
                         instruction.GetSignature() == sendCustomNetworkedEventName))
                    {
                        var pushEvent = method.actions[cur-1];

                        var variable = data.GetVariableNamed(pushEvent.GetVariableName());
                        string defaultValue = ((string)variable.defaultValue);

                        if (defaultValue != null && methodMap.TryGetValue(defaultValue, out string newMethod))
                        {
                            variable.defaultValue = newMethod;
                        }
                    }

                    if (instruction.GetInstructionType() == CyanTriggerInstructionType.PUSH && instruction.GetVariable() == null)
                    {
                        string varName = instruction.GetVariableName();
                        if (variableMap.TryGetValue(varName, out string newName))
                        {
                            var variable = data.GetVariableNamed(newName);
                            instruction.SetVariable(variable);
                        }
                    }
                }
            }

            return new CyanTriggerProgramTranslation
                {TranslatedMethods = methodTranslations, TranslatedVariables = variableTranslations};
        }
    }
}