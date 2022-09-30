using System;
using UnityEditor;
using VRC.Udon.Graph;

namespace CyanTrigger
{
    public abstract class CyanTriggerCustomUdonNodeDefinition
    {
        public abstract UdonNodeDefinition GetNodeDefinition();

        public abstract bool GetBaseMethod(
            CyanTriggerAssemblyProgram program,
            CyanTriggerActionInstance actionInstance,
            out CyanTriggerAssemblyMethod method);
        
        public abstract void AddEventToProgram(CyanTriggerCompileState compileState);
        public abstract void AddActionToProgram(CyanTriggerCompileState compileState);

        // TODO move to custom inspector related class?
        public virtual CyanTriggerActionVariableDefinition[] GetCustomVariableSettings()
        {
            throw new NotImplementedException();
        }
        
        public virtual bool HasCustomVariableSettings()
        {
            return false;
        }

        public virtual bool CreatesScope()
        {
            return false;
        }
        
        public virtual void HandleEndScope(CyanTriggerCompileState compileState)
        {
            throw new NotImplementedException();
        }
        
        
        
        // TODO custom inspectors?

        protected void AddDefaultEventToProgram(
            CyanTriggerAssemblyProgram program,
            CyanTriggerAssemblyMethod eventMethod,
            CyanTriggerAssemblyMethod actionMethod)
        {
            eventMethod.AddActions(CyanTriggerAssemblyActionsUtils.JumpToFunction(program, actionMethod.name));
        }


        public virtual bool HasDependencyNodes()
        {
            return false;
        }

        public virtual UdonNodeDefinition[] GetDependentNodes()
        {
            throw new NotImplementedException();
        }

        public virtual bool HasCustomVariableInitialization()
        {
            return false;
        }
        
        public virtual void InitializeVariableProperties(
            SerializedProperty inputProperties, 
            SerializedProperty multiInputProperties)
        {
            throw new NotImplementedException();
        }

        public virtual bool DefinesCustomEditorVariableOptions()
        {
            return false;
        }

        public virtual CyanTriggerEditorVariableOption[] GetCustomEditorVariableOptions(SerializedProperty variableProperties)
        {
            throw new NotImplementedException();
        }
    }
}
