using System;
using System.Collections.Generic;
using UnityEngine;

namespace CyanTrigger
{
    public enum CyanTriggerInstructionType
    {
        NOP,
        POP,
        COPY,
        PUSH,
        JUMP_IF_FALSE,
        JUMP,
        EXTERN,
        JUMP_INDIRECT,
    }
    
    public class CyanTriggerAssemblyInstruction
    {
        private CyanTriggerInstructionType instructionType;
        private uint instructionAddress;
        private string signature;
        private string jumpLabel;
        private CyanTriggerAssemblyDataType pushVariable;
        private CyanTriggerAssemblyInstruction jumpToInstruction;

        public CyanTriggerAssemblyInstruction Clone()
        {
            CyanTriggerAssemblyInstruction action = new CyanTriggerAssemblyInstruction(instructionType)
            {
                instructionAddress = instructionAddress,
                signature = signature,
                jumpLabel = jumpLabel,
                pushVariable = pushVariable,
                jumpToInstruction = jumpToInstruction
            };

            return action;
        }
        
        public void UpdateMapping(
            Dictionary<CyanTriggerAssemblyInstruction, CyanTriggerAssemblyInstruction> instructionMapping,
            Dictionary<CyanTriggerAssemblyDataType, CyanTriggerAssemblyDataType> variableMapping)
        {
            if (pushVariable != null)
            {
                pushVariable = variableMapping[pushVariable];
            }

            if (jumpToInstruction != null)
            {
                jumpToInstruction = instructionMapping[jumpToInstruction];
            }
        }
        
        public static CyanTriggerAssemblyInstruction Copy()
        {
            return new CyanTriggerAssemblyInstruction(CyanTriggerInstructionType.COPY);
        }

        public static CyanTriggerAssemblyInstruction Nop()
        {
            return new CyanTriggerAssemblyInstruction(CyanTriggerInstructionType.NOP);
        }

        public static CyanTriggerAssemblyInstruction Pop()
        {
            return new CyanTriggerAssemblyInstruction(CyanTriggerInstructionType.POP);
        }

        public static CyanTriggerAssemblyInstruction CreateExtern(string methodSignature)
        {
            return new CyanTriggerAssemblyInstruction(CyanTriggerInstructionType.EXTERN, "\"" + methodSignature + "\"");
        }

        public static CyanTriggerAssemblyInstruction PushVariable(CyanTriggerAssemblyDataType variable)
        {
            return new CyanTriggerAssemblyInstruction(CyanTriggerInstructionType.PUSH, variable);
        }

        public static CyanTriggerAssemblyInstruction PushVariable(string variableName)
        {
            return new CyanTriggerAssemblyInstruction(CyanTriggerInstructionType.PUSH, variableName);
        }

        public static CyanTriggerAssemblyInstruction JumpIndirect(CyanTriggerAssemblyDataType variable)
        {
            return new CyanTriggerAssemblyInstruction(CyanTriggerInstructionType.JUMP_INDIRECT, variable);
        }

        public static CyanTriggerAssemblyInstruction JumpLabel(string label)
        {
            return new CyanTriggerAssemblyInstruction(CyanTriggerInstructionType.JUMP)
            {
                jumpLabel = label
            };
        }

        public static CyanTriggerAssemblyInstruction Jump(CyanTriggerAssemblyInstruction instructionJump)
        {
            return new CyanTriggerAssemblyInstruction(CyanTriggerInstructionType.JUMP, instructionJump);
        }

        public static CyanTriggerAssemblyInstruction JumpIfFalse(CyanTriggerAssemblyInstruction instructionJump)
        {
            return new CyanTriggerAssemblyInstruction(CyanTriggerInstructionType.JUMP_IF_FALSE, instructionJump);
        }

        // Private constructors to force using static creation functions above.
        private CyanTriggerAssemblyInstruction(CyanTriggerInstructionType type)
        {
            instructionType = type;
        }

        private CyanTriggerAssemblyInstruction(CyanTriggerInstructionType type, string sig)
        {
            instructionType = type;
            signature = sig;
        }

        private CyanTriggerAssemblyInstruction(CyanTriggerInstructionType type, CyanTriggerAssemblyInstruction instructionJump)
        {
            instructionType = type;
            jumpToInstruction = instructionJump;
        }

        private CyanTriggerAssemblyInstruction(CyanTriggerInstructionType type, CyanTriggerAssemblyDataType variable)
        {
            instructionType = type;
            pushVariable = variable;
        }

        public void ConvertToNOP()
        {
            instructionType = CyanTriggerInstructionType.NOP;
        }

        public CyanTriggerInstructionType GetInstructionType()
        {
            return instructionType;
        }

        public string GetSignature()
        {
            return signature;
        }

        public CyanTriggerAssemblyDataType GetVariable()
        {
            return pushVariable;
        }

        public void SetVariable(CyanTriggerAssemblyDataType variable)
        {
            signature = null;
            pushVariable = variable;
        }

        public string GetVariableName()
        {
            if (pushVariable != null)
            {
                return pushVariable.name;
            }

            return signature;
        }
        
        public string GetJumpLabel()
        {
            return jumpLabel;
        }

        public CyanTriggerAssemblyInstruction GetJumpInstruction()
        {
            return jumpToInstruction;
        }

        public void SetJumpInstruction(CyanTriggerAssemblyInstruction instructionJump)
        {
            jumpToInstruction = instructionJump;
        }

        public void SetAddress(uint address)
        {
            instructionAddress = address;
        }

        public void UpdateAddress(uint address)
        {
            signature = "0x" + address.ToString("X8");
        }

        public uint GetAddress()
        {
            return instructionAddress;
        }

        public uint GetAddressAfterInstruction()
        {
            return instructionAddress + GetInstructionSize();
        }

        public uint GetInstructionSize()
        {
            if (instructionType == CyanTriggerInstructionType.NOP)
            {
                return 0u;
            }

            return GetUdonInstructionSize(instructionType);
        }

        public void ExportSignature()
        {
            if (!string.IsNullOrEmpty(signature))
            {
                return;
            }

            if (pushVariable != null)
            {
                signature = pushVariable.name;
            }

            if (jumpToInstruction != null)
            {
                UpdateAddress(jumpToInstruction.instructionAddress);
            }
        }

        public string Export()
        {
            ExportSignature();

            string output = "";
            switch (instructionType)
            {
                case CyanTriggerInstructionType.NOP:
                case CyanTriggerInstructionType.POP:
                case CyanTriggerInstructionType.COPY:
                    output = instructionType.ToString();
                    break;
                case CyanTriggerInstructionType.PUSH:
                case CyanTriggerInstructionType.JUMP_IF_FALSE:
                case CyanTriggerInstructionType.JUMP:
                case CyanTriggerInstructionType.EXTERN:
                case CyanTriggerInstructionType.JUMP_INDIRECT:
                    Debug.Assert(!string.IsNullOrEmpty(signature), "UdonAssemblyInstruction.Export Signature is empty on export");
                    output = instructionType.ToString() +", "+ signature;
                    break;
                default:
                    throw new Exception("Unsupported UdonInstructionType! " + instructionType.ToString());
            }

            return output;
            //return "# " + instructionAddress + " 0x" + instructionAddress.ToString("X8") + "\n    " + output;
        }

        public static uint GetUdonInstructionSize(CyanTriggerInstructionType instructionType)
        {
            switch (instructionType)
            {
                case CyanTriggerInstructionType.NOP:
                case CyanTriggerInstructionType.POP:
                case CyanTriggerInstructionType.COPY:
                    return 4;
                case CyanTriggerInstructionType.PUSH:
                case CyanTriggerInstructionType.JUMP_IF_FALSE:
                case CyanTriggerInstructionType.JUMP:
                case CyanTriggerInstructionType.EXTERN:
                case CyanTriggerInstructionType.JUMP_INDIRECT:
                    return 8;
                default:
                    throw new Exception("Unsupported UdonInstructionType! " + instructionType.ToString());
            }
        }
    }
}