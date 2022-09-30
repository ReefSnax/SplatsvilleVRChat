using System;

namespace CyanTrigger
{
    // TODO convert CyanTriggerDataInstance to use Odin serialization and delete SerializableType and SerializableObject
    // This conversion requires reworking all editor interfaces though...

    // This is all the data that should be affect compilation of a CyanTrigger program. 
    // Adding any compilation required data should also be added to these classes:
    // - CyanTriggerUtil.CopyCyanTriggerDataInstance
    // - CyanTriggerInstanceDataHash.GetProgramUniqueStringForCyanTrigger
    [Serializable]
    public class CyanTriggerDataInstance
    {
        public const int DataVersion = 3;

        public int version;
        // public bool addDebugLogsPerAction
        public int updateOrder;
        public CyanTriggerProgramSyncMode programSyncMode;
        
        public CyanTriggerEvent[] events;
        public CyanTriggerVariable[] variables;
        
        // Data that does not affect compilation and is visual only
        public CyanTriggerComment comment;
    }

    [Serializable]
    public class CyanTriggerVariable : CyanTriggerActionVariableInstance
    {
        public CyanTriggerSerializableType type;
        public CyanTriggerVariableSyncMode sync;
    }
    
    [Serializable]
    public class CyanTriggerActionVariableInstance
    {
        public bool isVariable;
        public string name;
        public string variableID;
        public CyanTriggerSerializableObject data;
    }
    
    [Serializable]
    public class CyanTriggerActionInstance
    {
        // Active false means do not generate assembly. Acts like commenting out the code
        // public bool active; // TODO
        
        public CyanTriggerActionType actionType;
        public CyanTriggerActionVariableInstance[] inputs;
        public CyanTriggerActionVariableInstance[] multiInput; // For first input only if it allows multiple
        
        // Data that does not affect compilation and is visual only
        public bool expanded;
        public CyanTriggerComment comment;
    }

    [Serializable]
    public class CyanTriggerActionType
    {
        public string directEvent;
        public string guid;
    }
    
    [Serializable]
    public class CyanTriggerEventOptions
    {
        public CyanTriggerUserGate userGate;
        public CyanTriggerActionVariableInstance[] userGateExtraData;
        public CyanTriggerBroadcast broadcast;   
        public float delay;
        
        // TODO figure out how to add custom input variables for custom triggers?
        // Local variables
        // CyanTriggerVariable[] tempVariables
    }
    
    [Serializable]
    public class CyanTriggerEvent
    {
        // TODO remove name field and use custom trigger's input directly
        public string name;
        public CyanTriggerActionInstance eventInstance;
        public CyanTriggerActionInstance[] actionInstances;

        public CyanTriggerEventOptions eventOptions;
        
        // Data that does not affect compilation and is visual only
        public bool expanded = true;
    }

    // This data isn't used in compilation, but is helpful in creating programs
    [Serializable]
    public class CyanTriggerComment
    {
        public string comment;
    }
}
