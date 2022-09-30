using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Editor;
using VRC.Udon.Graph;

namespace CyanTrigger
{
    public class CyanTriggerNodeDefinition
    {
        public enum UdonDefinitionType
        {
            None = -1,
            Const,
            Variable,
            Type,
            Event,
            Method,
            VrcSpecial,
            CyanTriggerSpecial, // Used for special or control nodes
            CyanTriggerVariable,
        }

        private static readonly string[] VrcSpecialNodeNames =
        {
            "Block",
            "Branch",
            "Comment",
            "For",
            "While",
            "Get_Variable",
            "Set_Variable",
        };
        
        
        public string fullName;
        public string methodName;

        public List<Type> inputs = new List<Type>();
        public List<Type> outputs = new List<Type>();

        // TODO remove
        public string type;
        public Type baseType;
        public string typeFriendlyName;

        public UdonDefinitionType definitionType = UdonDefinitionType.None;

        public UdonNodeDefinition definition;

        public string[] typeCategories;

        public readonly CyanTriggerActionVariableDefinition[] variableDefinitions;
        
        public CyanTriggerNodeDefinition(UdonNodeDefinition definition)
        {
            this.definition = definition;

            fullName = definition.fullName;
            baseType = GetFixedType(definition);

            if (fullName.StartsWith("VRCInstantiate"))
            {
                typeCategories = new string[] { "VRC", "Instantiate" };
                baseType = typeof(VRCInstantiate);
            }

            // Process parameters
            List<CyanTriggerActionVariableDefinition> varDefinitions = new List<CyanTriggerActionVariableDefinition>();

            // int multiIndex = -1;
            int outParams = 0;
            foreach (var parameter in definition.parameters)
            {
                var variableDef = new CyanTriggerActionVariableDefinition
                {
                    type = new CyanTriggerSerializableType(parameter.type == null ? typeof(object) : parameter.type),
                    udonName = parameter.name,
                };
                if (!string.IsNullOrEmpty(parameter.name))
                {
                    variableDef.displayName = Regex.Replace(parameter.name, "(\\B[A-Z])", " $1").Trim();
                    if (variableDef.displayName == "instance")
                    {
                        variableDef.displayName = CyanTriggerNameHelpers.GetTypeFriendlyName(parameter.type);
                    }
                }
                
                if (parameter.parameterType == UdonNodeParameter.ParameterType.IN)
                {
                    inputs.Add(parameter.type);
                    variableDef.variableType = CyanTriggerActionVariableTypeDefinition.Constant |
                                               CyanTriggerActionVariableTypeDefinition.VariableInput;
                }
                else if (parameter.parameterType == UdonNodeParameter.ParameterType.OUT)
                {
                    outputs.Add(parameter.type);
                    
                    variableDef.variableType = CyanTriggerActionVariableTypeDefinition.VariableOutput |
                                               CyanTriggerActionVariableTypeDefinition.VariableInput;

                    // multiIndex = curParam;
                    ++outParams;
                }
                else
                {
                    Type type = parameter.type;
                    if (!type.IsArray)
                    {
                        type = type.MakeByRefType();
                        variableDef.type = new CyanTriggerSerializableType(type);
                    }
                    inputs.Add(type);
                    
                    variableDef.variableType = CyanTriggerActionVariableTypeDefinition.VariableOutput |
                                               CyanTriggerActionVariableTypeDefinition.VariableInput;
                    
                    // multiIndex = curParam;
                    ++outParams;
                }
                
                if (fullName.StartsWith("Event_"))
                {
                    // Don't add output variables for events.
                    if (parameter.parameterType != UdonNodeParameter.ParameterType.IN)
                    {
                        continue;
                    }
                    
                    // Special case for handling OnVariableChanged...
                    variableDef.variableType = 
                        parameter.type == typeof(CyanTriggerVariable) ? 
                            CyanTriggerActionVariableTypeDefinition.VariableInput :
                            CyanTriggerActionVariableTypeDefinition.Constant;
                }
                
                varDefinitions.Add(variableDef);
            }

            if (CyanTriggerNodeDefinitionManager.TryGetCustomDefinition(fullName, out var customDefinition) && 
                customDefinition.HasCustomVariableSettings())
            {
                variableDefinitions = customDefinition.GetCustomVariableSettings();
            }
            
            if (variableDefinitions == null)
            {
                // TODO figure out more cases
                if (outParams == 0 && inputs.Count > 0 && definition.parameters[0].name == "instance" && !baseType.IsArray)
                {
                    varDefinitions[0].variableType |= CyanTriggerActionVariableTypeDefinition.AllowsMultiple;
                }

                // Moving parameters will break things!
                // if (outParams == 1)
                // {
                //     var outParam = varDefinitions[multiIndex];
                //     varDefinitions.RemoveAt(multiIndex);
                //     varDefinitions.Insert(0, outParam);
                //     outParam.variableType |= CyanTriggerActionVariableTypeDefinition.AllowsMultiple;
                // }
                
                variableDefinitions = varDefinitions.ToArray();
            }
            

            // Get definition type
            string[] split = fullName.Split('.');
            int totalDefinitionTypes = Enum.GetNames(typeof(UdonDefinitionType)).Length - 1;
            for (int i = 0; i < totalDefinitionTypes; ++i)
            {
                if (split[0].StartsWith(((UdonDefinitionType)i).ToString()))
                {
                    definitionType = (UdonDefinitionType)i;
                    break;
                }
            }
            
            if (definitionType == UdonDefinitionType.None)
            {
                bool isSpecial = false;
                foreach (string specialName in VrcSpecialNodeNames)
                {
                    isSpecial = specialName.Equals(fullName);
                    if (isSpecial) break;
                }

                definitionType = isSpecial ? UdonDefinitionType.VrcSpecial : UdonDefinitionType.Method;
            }
            else if (baseType == null)
            {
                string typeString = definitionType.ToString();
                typeCategories = new string[] { typeString, split[0].Substring(typeString.Length + 1) };
                baseType = typeof(void);
                type = baseType.ToString();
                typeFriendlyName = "void";
                return;
            }
            
            typeFriendlyName = CyanTriggerNameHelpers.GetTypeFriendlyName(baseType);

            // Get type and method name
            string typeFullName = baseType.FullName;
            if (split[0] == typeFullName.Replace(".", ""))
            {
                type = typeFullName;
            }
            else
            {
                type = split[0];
            }

            // Events?
            if (split.Length == 1)
            {
                methodName = type;
                methodName = methodName.Replace(definitionType + "_", "");

                typeCategories = new string[] { definitionType.ToString(), methodName };
                return;
            }

            split = split[1].Split(new string[] { "__" }, StringSplitOptions.RemoveEmptyEntries);
            methodName = CyanTriggerNameHelpers.GetMethodFriendlyName(split[0]);

            if (typeCategories == null)
            {
                List<string> categories = new List<string>(baseType.FullName.Split('.'));
                categories.Add(methodName);

                typeCategories = categories.ToArray();
            }
        }

        public static Type GetFixedType(UdonNodeDefinition typeDefinition)
        {
            Type returnType = typeDefinition.type;
            
            // TODO find a more generic way to fix this...
            if (typeDefinition.fullName.StartsWith("Type_VRCUdonCommonInterfacesIUdonEventReceive"))
            {
                returnType = returnType.IsArray ? typeof(IUdonEventReceiver[]) : typeof(IUdonEventReceiver);
            }

            return returnType;
        }

        public string[] GetTrieCategories()
        {
            return typeCategories;
        }
        
        public string GetMethodDisplayName()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append(methodName);
            sb.Append('(');

            for (int curIn = 0; curIn < inputs.Count; ++curIn)
            {
                sb.Append(CyanTriggerNameHelpers.GetTypeFriendlyName(inputs[curIn]));
                if (curIn + 1 < inputs.Count + outputs.Count)
                {
                    sb.Append(", ");
                }
            }
            
            for (int curOut = 0; curOut < outputs.Count; ++curOut)
            {
                sb.Append($"out {CyanTriggerNameHelpers.GetTypeFriendlyName(outputs[curOut])}");
                if (curOut + 1 < outputs.Count)
                {
                    sb.Append(", ");
                }
            }

            sb.Append(')');

            return sb.ToString();
        }

        public string GetActionDisplayName()
        {
            if (definitionType == UdonDefinitionType.CyanTriggerSpecial)
            {
                return methodName;
            }
            
            if (definitionType == UdonDefinitionType.CyanTriggerVariable)
            {
                return typeFriendlyName;
            }
            
            // TODO remove?
            if (definitionType == UdonDefinitionType.Const)
            {
                return typeFriendlyName + ".Set";
            }

            if (definitionType == UdonDefinitionType.Type)
            {
                return typeFriendlyName + ".Type";
            }
            
            StringBuilder sb = new StringBuilder();

            if (baseType != null && baseType != typeof(void))
            {
                sb.Append(typeFriendlyName);
                sb.Append('.');
            }
            sb.Append(methodName);

            return sb.ToString();
        }
        
        #region Event methods

        public string GetEventName()
        {
            if (definitionType != UdonDefinitionType.Event)
            {
                return "";
            }

            // "Event_<eventName>"
            string eventName = fullName.Substring(6);

            return "_" + char.ToLower(eventName[0]) + eventName.Substring(1);
        }

        public List<(string, Type)> GetEventVariables(int mask = 6 /* out | in_out */)
        {
            List<(string, Type)> outputs = new List<(string, Type)>();

            if (definitionType != UdonDefinitionType.Event)
            {
                return outputs;
            }

            // Remove the underscore
            string eventName = GetEventName().Substring(1);

            // if (eventName.Equals("custom"))
            // {
            //     return outputs;
            // }

            foreach (var parameter in definition.parameters)
            {
                if (((1 << (int)parameter.parameterType) & mask) == 0)
                {
                    continue;
                }
                
                string paramName = eventName;
                if (!string.IsNullOrEmpty(parameter.name))
                {
                    paramName += char.ToUpper(parameter.name[0]) + parameter?.name.Substring(1);
                }
                else
                {
                    paramName += "_parameter";
                }
                outputs.Add((paramName, parameter.type));
            }

            return outputs;
        }

        #endregion
    }
}