using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.Udon.Serialization.OdinSerializer;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CyanTrigger
{
    [Serializable]
    public class CyanTriggerSerializableObject : ISerializationCallbackReceiver
    {
        private object _obj;
        public object obj
        {
            get
            {
                if (_obj == null)
                {
                    _obj = DecodeObject(objEncoded, unityObjects);
                }

                return _obj;
            }
            set
            {
                _obj = value;
                objEncoded = EncodeObject(_obj, out unityObjects);
            }
        }
        
        [SerializeField]
        private string objEncoded;
        [SerializeField, HideInInspector]
        private List<UnityEngine.Object> unityObjects;

        public CyanTriggerSerializableObject() {}

        public CyanTriggerSerializableObject(object obj)
        {
            _obj = obj;
        }
        
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        { 
            _obj = DecodeObject(objEncoded, unityObjects);
        }
        
        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            objEncoded = EncodeObject(_obj, out unityObjects);
        }
        
        public static string EncodeObject(object obj, out List<UnityEngine.Object> unityObjects)
        {
            byte[] serializedBytes = SerializationUtility.SerializeValue(obj, DataFormat.Binary, out unityObjects);
            return Convert.ToBase64String(serializedBytes);
        }

        public static object DecodeObject(string objEncoded, List<UnityEngine.Object> unityObjects)
        {
            if (!string.IsNullOrEmpty(objEncoded))
            {
                byte[] serializedBytes = Convert.FromBase64String(objEncoded);
                return SerializationUtility.DeserializeValue<object>(serializedBytes, DataFormat.Binary, unityObjects);
            }

            return null;
        }

#if UNITY_EDITOR
        public static object ObjectFromSerializedProperty(SerializedProperty property)
        {
            SerializedProperty objEncodedProperty = property.FindPropertyRelative(nameof(objEncoded));
            SerializedProperty unityObjectsProperty = property.FindPropertyRelative(nameof(unityObjects));

            List<UnityEngine.Object> objs = new List<Object>();
            for (int cur = 0; cur < unityObjectsProperty.arraySize; ++cur)
            {
                SerializedProperty obj = unityObjectsProperty.GetArrayElementAtIndex(cur);
                objs.Add(obj.objectReferenceValue);
            }
            
            return DecodeObject(objEncodedProperty.stringValue, objs);
        }

        public static void UpdateSerializedProperty(SerializedProperty property, object obj)
        {
            List<UnityEngine.Object> objs;
            string encoded = EncodeObject(obj, out objs);
            
            SerializedProperty objEncodedProperty = property.FindPropertyRelative(nameof(objEncoded));
            SerializedProperty unityObjectsProperty = property.FindPropertyRelative(nameof(unityObjects));

            objEncodedProperty.stringValue = encoded;
            unityObjectsProperty.arraySize = objs == null? 0 : objs.Count;
                
            for (int cur = 0; cur < unityObjectsProperty.arraySize; ++cur)
            {
                SerializedProperty objProp = unityObjectsProperty.GetArrayElementAtIndex(cur);
                objProp.objectReferenceValue = objs[cur];
            }
        }

        public static void CopySerializedProperty(SerializedProperty srcProperty, SerializedProperty dstProperty)
        {
            SerializedProperty srcObjEncodedProperty = srcProperty.FindPropertyRelative(nameof(objEncoded));
            SerializedProperty srcUnityObjectsProperty = srcProperty.FindPropertyRelative(nameof(unityObjects));
            
            SerializedProperty dstObjEncodedProperty = dstProperty.FindPropertyRelative(nameof(objEncoded));
            SerializedProperty dstUnityObjectsProperty = dstProperty.FindPropertyRelative(nameof(unityObjects));

            dstObjEncodedProperty.stringValue = srcObjEncodedProperty.stringValue;
            dstUnityObjectsProperty.arraySize = srcUnityObjectsProperty.arraySize;
            
            for (int cur = 0; cur < srcUnityObjectsProperty.arraySize; ++cur)
            {
                SerializedProperty srcObjProp = srcUnityObjectsProperty.GetArrayElementAtIndex(cur);
                SerializedProperty dstObjProp = dstUnityObjectsProperty.GetArrayElementAtIndex(cur);
                dstObjProp.objectReferenceValue = srcObjProp.objectReferenceValue;
            }
        }
#endif
    }
}