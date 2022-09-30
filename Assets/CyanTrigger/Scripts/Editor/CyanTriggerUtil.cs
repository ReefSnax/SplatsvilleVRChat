using System;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

namespace CyanTrigger
{
    public static class CyanTriggerUtil
    {
        public enum InvalidReason
        {
            Valid,
            IsNull,
            InvalidDefinition,
            InvalidInput,
            MissingVariable,
            DataIsNull,
            InputTypeMismatch,
            InputLengthMismatch,
        }

        public static bool ValidateVariables(this CyanTriggerActionInstance actionInstance)
        {
            if (actionInstance == null)
            {
                return false;
            }
            var actionType = actionInstance.actionType;
            var actionInfoHolder = CyanTriggerActionInfoHolder.GetActionInfoHolder(
                actionType.guid,
                actionType.directEvent);
            if (!actionInfoHolder.IsValid())
            {
                return false;
            }

            bool changed = false;
            var variables = actionInfoHolder.GetVariables();

            if (actionInstance.inputs == null)
            {
                actionInstance.inputs = new CyanTriggerActionVariableInstance[variables.Length];
                changed = true;
            }
            else if (variables.Length != actionInstance.inputs.Length)
            {
                changed = true;
                Array.Resize(ref actionInstance.inputs, variables.Length); 
            }

            return changed;
        }
        
        // TODO return reason for being invalid instead of simply true/false
        public static InvalidReason IsValid(this CyanTriggerActionInstance actionInstance)
        {
            if (actionInstance == null)
            {
                return InvalidReason.IsNull;
            }
            
            var actionType = actionInstance.actionType;
            var actionInfoHolder = CyanTriggerActionInfoHolder.GetActionInfoHolder(
                actionType.guid,
                actionType.directEvent);
            if (!actionInfoHolder.IsValid())
            {
                return InvalidReason.InvalidDefinition;
            }

            var variables = actionInfoHolder.GetVariables();

            if (variables.Length != actionInstance.inputs.Length)
            {
#if CYAN_TRIGGER_DEBUG
                Debug.LogWarning("Input length did not equal variable def length. This shouldn't happen.");
#endif
                return InvalidReason.InputLengthMismatch;
            }
            
            bool firstAllowsMulti = 
                variables.Length > 0 &&
                (variables[0].variableType & CyanTriggerActionVariableTypeDefinition.AllowsMultiple) != 0;
            if (firstAllowsMulti && actionInstance.multiInput.Length == 0)
            {
                return InvalidReason.InvalidInput;
            }
            
            // TODO verify inputs match definition
            for (int input = 0; input < variables.Length; ++input)
            {
                if (input == 0 && firstAllowsMulti)
                {
                    if (actionInstance.multiInput.Length == 0)
                    {
                        return InvalidReason.InvalidInput;
                    }
                    
                    foreach (var variable in actionInstance.multiInput)
                    {
                        var reason = variable.IsValid(variables[input]);
                        if (reason != InvalidReason.Valid)
                        {
                            Debug.LogWarning("Invalid: " + reason);
                            return InvalidReason.InvalidInput;
                        }
                    }
                    continue;
                }
                
                if ((variables[input].variableType & CyanTriggerActionVariableTypeDefinition.AllowsMultiple) != 0)
                {
                    return InvalidReason.InvalidDefinition;
                }

                if (actionInstance.inputs[input].IsValid(variables[input]) != InvalidReason.Valid)
                {
                    return InvalidReason.InvalidInput;
                }
            }

            return InvalidReason.Valid;
        }

        public static InvalidReason IsValid(
            this CyanTriggerActionVariableInstance variableInstance, 
            CyanTriggerActionVariableDefinition variableDef = null)
        {
            if (variableInstance == null)
            {
                return InvalidReason.IsNull;
            }
            
            // TODO do we even need this check? If there is no variable, it's not an error, it just ignores setting the output.
            // if (variableInstance.isVariable)
            // {
            //     if (string.IsNullOrEmpty(variableInstance.name) &&
            //         string.IsNullOrEmpty(variableInstance.variableID))
            //     {
            //         return InvalidReason.MissingVariable;
            //     }
            //     
            //     // TODO verify variable options are valid given available variables
            // }
            //else // Constant object
            {
                // This doesn't appear to work on reload...
                
                // Object is null or deleted
                // if (variableInstance.data.obj != null && ( 
                //     (variableInstance.data.obj.GetType().IsSubclassOf(typeof(Component)) && 
                //     (variableInstance.data.obj as Component) == null) ||
                //     variableInstance.data.obj is GameObject otherGameObject && otherGameObject == null))
                // {
                //     Debug.Log(variableInstance.data.obj);
                //     return InvalidReason.DataIsNull;
                // }
                
                
                // Object stored does not match type in definition
                // if (variableDef != null && 
                //     variableInstance.data.obj != null && 
                //     !variableDef.type.type.IsInstanceOfType(variableInstance.data.obj))
                // {
                //     Debug.Log("Type is wrong? " + variableInstance.data.obj.GetType() +" -> " + variableDef.type.type);
                //     return InvalidReason.InputTypeMismatch;
                // }
            }
            
            // TODO Check other cases
            
            return InvalidReason.Valid;
        }

        /*
        public static bool IsValidActionInstance(SerializedProperty actionProperty)
        {
            if (!CyanTriggerActionInfoHolder.GetActionInfoHolderFromProperties(actionProperty).IsValid())
            {
                return false;
            }
            
            SerializedProperty inputProperty =
                actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.inputs));
            SerializedProperty multiInputProperty =
                actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.multiInput));
            
            // TODO verify inputs match definition
            for (int input = 0; input < inputProperty.arraySize; ++input)
            {
                if (input == 0 && multiInputProperty.arraySize > 0)
                {
                    for (int multiInput = 0; multiInput < inputProperty.arraySize; ++multiInput)
                    {
                        SerializedProperty multiVarProperty = inputProperty.GetArrayElementAtIndex(multiInput);
                        if (!IsValidActionVariableInstance(multiVarProperty))
                        {
                            return false;
                        }
                    }
                    continue;
                }

                SerializedProperty varProperty = inputProperty.GetArrayElementAtIndex(input);
                if (!IsValidActionVariableInstance(varProperty))
                {
                    return false;
                }
            }
            
            return true;
        }
        
        public static bool IsValidActionVariableInstance(SerializedProperty variableProp)
        {
            SerializedProperty isVariableProperty =
                variableProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
            SerializedProperty idProperty =
                variableProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID));
            SerializedProperty nameProperty =
                variableProp.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.name));

            if (isVariableProperty.boolValue && 
                string.IsNullOrEmpty(idProperty.stringValue) &&
                string.IsNullOrEmpty(nameProperty.stringValue))
            {
                return false;
            }
            
            // TODO other?
            
            return true;
        }
        */

        public static bool HasSyncedVariables(this CyanTriggerDataInstance data)
        {
            foreach (var variable in data.variables)
            {
                if (variable.sync != CyanTriggerVariableSyncMode.NotSynced)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool HasNetworkedEvents(this CyanTriggerDataInstance data)
        {
            foreach (var eventInst in data.events)
            {
                if (eventInst.eventOptions.broadcast != CyanTriggerBroadcast.Local)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool GameObjectRequiresContinuousSync(UdonBehaviour thisUdon)
        {
            UdonBehaviour[] behaviours = thisUdon.GetComponents<UdonBehaviour>();
            VRCObjectSync[] syncs = thisUdon.GetComponents<VRCObjectSync>();

            bool positionSynced = syncs.Length > 0;
            foreach (var udon in behaviours)
            {
#pragma warning disable 618
                positionSynced |= udon.SynchronizePosition;
#pragma warning restore 618
            }

            return positionSynced;
        }

        public static bool GameObjectRequiresNetworking(CyanTriggerSerializableInstance instance)
        {
            return true;
            // TODO Verify all instances of when something shouldn't be set to SyncModeNone
            // How does Ownership get handled?
            
            // return instance.triggerDataInstance.HasNetworkedEvents();
        }
        
        
        // TODO remove when dropping 2018 support.
#if UNITY_2018
        // Check if there are any continuous synced udon behaviours on the object.
        public static bool GetObjectSyncMethod(UdonBehaviour thisUdon)
        {
            bool manual = true;
            UdonBehaviour[] behaviours = thisUdon.GetComponents<UdonBehaviour>();
            foreach (var udon in behaviours)
            {
                if (udon == thisUdon)
                {
                    continue;
                }

                manual &= udon.Reliable;
            }

            return manual;
        }
        
        // Currently returns if this CyanTrigger should be Reliable (manual) sync.
        public static bool GetSyncMode(CyanTriggerSerializableInstance instance)
        {
            // If the CyanTrigger has any synced variables, then use the Sync Method set by the user.
            if (instance.triggerDataInstance.HasSyncedVariables())
            {
                return instance.triggerDataInstance.programSyncMode != CyanTriggerProgramSyncMode.Continuous;
            }

            // CyanTrigger doesn't care what sync method the udon behaviour is set to since there are no synced
            // variables. Determine the best option.
            
            return !GameObjectRequiresContinuousSync(instance.udonBehaviour) 
                   && GetObjectSyncMethod(instance.udonBehaviour);
        }
#elif UNITY_2019
        // Check if there are any udon behaviours on the object and return their sync method, assuming this the
        // object's sync method.
        public static Networking.SyncType GetObjectSyncMethod(UdonBehaviour thisUdon)
        {
            UdonBehaviour[] behaviours = thisUdon.GetComponents<UdonBehaviour>();
            foreach (var udon in behaviours)
            {
                if (udon == thisUdon)
                {
                    continue;
                }

                return udon.SyncMethod;
            }

            return Networking.SyncType.Unknown;
        }

        public static Networking.SyncType GetSyncMode(CyanTriggerSerializableInstance instance)
        {
            // If the CyanTrigger has any synced variables, then use the Sync Method set by the user.
            if (instance.triggerDataInstance.HasSyncedVariables())
            {
                return instance.triggerDataInstance.programSyncMode == CyanTriggerProgramSyncMode.Continuous 
                    ? Networking.SyncType.Continuous 
                    : Networking.SyncType.Manual;
            }
            
            // CyanTrigger doesn't care what sync method the udon behaviour is set to since there are no synced
            // variables. Determine the best option.

            UdonBehaviour thisUdon = instance.udonBehaviour;
            
            // Sync method already set by other udon behaviours, use that instead.
            Networking.SyncType syncMethod = GetObjectSyncMethod(thisUdon);
            if (syncMethod != Networking.SyncType.Unknown)
            {
                return syncMethod;
            }

            // This object is position synced, and is required to be continuous.
            if (GameObjectRequiresContinuousSync(thisUdon))
            {
                return Networking.SyncType.Continuous;
            }

            if (GameObjectRequiresNetworking(instance))
            {
                return Networking.SyncType.Manual;
            }
            
            // TODO SyncType.None currently breaks initialization
            // https://vrchat.canny.io/vrchat-udon-closed-alpha-bugs/p/1132-start-and-update-not-called-on-enabled-udonbehaviours-with-syncmode-none
            return Networking.SyncType.None;
        }
#endif
        

        public static CyanTriggerDataInstance CopyCyanTriggerDataInstance(CyanTriggerDataInstance data, bool copyData)
        {
            if (data == null)
            {
                return null;
            }
            
            CyanTriggerDataInstance ret = new CyanTriggerDataInstance()
            {
                version = data.version,
                events = new CyanTriggerEvent[data.events.Length],
                variables = new CyanTriggerVariable[data.variables.Length],
                programSyncMode = data.programSyncMode,
                updateOrder = data.updateOrder
            };

            for (int cur = 0; cur < ret.events.Length; ++cur)
            {
                ret.events[cur] = CopyEvent(data.events[cur], copyData);
            }
            
            for (int cur = 0; cur < ret.variables.Length; ++cur)
            {
                ret.variables[cur] = CopyVariable(data.variables[cur], copyData);
            }            
            
            return ret;
        }
        
        public static CyanTriggerVariable CopyVariable(CyanTriggerVariable variable, bool copyData)
        {
            CyanTriggerVariable ret = new CyanTriggerVariable
            {
                name = variable.name,
                sync = variable.sync,
                isVariable = variable.isVariable,
                variableID = variable.variableID,
                type = new CyanTriggerSerializableType(variable.type.type),
            };

            if (copyData)
            {
                ret.data = new CyanTriggerSerializableObject(variable.data.obj);
            }

            return ret;
        }
                
        public static CyanTriggerActionVariableInstance CopyVariableInst(
            CyanTriggerActionVariableInstance variable, bool copyData)
        {
            CyanTriggerActionVariableInstance ret = new CyanTriggerActionVariableInstance
            {
                name = variable.name,
                isVariable = variable.isVariable,
                variableID = variable.variableID,
            };

            // Some values are used in the program and are needed in compilation...
            // CyanTrigger.ActivateCustomTrigger requires string data
            // TODO eventually move those to another field
            object data = variable.data.obj;
            if (copyData || (data != null && !(data is UnityEngine.Object)))
            {
                ret.data = new CyanTriggerSerializableObject(data);
            }
            
            return ret;
        }

        public static CyanTriggerActionType CopyActionType(CyanTriggerActionType actionType)
        {
            return new CyanTriggerActionType
            {
                guid = actionType.guid,
                directEvent = actionType.directEvent,
            };
        }
        
        public static CyanTriggerActionInstance CopyActionInst(CyanTriggerActionInstance action, bool copyData)
        {
            var ret = new CyanTriggerActionInstance
            {
                actionType = CopyActionType(action.actionType),
                inputs = new CyanTriggerActionVariableInstance[action.inputs.Length],
                multiInput = new CyanTriggerActionVariableInstance[action.multiInput.Length],
            };

            for (int cur = 0; cur < ret.inputs.Length; ++cur)
            {
                ret.inputs[cur] = CopyVariableInst(action.inputs[cur], copyData);
            }
            for (int cur = 0; cur < ret.multiInput.Length; ++cur)
            {
                ret.multiInput[cur] = CopyVariableInst(action.multiInput[cur], copyData);
            }

            return ret;
        }

        public static CyanTriggerEventOptions CopyEventOptions(CyanTriggerEventOptions eventOptions)
        {
            var ret = new CyanTriggerEventOptions
            {
                broadcast = eventOptions.broadcast,
                delay = eventOptions.delay,
                userGate = eventOptions.userGate,
                userGateExtraData =
                    new CyanTriggerActionVariableInstance[eventOptions.userGateExtraData.Length],
            };
            
            for (int cur = 0; cur < ret.userGateExtraData.Length; ++cur)
            {
                // TODO Update copy data once it becomes a reference instead of const in the program.
                ret.userGateExtraData[cur] = CopyVariableInst(eventOptions.userGateExtraData[cur], true);
            }
            
            return ret;
        }

        public static CyanTriggerEvent CopyEvent(CyanTriggerEvent oldEvent, bool copyData)
        {
            var ret = new CyanTriggerEvent
            {
                name = oldEvent.name,
                actionInstances = new CyanTriggerActionInstance[oldEvent.actionInstances.Length],
                eventInstance = CopyActionInst(oldEvent.eventInstance, copyData),
                eventOptions = CopyEventOptions(oldEvent.eventOptions),
            };

            for (int cur = 0; cur < ret.actionInstances.Length; ++cur)
            {
                ret.actionInstances[cur] = CopyActionInst(oldEvent.actionInstances[cur], copyData);
            }
            
            return ret;
        }
    }
}
