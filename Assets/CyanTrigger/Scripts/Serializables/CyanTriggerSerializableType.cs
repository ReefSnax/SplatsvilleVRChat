using System;
using UnityEngine;

namespace CyanTrigger
{
    [Serializable]
    public class CyanTriggerSerializableType : ISerializationCallbackReceiver
    {
        public string typeDef; 
        private Type _type;

        public CyanTriggerSerializableType() {}

        public CyanTriggerSerializableType(Type type)
        {
            _type = type;
            typeDef = type.AssemblyQualifiedName;
        }

        public Type type
        {
            get
            {
                DeserializeType();
                return _type;
            }
        }

        private void DeserializeType()
        {
            if (!string.IsNullOrEmpty(typeDef))
            {
                _type = GetTypeFromDef();
            }
        }

        private Type GetTypeFromDef()
        {
            return Type.GetType(typeDef);
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            DeserializeType();
        }
        
        void ISerializationCallbackReceiver.OnBeforeSerialize() { }
    }
}
