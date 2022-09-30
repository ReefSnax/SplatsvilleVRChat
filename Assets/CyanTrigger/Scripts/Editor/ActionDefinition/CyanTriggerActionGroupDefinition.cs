using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using VRC.Udon.Common.Interfaces;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CyanTrigger
{
    [Flags]
    public enum CyanTriggerActionVariableTypeDefinition
    {
        None = 0,
        Constant = 1, // Value will be unchanged
        VariableInput = 1 << 1, // Value allows variables as input
        VariableOutput = 1 << 2, // Value allows variables to be saved as output
        Hidden = 1 << 3, // Variable will be hidden in the inspector and default value will be used
        AllowsMultiple = 1 << 4, // Allows you to have multiple of this variable. Only used for the first variable in the list.
        // Instanced = 1 << 5 // 
        
        // Event temporary? Only available during the event's lifetime

        // instance or persisted (Forced hidden and means that a new variable is created/copied per instance of this action)
        // allows multiple? (Why make this other than to copy previous UI)
    }
    
    [Serializable]
    public class CyanTriggerActionVariableDefinition
    {
        public CyanTriggerSerializableType type;
        public string udonName;
        public string displayName;
        public string description;
        public CyanTriggerSerializableObject defaultValue;
        
        public CyanTriggerActionVariableTypeDefinition variableType;
    }
    
    [Serializable]
    public class CyanTriggerActionDefinition
    {
        public string guid;
        public string actionName;
        public string actionVariantName;
        public string description;

        public CyanTriggerActionVariableDefinition[] variables;

        public string baseEventName;
        public string eventEntry;

        public bool IsValid()
        {
            // TODO
            return false;
        }
        
        public bool IsEvent()
        {
            return baseEventName != "Event_Custom";
        }

        public string FullName()
        {
            return $"{actionName}/{actionVariantName}";
        }

        public string GetMethodName()
        {
            return $"{actionName}.{actionVariantName}";
        }

        public string GetMethodSignature()
        {
            StringBuilder sb = new StringBuilder();

            //sb.Append(GetMethodName());
            sb.Append(actionVariantName);
            sb.Append('(');

            for (int curIn = 0; curIn < variables.Length; ++curIn)
            {
                var variable = variables[curIn];
                if (variable.variableType == CyanTriggerActionVariableTypeDefinition.VariableOutput)
                {
                    sb.Append("out ");
                }
                
                sb.Append(CyanTriggerNameHelpers.GetTypeFriendlyName(variable.type.type));
                if (curIn + 1 < variables.Length)
                {
                    sb.Append(", ");
                }
            }

            sb.Append(')');

            return sb.ToString();
        }
    }
    
   
#if UNITY_EDITOR 
    public abstract class CyanTriggerActionGroupDefinition : ScriptableObject
    {
        public CyanTriggerActionDefinition[] exposedActions = new CyanTriggerActionDefinition[0];

        public abstract CyanTriggerAssemblyProgram GetCyanTriggerAssemblyProgram();

        public virtual void Initialize() { }

        public virtual bool DisplayExtraEditorOptions(SerializedObject obj)
        {
            return true;
        }
        
        public virtual bool IsEditorModifiable()
        {
            return false;
        }

        public bool VerifyProgramActions(IUdonProgram program)
        {
            HashSet<string> entryPoints = new HashSet<string>(program.EntryPoints.GetExportedSymbols());
            
            var symbolTable = program.SymbolTable;
            var symbols = symbolTable.GetExportedSymbols();
            Dictionary<string, Type> variables = new Dictionary<string, Type>();

            for (int cur = 0; cur < symbols.Length; ++cur)
            {
                string symbolName = symbols[cur];
                variables.Add(symbolName, symbolTable.GetSymbolType(symbolName));
            }

            foreach (var action in exposedActions)
            {
                if (!entryPoints.Contains(action.eventEntry))
                {
                    return false;
                }

                foreach (var variable in action.variables)
                {
                    if (!variables.TryGetValue(variable.udonName, out Type type) || variable.type.type != type)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public virtual void AddNewEvent(SerializedProperty eventListProperty)
        {
            AddNewEvent(
                eventListProperty, 
                "name", 
                "variant", 
                "Event_Custom", 
                "name");
        }

        public static SerializedProperty AddNewEvent(
            SerializedProperty eventListProperty,
            string eventName, 
            string eventVariant,
            string baseEvent,
            string entryEvent,
            string guid = "",
            string description = "")
        {
            eventListProperty.arraySize++;
            SerializedProperty newActionProperty = eventListProperty.GetArrayElementAtIndex(eventListProperty.arraySize - 1);
                
            SerializedProperty guidProperty =
                newActionProperty.FindPropertyRelative(nameof(CyanTriggerActionDefinition.guid));
            SerializedProperty nameProperty =
                newActionProperty.FindPropertyRelative(nameof(CyanTriggerActionDefinition.actionName));
            SerializedProperty actionVariantNameProperty =
                newActionProperty.FindPropertyRelative(nameof(CyanTriggerActionDefinition.actionVariantName));
            SerializedProperty baseEventProperty =
                newActionProperty.FindPropertyRelative(nameof(CyanTriggerActionDefinition.baseEventName));
            SerializedProperty entryEventProperty =
                newActionProperty.FindPropertyRelative(nameof(CyanTriggerActionDefinition.eventEntry));
            SerializedProperty descriptionProperty =
                newActionProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableDefinition.description));
            SerializedProperty variablesProperty =
                newActionProperty.FindPropertyRelative(nameof(CyanTriggerActionDefinition.variables));

            // TODO register with manager to get a GUID instead of creating one here
            guidProperty.stringValue = !string.IsNullOrEmpty(guid) ? guid : Guid.NewGuid().ToString();
            nameProperty.stringValue = eventName;
            actionVariantNameProperty.stringValue = eventVariant;
            baseEventProperty.stringValue = baseEvent;
            descriptionProperty.stringValue = description;
            entryEventProperty.stringValue = entryEvent;
            
            // TODO check base event and auto add variable. example: OnTriggerEnter(Collider other)
            variablesProperty.ClearArray();

            eventListProperty.serializedObject.ApplyModifiedProperties();
            
            return newActionProperty;
        }

        public virtual void AddNewVariable(SerializedProperty variableListProperty)
        {
            AddNewVariable(variableListProperty, "variable_name", "Variable Name", typeof(void));
        }

        public static SerializedProperty AddNewVariable(
            SerializedProperty variableListProperty, 
            string variableName, 
            string displayName,
            Type varType,
            string description = "",
            CyanTriggerActionVariableTypeDefinition variableType = 
                CyanTriggerActionVariableTypeDefinition.Constant | CyanTriggerActionVariableTypeDefinition.VariableInput)
        {
            variableListProperty.arraySize++;
            SerializedProperty newVariableProperty = variableListProperty.GetArrayElementAtIndex(variableListProperty.arraySize - 1);
                
            SerializedProperty udonNameProperty =
                newVariableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableDefinition.udonName));
            SerializedProperty displayNameProperty =
                newVariableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableDefinition.displayName));
            SerializedProperty varDescriptionProperty =
                newVariableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableDefinition.description));
            SerializedProperty typeProperty =
                newVariableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableDefinition.type));
            SerializedProperty typeDefProperty =
                typeProperty.FindPropertyRelative(nameof(CyanTriggerSerializableType.typeDef));
            
            SerializedProperty typeOptionsProperty =
                newVariableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableDefinition.variableType));
                

            udonNameProperty.stringValue = variableName;
            displayNameProperty.stringValue = displayName;
            varDescriptionProperty.stringValue = description;
            typeDefProperty.stringValue = varType.AssemblyQualifiedName;
            typeOptionsProperty.intValue = (int) (variableType);

            variableListProperty.serializedObject.ApplyModifiedProperties();

            return newVariableProperty;
        }
    }
#endif
}