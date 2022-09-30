using System;
using UnityEditor;
using VRC.Udon.Graph;

namespace CyanTrigger
{
    public abstract class CyanTriggerCustomNodeVariableProvider : CyanTriggerCustomUdonActionNodeDefinition
    {
        public override bool HasCustomVariableSettings()
        {
            return true;
        }

        public override bool HasCustomVariableInitialization()
        {
            return true;
        }
        
        public override bool DefinesCustomEditorVariableOptions()
        {
            return true;
        }
        
        protected abstract (string, Type)[] GetVariables();
        protected abstract bool ShowDefinedVariablesAtBeginning();
        
        public override CyanTriggerActionVariableDefinition[] GetCustomVariableSettings()
        {
            var nodeDef = GetNodeDefinition();
            int initialVarCount = nodeDef.parameters.Count;
            int startIndex = ShowDefinedVariablesAtBeginning() ? initialVarCount : 0;
            (string, Type)[] variables = GetVariables();
            Type stringType = typeof(string);
            
            CyanTriggerActionVariableDefinition[] definitions = 
                new CyanTriggerActionVariableDefinition[initialVarCount + variables.Length];
            for (int index = 0; index < variables.Length; ++index)
            {
                var (name, _) = variables[index];
                CyanTriggerActionVariableTypeDefinition nameType =
                    CyanTriggerActionVariableTypeDefinition.Constant |
                    (string.IsNullOrEmpty(name)
                        ? CyanTriggerActionVariableTypeDefinition.Hidden
                        : CyanTriggerActionVariableTypeDefinition.None);
                
                // Add name parameter
                definitions[startIndex + index] = new CyanTriggerActionVariableDefinition
                {
                    type = new CyanTriggerSerializableType(stringType),
                    displayName = name,
                    udonName = name+"_variable",
                    variableType = nameType
                };
            }
            
            startIndex = ShowDefinedVariablesAtBeginning() ? 0 : variables.Length;
            for (int index = 0; index < nodeDef.parameters.Count; ++index)
            {
                var param = nodeDef.parameters[index];

                var variableType = CyanTriggerActionVariableTypeDefinition.VariableInput |
                                   (param.parameterType == UdonNodeParameter.ParameterType.OUT
                                       ? CyanTriggerActionVariableTypeDefinition.VariableOutput
                                       : CyanTriggerActionVariableTypeDefinition.Constant);

                definitions[startIndex + index] = new CyanTriggerActionVariableDefinition
                {
                    type = new CyanTriggerSerializableType(param.type),
                    displayName = param.name,
                    udonName = param.name,
                    variableType = variableType,
                };
            }

            return definitions;
        }
        
        protected virtual string GetVariableName(CyanTriggerAssemblyProgram program, Type type)
        {
            return program.data.CreateVariableName("local_var", type);
        }
        
        public CyanTriggerEditorVariableOption[] GetCustomEditorVariableOptions(
            CyanTriggerAssemblyProgram program,
            CyanTriggerActionVariableInstance[] variableInstances)
        {
            int initialVarCount = GetNodeDefinition().parameters.Count;
            int startIndex = ShowDefinedVariablesAtBeginning() ? initialVarCount : 0;
            (string, Type)[] variables = GetVariables();

            CyanTriggerEditorVariableOption[] options = new CyanTriggerEditorVariableOption[variables.Length];
            int index = 0;
            for (int input = startIndex; index < variables.Length; ++input, ++index)
            {
                string name = (string)variableInstances[input].data.obj;
                string guid = variableInstances[input].variableID;
                Type type = variables[index].Item2;

                if (program != null)
                {
                    // Convert names to unique names
                    name = GetVariableName(program, type);
                }
                options[index] = new CyanTriggerEditorVariableOption
                {
                    ID = guid,
                    Name = name,
                    Type = type,
                };
            }
            return options;
        }

        public override CyanTriggerEditorVariableOption[] GetCustomEditorVariableOptions(
            SerializedProperty inputsProperty)
        {
            int initialVarCount = GetNodeDefinition().parameters.Count;
            int startIndex = ShowDefinedVariablesAtBeginning() ? initialVarCount : 0;
            (string, Type)[] variables = GetVariables();

            CyanTriggerEditorVariableOption[] options = new CyanTriggerEditorVariableOption[variables.Length];
            int index = 0;
            for (int input = startIndex; index < variables.Length; ++input, ++index)
            {
                SerializedProperty inputProperty = inputsProperty.GetArrayElementAtIndex(input);
                SerializedProperty nameDataProperty =
                    inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
                string name = (string) CyanTriggerSerializableObject.ObjectFromSerializedProperty(nameDataProperty);
                
                SerializedProperty idProperty =
                    inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID));
                string guid = idProperty.stringValue;
                
                options[index] = new CyanTriggerEditorVariableOption
                {
                    ID = guid,
                    Name = name,
                    Type = variables[index].Item2,
                };
            }
            return options;
        }

        public override void InitializeVariableProperties(
            SerializedProperty inputsProperty, 
            SerializedProperty multiInputsProperty)
        {
            int initialVarCount = GetNodeDefinition().parameters.Count;
            int startIndex = ShowDefinedVariablesAtBeginning() ? initialVarCount : 0;
            (string, Type)[] variables = GetVariables();
            int index = 0;

            for (int input = startIndex; index < variables.Length; ++input, ++index)
            {
                var variable = variables[index];
                string rawName = variable.Item1 + "_" + CyanTriggerNameHelpers.GetTypeFriendlyName(variable.Item2);
                string varName = CyanTriggerNameHelpers.SanitizeName(rawName);
                
                SerializedProperty inputProperty = inputsProperty.GetArrayElementAtIndex(input);
                SerializedProperty nameDataProperty =
                    inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
                CyanTriggerSerializableObject.UpdateSerializedProperty(nameDataProperty, varName);
                
                SerializedProperty idProperty =
                    inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID));
                idProperty.stringValue = Guid.NewGuid().ToString();
            }

            inputsProperty.serializedObject.ApplyModifiedProperties();
        }

        public void CopyVariableName(SerializedProperty srcInputs, SerializedProperty dstInputs)
        {
            int initialVarCount = GetNodeDefinition().parameters.Count;
            int startIndex = ShowDefinedVariablesAtBeginning() ? initialVarCount : 0;
            (string, Type)[] variables = GetVariables();

            int index = 0;
            for (int input = startIndex; index < variables.Length; ++input, ++index)
            {
                SerializedProperty srcInputProperty = srcInputs.GetArrayElementAtIndex(input);
                SerializedProperty srcNameDataProperty =
                    srcInputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
                
                SerializedProperty dstInputProperty = dstInputs.GetArrayElementAtIndex(input);
                SerializedProperty dstNameDataProperty =
                    dstInputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
                
                CyanTriggerSerializableObject.CopySerializedProperty(srcNameDataProperty, dstNameDataProperty);
            }
        }
        
        protected string GetUserDefinedVariableName(CyanTriggerActionInstance actionInstance, int index)
        {
            int initialVarCount = GetNodeDefinition().parameters.Count;
            int startIndex = ShowDefinedVariablesAtBeginning() ? initialVarCount : 0;
            return (string) actionInstance.inputs[startIndex + index].data.obj;
        }

        protected string GetVariableGuid(CyanTriggerActionInstance actionInstance, int index)
        {
            int initialVarCount = GetNodeDefinition().parameters.Count;
            int startIndex = ShowDefinedVariablesAtBeginning() ? initialVarCount : 0;
            return actionInstance.inputs[startIndex + index].variableID;
        }

        public (int, int) GetDefinitionVariableRange()
        {
            int initialVarCount = GetVariables().Length;
            int startIndex = ShowDefinedVariablesAtBeginning() ? 0 : initialVarCount;
            int len = GetNodeDefinition().parameters.Count;

            return (startIndex, startIndex + len);
        }

        
        public void MigrateTriggerToVersion1(CyanTriggerActionInstance actionInstance)
        {
            int initialVarCount = GetNodeDefinition().parameters.Count;
            bool showAtStart = ShowDefinedVariablesAtBeginning();
            int providedVariablesCount = GetVariables().Length;
            
            CyanTriggerActionVariableInstance[] newInputs =
                new CyanTriggerActionVariableInstance[initialVarCount + providedVariablesCount];

            int definedStart = showAtStart ? 0 : providedVariablesCount;
            int index = 0;
            for (int input = definedStart * 2; index < initialVarCount; ++input, ++index)
            {
                newInputs[definedStart + index] = actionInstance.inputs[input];
            }
            
            int startIndex = showAtStart ? initialVarCount : 0;
            index = 0;
            for (int input = startIndex; index < providedVariablesCount; input += 2, ++index)
            {
                newInputs[startIndex + index] = new CyanTriggerActionVariableInstance()
                {
                    variableID = (string) actionInstance.inputs[input + 1].data.obj,
                    data = actionInstance.inputs[input].data
                };
            }
            actionInstance.inputs = newInputs;
        }
    }
}
