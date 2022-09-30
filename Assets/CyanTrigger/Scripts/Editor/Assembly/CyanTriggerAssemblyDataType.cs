using System;

namespace CyanTrigger
{
    public class CyanTriggerAssemblyDataType
    {
        public string name;
        public uint address;
        public Type type;
        public bool export;
        public object defaultValue;
        public CyanTriggerVariableSyncMode sync;
        public bool hasCallback;
        public CyanTriggerAssemblyDataType previousVariable;
        public string guid;

        public string resolvedType;

        public CyanTriggerAssemblyDataType(string name, Type type, string resolvedType, bool export)
        {
            this.name = name;
            this.type = type;
            this.export = export;
            this.resolvedType = resolvedType;
        }
        
        public override string ToString()
        {
            return name + ": %" + resolvedType + ", " + GetDefaultString();
        }

        private string GetDefaultString()
        {
            if (CyanTriggerAssemblyData.IsSpecialType(type))
            {
                return "this";
            }

            return "null";
        }

        public CyanTriggerAssemblyDataType Clone()
        {
            CyanTriggerAssemblyDataType variable = new CyanTriggerAssemblyDataType(name, type, resolvedType, export);

            variable.address = address;
            variable.defaultValue = defaultValue;
            variable.sync = sync;
            variable.hasCallback = hasCallback;
            variable.previousVariable = previousVariable;
            variable.guid = guid;
            
            return variable;
        }
    }
}