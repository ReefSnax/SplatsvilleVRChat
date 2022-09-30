
namespace CyanTrigger
{
    public enum CyanTriggerVariableSyncMode
    {
        NotSynced = 0,
        Synced = 1,
        SyncedLinear = 2,
        SyncedSmooth = 3,
    }
    
    public enum CyanTriggerUserGate
    {
        Anyone = 0,
        Owner = 1,
        Master = 2,
        UserAllowList = 3,
        UserDenyList = 4,
        InstanceOwner = 5,
    }

    public enum CyanTriggerBroadcast
    {
        Local = 0,
        Owner = 1,
        All = 2,
        
        // TODO research buffering using the networking patch.
        // AllBufferOne,
        // AllBuffered
    }
    
    public enum CyanTriggerProgramSyncMode
    { 
        Continuous = 0,
        Manual = 1,
        ManualWithAutoRequest = 2,
        
        // TODO
        // ManualWithPeriodicRequest = 3
    }
}
