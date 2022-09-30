using System;
using System.Collections.Generic;
using UnityEngine;

namespace CyanTrigger
{
    public class CyanTriggerCompiler
    {
        private readonly CyanTriggerDataInstance _cyanTriggerDataInstance;
        private readonly string _triggerHash;
        
        private readonly CyanTriggerAssemblyCode _code;
        private readonly CyanTriggerAssemblyData _data;
        private readonly CyanTriggerAssemblyProgram _program;

        private readonly HashSet<CyanTriggerActionGroupDefinition> _processedActionGroupDefinitions =
            new HashSet<CyanTriggerActionGroupDefinition>();
        private readonly Dictionary<CyanTriggerActionDefinition, CyanTriggerEventTranslation> _actionDefinitionTranslations =
            new Dictionary<CyanTriggerActionDefinition, CyanTriggerEventTranslation>();

        private readonly List<CyanTriggerAssemblyMethod> _cyanTriggerMethods = new List<CyanTriggerAssemblyMethod>();

        private readonly CyanTriggerProgramScopeData _programScopeData = new CyanTriggerProgramScopeData();

        private readonly Dictionary<Vector3Int, CyanTriggerAssemblyDataType> _refVariablesDataCache =
            new Dictionary<Vector3Int, CyanTriggerAssemblyDataType>();

        private readonly CyanTriggerDataReferences _variableReferences;

        // TODO add errors and warnings to udon behaviour
        private readonly List<string> _logWarningMessages = new List<string>();
        private readonly List<string> _logErrorMessages = new List<string>();

        private bool _autoRequestSerialization = false;
        private CyanTriggerAssemblyInstruction _autoRequestSerializationNop;
        
        public static void PreBatchCompile()
        {
            // TODO clear processed programs
        }

        public static void PostBatchCompile()
        {
            // TODO clear processed programs
        }
        
        // TODO cache processed custom programs statically so that work isn't repeated between trigger compiles.
        

        public static bool CompileCyanTrigger(
            CyanTriggerDataInstance trigger,
            CyanTriggerProgramAsset triggerProgramAsset,
            string triggerHash = "")
        {
            // Don't try to compile the default program.
            if (triggerHash == CyanTriggerSerializedProgramManager.DefaultProgramAssetGuid)
            {
                triggerProgramAsset.SetCompiledData(triggerHash, "", null, null, null, null, null);
                return true;
            }
            
            try
            {
                if (trigger == null || trigger.variables == null || trigger.events == null)
                {
                    List<string> errors = new List<string>()
                    {
                        "Failed to compile because trigger, variables, or events were null.",
                    };
                    triggerProgramAsset.SetCompiledData("", "", null, null, null, null, errors);
                    return false;
                }

                if (string.IsNullOrEmpty(triggerHash))
                {
                    triggerHash = CyanTriggerInstanceDataHash.HashCyanTriggerInstanceData(trigger);
                }

                CyanTriggerCompiler compiler = new CyanTriggerCompiler(trigger, triggerHash);
                compiler.ApplyProgram(triggerProgramAsset);

                if (compiler._logErrorMessages.Count > 0)
                {
                    triggerProgramAsset.SetCompiledData("", "", null, null, null, 
                        compiler._logWarningMessages, compiler._logErrorMessages);
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message + "\n" + e.StackTrace);
                List<string> errors = new List<string>()
                {
                    "Failed to compile due to error: " + e.Message,
                };
                triggerProgramAsset.SetCompiledData("", "", null, null, null,null, errors);
                
                return false;
            }
        }

        private CyanTriggerCompiler(CyanTriggerDataInstance trigger, string triggerHash)
        {
            _cyanTriggerDataInstance = trigger;
            _triggerHash = string.IsNullOrEmpty(triggerHash) ? 
                CyanTriggerInstanceDataHash.HashCyanTriggerInstanceData(_cyanTriggerDataInstance) :
                triggerHash;
            
            _code = new CyanTriggerAssemblyCode(trigger.updateOrder);
            _data = new CyanTriggerAssemblyData();
            _program = new CyanTriggerAssemblyProgram(_code, _data);
            _variableReferences = new CyanTriggerDataReferences();

            _autoRequestSerialization = _cyanTriggerDataInstance.programSyncMode ==
                                        CyanTriggerProgramSyncMode.ManualWithAutoRequest;
            _autoRequestSerializationNop = CyanTriggerAssemblyInstruction.Nop();

            // Always create these first.
            _data.CreateSpecialAddressVariables();
            
            _data.AddThisVariables();
            
            AddUserDefinedVariables();

            AddExtraMethods();

            ProcessAllEventsAndActions();
            
            AddCyanTriggerEvents();
            
            Finish();
        }

        private void Finish()
        {
            _program.Finish();
            
            // Automatic RequestSerialization
            if (_autoRequestSerialization)
            {
                var requestActions = CyanTriggerAssemblyActionsUtils.RequestSerializationVariable(_program);
                foreach (CyanTriggerAssemblyMethod method in _cyanTriggerMethods)
                {
                    bool shouldAddRequestSerialization = false;
                    foreach (CyanTriggerAssemblyInstruction instruction in method.actions)
                    {
                        if (instruction == _autoRequestSerializationNop)
                        {
                            shouldAddRequestSerialization = true;
                            break;
                        }
                    }

                    if (shouldAddRequestSerialization)
                    {
                        method.AddActions(requestActions);
                    }
                }
            }
            
            foreach (CyanTriggerAssemblyMethod method in _cyanTriggerMethods)
            {
                method?.PushMethodEndReturnJump(_data);
            }

            try
            {
                _program.ApplyAddresses();
            }
            catch (CyanTriggerAssemblyMethod.MissingJumpLabelException e)
            {
                LogError("Missing Custom Event name: \"" + e.MissingLabel +"\"");
            }
        }

        public void ApplyProgram(CyanTriggerProgramAsset programAsset)
        {
            if (programAsset == null)
            {
                LogError("Cannot apply program for empty program asset");
                return;
            }
            
            // TODO add errors and warnings to program
            
            programAsset.SetCompiledData(
                _triggerHash, 
                _program.Export(), 
                _data.GetHeapDefaultValues(),
                _variableReferences,
                _cyanTriggerDataInstance,
                _logWarningMessages,
                _logErrorMessages);
        }

        private void LogWarning(string warning)
        {
            Debug.LogWarning(warning);
            _logWarningMessages.Add(warning);
        }

        private void LogError(string error)
        {
            Debug.LogError(error);
            _logErrorMessages.Add(error);
        }
        
        private void AddUserDefinedVariables()
        {
            bool allValid = false;
            HashSet<string> variablesWithCallbacks = CyanTriggerCustomNodeOnVariableChanged
                .GetVariablesWithOnChangedCallback(_cyanTriggerDataInstance.events, ref allValid);

            if (!allValid)
            {
                throw new Exception("OnVariableChanged event does not have a selected variable!");
            }
               
            foreach (var variable in _cyanTriggerDataInstance.variables)
            {
                if (string.IsNullOrEmpty(variable.name))
                {
                    LogError("Global Variable with no name!");
                    continue;
                }
                if (char.IsDigit(variable.name[0]))
                {
                    LogError("Global Variable's name starts with a digit! Please ensure the first character is a letter or underscore: "+ variable.name);
                    continue;
                }
                
                bool hasCallback = variablesWithCallbacks.Contains(variable.variableID);
                _data.AddUserDefinedVariable(variable.name, variable.variableID, variable.type.type, variable.sync, hasCallback);
                
                _variableReferences.userVariables.Add(variable.name, variable.type.type);
            }
        }

        private void AddExtraMethods()
        {
            // Get the local player in start
            {
                CyanTriggerAssemblyMethod udonMethod = GetOrAddMethod("_start");
                udonMethod.AddActions(CyanTriggerAssemblyActionsUtils.GetLocalPlayer(_program));
            }
        }

        // TODO figure out instance version of actions/events work
        private void ProcessAllEventsAndActions()
        {
            // Go through all events and actions and merge unique programs in.
            foreach (var trigEvent in _cyanTriggerDataInstance.events)
            {
                var eventType = trigEvent.eventInstance.actionType;
                if (!string.IsNullOrEmpty(eventType.guid) &&
                    CyanTriggerActionGroupDefinitionUtil.TryGetActionGroupDefinition(
                    eventType.guid, out var actionGroupDefinition))
                {
                    ProcessActionDefinition(actionGroupDefinition);
                }

                foreach (var actionInstance in trigEvent.actionInstances)
                {
                    var actionType = actionInstance.actionType;
                    if (!string.IsNullOrEmpty(actionType.guid) &&
                        CyanTriggerActionGroupDefinitionUtil.TryGetActionGroupDefinition(
                        actionType.guid, out actionGroupDefinition))
                    {
                        ProcessActionDefinition(actionGroupDefinition);
                    }
                }
            }
        }

        private CyanTriggerAssemblyMethod GetOrAddMethod(string baseEvent)
        {
            if (_code.GetOrCreateMethod(baseEvent, true, out var method))
            {
                _data.GetEventVariables(baseEvent);
                AddMethod(method);
            }

            return method;
        }
        
        private void AddMethod(CyanTriggerAssemblyMethod method)
        {
            method.PushInitialEndVariable(_data);
            _cyanTriggerMethods.Add(method);
            _code.AddMethod(method);
        }

        private void AddCyanTriggerEvents()
        {
            var events = _cyanTriggerDataInstance.events;
            for (int curEvent = 0; curEvent < events.Length; ++curEvent)
            {
                CyanTriggerEvent trigEvent = events[curEvent];
                CyanTriggerActionInstance eventAction = trigEvent.eventInstance;
                
                // Add event itself to the scope stack. This way local variables can be added properly
                _programScopeData.Clear();
                _programScopeData.ScopeStack.Push(new CyanTriggerScopeFrame(null, eventAction));
                
                // TODO
                // if (!eventAction.active) 
                // {
                //     continue;
                // }

                // Get base action for event
                CyanTriggerAssemblyMethod udonMethod = GetOrCreateMethodForBaseAction(eventAction, trigEvent.name);

                CyanTriggerEventOptions eventOptions = trigEvent.eventOptions;

                // Only add special event gate if it is not anyone gating. 
                CyanTriggerAssemblyMethod gatedMethod = udonMethod;
                if (eventOptions.userGate != CyanTriggerUserGate.Anyone)
                {
                    gatedMethod =
                        new CyanTriggerAssemblyMethod($"__intern_event_{curEvent}_gated", false);
                    AddMethod(gatedMethod);
                
                    // add gate checks
                    udonMethod.AddActions(
                        CyanTriggerAssemblyActionsUtils.EventUserGate(
                            _program, 
                            gatedMethod.name,
                            eventOptions.userGate, 
                            eventOptions.userGateExtraData));
                }
                
                CyanTriggerAssemblyMethod actionsMethod =
                    new CyanTriggerAssemblyMethod($"__intern_event_{curEvent}_actions", false);
                AddMethod(actionsMethod);
                
                // Call to event with call to action method
                CallEventAction(curEvent, eventAction, gatedMethod, actionsMethod);

                // Add network call
                if (eventOptions.broadcast != CyanTriggerBroadcast.Local)
                {
                    CyanTriggerAssemblyMethod networkedActionsMethod =
                        new CyanTriggerAssemblyMethod($"intern_event_{curEvent}_networked_actions", true);
                    AddMethod(networkedActionsMethod);
                    
                    actionsMethod.AddActions(CyanTriggerAssemblyActionsUtils.EventBroadcast(
                        _program,
                        networkedActionsMethod.name,
                        eventOptions.broadcast));
                    
                    actionsMethod = networkedActionsMethod;
                }
                
                // add delay to action method
                if (eventOptions.delay > 0)
                {
                    CyanTriggerAssemblyMethod delayMethod =
                        new CyanTriggerAssemblyMethod($"__intern_event_{curEvent}_delayed_actions", true);
                    AddMethod(delayMethod);
                    
                    actionsMethod.AddActions(CyanTriggerAssemblyActionsUtils.DelayEvent(
                        _program,
                        delayMethod.name,
                        eventOptions.delay));

                    actionsMethod = delayMethod;
                }
                
                // TODO Initialize all temp variables
                
                AddCyanTriggerEventsActionsInList(curEvent, trigEvent.actionInstances, actionsMethod);
            }
        }

        private void AddCyanTriggerEventsActionsInList(
            int eventIndex,
            CyanTriggerActionInstance[] actionInstances, 
            CyanTriggerAssemblyMethod actionMethod)
        {
            for (int curAction = 0; curAction < actionInstances.Length; ++curAction)
            {
                CyanTriggerActionInstance actionInstance = actionInstances[curAction];
                
                // TODO
                // if (!actionInstance.active) 
                // {
                //     continue;
                // }

                var valid = actionInstance.IsValid();
                if (valid != CyanTriggerUtil.InvalidReason.Valid)
                {
                    CyanTriggerActionInfoHolder info = CyanTriggerActionInfoHolder.GetActionInfoHolder(actionInstance.actionType.guid, actionInstance.actionType.directEvent);
                    // TODO give better information
                    LogWarning($"Event[{eventIndex}].Action[{curAction}] {info.GetActionRenderingDisplayName()} is invalid {valid}!");
                    continue;
                }
                
                CallAction(eventIndex, curAction, actionInstance, actionMethod);
            }
        }

        private CyanTriggerAssemblyMethod GetOrCreateMethodForBaseAction(CyanTriggerActionInstance action, string customName)
        {
            var actionType = action.actionType;
            if (CyanTriggerNodeDefinitionManager.TryGetCustomDefinition(actionType.directEvent, out var customDefinition))
            {
                if (customDefinition.GetBaseMethod(_program, action, out var customMethod))
                {
                    AddMethod(customMethod);
                }
                
                return customMethod;
            }
            
            string baseEvent = actionType.directEvent;
            if (!string.IsNullOrEmpty(actionType.guid))
            {
                if (!CyanTriggerActionGroupDefinitionUtil.TryGetActionDefinition(actionType.guid,
                    out CyanTriggerActionDefinition actionDefinition))
                {
                    LogError($"Action Definition GUID is not valid! {actionType.guid}");
                    return null;
                }

                baseEvent = actionDefinition.baseEventName;
                customName = actionDefinition.eventEntry;
            }
            
            CyanTriggerNodeDefinition nodeDefinition = CyanTriggerNodeDefinitionManager.GetDefinition(baseEvent);
            if (nodeDefinition == null)
            {
                LogError("Base event is not a valid event! " + baseEvent);
                return null;
            }

            if (baseEvent == "Event_Custom")
            {
                baseEvent = customName;
                
                if (string.IsNullOrEmpty(customName))
                {
                    LogError("Custom Event with no name!");
                }
                else if (char.IsDigit(customName[0]))
                {
                    LogError("Custom Event's name starts with a digit! Please ensure the first character is a letter or underscore: "+ customName);
                }
            }
            else
            {
                baseEvent = "_" + char.ToLower(baseEvent[6]) + baseEvent.Substring(7);
            }

            return GetOrAddMethod(baseEvent);
        }

        private void CallEventAction(
            int eventIndex,
            CyanTriggerActionInstance actionInstance, 
            CyanTriggerAssemblyMethod eventMethod,
            CyanTriggerAssemblyMethod actionMethod)
        {
            var actionType = actionInstance.actionType;
            if (!string.IsNullOrEmpty(actionType.directEvent))
            {
                HandleDirectActionForEvents(eventIndex, actionInstance, eventMethod, actionMethod);
                return;
            }
            
            if (!CyanTriggerActionGroupDefinitionUtil.TryGetActionDefinition(
                actionType.guid, out CyanTriggerActionDefinition actionDefinition))
            {
                LogError("Action Definition GUID is not valid! " + actionType.guid);
                return;
            }

            var actionTranslation = GetActionTranslation(actionType.guid, actionDefinition);
            if (actionTranslation == null)
            {
                LogError("Action translation is missing! " + actionDefinition.FullName());
                return;
            }

            AddEventJumpToActionVariableCopy(eventMethod, actionMethod, actionTranslation);

            CallAction(eventIndex, -1, actionInstance, eventMethod, actionTranslation, actionDefinition.variables);
        }

        private void CallAction(
            int eventIndex,
            int actionIndex,
            CyanTriggerActionInstance actionInstance,
            CyanTriggerAssemblyMethod actionMethod)
        {
            var actionType = actionInstance.actionType;
            if (!string.IsNullOrEmpty(actionType.directEvent))
            {
                HandleDirectAction(eventIndex, actionIndex, actionInstance, actionMethod);
                return;
            }
            _programScopeData.PreviousScopeDefinition = null;
            
            if (!CyanTriggerActionGroupDefinitionUtil.TryGetActionDefinition(
                actionType.guid, out CyanTriggerActionDefinition actionDefinition))
            {
                LogError("Action Definition GUID is not valid! " + actionType.guid);
                return;
            }

            var actionTranslation = GetActionTranslation(actionType.guid, actionDefinition);
            if (actionTranslation == null)
            {
                LogError("Action translation is missing! " + actionDefinition.FullName());
                return;
            }
            
            CallAction(eventIndex, actionIndex, actionInstance, actionMethod, actionTranslation, actionDefinition.variables);
        }

        private void AddEventJumpToActionVariableCopy(
            CyanTriggerAssemblyMethod eventMethod,
            CyanTriggerAssemblyMethod actionMethod,
            CyanTriggerEventTranslation actionTranslation)
        {
            // Set the action jump method
            var actionJumpLoc = _data.CreateMethodReturnVar(actionMethod.actions[0]);
            eventMethod.AddActions(CyanTriggerAssemblyActionsUtils.CopyVariables(
                actionJumpLoc,
                _data.GetVariableNamed(actionTranslation.ActionJumpVariableName)));
        }

        private void HandleDirectActionForEvents(
            int eventIndex,
            CyanTriggerActionInstance actionInstance, 
            CyanTriggerAssemblyMethod eventMethod,
            CyanTriggerAssemblyMethod actionMethod)
        {
            var actionType = actionInstance.actionType;
            if (CyanTriggerNodeDefinitionManager.TryGetCustomDefinition(actionType.directEvent, out var customDefinition))
            {
                customDefinition.AddEventToProgram(new CyanTriggerCompileState
                {
                    Program = _program,
                    ScopeData = _programScopeData,
                    ActionInstance = actionInstance,
                    EventMethod = eventMethod,
                    ActionMethod = actionMethod,
                    
                    GetDataFromVariableInstance = (multiVarIndex, varIndex, variableInstance, type, output) => 
                        GetDataFromVariableInstance(eventIndex, -1, multiVarIndex, varIndex, variableInstance, type, output),
                    
                    CheckVariableChanged = (method, variablesToCheckChanges) => 
                        CheckVariablesChanged(method, variablesToCheckChanges),
                    
                    LogWarning = LogWarning,
                    LogError = LogError,
                });
                return;
            }
            
            eventMethod.AddActions(CyanTriggerAssemblyActionsUtils.JumpToFunction(_program, actionMethod.name));
        }
        
        private void HandleDirectAction(
            int eventIndex,
            int actionIndex,
            CyanTriggerActionInstance actionInstance,
            CyanTriggerAssemblyMethod actionMethod)
        {
            var actionType = actionInstance.actionType;
            if (CyanTriggerNodeDefinitionManager.TryGetCustomDefinition(actionType.directEvent, out var customDefinition))
            {
                if (customDefinition.CreatesScope())
                {
                    var scopeFrame = new CyanTriggerScopeFrame(customDefinition, actionInstance);
                    _programScopeData.ScopeStack.Push(scopeFrame);
                } 
                
                if (customDefinition is CyanTriggerCustomNodeVariableProvider variableProvider)
                {
                    _programScopeData.AddVariableOptions(_program, actionInstance, variableProvider);
                }

                var compileState = new CyanTriggerCompileState
                {
                    Program = _program,
                    ScopeData = _programScopeData,
                    ActionInstance = actionInstance,
                    ActionMethod = actionMethod,

                    GetDataFromVariableInstance = (multiVarIndex, varIndex, variableInstance, type, output) =>
                        GetDataFromVariableInstance(eventIndex, actionIndex, multiVarIndex, varIndex, variableInstance,
                            type, output),
                    
                    CheckVariableChanged = (method, variablesToCheckChanges) => 
                        CheckVariablesChanged(method, variablesToCheckChanges),
                    
                    LogWarning = LogWarning,
                    LogError = LogError,
                };
                
                customDefinition.AddActionToProgram(compileState);

                // End scope, cleanup stack item
                if (customDefinition is CyanTriggerCustomNodeBlockEnd)
                {
                    var lastScope = _programScopeData.ScopeStack.Peek();
                    compileState.ActionInstance = lastScope.ActionInstance;
                    lastScope.Definition.HandleEndScope(compileState);
                    _programScopeData.PopScope(_program);
                    
                    // TODO verify next definition too? Needed for condition to expect condition body
                }
                
                return;
            }
            _programScopeData.PreviousScopeDefinition = null;
            
            CyanTriggerNodeDefinition nodeDef = CyanTriggerNodeDefinitionManager.GetDefinition(actionType.directEvent);
            if (nodeDef == null)
            {
                LogError("No definition found for action name: "+actionType.directEvent);
                return;
            }
            
            if (nodeDef.variableDefinitions.Length > 0 && 
                (nodeDef.variableDefinitions[0].variableType & CyanTriggerActionVariableTypeDefinition.AllowsMultiple) != 0)
            {
                for (int curInput = 0; curInput < actionInstance.multiInput.Length; ++curInput)
                {
                    HandleDirectActionSingle(eventIndex, actionIndex, curInput, actionInstance, actionMethod, nodeDef.variableDefinitions);
                }
            }
            else
            {
                HandleDirectActionSingle(eventIndex, actionIndex, -1, actionInstance, actionMethod, nodeDef.variableDefinitions);
            }
        }

        private void CallAction(
            int eventIndex,
            int actionIndex,
            CyanTriggerActionInstance actionInstance,
            CyanTriggerAssemblyMethod actionMethod,
            CyanTriggerEventTranslation actionTranslation,
            CyanTriggerActionVariableDefinition[] variableDefinitions)
        {
            if (string.IsNullOrEmpty(actionTranslation.TranslatedAction.TranslatedName))
            {
                LogError($"Event[{eventIndex}].Action[{actionIndex}] Translation name is null");
                return;
            }
            
            if (variableDefinitions.Length > 0 && 
                (variableDefinitions[0].variableType & CyanTriggerActionVariableTypeDefinition.AllowsMultiple) != 0)
            {
                for (int curInput = 0; curInput < actionInstance.multiInput.Length; ++curInput)
                {
                    CallActionSingle(eventIndex, actionIndex, curInput, actionInstance, actionMethod, actionTranslation, variableDefinitions);
                }
            }
            else
            {
                CallActionSingle(eventIndex, actionIndex, -1, actionInstance, actionMethod, actionTranslation, variableDefinitions);
            }
        }

        private void HandleDirectActionSingle(
            int eventIndex,
            int actionIndex,
            int multiVarIndex,
            CyanTriggerActionInstance actionInstance,
            CyanTriggerAssemblyMethod actionMethod,
            CyanTriggerActionVariableDefinition[] variableDefinitions)
        {
            var actionType = actionInstance.actionType;
            for (int curVar = 0; curVar < actionInstance.inputs.Length; ++curVar)
            {
                var def = variableDefinitions[curVar];
                var input = (curVar == 0 && multiVarIndex != -1) ?
                    actionInstance.multiInput[multiVarIndex] :
                    actionInstance.inputs[curVar];

                var variable = GetDataFromVariableInstance(
                    eventIndex, 
                    actionIndex, 
                    multiVarIndex, 
                    curVar, 
                    input,
                    def.type.type,
                    false);
                actionMethod.AddAction(CyanTriggerAssemblyInstruction.PushVariable(variable));
            }

            // TODO Remove now that "Set_" has been created?
            if (actionType.directEvent.StartsWith("Const_"))
            {
                actionMethod.AddAction(CyanTriggerAssemblyInstruction.Copy());
            }
            else
            {
                actionMethod.AddAction(CyanTriggerAssemblyInstruction.CreateExtern(actionType.directEvent));
            }
            
            CheckVariablesChanged(multiVarIndex, actionInstance, actionMethod, variableDefinitions, null);
        }
        
        private void CallActionSingle(
            int eventIndex,
            int actionIndex,
            int multiVarIndex,
            CyanTriggerActionInstance actionInstance,
            CyanTriggerAssemblyMethod actionMethod,
            CyanTriggerEventTranslation actionTranslation,
            CyanTriggerActionVariableDefinition[] variableDefinitions)
        {
            if (string.IsNullOrEmpty(actionTranslation.TranslatedAction.TranslatedName))
            {
                LogError($"Event[{eventIndex}].Action[{actionIndex}] Translation name is null");
                return;
            }
            
            
            // Copy event specific variable data
            foreach (var variable in actionTranslation.EventTranslatedVariables)
            {
                CyanTriggerAssemblyDataType srcVariable = _data.GetVariableNamed(variable.BaseName);
                CyanTriggerAssemblyDataType destVariable = _data.GetVariableNamed(variable.TranslatedName);
                actionMethod.AddActions(CyanTriggerAssemblyActionsUtils.CopyVariables(srcVariable, destVariable));
            }
            
            // Copy user variable data
            for (int curVar = 0; curVar < actionInstance.inputs.Length; ++curVar)
            {
                var def = variableDefinitions[curVar];
                var input = (curVar == 0 && multiVarIndex != -1) ?
                    actionInstance.multiInput[multiVarIndex] :
                    actionInstance.inputs[curVar];
                
                CyanTriggerAssemblyDataType srcVariable = GetDataFromVariableInstance(
                    eventIndex, 
                    actionIndex, 
                    multiVarIndex, 
                    curVar, 
                    input,
                    def.type.type,
                    false);
                
                CyanTriggerAssemblyDataType destVariable =
                    _data.GetVariableNamed(actionTranslation.TranslatedVariables[curVar].TranslatedName);
                actionMethod.AddActions(CyanTriggerAssemblyActionsUtils.CopyVariables(srcVariable, destVariable));
            }
            
            
            // Call method itself
            actionMethod.AddActions(CyanTriggerAssemblyActionsUtils.JumpToFunction(_program,
                actionTranslation.TranslatedAction.TranslatedName));
            

            // Copy saved variables back
            CheckVariablesChanged(multiVarIndex, actionInstance, actionMethod, variableDefinitions, actionTranslation);
        }

        private void CheckVariablesChanged(
            int multiVarIndex,
            CyanTriggerActionInstance actionInstance,
            CyanTriggerAssemblyMethod actionMethod,
            CyanTriggerActionVariableDefinition[] variableDefinitions,
            CyanTriggerEventTranslation actionTranslation = null)
        {
            List<CyanTriggerAssemblyDataType> variablesToCheckChanges = new List<CyanTriggerAssemblyDataType>();
            List<CyanTriggerAssemblyDataType> translationVariables = new List<CyanTriggerAssemblyDataType>();
            for (int curVar = 0; curVar < actionInstance.inputs.Length; ++curVar)
            {
                var def = variableDefinitions[curVar];
                if ((def.variableType & CyanTriggerActionVariableTypeDefinition.VariableOutput) == 0)
                {
                    continue;
                }

                var input = (curVar == 0 && multiVarIndex != -1) ?
                    actionInstance.multiInput[multiVarIndex] :
                    actionInstance.inputs[curVar];
                
                CyanTriggerAssemblyDataType dstVariable = GetOutputDataFromVariableInstance(_data, input);
                if (dstVariable == null)
                {
                    continue;
                }
                
                variablesToCheckChanges.Add(dstVariable);
                
                if (actionTranslation != null)
                {
                    CyanTriggerAssemblyDataType srcVariable =
                        _data.GetVariableNamed(actionTranslation.TranslatedVariables[curVar].TranslatedName);
                    translationVariables.Add(srcVariable);
                }
            }

            CheckVariablesChanged(actionMethod, variablesToCheckChanges, translationVariables);
        }

        private void CheckVariablesChanged(
            CyanTriggerAssemblyMethod actionMethod,
            List<CyanTriggerAssemblyDataType> variablesToCheckChanges,
            List<CyanTriggerAssemblyDataType> translationVariables = null)
        {
            // Ensure copy actions happen before variable changed checks
            if (translationVariables != null && translationVariables.Count == variablesToCheckChanges.Count)
            {
                for (int cur = 0; cur < translationVariables.Count; ++cur)
                {
                    var srcVariable = translationVariables[cur];
                    var dstVariable = variablesToCheckChanges[cur];
                    actionMethod.AddActions(CyanTriggerAssemblyActionsUtils.CopyVariables(srcVariable, dstVariable));
                }
            }

            bool isSynced = false;
            foreach (var dstVariable in variablesToCheckChanges)
            {
                if (dstVariable.sync != CyanTriggerVariableSyncMode.NotSynced)
                {
                    isSynced = true;
                }
                actionMethod.AddActions(CyanTriggerAssemblyActionsUtils.OnVariableChangedCheck(_program, dstVariable));
            }

            if (isSynced && _autoRequestSerialization)
            {
                // Signal that we want to generate code to request serialization at the end of this method.
                actionMethod.AddAction(_autoRequestSerializationNop);
            }
        }

        private CyanTriggerAssemblyDataType GetDataFromVariableInstance(
            int eventIndex,
            int actionIndex,
            int multiVarIndex,
            int varIndex,
            CyanTriggerActionVariableInstance input, 
            Type type, 
            bool outputOnly)
        {
            if (outputOnly)
            {
                var variable = GetOutputDataFromVariableInstance(_program.data, input);
                if (variable == null)
                {
                    variable = _program.data.RequestTempVariable(type);
                    _program.data.ReleaseTempVariable(variable);
                }
                return variable;
            }

            if (input.isVariable)
            {
                return GetInputDataFromVariableInstance(_program.data, input, type);
            }

            bool isMulti = varIndex == 0 && multiVarIndex != -1;
            
            // Is a constant value. Create a reference for this variable so that data
            // is not in the code directly, allowing program reuse.
            
            Vector3Int cacheIndex = new Vector3Int(eventIndex, actionIndex, varIndex);
            // Do not hash multi vars as those will never repeat.
            // Check cache first before creating a new reference
            if (!isMulti && _refVariablesDataCache.TryGetValue(cacheIndex, out var cachedData))
            {
                return cachedData;
            }
            
            // TODO do not pass in the data here. Ensure that public variables are properly updated in the program asset
            var varData = _program.data.CreateReferenceVariable(type);

            // Add variable to the list of exported variables.
            _variableReferences.ActionDataIndices.Add(new CyanTriggerActionDataReferenceIndex
            {
                eventIndex = eventIndex,
                actionIndex = actionIndex,
                multiVariableIndex = isMulti ? multiVarIndex : -1,
                variableIndex = varIndex,
                symbolName = varData.name,
                symbolType = type,
            });
            
            if (!isMulti)
            {
                _refVariablesDataCache.Add(cacheIndex, varData);
            }
                
            return varData;
        }
        
        public static CyanTriggerAssemblyDataType GetInputDataFromVariableInstance(
            CyanTriggerAssemblyData data,
            CyanTriggerActionVariableInstance input, 
            Type type)
        {
            if (!input.isVariable)
            {
                // Try to minimize the usage of this as this is defined in the program itself...
                return data.GetOrCreateVariableConstant(type, input.data.obj, false);
            }

            // These methods should automatically verify if the variable exists.
            if (input.variableID != null && CyanTriggerAssemblyData.IsIdThisVariable(input.variableID))
            {
                return data.GetThisConst(type, input.variableID);
            }
            
            if (!string.IsNullOrEmpty(input.variableID))
            {
                return data.GetUserDefinedVariable(input.variableID);
            }

            if (!string.IsNullOrEmpty(input.name))
            {
                return data.GetVariableNamed(input.name);
            }
            
            // Variable is missing. Provide a temporary one to ignore the data.
            var variable = data.RequestTempVariable(type);
            data.ReleaseTempVariable(variable);
            return variable;
        }
        
        private CyanTriggerAssemblyDataType GetOutputDataFromVariableInstance(
            CyanTriggerAssemblyData data,
            CyanTriggerActionVariableInstance input)
        {
            if (!input.isVariable)
            {
                LogWarning("Trying to copy from a constant value");
                return null;
            }
            if (string.IsNullOrEmpty(input.variableID))
            {
                LogWarning("Output Variable is missing");
                return null;
            }

            if (CyanTriggerAssemblyData.IsIdThisVariable(input.variableID))
            {
                LogWarning("Cannot use this with output variables");
                return null;
            }
            
            // This should automatically verify if the variable exists.
            return data.GetUserDefinedVariable(input.variableID);
        }

        private CyanTriggerEventTranslation GetActionTranslation(
            string actionGuid,
            CyanTriggerActionDefinition actionDefinition)
        {
            if (!CyanTriggerActionGroupDefinitionUtil.TryGetActionGroupDefinition(
                actionGuid, out var actionGroupDefinition))
            {
                return null; 
            }

            ProcessActionDefinition(actionGroupDefinition);
            if (!_actionDefinitionTranslations.TryGetValue(actionDefinition, out var actionTranslation))
            {
                return null;
            }

            return actionTranslation;
        }
        
        private void ProcessActionDefinition(CyanTriggerActionGroupDefinition actionGroupDefinition)
        {
            if (actionGroupDefinition == null)
            {
                return;
            }

            if (_processedActionGroupDefinitions.Contains(actionGroupDefinition))
            {
                return;
            }
            
            CyanTriggerAssemblyProgram program = actionGroupDefinition.GetCyanTriggerAssemblyProgram();
            if (program == null)
            {
                LogWarning("Program is null for action group! " + actionGroupDefinition.name);
                return;
            }

            try
            {
                _processedActionGroupDefinitions.Add(actionGroupDefinition);

                CyanTriggerAssemblyProgram actionProgram = program.Clone();
                CyanTriggerAssemblyProgramUtil.ProcessProgramForCyanTriggers(actionProgram);

                CyanTriggerProgramTranslation programTranslation =
                    CyanTriggerAssemblyProgramUtil.AddNamespace(
                        actionProgram,
                        "__action_group_" + _actionDefinitionTranslations.Count);

                Dictionary<string, CyanTriggerItemTranslation> methodMap =
                    new Dictionary<string, CyanTriggerItemTranslation>();
                Dictionary<string, CyanTriggerItemTranslation> variableMap =
                    new Dictionary<string, CyanTriggerItemTranslation>();

                foreach (var method in programTranslation.TranslatedMethods)
                {
                    methodMap.Add(method.BaseName, method);
                }

                foreach (var variable in programTranslation.TranslatedVariables)
                {
                    variableMap.Add(variable.BaseName, variable);
                }

                _program.MergeProgram(actionProgram);

                foreach (var action in actionGroupDefinition.exposedActions)
                {
                    CyanTriggerEventTranslation eventTranslation = new CyanTriggerEventTranslation();
                    _actionDefinitionTranslations.Add(action, eventTranslation);

                    eventTranslation.TranslatedAction = methodMap[action.eventEntry];
                    eventTranslation.TranslatedVariables = new CyanTriggerItemTranslation[action.variables.Length];

                    for (int cur = 0; cur < action.variables.Length; ++cur)
                    {
                        eventTranslation.TranslatedVariables[cur] = variableMap[action.variables[cur].udonName];
                    }

                    eventTranslation.ActionJumpVariableName = variableMap[
                        CyanTriggerAssemblyData.GetSpecialVariableName(CyanTriggerAssemblyData
                            .CyanTriggerSpecialVariableName.ActionJumpAddress)].TranslatedName;

                    List<CyanTriggerItemTranslation> eventInputTranslation = new List<CyanTriggerItemTranslation>();
                    var eventVariables = CyanTriggerAssemblyData.GetEventVariableTypes(action.baseEventName);
                    if (eventVariables != null)
                    {
                        foreach (var variable in eventVariables)
                        {
                            eventInputTranslation.Add(variableMap[variable.Item1]);
                        }
                    }

                    eventTranslation.EventTranslatedVariables = eventInputTranslation.ToArray();
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to process custom action: " + actionGroupDefinition.name +" " + e);
                _processedActionGroupDefinitions.Remove(actionGroupDefinition);
            }
        }
    }

    public class CyanTriggerScopeFrame
    {
        public CyanTriggerAssemblyInstruction StartNop;
        public CyanTriggerAssemblyInstruction EndNop;
        public readonly CyanTriggerCustomUdonNodeDefinition Definition;
        public readonly CyanTriggerActionInstance ActionInstance;
        public readonly bool IsLoop;
        public readonly List<CyanTriggerEditorVariableOption> ScopeVariables = 
            new List<CyanTriggerEditorVariableOption>();

        public CyanTriggerScopeFrame(
            CyanTriggerCustomUdonNodeDefinition definition,
            CyanTriggerActionInstance actionInstance)
        {
            Definition = definition;
            ActionInstance = actionInstance;

            IsLoop = definition is ICyanTriggerCustomNodeLoop;
        }

        public void AddVariables(
            CyanTriggerAssemblyProgram program,
            CyanTriggerEditorVariableOption[] variableOptions)
        {
            foreach (var variable in variableOptions)
            {
                program.data.AddUserDefinedVariable(
                    variable.Name, 
                    variable.ID,
                    variable.Type, 
                    CyanTriggerVariableSyncMode.NotSynced,
                    false);
                program.data.GetUserDefinedVariable(variable.ID).export = false;
                
                ScopeVariables.Add(variable);
            }
        }
    }

    public class CyanTriggerProgramScopeData
    {
        public readonly Stack<CyanTriggerScopeFrame> ScopeStack = new Stack<CyanTriggerScopeFrame>();
        public CyanTriggerCustomUdonNodeDefinition PreviousScopeDefinition;

        public void Clear()
        {
            ScopeStack.Clear();
            PreviousScopeDefinition = null;
        }
        
        public bool VerifyPreviousScope()
        {
            // TODO
            return true;
        }

        public void AddVariableOptions(
            CyanTriggerAssemblyProgram program, 
            CyanTriggerActionInstance actionInstance,
            CyanTriggerCustomNodeVariableProvider variableProvider)
        {
            var scopeFrame = ScopeStack.Peek();
            var variableOptions = variableProvider.GetCustomEditorVariableOptions(program, actionInstance.inputs);
            scopeFrame.AddVariables(program, variableOptions);
        }

        public CyanTriggerScopeFrame PopScope(CyanTriggerAssemblyProgram program)
        {
            var scopeFrame = ScopeStack.Pop();

            foreach (var variable in scopeFrame.ScopeVariables)
            {
                program.data.RemoveUserDefinedVariable(variable.ID);
            }

            PreviousScopeDefinition = scopeFrame.Definition;
            return scopeFrame;
        }
    }
    
    public class CyanTriggerItemTranslation
    {
        public string BaseName;
        public string TranslatedName;
    }

    public class CyanTriggerEventTranslation
    {
        public string ActionJumpVariableName;
        public CyanTriggerItemTranslation TranslatedAction;
        public CyanTriggerItemTranslation[] TranslatedVariables;
        public CyanTriggerItemTranslation[] EventTranslatedVariables;
    }
    
    public class CyanTriggerProgramTranslation
    {
        public CyanTriggerItemTranslation[] TranslatedMethods;
        public CyanTriggerItemTranslation[] TranslatedVariables;
    }

    public class CyanTriggerProgramTranslated
    {
        public CyanTriggerAssemblyProgram Program;
        public Dictionary<string, CyanTriggerItemTranslation> MethodMap = 
            new Dictionary<string, CyanTriggerItemTranslation>();
        public Dictionary<string, CyanTriggerItemTranslation> VariableMap = 
            new Dictionary<string, CyanTriggerItemTranslation>();
    }
    
    public class CyanTriggerCompileState
    {
        public CyanTriggerAssemblyProgram Program;
        public CyanTriggerProgramScopeData ScopeData;
        public CyanTriggerActionInstance ActionInstance;
        public CyanTriggerAssemblyMethod EventMethod;
        public CyanTriggerAssemblyMethod ActionMethod;

        // multi index, variable index, variable instance, expected type
        public Func<int, int, CyanTriggerActionVariableInstance, Type, bool, CyanTriggerAssemblyDataType> 
            GetDataFromVariableInstance;
        
        public Action<CyanTriggerAssemblyMethod, List<CyanTriggerAssemblyDataType>> CheckVariableChanged;

        public Action<string> LogWarning;
        public Action<string> LogError;
    }
}