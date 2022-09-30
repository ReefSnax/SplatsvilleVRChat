using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Editor.ProgramSources;

namespace CyanTrigger
{
    [CreateAssetMenu(menuName = "VRChat/CyanTrigger/CyanTrigger Custom Udon Action", fileName = "New CyanTrigger Custom Action")]
    public class CyanTriggerActionGroupDefinitionUdonAsset : CyanTriggerActionGroupDefinition
    {
        public UdonProgramAsset udonProgramAsset;

        [SerializeField] 
        private string assetGuid;
        
        private (string, string)[] _eventNames;
        private (Type, string)[] _variables;

        private bool VerifyAsset()
        {
            bool dirty = false;
            
            // The UdonAsset was not properly loaded, but there is still a reference. Unity will say it is null,
            // but the object still exists. Try loading the asset based on the guid and resave the asset.
            if (((object) udonProgramAsset) != null && udonProgramAsset == null && !string.IsNullOrEmpty(assetGuid))
            {
                string path = AssetDatabase.GUIDToAssetPath(assetGuid);
                if (!string.IsNullOrEmpty(path))
                {
                    udonProgramAsset = AssetDatabase.LoadAssetAtPath<UdonProgramAsset>(path);
                    dirty = true;
                }
            }
            
            if (udonProgramAsset)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(udonProgramAsset, out string newGuid, out long _);
                dirty = assetGuid != newGuid;
                assetGuid = newGuid;
            } 
            else if (!string.IsNullOrEmpty(assetGuid))
            {
                assetGuid = "";
                dirty = true;
            }

            return dirty;
        }

        private void VerifyAndSave()
        {
            if (VerifyAsset())
            {
                EditorUtility.SetDirty(this);
                AssetDatabase.SaveAssets();
            }
        }
        
        public override void Initialize()
        {
            VerifyAndSave();
            if (!udonProgramAsset)
            {
                _eventNames = new (string, string)[0];
                _variables = new (Type, string)[0];
                return;
            }

            GetProgramInfo();
        }

        private void GetProgramInfo()
        {
            IUdonProgram program = udonProgramAsset.SerializedProgramAsset.RetrieveProgram();
            var entry = program.EntryPoints.GetExportedSymbols();
            _eventNames = new (string, string)[entry.Length];
            for (int cur = 0; cur < _eventNames.Length; ++cur)
            {
                string eventName = entry[cur];
                string baseEvent = "Event_Custom";
                
                if (!string.IsNullOrEmpty(eventName) && eventName[0] == '_' && eventName.Length > 1)
                {
                    string definitionName = "Event_" + char.ToUpper(eventName[1]) + eventName.Substring(2);
                    CyanTriggerNodeDefinition node = CyanTriggerNodeDefinitionManager.GetDefinition(definitionName);
                    if (node != null)
                    {
                        baseEvent = definitionName;
                    }
                }
                
                _eventNames[cur] = (eventName, baseEvent);
            }

            var symbolTable = program.SymbolTable;
            var symbols = symbolTable.GetExportedSymbols();
            _variables = new (Type, string)[symbols.Length];
            
            for (int cur = 0; cur < _variables.Length; ++cur)
            {
                string symbolName = symbols[cur];
                _variables[cur] = (symbolTable.GetSymbolType(symbolName), symbolName);
            }
        }
        
        public override CyanTriggerAssemblyProgram GetCyanTriggerAssemblyProgram()
        {
            VerifyAndSave();
            if (!udonProgramAsset)
            {
#if CYAN_TRIGGER_DEBUG
                Debug.LogError("ProgramAsset is null: " + name);
#endif
                return null;
            }
            
            IUdonProgram program = udonProgramAsset.SerializedProgramAsset.RetrieveProgram();
            
            // Verify program has actions expected?
            if (!VerifyProgramActions(program))
            {
                udonProgramAsset.RefreshProgram();
                program = udonProgramAsset.SerializedProgramAsset.RetrieveProgram();
                if (!VerifyProgramActions(program))
                {
                    Debug.LogError("CyanTrigger Custom Action Definition is invalid! " + name);
                    return null;
                }
            }
            
            return CyanTriggerAssemblyProgramUtil.CreateProgram(program);
        }

        public override bool DisplayExtraEditorOptions(SerializedObject obj)
        {
            SerializedProperty udonAssetProperty = obj.FindProperty(nameof(udonProgramAsset));

            EditorGUI.BeginChangeCheck();
            
            EditorGUILayout.ObjectField(udonAssetProperty);
            
            obj.ApplyModifiedProperties();
            
            if (EditorGUI.EndChangeCheck())
            {
                Initialize();
            }
            
            //udonAssetProperty.objectReferenceValue
            // If udonProgram is missing, only display that
            return udonProgramAsset;
        }
        
        public override bool IsEditorModifiable()
        {
            return true;
        }

        public override void AddNewEvent(SerializedProperty eventListProperty)
        {
            GenericMenu menu = new GenericMenu();

            for (int cur = 0; cur < _eventNames.Length; ++cur)
            {
                menu.AddItem(new GUIContent(_eventNames[cur].Item1), false, (i) =>
                {
                    int index = (int) i;
                    (string eventName, string baseEvent) = _eventNames[index];
                    SerializedProperty newEvent = 
                        AddNewEvent(eventListProperty, "UserCustom", eventName, baseEvent, eventName);
                    
                    
                    // Pretty sure this is not needed as the variable will be auto
                    // set in compilation stage and not needed here
                    /*
                    var variables = UdonTriggerAssemblyData.GetEventVariableTypes(eventName);
                    if (variables != null && variables.Count > 0)
                    {
                        SerializedProperty variableListProperty =
                            newEvent.FindPropertyRelative(nameof(CyanTriggerActionDefinition.variables));
                        foreach (var (variableName, variableType) in variables)
                        {
                            SerializedProperty varProp = AddNewVariable(variableListProperty, variableName, variableName, variableType);
                            
                        }
                    }
                    */
                    
                }, cur);
            }

            menu.ShowAsContext();
        }

        public override void AddNewVariable(SerializedProperty variableListProperty)
        {
            GenericMenu menu = new GenericMenu();

            HashSet<string> usedvariables = new HashSet<string>();
            for (int cur = 0; cur < variableListProperty.arraySize; ++cur)
            {
                SerializedProperty variable = variableListProperty.GetArrayElementAtIndex(cur);
                SerializedProperty varName =
                    variable.FindPropertyRelative(nameof(CyanTriggerActionVariableDefinition.udonName));
                usedvariables.Add(varName.stringValue);
            }

            for (int cur = 0; cur < _variables.Length; ++cur)
            {
                string varName = _variables[cur].Item2;
                if (usedvariables.Contains(varName))
                {
                    continue;
                }
                
                menu.AddItem(new GUIContent(varName), false, (t) =>
                {
                    int index = (int) t;
                    
                    var variable = _variables[index];
                    var name = variable.Item2;
                    AddNewVariable(variableListProperty, name, name, variable.Item1);
                    
                }, cur);
            }

            menu.ShowAsContext();
        }
    }
}