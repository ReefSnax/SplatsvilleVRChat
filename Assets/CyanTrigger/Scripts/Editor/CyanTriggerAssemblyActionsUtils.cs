using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Enums;
using VRC.Udon.Common.Interfaces;

namespace CyanTrigger
{
    public static class CyanTriggerAssemblyActionsUtils
    {
        public static readonly List<CyanTriggerAssemblyInstruction> EmptyActions = new List<CyanTriggerAssemblyInstruction>();
        
        
        public static List<CyanTriggerAssemblyInstruction> JumpToFunction(
            CyanTriggerAssemblyProgram triggerProgram, 
            string functionName)
        {
            CyanTriggerAssemblyInstruction jumpAction = CyanTriggerAssemblyInstruction.JumpLabel(functionName);
            CyanTriggerAssemblyDataType jumpReturnVar = triggerProgram.data.CreateMethodReturnVar(jumpAction);
            CyanTriggerAssemblyInstruction setReturnAction = CyanTriggerAssemblyInstruction.PushVariable(jumpReturnVar);

            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();
            actions.Add(setReturnAction);
            actions.Add(jumpAction);

            return actions;
        }
        
        public static List<CyanTriggerAssemblyInstruction> JumpIndirect(
            CyanTriggerAssemblyData data, 
            CyanTriggerAssemblyDataType jumpVariable)
        {
            CyanTriggerAssemblyInstruction jumpAction = CyanTriggerAssemblyInstruction.JumpIndirect(jumpVariable);
            CyanTriggerAssemblyDataType jumpReturnVar = data.CreateMethodReturnVar(jumpAction);
            CyanTriggerAssemblyInstruction setReturnAction = CyanTriggerAssemblyInstruction.PushVariable(jumpReturnVar);

            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();
            actions.Add(setReturnAction);
            actions.Add(jumpAction);

            return actions;
        }
        
        public static List<CyanTriggerAssemblyInstruction> CopyVariables(
            CyanTriggerAssemblyDataType srcVariable,
            CyanTriggerAssemblyDataType dstVariable)
        {
            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();

            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(srcVariable));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(dstVariable));
            actions.Add(CyanTriggerAssemblyInstruction.Copy());
            
            return actions;
        }

        public static List<CyanTriggerAssemblyInstruction> OnVariableChangedCheck(
            CyanTriggerAssemblyProgram program,
            CyanTriggerAssemblyDataType variable)
        {
            if (!variable.hasCallback)
            {
                return EmptyActions;
            }
            
            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();
            
            CyanTriggerAssemblyInstruction nop = CyanTriggerAssemblyInstruction.Nop();
            CyanTriggerAssemblyDataType tempBool = program.data.RequestTempVariable(typeof(bool));
            CyanTriggerAssemblyInstruction pushTempBool = CyanTriggerAssemblyInstruction.PushVariable(tempBool);

            // push prev
            // push cur
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(variable.previousVariable));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(variable));
            
            // push temp bool
            // push comparison
            actions.Add(pushTempBool);
            actions.Add(CyanTriggerAssemblyInstruction.CreateExtern(
                CyanTriggerDefinitionResolver.GetPrimitiveOperationSignature(
                    variable.type.IsValueType ? variable.type : typeof(object), 
                    PrimitiveOperation.Equality)));
            
            // Invert value
            actions.Add(pushTempBool);
            actions.Add(pushTempBool);
            actions.Add(CyanTriggerAssemblyInstruction.CreateExtern(
                CyanTriggerDefinitionResolver.GetPrimitiveOperationSignature(
                    typeof(bool), 
                    PrimitiveOperation.UnaryNegation)));
            
            // push temp bool
            // push jump if false nop
            actions.Add(pushTempBool);
            actions.Add(CyanTriggerAssemblyInstruction.JumpIfFalse(nop));
            
            // call method for variable changed
            // Copying into the old value is handled in the callback itself.
            string methodName = CyanTriggerCustomNodeOnVariableChanged.GetVariableChangeEventName(variable.name);
            actions.AddRange(JumpToFunction(program, methodName));
            
            // push nop
            actions.Add(nop);
            
            program.data.ReleaseTempVariable(tempBool);

            return actions;
        }
        
        public static List<CyanTriggerAssemblyInstruction> SendCustomEvent(
            CyanTriggerAssemblyProgram program,
            UdonBehaviour udonBehaviour, 
            string customEventName)
        {
            CyanTriggerAssemblyDataType udonBehaviourVariable = 
                program.data.GetOrCreateVariableConstant(typeof(UdonBehaviour), udonBehaviour, true);
            CyanTriggerAssemblyDataType methodNameVariable = 
                program.data.GetOrCreateVariableConstant(typeof(string), customEventName);
            return SendCustomEvent(program, udonBehaviourVariable, methodNameVariable);
        }

        public static List<CyanTriggerAssemblyInstruction> SendCustomEvent(
            CyanTriggerAssemblyProgram program,
            CyanTriggerAssemblyDataType udonBehaviourVariable,
            CyanTriggerAssemblyDataType methodNameVariable)
        {
            CyanTriggerAssemblyData data = program.data;
            
            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();
            
            CyanTriggerAssemblyInstruction pushUdonBehaviourAction = 
                CyanTriggerAssemblyInstruction.PushVariable(udonBehaviourVariable);
            actions.Add(pushUdonBehaviourAction);
            
            CyanTriggerAssemblyInstruction pushMethodNameAction = 
                CyanTriggerAssemblyInstruction.PushVariable(methodNameVariable);
            actions.Add(pushMethodNameAction);

            string sendCustomEventName = 
                CyanTriggerDefinitionResolver.GetMethodSignature(
                    typeof(UdonBehaviour).GetMethod(
                        nameof(UdonBehaviour.SendCustomEvent), 
                        new Type[] { typeof(string) }));
            CyanTriggerAssemblyInstruction sendCustomEventExtern = 
                CyanTriggerAssemblyInstruction.CreateExtern(sendCustomEventName);
            actions.Add(sendCustomEventExtern);

            return actions;
        }

        public static List<CyanTriggerAssemblyInstruction> SendNetworkEvent(
            CyanTriggerAssemblyProgram program, 
            string functionName, 
            CyanTriggerAssemblyDataType udonVariable,
            NetworkEventTarget networkTarget = NetworkEventTarget.All)
        {
            Debug.Assert(functionName[0] != '_', "Trying to broadcast to an event that starts with an '_'. " +functionName);
            
            CyanTriggerAssemblyData data = program.data;

            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();

            // Type udonType = udon == null ? typeof(UdonGameObjectComponentHeapReference) : typeof(UdonBehaviour);
            // object value = udon == null 
            //     ? (object)new UdonGameObjectComponentHeapReference(typeof(UdonBehaviour)) 
            //     : (object)udon;
            
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(udonVariable));

            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(
                data.GetOrCreateVariableConstant(typeof(NetworkEventTarget), networkTarget)));

            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(
                data.GetOrCreateVariableConstant(typeof(string), functionName)));

            string networkEventName = CyanTriggerDefinitionResolver.GetMethodSignature(
                typeof(UdonBehaviour).GetMethod(
                    nameof(UdonBehaviour.SendCustomNetworkEvent), 
                    new Type[] { typeof(NetworkEventTarget), typeof(string) }));
            CyanTriggerAssemblyInstruction sendNetworkEventExtern = 
                CyanTriggerAssemblyInstruction.CreateExtern(networkEventName);
            actions.Add(sendNetworkEventExtern);

            return actions;
        }
        
        public static List<CyanTriggerAssemblyInstruction> EventUserGate(
            CyanTriggerAssemblyProgram program, 
            string destMethodName,
            CyanTriggerUserGate userGate, 
            CyanTriggerActionVariableInstance[] userGateExtraData)
        {
            if (userGate == CyanTriggerUserGate.Anyone)
            {
                return JumpToFunction(program, destMethodName);
            }

            CyanTriggerAssemblyData data = program.data;

            List<CyanTriggerAssemblyInstruction> instructions = new List<CyanTriggerAssemblyInstruction>();

            CyanTriggerAssemblyInstruction nop = CyanTriggerAssemblyInstruction.Nop();
            CyanTriggerAssemblyDataType tempBoolVariable = data.RequestTempVariable(typeof(bool));
            CyanTriggerAssemblyInstruction pushTempBoolAction = 
                CyanTriggerAssemblyInstruction.PushVariable(tempBoolVariable);

            if (userGate == CyanTriggerUserGate.Master)
            {
                instructions.Add(pushTempBoolAction);

                string isMasterMethodName = CyanTriggerDefinitionResolver.GetMethodSignature(
                    typeof(Networking).GetProperty(
                        nameof(Networking.IsMaster), 
                        BindingFlags.Static | BindingFlags.Public).GetGetMethod());
                instructions.Add(CyanTriggerAssemblyInstruction.CreateExtern(isMasterMethodName));
                
                // Jump to end if false
                instructions.Add(pushTempBoolAction);
                instructions.Add(CyanTriggerAssemblyInstruction.JumpIfFalse(nop));
            }
            else if (userGate == CyanTriggerUserGate.InstanceOwner)
            {
                instructions.Add(pushTempBoolAction);

                string isInstanceOwnerMethodName = CyanTriggerDefinitionResolver.GetMethodSignature(
                    typeof(Networking).GetProperty(
                        nameof(Networking.IsInstanceOwner), 
                        BindingFlags.Static | BindingFlags.Public).GetGetMethod());
                instructions.Add(CyanTriggerAssemblyInstruction.CreateExtern(isInstanceOwnerMethodName));
                
                // Jump to end if false
                instructions.Add(pushTempBoolAction);
                instructions.Add(CyanTriggerAssemblyInstruction.JumpIfFalse(nop));
            }
            else if (userGate == CyanTriggerUserGate.Owner)
            {
                CyanTriggerAssemblyDataType triggerProgramGameObjectVariable = data.GetThisConst(typeof(GameObject));
                CyanTriggerAssemblyInstruction pushGameObjectAction = 
                    CyanTriggerAssemblyInstruction.PushVariable(triggerProgramGameObjectVariable);
                instructions.Add(pushGameObjectAction);
                instructions.Add(pushTempBoolAction);

                instructions.Add(
                    CyanTriggerAssemblyInstruction.CreateExtern(
                        CyanTriggerDefinitionResolver.GetMethodSignature(
                            typeof(Networking).GetMethod(
                                nameof(Networking.IsOwner), 
                                BindingFlags.Static | BindingFlags.Public, 
                                null, 
                                new Type[] { typeof(GameObject) }, null))));
                
                // Jump to end if false
                instructions.Add(pushTempBoolAction);
                instructions.Add(CyanTriggerAssemblyInstruction.JumpIfFalse(nop));
            }
            else if (userGate == CyanTriggerUserGate.UserAllowList ||
                     userGate == CyanTriggerUserGate.UserDenyList)
            {
                bool allow = userGate == CyanTriggerUserGate.UserAllowList;
                List<CyanTriggerAssemblyDataType> variablesToCheck = new List<CyanTriggerAssemblyDataType>();
                for (int curUser = 0; curUser < userGateExtraData.Length; ++curUser)
                {
                    CyanTriggerAssemblyDataType variable =
                        CyanTriggerCompiler.GetInputDataFromVariableInstance(data, userGateExtraData[curUser],
                            typeof(string));

                    if (variable == null)
                    {
                        continue;
                    }
                    variablesToCheck.Add(variable);
                }
                
                // Nothing, so never allow 
                if (variablesToCheck.Count == 0)
                {
                    // Never allow since there isn't anyone to allow
                    if (allow)
                    {
                        // Jump to end
                        instructions.Add(CyanTriggerAssemblyInstruction.PushVariable(
                            data.GetOrCreateVariableConstant(typeof(bool), false)));
                        instructions.Add(CyanTriggerAssemblyInstruction.JumpIfFalse(nop));
                    }
                    // No one on the deny list, so always pass.
                    else
                    {
                        // No need to do anything here as it will just be true.
                    }
                }
                else
                {
                    CyanTriggerAssemblyInstruction pushLocalPlayer =
                        CyanTriggerAssemblyInstruction.PushVariable(data.GetThisConst(typeof(VRCPlayerApi)));
                    
                    CyanTriggerAssemblyDataType tempStringVariable = data.RequestTempVariable(typeof(string));
                    CyanTriggerAssemblyInstruction pushTempString = 
                        CyanTriggerAssemblyInstruction.PushVariable(tempStringVariable);
                    
                    // push local player
                    // push temp string
                    // extern player api display name
                    instructions.Add(pushLocalPlayer);
                    instructions.Add(pushTempString);
                    instructions.Add(CyanTriggerAssemblyInstruction.CreateExtern(
                        CyanTriggerDefinitionResolver.GetFieldSignature(
                            typeof(VRCPlayerApi).GetField(nameof(VRCPlayerApi.displayName)),
                            FieldOperation.Get)));


                    // For deny lists, if you find a match, jump to the end, skipping the method.
                    CyanTriggerAssemblyInstruction foundMatchNop = nop;
                    if (allow)
                    {
                        // For allow list, if you find a match, jump just before the actions
                        foundMatchNop = CyanTriggerAssemblyInstruction.Nop(); 
                    }
                    
                    foreach (var variable in variablesToCheck)
                    {
                        // push temp string
                        // push string specific
                        // push temp bool
                        // extern string compares
                        instructions.Add(CyanTriggerAssemblyInstruction.PushVariable(variable));
                        instructions.Add(pushTempString);
                        instructions.Add(pushTempBoolAction);
                        instructions.Add(CyanTriggerAssemblyInstruction.CreateExtern(
                            CyanTriggerDefinitionResolver.GetMethodSignature(
                                typeof(string).GetMethod(nameof(string.Equals),
                                    BindingFlags.Static | BindingFlags.Public,
                                    null,
                                    new []{typeof(string), typeof(string)},
                                    null))));

                        // Negate the value since we want false if name is equal
                        instructions.Add(pushTempBoolAction);
                        instructions.Add(pushTempBoolAction);
                        instructions.Add(CyanTriggerAssemblyInstruction.CreateExtern(
                            CyanTriggerDefinitionResolver.GetPrimitiveOperationSignature(
                                typeof(bool), 
                                PrimitiveOperation.UnaryNegation)));
                        
                        // Jump to method if name is equal
                        instructions.Add(pushTempBoolAction);
                        instructions.Add(CyanTriggerAssemblyInstruction.JumpIfFalse(foundMatchNop));
                    }

                    if (allow)
                    {
                        // No matches, jump to end without executing
                        instructions.Add(CyanTriggerAssemblyInstruction.Jump(nop));
                        instructions.Add(foundMatchNop);
                    }
                    
                    data.ReleaseTempVariable(tempStringVariable);
                }
            }

            instructions.AddRange(JumpToFunction(program, destMethodName));

            instructions.Add(nop);

            data.ReleaseTempVariable(tempBoolVariable);
            return instructions;
        }
        
        // network event
        public static List<CyanTriggerAssemblyInstruction> EventBroadcast(
            CyanTriggerAssemblyProgram program,
            string destMethodName,
            CyanTriggerBroadcast broadcast)
        {
            Debug.Assert(destMethodName[0] != '_', "Trying to broadcast to an event that starts with an '_'. " +destMethodName);

            CyanTriggerAssemblyDataType udonVariable = program.data.GetThisConst(typeof(IUdonEventReceiver));
            
            if (broadcast == CyanTriggerBroadcast.All)
            {
                return SendNetworkEvent(program, destMethodName, udonVariable, NetworkEventTarget.All);
            }
            
            if (broadcast == CyanTriggerBroadcast.Owner)
            {
                return SendNetworkEvent(program, destMethodName, udonVariable, NetworkEventTarget.Owner);
            }

            return EmptyActions;
        }

        public static List<CyanTriggerAssemblyInstruction> DelayEvent(
            CyanTriggerAssemblyProgram program,
            string eventName,
            float durationVariable)
        {
            CyanTriggerAssemblyDataType floatVariable =
                program.data.GetOrCreateVariableConstant(typeof(float), durationVariable, false);
            CyanTriggerAssemblyDataType udonVariable = program.data.GetThisConst(typeof(IUdonEventReceiver));
            CyanTriggerAssemblyDataType eventNameVariable = 
                program.data.GetOrCreateVariableConstant(typeof(string), eventName);
            CyanTriggerAssemblyDataType eventTimingVariable = 
                program.data.GetOrCreateVariableConstant(typeof(EventTiming), EventTiming.Update);
            
            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();
            
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(udonVariable));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(eventNameVariable));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(floatVariable));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(eventTimingVariable));

            string sendCustomEventName = 
                CyanTriggerDefinitionResolver.GetMethodSignature(
                    typeof(UdonBehaviour).GetMethod(
                        nameof(UdonBehaviour.SendCustomEventDelayedSeconds)));
            CyanTriggerAssemblyInstruction sendCustomEventExtern = 
                CyanTriggerAssemblyInstruction.CreateExtern(sendCustomEventName);
            actions.Add(sendCustomEventExtern);

            return actions;
        }

        public static List<CyanTriggerAssemblyInstruction> SendToTimerQueue(
            CyanTriggerAssemblyProgram program,
            string eventName, 
            float durationVariable)
        {
            CyanTriggerAssemblyDataType floatVariable =
                program.data.GetOrCreateVariableConstant(typeof(float), durationVariable, false);
            CyanTriggerAssemblyDataType udonVariable = program.data.GetThisConst(typeof(IUdonEventReceiver));
            CyanTriggerAssemblyDataType eventNameVariable = 
                program.data.GetOrCreateVariableConstant(typeof(string), eventName);
            
            return SendToTimerQueue(program, udonVariable, eventNameVariable, floatVariable);
        }
        
        public static List<CyanTriggerAssemblyInstruction> SendToTimerQueue(
            CyanTriggerAssemblyProgram program, 
            CyanTriggerAssemblyDataType udonVariable, 
            CyanTriggerAssemblyDataType eventNameVariable, 
            CyanTriggerAssemblyDataType durationVariable)
        {
            CyanTriggerAssemblyData data = program.data;

            CyanTriggerAssemblyDataType timerQueueVariable = 
                data.GetSpecialVariable(CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.TimerQueue);
            CyanTriggerAssemblyInstruction pushTimerQueue = 
                CyanTriggerAssemblyInstruction.PushVariable(timerQueueVariable);


            CyanTriggerAssemblyDataType udonParamNameVariable = 
                data.GetOrCreateVariableConstant(typeof(string), "EventGraph", false);
            CyanTriggerAssemblyDataType eventParamNameVariable = 
                data.GetOrCreateVariableConstant(typeof(string), "EventName", false);
            CyanTriggerAssemblyDataType durationParamNameVariable = 
                data.GetOrCreateVariableConstant(typeof(string), "EventDuration", false);

            string setProgramVariableMethodName = 
                CyanTriggerDefinitionResolver.GetMethodSignature(
                    typeof(UdonBehaviour).GetMethod(
                        nameof(UdonBehaviour.SetProgramVariable),
                        new Type[] { typeof(string), typeof(object) }));

            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();

            // Push udon graph
            actions.Add(pushTimerQueue);
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(udonParamNameVariable));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(udonVariable));
            actions.Add(CyanTriggerAssemblyInstruction.CreateExtern(setProgramVariableMethodName));

            // Push event name
            actions.Add(pushTimerQueue);
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(eventParamNameVariable));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(eventNameVariable));
            actions.Add(CyanTriggerAssemblyInstruction.CreateExtern(setProgramVariableMethodName));

            // Push duration
            actions.Add(pushTimerQueue);
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(durationParamNameVariable));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(durationVariable));
            actions.Add(CyanTriggerAssemblyInstruction.CreateExtern(setProgramVariableMethodName));

            CyanTriggerAssemblyDataType methodNameVariable = 
                program.data.GetOrCreateVariableConstant(typeof(string), "Add");
            // Call add
            actions.AddRange(SendCustomEvent(program, timerQueueVariable, methodNameVariable));

            return actions;
        }
        
        public static List<CyanTriggerAssemblyInstruction> RemoveFromTimerQueue(
            CyanTriggerAssemblyProgram program, 
            UdonBehaviour udonBehaviour, 
            string eventName)
        {
            CyanTriggerAssemblyData data = program.data;

            CyanTriggerAssemblyDataType timerQueueVariable = 
                data.GetSpecialVariable(CyanTriggerAssemblyData.CyanTriggerSpecialVariableName.TimerQueue);
            CyanTriggerAssemblyInstruction pushTimerQueue = 
                CyanTriggerAssemblyInstruction.PushVariable(timerQueueVariable);

            CyanTriggerAssemblyDataType udonVariable = 
                data.GetOrCreateVariableConstant(typeof(UdonBehaviour), udonBehaviour, true);
            CyanTriggerAssemblyDataType eventNameVariable = data.GetOrCreateVariableConstant(typeof(string), eventName);

            CyanTriggerAssemblyDataType udonParamNameVariable = 
                data.GetOrCreateVariableConstant(typeof(string), "EventGraph", false);
            CyanTriggerAssemblyDataType eventParamNameVariable = 
                data.GetOrCreateVariableConstant(typeof(string), "EventName", false);

            string setProgramVariableMethodName = 
                CyanTriggerDefinitionResolver.GetMethodSignature(
                    typeof(UdonBehaviour).GetMethod(
                        nameof(UdonBehaviour.SetProgramVariable), 
                        new Type[] { typeof(string), typeof(object) }));

            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();

            // Push udon graph
            actions.Add(pushTimerQueue);
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(udonParamNameVariable));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(udonVariable));
            actions.Add(CyanTriggerAssemblyInstruction.CreateExtern(setProgramVariableMethodName));

            // Push event name
            actions.Add(pushTimerQueue);
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(eventParamNameVariable));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(eventNameVariable));
            actions.Add(CyanTriggerAssemblyInstruction.CreateExtern(setProgramVariableMethodName));

            
            CyanTriggerAssemblyDataType methodNameVariable = 
                program.data.GetOrCreateVariableConstant(typeof(string), "Remove");
            // Call remove
            actions.AddRange(SendCustomEvent(program, timerQueueVariable, methodNameVariable));

            return actions;
        }
        

        public static List<CyanTriggerAssemblyInstruction> GetLocalPlayer(CyanTriggerAssemblyProgram program)
        {
            CyanTriggerAssemblyData data = program.data;

            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();

            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(data.GetThisConst(typeof(VRCPlayerApi))));
            actions.Add(CyanTriggerAssemblyInstruction.CreateExtern(
                CyanTriggerDefinitionResolver.GetMethodSignature(
                    typeof(Networking).GetProperty(nameof(Networking.LocalPlayer)).GetGetMethod())));
            
            return actions;
        }

        public static List<CyanTriggerAssemblyInstruction> RequestSerializationVariable(
            CyanTriggerAssemblyProgram program)
        {
            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();

            var thisUdon = program.data.GetThisConst(typeof(IUdonEventReceiver));
            actions.Add(CyanTriggerAssemblyInstruction.PushVariable(thisUdon));
            
            actions.Add(CyanTriggerAssemblyInstruction.CreateExtern(
                CyanTriggerDefinitionResolver.GetMethodSignature(
                    typeof(UdonBehaviour).GetMethod(nameof(UdonBehaviour.RequestSerialization)))));
            
            return actions;
        }
        
        
        #region Debug

        // This should only be used for debug :eyes:
        public static List<CyanTriggerAssemblyInstruction> DebugLog(string message, CyanTriggerAssemblyData data)
        {
            CyanTriggerAssemblyDataType messageVariable = data.GetOrCreateVariableConstant(typeof(string), message);
            CyanTriggerAssemblyInstruction pushMessageAction = CyanTriggerAssemblyInstruction.PushVariable(messageVariable);
            string udonMethodName = CyanTriggerDefinitionResolver.GetMethodSignature(typeof(Debug).GetMethod("Log", new Type[] { typeof(object) }));
            CyanTriggerAssemblyInstruction logExtern = CyanTriggerAssemblyInstruction.CreateExtern(udonMethodName);

            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();
            actions.Add(pushMessageAction);
            actions.Add(logExtern);

            return actions;
        }

        // 
        public static List<CyanTriggerAssemblyInstruction> DebugLogTopHeap()
        {
            string udonMethodName = CyanTriggerDefinitionResolver.GetMethodSignature(typeof(Debug).GetMethod("Log", new Type[] { typeof(object) }));
            CyanTriggerAssemblyInstruction logExtern = CyanTriggerAssemblyInstruction.CreateExtern(udonMethodName);

            List<CyanTriggerAssemblyInstruction> actions = new List<CyanTriggerAssemblyInstruction>();
            actions.Add(logExtern);

            return actions;
        }

        #endregion
    }
}
