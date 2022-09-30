using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using VRC.Udon;
using System.Reflection;
using VRC.SDKBase;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;

namespace CyanTrigger
{
    public class CyanTriggerAssemblyData
    {
        public const string JumpReturnVariableName = "jump_return_";

        public const string ThisGameObjectGUID = "_this_gameobject";
        public const string ThisGameObjectName = "This GameObject";
        public const string ThisTransformGUID = "_this_transform";
        public const string ThisTransformName = "This Transform";
        public const string ThisCyanTriggerGUID = "_this_cyantrigger";
        public const string ThisCyanTriggerName = "This CyanTrigger";
        public const string ThisUdonBehaviourGUID = "_this_udonbehaviour";
        public const string ThisUdonBehaviourName = "This UdonBehaviour";
        public const string LocalPlayerGUID = "_this_local_player";
        public const string LocalPlayerName = "Local Player";
        
        

        private static readonly Dictionary<string, List<(string, Type)>> EventsToEventVariables = 
            new Dictionary<string, List<(string, Type)>>();
        private static readonly HashSet<string> SpecialVariableNames = new HashSet<string>();

        private readonly Dictionary<string, CyanTriggerAssemblyDataType> _variables = 
            new Dictionary<string, CyanTriggerAssemblyDataType>();
        private readonly Dictionary<Type, Dictionary<object, CyanTriggerAssemblyDataType>> _variableConstants = 
            new Dictionary<Type, Dictionary<object, CyanTriggerAssemblyDataType>>();
        private readonly Dictionary<Type, Queue<CyanTriggerAssemblyDataType>> _tempVariables = 
            new Dictionary<Type, Queue<CyanTriggerAssemblyDataType>>();

        private readonly Dictionary<string, string> _userDefinedVariables = new Dictionary<string, string>();
        
        private readonly List<(CyanTriggerAssemblyDataType, CyanTriggerAssemblyInstruction)> _jumpReturnVariables = 
            new List<(CyanTriggerAssemblyDataType, CyanTriggerAssemblyInstruction)>();

        private readonly Dictionary<Type, CyanTriggerAssemblyDataType> _thisConsts =
            new Dictionary<Type, CyanTriggerAssemblyDataType>();
        
        public enum CyanTriggerSpecialVariableName
        {
            Null,
            ReturnAddress,
            EndAddress,
            ActionJumpAddress,
            TimerQueue,
            ReturnValue,
        }

        private static readonly (string, Type)[] UdonSpecialVariableNames =
        {
            ("const_null", typeof(object)),
            ("uint_return_address", typeof(uint)),
            ("uint_int_end_address", typeof(uint)),
            ("uint_action_jump_address", typeof(uint)),
            ("_timer_queue", typeof(IUdonEventReceiver)),
            (UdonBehaviour.ReturnVariableName, typeof(object)),
        };

        // Unchanging data
        static CyanTriggerAssemblyData()
        {
            foreach (var eventDefinition in CyanTriggerNodeDefinitionManager.GetEventDefinitions())
            {
                List<(string, Type)> eventVariables = eventDefinition.GetEventVariables();
                EventsToEventVariables.Add(eventDefinition.GetEventName(), eventVariables);

                // Add each variable name to special variable names list
                foreach (var variable in eventVariables)
                {
                    SpecialVariableNames.Add(variable.Item1);
                }
            }

            // Add predefined special variable names
            foreach (var special in UdonSpecialVariableNames)
            {
                SpecialVariableNames.Add(special.Item1);
            }
        }
        
        public static void MergeData(CyanTriggerAssemblyData baseData, params CyanTriggerAssemblyData[] dataToMerge)
        {
            foreach (var data in dataToMerge)
            {
                baseData._jumpReturnVariables.AddRange(data._jumpReturnVariables);

                // TODO
                //baseData.userDefinedVariables.

                foreach (var variable in data._variables.Values)
                {
                    if (SpecialVariableNames.Contains(variable.name) && baseData._variables.ContainsKey(variable.name))
                    {
                        continue;
                    }

                    if (baseData._variables.ContainsKey(variable.name))
                    {
                        Debug.LogWarning("Base data already contains variable named " + variable.name);
                        continue;
                    }

                    baseData._variables.Add(variable.name, variable);
                }
            }
        }

        public static List<(string, Type)> GetEventVariableTypes(string eventName)
        {
            EventsToEventVariables.TryGetValue(eventName, out List<(string, Type)> variableData);
            return variableData;
        }
        
        public void AddThisVariables()
        {
            var gameObjectVar = AddNamedVariable(ThisGameObjectGUID, typeof(GameObject), true);
            var transformVar = AddNamedVariable(ThisTransformGUID, typeof(Transform), true);
            var udonVar = AddNamedVariable(ThisUdonBehaviourGUID, typeof(IUdonEventReceiver), true);
            var localPlayer = AddNamedVariable(LocalPlayerGUID, typeof(VRCPlayerApi), false);

            _thisConsts.Add(typeof(GameObject), gameObjectVar);
            _thisConsts.Add(typeof(Transform), transformVar);
            _thisConsts.Add(typeof(IUdonEventReceiver), udonVar);
            _thisConsts.Add(typeof(CyanTrigger), udonVar);
            _thisConsts.Add(typeof(VRCPlayerApi), localPlayer);
        }

        public static bool IsIdThisVariable(string varId)
        {
            return varId.StartsWith("_this_");
        }

        public void CreateSpecialAddressVariables()
        {
            CyanTriggerAssemblyDataType returnAddress = GetSpecialVariable(CyanTriggerSpecialVariableName.ReturnAddress);
            CyanTriggerAssemblyDataType endAddress = GetSpecialVariable(CyanTriggerSpecialVariableName.EndAddress);
            endAddress.defaultValue = 0xFFFFF0u;
        }

        public int GetVariableCount()
        {
            return _variables.Count;
        }

        public string CreateVariableName(string name, Type type)
        {
            return VRC.Udon.Compiler.Compilers.UdonGraphCompiler.INTERNAL_VARIABLE_PREFIX + 
                   _variables.Count + "_" + name + "_" + CyanTriggerNameHelpers.GetSanitizedTypeName(type);
        }
        
        public CyanTriggerAssemblyDataType AddVariable(string name, Type type, bool export, object defaultValue = null)
        {
            string variableName = CreateVariableName(name, type);
            CyanTriggerAssemblyDataType var = new CyanTriggerAssemblyDataType(variableName, type, GetResolvedType(type), export);
            _variables.Add(var.name, var);

            if (defaultValue != null)
            {
                var.defaultValue = defaultValue;
            }

            return var;
        }

        public CyanTriggerAssemblyDataType AddNamedVariable(string name, Type type, bool export = false)
        {
            CyanTriggerAssemblyDataType var = new CyanTriggerAssemblyDataType(name, type, GetResolvedType(type), export);
            _variables.Add(var.name, var);
            return var;
        }

        public void AddUserDefinedVariable(string name, string guid, Type type, CyanTriggerVariableSyncMode sync, bool hasCallback)
        {
            _userDefinedVariables.Add(guid, name);
            CyanTriggerAssemblyDataType var = new CyanTriggerAssemblyDataType(name, type, GetResolvedType(type), true);
            var.sync = sync;
            var.hasCallback = hasCallback;
            var.guid = guid;

            _variables.Add(var.name, var);

            if (hasCallback)
            {
                var.previousVariable = AddPreviousVariable(name, type);
            }
        }
        
        public CyanTriggerAssemblyDataType AddPreviousVariable(string varName, Type type)
        {
            string name = CyanTriggerCustomNodeOnVariableChanged.GetOldVariableName(varName);
            if (_variables.ContainsKey(name))
            {
                return _variables[name];
            }

            // Note that the previous variable should be exported as this is how the initial data is properly set.
            CyanTriggerAssemblyDataType var = new CyanTriggerAssemblyDataType(name, type, GetResolvedType(type), true);
            
            _variables.Add(var.name, var);
            return var;
        }

        public void RemoveUserDefinedVariable(string guid)
        {
            if (!_userDefinedVariables.TryGetValue(guid, out string variableName))
            {
                return;
            }

            _userDefinedVariables.Remove(guid);
            //_variables.Remove(variableName);
        }

        public CyanTriggerAssemblyDataType CreateReferenceVariable(Type type)
        {
            if (type == typeof(CyanTrigger))
            {
                type = typeof(IUdonEventReceiver);
            }
            
            return AddVariable("ref", type, true);
        }

        private string GetResolvedType(Type type)
        {
            // TODO find a better way here... UdonGameObjectComponentHeapReference
            return typeof(UdonBehaviour) == type ? "VRCUdonUdonBehaviour" : CyanTriggerDefinitionResolver.GetTypeSignature(type);
        }


        public CyanTriggerAssemblyDataType GetOrCreateVariableConstant(Type type, object value, bool export = false)
        {
            if (type.IsSubclassOf(typeof(UnityEngine.Object)) || type == typeof(UnityEngine.Object))
            {
                throw new Exception("Cannot create const " + type.Name +" variable as it is a unity object!");
            }
            if (value == null)
            {
                throw new Exception("Cannot create const " + type.Name +" variable for null!");
            }
            
            
            if (!_variableConstants.TryGetValue(type, out Dictionary<object, CyanTriggerAssemblyDataType> objs))
            {
                objs = new Dictionary<object, CyanTriggerAssemblyDataType>();
                _variableConstants.Add(type, objs);
            }
            
            // TODO force initialize
            

            if (!objs.TryGetValue(value, out CyanTriggerAssemblyDataType variable))
            {
                variable = AddVariable("const", type, export, value);
                objs.Add(value, variable);
            }

            return objs[value];
        }

        public CyanTriggerAssemblyDataType GetDefaultVariableConstant(Type type)
        {
            if (type.IsValueType)
            {
                return GetOrCreateVariableConstant(type, Activator.CreateInstance(type));
            }
            
            if (type == typeof(string))
            {
                return GetOrCreateVariableConstant(type, "");
            }
            
            return GetSpecialVariable(CyanTriggerSpecialVariableName.Null); 
        }

        public CyanTriggerAssemblyDataType GetVariableNamed(string name)
        {
            if (_variables.TryGetValue(name, out CyanTriggerAssemblyDataType variable))
            {
                return variable;
            }

            return null;
        }

        public CyanTriggerAssemblyDataType GetThisConst(Type type, string name = null)
        {
            if (type == typeof(UdonBehaviour))
            {
                type = typeof(IUdonEventReceiver);
            }
            
            if (!_thisConsts.TryGetValue(type, out var variable))
            {
                if (name.Equals(ThisGameObjectGUID))
                {
                    return _thisConsts[typeof(GameObject)];
                }
                if (name.Equals(ThisTransformGUID))
                {
                    return _thisConsts[typeof(Transform)];
                }
                if (name.Equals(ThisUdonBehaviourGUID) || name.Equals(ThisCyanTriggerGUID))
                {
                    return _thisConsts[typeof(IUdonEventReceiver)];
                }
                if (name.Equals(LocalPlayerGUID))
                {
                    return _thisConsts[typeof(VRCPlayerApi)];
                }
            }

            if (variable == null)
            {
                Debug.LogError("Could not find const: " + type);
            }
            return variable;
        }
        
        public CyanTriggerAssemblyDataType GetUserDefinedVariable(string guid)
        {
            CyanTriggerAssemblyDataType var;
            if (!_userDefinedVariables.TryGetValue(guid, out string varName))
            {
                // Try using guid as variable name directly
                if (_variables.TryGetValue(guid, out var))
                {
                    return var;
                }
                
                // Try get variable name from guid tag
                varName = CyanTriggerAssemblyDataGuidTags.GetVariableName(guid);
                if (!string.IsNullOrEmpty(varName) && _variables.TryGetValue(varName, out var))
                {
                    return var;
                }
                
                Debug.LogError("GUID does not exist in user defined variables! " + guid);
                return null;
            }

            if (!_variables.TryGetValue(varName, out var))
            {
                Debug.LogError("User variable name is not a defined variable! " + varName);
                return null;
            }

            return var;
        }

        public CyanTriggerAssemblyDataType RequestTempVariable(Type type)
        {
            if (!_tempVariables.TryGetValue(type, out Queue<CyanTriggerAssemblyDataType> tempQueue))
            {
                tempQueue = new Queue<CyanTriggerAssemblyDataType>();
                _tempVariables.Add(type, tempQueue);
            }

            if (tempQueue.Count == 0)
            {
                tempQueue.Enqueue(AddVariable("temp", type, false));
            }

            return tempQueue.Dequeue();
        }

        public void ReleaseTempVariable(CyanTriggerAssemblyDataType var)
        {
            if (!_tempVariables.TryGetValue(var.type, out Queue<CyanTriggerAssemblyDataType> tempQueue))
            {
                tempQueue = new Queue<CyanTriggerAssemblyDataType>();
                _tempVariables.Add(var.type, tempQueue);
            }

            tempQueue.Enqueue(var);
        }

        public CyanTriggerAssemblyDataType GetSpecialVariable(CyanTriggerSpecialVariableName udonSpecialVariableName)
        {
            (string, Type) varPair = UdonSpecialVariableNames[(int)udonSpecialVariableName];
            if (!ContainsName(varPair.Item1))
            {
                var variable = AddNamedVariable(varPair.Item1, varPair.Item2);
                
                // TODO do this in a more generic way...
                if (udonSpecialVariableName == CyanTriggerSpecialVariableName.TimerQueue)
                {
                    variable.export = true;
                }
            }

            return _variables[varPair.Item1];
        }

        public static string GetSpecialVariableName(CyanTriggerSpecialVariableName udonSpecialVariableName)
        {
            return UdonSpecialVariableNames[(int)udonSpecialVariableName].Item1;
        }

        public bool ContainsSpecialVariable(CyanTriggerSpecialVariableName udonSpecialVariableName)
        {
            (string, Type) varPair = UdonSpecialVariableNames[(int)udonSpecialVariableName];
            return ContainsName(varPair.Item1);
        }

        public bool ContainsName(string name)
        {
            return _variables.ContainsKey(name);
        }
        
        public CyanTriggerAssemblyDataType CreateMethodReturnVar(CyanTriggerAssemblyInstruction afterInstruction)
        {
            CyanTriggerAssemblyDataType var = AddVariable(JumpReturnVariableName + _jumpReturnVariables.Count, typeof(uint), false);
            AddJumpReturnVariable(afterInstruction, var);
            return var;
        }
        
        public void AddJumpReturnVariable(CyanTriggerAssemblyInstruction afterInstruction, CyanTriggerAssemblyDataType var)
        {
            _jumpReturnVariables.Add((var, afterInstruction));
        }

        public void FinalizeJumpVariableAddresses()
        {
            foreach (var jumpReturn in _jumpReturnVariables)
            {
                jumpReturn.Item1.defaultValue = jumpReturn.Item2.GetAddressAfterInstruction();
            }
        }

        public string Export()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(".data_start");

            foreach (var variable in _variables)
            {
                if (variable.Value.export)
                {
                    sb.AppendLine("  .export " + variable.Value.name);
                }
                if (variable.Value.sync != CyanTriggerVariableSyncMode.NotSynced)
                {
                    sb.AppendLine("  .sync " + variable.Value.name + ", " + GetSyncExportName(variable.Value.sync));
                }
            }

            foreach (var variable in _variables)
            {
                sb.AppendLine("  " + variable.Value.ToString());
            }

            sb.AppendLine(".data_end");

            return sb.ToString();
        }
        
        public static string GetSyncExportName(CyanTriggerVariableSyncMode sync)
        {
            switch(sync)
            {
                case CyanTriggerVariableSyncMode.NotSynced:
                case CyanTriggerVariableSyncMode.Synced:
                    return "none";
                case CyanTriggerVariableSyncMode.SyncedLinear:
                    return "linear";
                case CyanTriggerVariableSyncMode.SyncedSmooth:
                    return "smooth";
                default:
                    throw new Exception("Unexpected SyncMode " + sync);
            }
        }
        
        public static bool IsSpecialType(Type type)
        {
            return type == typeof(GameObject) || 
                   type == typeof(Transform) || 
                   type == typeof(UdonBehaviour) ||
                   type == typeof(UdonGameObjectComponentHeapReference);
        }

        public void SetVariableDefault(string name, object defaultValue)
        {
            _variables[name].defaultValue = defaultValue;
        }

        public void ApplyAddresses()
        {
            uint address = 0;
            foreach (var variable in _variables)
            {
                CyanTriggerAssemblyDataType var = variable.Value;
                var.address = address;
                address += 4;
            }
        }

        public Dictionary<string, (object value, Type type)> GetHeapDefaultValues()
        {
            Dictionary<string, (object value, Type type)> heapDefaultValues = new Dictionary<string, (object value, Type type)>();
            foreach (var variable in _variables)
            {
                CyanTriggerAssemblyDataType var = variable.Value;
                heapDefaultValues.Add(variable.Value.name, (variable.Value.defaultValue, variable.Value.type));
            }
            
            return heapDefaultValues;
        }

        private static readonly List<CyanTriggerAssemblyDataType> EmptyVariablesList = 
            new List<CyanTriggerAssemblyDataType>();
        public List<CyanTriggerAssemblyDataType> GetEventVariables(string eventName)
        {
            if (!EventsToEventVariables.TryGetValue(eventName, out List<(string, Type)> eventVariablePairs))
            {
                return EmptyVariablesList;
            }
            
            List<CyanTriggerAssemblyDataType> eventVariables = new List<CyanTriggerAssemblyDataType>();
            foreach (var eventVariable in eventVariablePairs)
            {
                CyanTriggerAssemblyDataType variable = GetVariableNamed(eventVariable.Item1);

                if (variable == null)
                {
                    variable = AddNamedVariable(eventVariable.Item1, eventVariable.Item2);
                }
                eventVariables.Add(variable);
            }

            return eventVariables;
        }

        public CyanTriggerItemTranslation[] AddPrefixToAllVariables(string prefix)
        {
            List<CyanTriggerItemTranslation> translations = new List<CyanTriggerItemTranslation>();
            List<CyanTriggerAssemblyDataType> allVariables = new List<CyanTriggerAssemblyDataType>(_variables.Values);
            foreach (var variable in allVariables)
            {
                // TODO skip if user defined? Should not happen in the UdonTrigger merge 
                // flow as only the main program will have user defined variables

                if (SpecialVariableNames.Contains(variable.name) && 
                    variable.name != GetSpecialVariableName(CyanTriggerSpecialVariableName.ActionJumpAddress))
                {
                    // TODO?
                    //translations.Add(new CyanTriggerItemTranslation{ baseName = variable.name, translatedName = variable.name});
                    continue;
                }

                string prevName = variable.name;
                RenameVariable(prefix + "_" + variable.name, variable);
                translations.Add(new CyanTriggerItemTranslation{ BaseName = prevName, TranslatedName = variable.name});

            }

            return translations.ToArray();
        }

        public bool RenameVariable(string newName, CyanTriggerAssemblyDataType variable)
        {
            if (SpecialVariableNames.Contains(variable.name) && 
                variable.name != GetSpecialVariableName(CyanTriggerSpecialVariableName.ActionJumpAddress))
            {
                return false;
            }

            _variables.Remove(variable.name);
            variable.name = newName;
            _variables.Add(variable.name, variable);

            return true;
        }
        
        public CyanTriggerAssemblyData Clone(
            Dictionary<CyanTriggerAssemblyDataType, CyanTriggerAssemblyDataType> variableMapping)
        {
            CyanTriggerAssemblyData data = new CyanTriggerAssemblyData();

            foreach (var variable in _userDefinedVariables)
            {
                data._userDefinedVariables.Add(variable.Key, variable.Value);
            }
            
            foreach (var variablePair in _variables)
            {
                var clone = variablePair.Value.Clone();
                data._variables.Add(variablePair.Key, clone);
                
                variableMapping.Add(variablePair.Value, clone);
            }
            
            foreach (var type in _variableConstants)
            {
                Dictionary<object, CyanTriggerAssemblyDataType> dict =
                    new Dictionary<object, CyanTriggerAssemblyDataType>();
                data._variableConstants.Add(type.Key, dict);
                foreach (var obj in type.Value)
                {
                    dict.Add(obj.Key, data._variables[obj.Value.name]);
                }
            }
            
            foreach (var temp in _tempVariables)
            {
                Queue<CyanTriggerAssemblyDataType> queue = new Queue<CyanTriggerAssemblyDataType>();
                data._tempVariables.Add(temp.Key, queue);
                foreach (var variable in temp.Value)
                {
                    queue.Enqueue(data._variables[variable.name]);
                }
            }

            foreach (var variablePair in _jumpReturnVariables)
            {
                data._jumpReturnVariables.Add((data._variables[variablePair.Item1.name], variablePair.Item2));
            }

            return data;
        }

        public void UpdateJumpInstructions(
            Dictionary<CyanTriggerAssemblyInstruction, CyanTriggerAssemblyInstruction> mapping)
        {
            for (int cur = 0; cur < _jumpReturnVariables.Count; ++cur)
            {
                var pair = _jumpReturnVariables[cur];
                _jumpReturnVariables[cur] = (pair.Item1, mapping[pair.Item2]);
            }
        }
    }

    public static class CyanTriggerAssemblyDataGuidTags
    {
        public const string VariableNameTag = "VariableName";
        public const string VariableIdTag = "VariableId";

        // This is stupidly hacky.
        private const char GuidTagSeparator = ',';
        private const char GuidTagDataSeparator = ':';

        public static string AddVariableGuidTag(string tag, string data, string guid = null)
        {
            string nTag = tag + GuidTagDataSeparator + data;
            if (string.IsNullOrEmpty(guid))
            {
                return nTag;
            }

            return guid + GuidTagSeparator + nTag;
        }

        // TODO optimize?
        public static string GetVariableGuidTag(string guid, string tag)
        {
            foreach (var tagPair in guid.Split(GuidTagSeparator))
            {
                if (tagPair.StartsWith(tag + GuidTagDataSeparator))
                {
                    return tagPair.Substring(tag.Length + 1);
                }
            }

            return null;
        }

        public static string AddVariableIdTag(string id, string guid = null)
        {
            return AddVariableGuidTag(VariableIdTag, id, guid);
        }

        public static string GetVariableId(string guid)
        {
            return GetVariableGuidTag(guid, VariableIdTag);
        }
        
        public static string AddVariableNameTag(string name, string guid = null)
        {
            return AddVariableGuidTag(VariableNameTag, name, guid);
        }

        public static string GetVariableName(string guid)
        {
            return GetVariableGuidTag(guid, VariableNameTag);
        }
    }
}
