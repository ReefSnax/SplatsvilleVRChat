using System;
using System.Collections.Generic;
using System.Linq;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using Object = UnityEngine.Object;

namespace CyanTrigger
{
    public static class CyanTriggerPropertyEditor
    {
        private const float FoldoutListHeaderHeight = 16;
        private const float FoldoutListHeaderAreaHeight = 19;
        
        private static GUIStyle _footerButtonStyle;
        private static GUIStyle _footerBackgroundStyle;
        private static GUIStyle _headerBackgroundStyle;

        public static void DrawEditor(SerializedProperty dataProperty, Rect rect, GUIContent variableName, Type type, bool layout = false)
        {
            //EditorGUI.BeginProperty(rect, GUIContent.none, dataProperty);
            object obj = CyanTriggerSerializableObject.ObjectFromSerializedProperty(dataProperty);
            bool dirty = false;
            obj = DisplayPropertyEditor(rect, variableName, type, obj, ref dirty, layout);

            if (dirty)
            {
                CyanTriggerSerializableObject.UpdateSerializedProperty(dataProperty, obj);
            }
            //EditorGUI.EndProperty();
        }

        public static void DrawArrayEditor(SerializedProperty dataProperty, GUIContent variableName, Type type, ref bool arrayExpand, ref ReorderableList list, bool layout = true, Rect rect = default)
        {
            //EditorGUI.BeginProperty(rect, GUIContent.none, dataProperty);
            object obj = CyanTriggerSerializableObject.ObjectFromSerializedProperty(dataProperty);
            bool dirty = false;
            obj = DisplayArrayPropertyEditor(variableName, type, obj, ref dirty, ref arrayExpand, ref list, layout, rect);

            if (dirty)
            {
                if(typeof(UnityEngine.Object).IsAssignableFrom(type.GetElementType()))
                {
                    var array = (Array) obj;
                    
                    Array destinationArray = Array.CreateInstance(type.GetElementType(), array.Length);
                    Array.Copy(array, destinationArray, array.Length);
                
                    obj = destinationArray;
                }
                
                CyanTriggerSerializableObject.UpdateSerializedProperty(dataProperty, obj);
            }
            //EditorGUI.EndProperty();
        }

        /*
        public static bool DrawEditor(ref SerializableObject serializedObject, Rect rect, string variableName, Type type, ref bool arrayExpand, ref ReorderableList list)
        {
            bool dirty = false;
            object obj = DisplayPropertyEditor(rect, new GUIContent(variableName), type, serializedObject.obj, ref dirty, ref arrayExpand, ref list);

            if (dirty)
            {
                serializedObject.obj = obj;
            }

            return dirty;
        }
        */

        public static bool TypeHasSingleLineEditor(Type type)
        {
            return
                !type.IsArray &&
                type != typeof(ParticleSystem.MinMaxCurve);
        }
        
        public static bool TypeHasInLineEditor(Type type)
        {
            return !type.IsArray;
        }

        public static float HeightForInLineEditor(Type variableType)
        {
            if (TypeHasSingleLineEditor(variableType))
            {
                return EditorGUIUtility.singleLineHeight;
            }

            if (!variableType.IsArray)
            {
                if (variableType == typeof(ParticleSystem.MinMaxCurve))
                {
                    // 3 items with 2 pixels between each
                    return EditorGUIUtility.singleLineHeight * 3 + (2 * 2); 
                }
                
                throw new NotSupportedException("Cannot calculate line height for type: "+variableType);
            }

            throw new NotSupportedException("Array types are not supported in line: " + variableType);
            //return EditorGUIUtility.singleLineHeight;
        }
        
        // TODO make a better api that doesn't take a list...
        public static float HeightForEditor(Type variableType, object variableValue, bool showList, ref ReorderableList list)
        {
            if (!variableType.IsArray)
            {
                return HeightForInLineEditor(variableType);
            }

            float height = FoldoutListHeaderAreaHeight;
            if (showList)
            {
                Type elementType = variableType.GetElementType();
                CreateReorderableListForVariable(elementType, variableValue as Array, ref list);
                height += list.GetHeight();
            }

            return height;
        }

        public static object CreateInitialValueForType(Type variableType, object variableValue, ref bool dirty)
        {
            if(!variableType.IsInstanceOfType(variableValue))
            {
                if(variableType.IsValueType)
                {
                    variableValue = Activator.CreateInstance(variableType);
                    dirty = true;
                }
                else if (variableType.IsArray)
                {
                    variableValue = Array.CreateInstance(variableType.GetElementType(), 0);
                    dirty = true;
                }
                else
                {
                    variableValue = null;
                }
            }

            return variableValue;
        }
        
        public static object DisplayPropertyEditor(
            Rect rect, 
            GUIContent content, 
            Type variableType, 
            object variableValue, 
            ref bool dirty, 
            bool layout = false)
        {
            if (variableType.IsArray)
            {
                Debug.LogWarning("Trying to display an array type using the object method!");
                return variableValue;
            }

            variableValue = CreateInitialValueForType(variableType, variableValue, ref dirty);

            if (layout)
            {
                EditorGUILayout.BeginHorizontal();
            }

            EditorGUI.BeginChangeCheck();
            
            if(typeof(UnityEngine.Object).IsAssignableFrom(variableType))
            {
                variableValue = DisplayObjectEditor(rect, content, (UnityEngine.Object)variableValue, variableType, layout);
            }
            else if(typeof(IUdonEventReceiver).IsAssignableFrom(variableType))
            {
                variableValue = (IUdonEventReceiver)DisplayObjectEditor(rect, content, (UnityEngine.Object)variableValue, typeof(UdonBehaviour), layout);
            }
            else if(variableType == typeof(string))
            {
                variableValue = DisplayStringEditor(rect, content, (string) variableValue, layout);
            }
            else if(variableType == typeof(float))
            {
                variableValue = DisplayFloatEditor(rect, content, (float?) variableValue ?? default, layout);
            }
            else if (variableType == typeof(double))
            {
                variableValue = DisplayDoubleEditor(rect, content, (double?) variableValue ?? default, layout);
            }
            else if(variableType == typeof(byte))
            {
                variableValue = (byte)DisplayIntEditor(rect, content, (byte?)variableValue ?? default, layout);
            }
            else if(variableType == typeof(int))
            {
                variableValue = DisplayIntEditor(rect, content, (int?)variableValue ?? default, layout);
            }
            else if(variableType == typeof(short))
            {
                variableValue = (short)DisplayIntEditor(rect, content, (short?)variableValue ?? default, layout);
            }
            else if(variableType == typeof(long))
            {
                variableValue = DisplayLongEditor(rect, content, (long?)variableValue ?? default, layout);
            }
            else if(variableType == typeof(bool))
            {
                variableValue = DisplayBoolEditor(rect, content, (bool?) variableValue ?? default, layout);
            }
            else if(variableType == typeof(Vector2))
            {
                variableValue = DisplayVector2Editor(rect, content, (Vector2?)variableValue ?? default, layout);
            }
            else if(variableType == typeof(Vector3))
            {
                variableValue = DisplayVector3Editor(rect, content, (Vector3?)variableValue ?? default, layout);
            }
            else if(variableType == typeof(Vector4))
            {
                variableValue = DisplayVector4Editor(rect, content, (Vector4?)variableValue ?? default, layout);
            }
            else if(variableType == typeof(Quaternion))
            {
                variableValue = DisplayQuaternionEditor(rect, content, (Quaternion?)variableValue ?? default, layout);
            }
            else if(variableType == typeof(Color))
            {
                variableValue = DisplayColorEditor(rect, content, (Color?)variableValue ?? default, layout);
            }
            else if(variableType == typeof(Color32))
            {
                variableValue = DisplayColor32Editor(rect, content, (Color32?)variableValue ?? default, layout);
            }
            else if(variableType == typeof(ParticleSystem.MinMaxCurve))
            {
                variableValue = DisplayMinMaxCurveEditor(rect, content,
                    (ParticleSystem.MinMaxCurve?) variableValue ?? default, layout);
            }
            else if (variableType == typeof(AnimationCurve))
            {
                variableValue = DisplayCurveEditor(rect, content, (AnimationCurve)variableValue, layout);
            }
            else if(variableType.IsEnum)
            {
                variableValue = DisplayEnumEditor(rect, content, (Enum)variableValue, variableType, layout);
            }
            else if(variableType == typeof(Type))
            {
                // TODO display proper editor based on what values are exposed
                DisplayTypeEditor(rect, content, (Type) variableValue, layout);
            }
            else if (variableType == typeof(VRCUrl))
            {
                variableValue = DisplayVrcUrlEditor(rect, content, (VRCUrl)variableValue, layout);
            }
            else if (variableType == typeof(LayerMask))
            {
                variableValue = DisplayLayerMaskEditor(rect, content, (LayerMask?) variableValue ?? default, layout);
            }
            else if (variableType == typeof(Gradient))
            {
                variableValue = DisplayGradientEditor(rect, content, (Gradient) variableValue, layout);
            }
            // TODO add more types here
            else
            {
                DisplayMissingEditor(rect, content, variableType, layout);
            }

            if (layout)
            {
                EditorGUILayout.EndHorizontal();
            }
            
            
            if(EditorGUI.EndChangeCheck())
            {
                dirty = true;
            }

            return variableValue;
        }

        public static object DisplayArrayPropertyEditor(
            GUIContent variableName, 
            Type variableType, 
            object variableValue, 
            ref bool dirty, 
            ref bool showList, 
            ref ReorderableList list,
            bool layout = true,
            Rect rect = default)
        {
            if (!variableType.IsArray)
            {
                Debug.LogWarning("Trying to display a non array type using the array method!");
                return variableValue;
            }

            if (variableValue == null)
            {
                variableValue = Array.CreateInstance(variableType, 0);
                dirty = true;
            }

            return DisplayArrayPropertyEditor(
                variableType.GetElementType(), 
                variableValue as Array, 
                ref dirty,
                ref showList, 
                variableName,
                ref list,
                layout,
                rect);
        }

        #region TypeEditors

        private static void DisplayMissingEditor(Rect rect, GUIContent symbol, Type variableType, bool layout)
        {
            GUIContent content = new GUIContent("null");
            //GUIContent content = new GUIContent("No defined editor for type of " + variableType);
            DisplayLabel(rect, content, layout);
        }

        private static void DisplayTypeEditor(Rect rect, GUIContent symbol, Type typeValue, bool layout)
        {
            GUIContent content = new GUIContent(
                typeValue == null ? 
                    $"Type = null" :
                    $"Type = {CyanTriggerNameHelpers.GetTypeFriendlyName(typeValue)}");
            DisplayLabel(rect, content, layout);
        }

        private static void DisplayLabel(Rect rect, GUIContent content, bool layout)
        {
            if (layout)
            {
                EditorGUILayout.LabelField(content);
            }
            else
            {
                EditorGUI.LabelField(rect, content);
            }
        }
        
        private static UnityEngine.Object DisplayObjectEditor(
            Rect rect, 
            GUIContent symbol, 
            UnityEngine.Object unityEngineObjectValue, 
            Type variableType, 
            bool layout)
        {
            if (layout)
            {
                unityEngineObjectValue = EditorGUILayout.ObjectField(symbol, unityEngineObjectValue, variableType, true);
            }
            else
            {
                unityEngineObjectValue = EditorGUI.ObjectField(rect, symbol, unityEngineObjectValue, variableType, true);
            }

            return unityEngineObjectValue;
        }
        
        private static string DisplayStringEditor(Rect rect, GUIContent symbol, string stringValue, bool layout)
        {
            if (layout)
            {
                stringValue = EditorGUILayout.TextField(symbol, stringValue);
            }
            else
            {
                stringValue = EditorGUI.TextField(rect, symbol, stringValue);
            }

            return stringValue;
        }
        
        private static bool DisplayBoolEditor(Rect rect, GUIContent symbol, bool boolValue, bool layout)
        {
            if (layout)
            {
                rect = EditorGUILayout.BeginHorizontal();
            }

            Rect toggleRect = new Rect(rect);
            toggleRect.width = GUI.skin.label.CalcSize(symbol).x + 15;
            Rect labelRect = new Rect(rect);
            labelRect.width = rect.width - toggleRect.width;
            labelRect.x += toggleRect.width;

            boolValue = EditorGUI.Toggle(toggleRect, symbol, boolValue);
            
            EditorGUI.LabelField(labelRect, boolValue.ToString());

            if (layout)
            {
                EditorGUILayout.EndHorizontal();
            }

            return boolValue;
        }
        
        private static int DisplayIntEditor(Rect rect, GUIContent symbol, int intValue, bool layout)
        {
            if (layout)
            {
                intValue = EditorGUILayout.IntField(symbol, intValue);
            }
            else
            {
                intValue = EditorGUI.IntField(rect, symbol, intValue);
            }

            return intValue;
        }
        
        private static long DisplayLongEditor(Rect rect, GUIContent symbol, long longValue, bool layout)
        {
            if (layout)
            {
                longValue = EditorGUILayout.LongField(symbol, longValue);
            }
            else
            {
                longValue = EditorGUI.LongField(rect, symbol, longValue);
            }

            return longValue;
        }
        
        private static float DisplayFloatEditor(Rect rect, GUIContent symbol, float floatValue, bool layout)
        {
            if (layout)
            {
                floatValue = EditorGUILayout.FloatField(symbol, floatValue);
            }
            else
            {
                floatValue = EditorGUI.FloatField(rect, symbol, floatValue);
            }

            return floatValue;
        }
        
        private static double DisplayDoubleEditor(Rect rect, GUIContent symbol, double doubleValue, bool layout)
        {
            if (layout)
            {
                doubleValue = EditorGUILayout.DoubleField(symbol, doubleValue);
            }
            else
            {
                doubleValue = EditorGUI.DoubleField(rect, symbol, doubleValue);
            }

            return doubleValue;
        }
        
        private static Vector2 DisplayVector2Editor(Rect rect, GUIContent symbol, Vector2 vector2Value, bool layout)
        {
            if (layout)
            {
                vector2Value = EditorGUILayout.Vector2Field(symbol, vector2Value);
            }
            else
            {
                vector2Value = EditorGUI.Vector2Field(rect, symbol, vector2Value);
            }

            return vector2Value;
        }
        
        private static Vector3 DisplayVector3Editor(Rect rect, GUIContent symbol, Vector3 vector3Value, bool layout)
        {
            if (layout)
            {
                vector3Value = EditorGUILayout.Vector3Field(symbol, vector3Value);
            }
            else
            {
                vector3Value = EditorGUI.Vector3Field(rect, symbol, vector3Value);
            }

            return vector3Value;
        }
        
        private static Vector4 DisplayVector4Editor(Rect rect, GUIContent symbol, Vector4 vector4Value, bool layout)
        {
            if (layout)
            {
                vector4Value = EditorGUILayout.Vector4Field(symbol, vector4Value);
            }
            else
            {
                vector4Value = EditorGUI.Vector4Field(rect, symbol, vector4Value);
            }

            return vector4Value;
        }



        private static Vector3 _cachedQuaterionEulerVector;
        private static Quaternion DisplayQuaternionEditor(Rect rect, GUIContent symbol, Quaternion quaternionValue, bool layout)
        {
            int controlIndex = GUIUtility.GetControlID(FocusType.Passive);

            // Vector3 property editors create 4 controls. Check if current control is within this range.
            bool IsCurrentControl()
            {
                return controlIndex < GUIUtility.keyboardControl && 
                       GUIUtility.keyboardControl <= controlIndex + 4;
            }

            Vector3 euler = quaternionValue.eulerAngles;
            if (IsCurrentControl())
            {
                euler = _cachedQuaterionEulerVector;
            }

            euler = DisplayVector3Editor(rect, symbol, euler, layout);
            quaternionValue = Quaternion.Euler(euler);
            
            if (IsCurrentControl())
            {
                _cachedQuaterionEulerVector = euler;
            }
            
            return quaternionValue;
            /*
            Vector4 quaternionVector4 = new Vector4(quaternionValue.x, quaternionValue.y, quaternionValue.z, quaternionValue.w);
            quaternionVector4 = DisplayVector4Editor(rect, symbol, quaternionVector4, layout);
            return new Quaternion(quaternionVector4.x, quaternionVector4.y, quaternionVector4.z, quaternionVector4.w);
            */
        }

        private static Color DisplayColorEditor(Rect rect,  GUIContent symbol, Color colorValue, bool layout)
        {
            if (layout)
            {
                colorValue = EditorGUILayout.ColorField(symbol, colorValue);
            }
            else
            {
                colorValue = EditorGUI.ColorField(rect, symbol, colorValue);
            }

            return colorValue;
        }

        private static Color32 DisplayColor32Editor(Rect rect, GUIContent symbol, Color32 colorValue, bool layout)
        {
            return DisplayColorEditor(rect, symbol, colorValue, layout);
        }

        private static Enum DisplayEnumEditor(Rect rect, GUIContent symbol, Enum enumValue, Type enumType, bool layout)
        {
            if (enumValue == null)
            {
                enumValue = (Enum)Enum.ToObject(enumType, 0);
            }
            
            if (layout)
            {
                enumValue = EditorGUILayout.EnumPopup(symbol, enumValue);
            }
            else
            {
                enumValue = EditorGUI.EnumPopup(rect, symbol, enumValue);
            }

            return enumValue;
        }

        private static VRCUrl DisplayVrcUrlEditor(Rect rect, GUIContent symbol, VRCUrl urlValue, bool layout)
        {
            if (urlValue == null)
            {
                urlValue = new VRCUrl("");
            }
            return new VRCUrl(DisplayStringEditor(rect, symbol, urlValue.Get(), layout));
        }

        private static ParticleSystem.MinMaxCurve DisplayMinMaxCurveEditor(Rect rect, GUIContent symbol,
            ParticleSystem.MinMaxCurve minMaxCurve, bool layout)
        {
            float multiplier = minMaxCurve.curveMultiplier;
            AnimationCurve minCurve = minMaxCurve.curveMin ?? new AnimationCurve();
            AnimationCurve maxCurve = minMaxCurve.curveMax ?? new AnimationCurve();
            if (layout)
            {
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(symbol);
                EditorGUI.indentLevel++;
                multiplier = EditorGUILayout.FloatField("Multiplier", multiplier);
                minCurve = EditorGUILayout.CurveField("Min Curve", minCurve);
                maxCurve = EditorGUILayout.CurveField("Max Curve", maxCurve);
                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
            }
            else
            {
                float height = (rect.height - 4) / 3;
                Rect r1 = new Rect(rect);
                r1.height = height;
                Rect r2 = new Rect(r1);
                r2.y = r1.yMax + 2;
                Rect r3 = new Rect(r2);
                r3.y = r2.yMax + 2;
                
                //EditorGUI.LabelField(rect, symbol); // TODO?
                multiplier = EditorGUI.FloatField(r1, "Multiplier", multiplier);
                minCurve = EditorGUI.CurveField(r2, "Min Curve", minCurve);
                maxCurve = EditorGUI.CurveField(r3, "Max Curve", maxCurve);
            }
            
            return new ParticleSystem.MinMaxCurve(multiplier, minCurve, maxCurve);
        }

        private static AnimationCurve DisplayCurveEditor(Rect rect, GUIContent symbol, AnimationCurve curve,
            bool layout)
        {
            if (curve == null)
            {
                curve = new AnimationCurve();
            }
            
            if (layout)
            {
                return EditorGUILayout.CurveField(symbol, curve);
            }

            return EditorGUI.CurveField(rect, symbol, curve);
        }

        private static LayerMask DisplayLayerMaskEditor(Rect rect, GUIContent symbol, LayerMask maskValue, bool layout)
        {
            // Using workaround from http://answers.unity.com/answers/1387522/view.html
            if (layout)
            {
                EditorGUILayout.LabelField(symbol);
                LayerMask tempMask = EditorGUILayout.MaskField(InternalEditorUtility.LayerMaskToConcatenatedLayersMask(maskValue), InternalEditorUtility.layers);
                maskValue = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(tempMask);
            }
            else
            {
                LayerMask tempMask = EditorGUI.MaskField(rect, InternalEditorUtility.LayerMaskToConcatenatedLayersMask(maskValue), InternalEditorUtility.layers);
                maskValue = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(tempMask);
            }
                
            return maskValue;
        }
        
        // TODO Will this ever be used?
        private static LayerMask DisplayLayerEditor(Rect rect, GUIContent symbol, int layer, bool layout)
        {
            if (layout)
            {
                return EditorGUILayout.LayerField(symbol, layer);
            }
            return EditorGUI.LayerField(rect, symbol, layer);
        }

        private static Gradient DisplayGradientEditor(Rect rect, GUIContent symbol, Gradient gradient, bool layout)
        {
            if (layout)
            {
                return EditorGUILayout.GradientField(symbol, gradient);    
            }
            return EditorGUI.GradientField(rect, symbol, gradient);
        }
        

        #endregion

        public static void CreateReorderableListForVariable(
            Type variableType,
            Array variableValue,
            ref ReorderableList list)
        {
            if (list != null)
            {
                return;
            }
            
            Debug.Assert(variableValue != null, "Cannot create list if value is null!");
            
            ReorderableList listInstance = list = new ReorderableList(
                variableValue, 
                variableType, 
                true, 
                false,
                true,
                true);
            list.headerHeight = 0;
            list.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                bool changed = false;
                listInstance.list[index] = DisplayPropertyEditor(
                    rect,
                    GUIContent.none,
                    variableType,
                    listInstance.list[index],
                    ref changed);
            };
            list.onAddCallback = reorderableList =>
            {
                int length = reorderableList.list.Count;
                Array values = Array.CreateInstance(variableType, length + 1);
                for (int i = 0; i < length; ++i)
                {
                    values.SetValue(reorderableList.list[i], i);
                }

                reorderableList.list = values;
            };
            list.onRemoveCallback = reorderableList =>
            {
                int selected = reorderableList.index;
                int length = reorderableList.list.Count;
                Array values = Array.CreateInstance(variableType, length - 1);

                int selectedFound = 0;
                for (int i = 0; i < values.Length; ++i)
                {
                    if (i == selected)
                    {
                        selectedFound = 1;
                    }
                    values.SetValue(reorderableList.list[i + selectedFound], i);
                }
                reorderableList.list = values;
            };
            list.elementHeight = HeightForInLineEditor(variableType);
        }
        
        public static Array DisplayArrayPropertyEditor(
            Type variableType,
            Array variableValue,
            ref bool dirty,
            ref bool showList,
            GUIContent content,
            ref ReorderableList list,
            bool layout,
            Rect rect)
        {
            if (layout)
            {
                EditorGUILayout.BeginVertical();
            }

            ReorderableList reorderableList = list;
            DrawFoldoutListHeader(
                content,
                ref showList,
                true,
                variableValue.Length,
                size =>
                {
                    if (reorderableList == null)
                    {
                        return;
                    }

                    Array values = Array.CreateInstance(variableType, size);
                    for (int i = 0; i < size; ++i)
                    {
                        values.SetValue(i < reorderableList.list.Count ? reorderableList.list[i] : default, i);
                    }

                    reorderableList.list = values;
                },
                // Only allow drag for GameObject or Component fields
                typeof(GameObject).IsAssignableFrom(variableType) ||
                 typeof(Component).IsAssignableFrom(variableType) ||
                 typeof(IUdonEventReceiver).IsAssignableFrom(variableType),
                dragObjects =>
                {
                    if (reorderableList == null)
                    {
                        return;
                    }
                    
                    List<Object> objects = GetGameObjectsOrComponentsFromDraggedObjects(dragObjects, variableType);

                    if (objects.Count == 0)
                    {
                        return;
                    }
                    
                    int startSize = reorderableList.list.Count;
                    int size = startSize + objects.Count;
                    Array values = Array.CreateInstance(variableType, size);
                    for (int i = 0; i < reorderableList.list.Count; ++i)
                    {
                        values.SetValue(reorderableList.list[i], i);
                    }
                    for (int i = 0; i < objects.Count; ++i)
                    {
                        values.SetValue(objects[i], startSize + i);
                    }

                    reorderableList.list = values;
                },
                false, 
                true, 
                layout,
                rect);

            if (!showList)
            {
                if (layout)
                {
                    EditorGUILayout.EndVertical();
                }
                return variableValue;
            }
            
            if (list == null)
            {
                CreateReorderableListForVariable(variableType, variableValue, ref list);
            }
            
            EditorGUI.BeginChangeCheck();

            if (layout)
            {
                list.DoLayoutList();
            }
            else
            {
                rect.y += FoldoutListHeaderHeight + 2;
                rect.height -= FoldoutListHeaderHeight + 2;
                list.DoList(rect);
            }

            bool allEqual = list.count == variableValue.Length;
            for (int i = 0; allEqual && i < list.count && i < variableValue.Length; ++i)
            {
                allEqual &= list.list[i] != variableValue.GetValue(i);
            }
        
            if (
                EditorGUI.EndChangeCheck() ||
                list.count != variableValue.Length ||
                !allEqual
            )
            {
                if (variableValue.Length != list.count)
                {
                    variableValue = Array.CreateInstance(variableType, list.count);
                }

                for (int i = 0; i < variableValue.Length; ++i)
                {
                    variableValue.SetValue(list.list[i], i);
                }
                
                dirty = true;
            }
            
            if (layout)
            {
                EditorGUILayout.EndVertical();
            }

            return variableValue;
        }

        public static List<Object> GetGameObjectsOrComponentsFromDraggedObjects(Object[] dragObjects, Type type)
        {
            List<Object> objects = new List<Object>();
            bool isGameObject = typeof(GameObject).IsAssignableFrom(type);
            bool isComponent = typeof(Component).IsAssignableFrom(type) ||
                               typeof(IUdonEventReceiver).IsAssignableFrom(type);
                    
            for (int i = 0; i < dragObjects.Length; i++)
            {
                var obj = dragObjects[i];
                if (isGameObject)
                {
                    if (obj is GameObject gameObject)
                    {
                        objects.Add(gameObject);
                    }
                    else if (obj is Component component)
                    {
                        objects.Add(component.gameObject);
                    }
                }
                else if (isComponent)
                {
                    if (obj is Component component)
                    {
                        objects.Add(component);
                    }
                    else if (obj is GameObject gameObject)
                    {
                        var components = gameObject.GetComponents(type);
                        if (components.Length > 0)
                        {
                            objects.AddRange(components);
                        }
                    }
                }
            }

            return objects;
        }
        
        public static void DrawFoldoutListHeader(
            GUIContent content,
            ref bool visibilityState,
            bool showSizeEditor,
            int currentSize,
            Action<int> onSizeChanged,
            bool allowItemDrag,
            Action<Object[]> onItemDragged,
            bool showError = false,
            bool showHeaderBackground = true,
            bool layout = true,
            Rect rect = default)
        {
            Rect foldMainRect = rect;
            if (layout)
            {
                foldMainRect = EditorGUILayout.BeginHorizontal();
            }
            
            Rect foldoutRect = new Rect(foldMainRect.x + 17, foldMainRect.y + 1, foldMainRect.width - 18, FoldoutListHeaderHeight);
            Rect header = new Rect(foldMainRect);
            header.height = foldoutRect.height + 4;
            if (showHeaderBackground && Event.current.type == EventType.Repaint)
            {
                if (_headerBackgroundStyle == null)
                {
                    _headerBackgroundStyle = "RL Header";
                }
                _headerBackgroundStyle.Draw(header, false, false, false, false);
            }
            
            Rect sizeRect = new Rect(foldoutRect);
            float separatorSize = 6;
            float maxSizeWidth = 75;

            if (visibilityState && showSizeEditor)
            {
                foldoutRect.width -= maxSizeWidth - separatorSize;
            }

            if (showError)
            {
                content.image = EditorGUIUtility.FindTexture("Error");
            }
            
            // Check dragged objects before foldout as it will become "used" after
            Event evt = Event.current;
            if (allowItemDrag &&
                visibilityState && 
                header.Contains(evt.mousePosition))
            {
                if (evt.type == EventType.DragUpdated)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                }
                if (evt.type == EventType.DragPerform)
                {
                    Object[] dragObjects = DragAndDrop.objectReferences.ToArray();
                    onItemDragged?.Invoke(dragObjects);
                    DragAndDrop.AcceptDrag();
                    evt.Use();
                }
            }
            
            CyanTriggerNameHelpers.TruncateContent(content, foldoutRect);
            bool show = EditorGUI.Foldout(foldoutRect, visibilityState, content, true);
            // Just clicked the arrow, unfocus any elements, which could have been the size component
            if (!show && visibilityState)
            {
                GUI.FocusControl(null);
            }
            visibilityState = show;

            if (visibilityState && showSizeEditor)
            {
                sizeRect.y += 1;
                sizeRect.height -= 1;
                sizeRect.width = maxSizeWidth;
                sizeRect.x += foldoutRect.width - separatorSize;

                float prevWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 30;
                
                int size = EditorGUI.IntField(sizeRect, "Size", currentSize);
                size = Math.Max(0, size);
                
                EditorGUIUtility.labelWidth = prevWidth;

                if (size != currentSize)
                {
                    onSizeChanged?.Invoke(size);
                }
            }
            
            if (layout)
            {
                EditorGUILayout.EndHorizontal();
                float offset = 0;
#if !UNITY_2019_3_OR_NEWER
                offset = -3;
#endif
                GUILayout.Space(header.height + offset);
            }
        }

        public static void DrawButtonFooter(GUIContent[] icons, Action[] buttons, bool[] shouldDisable)
        {
            if (_footerButtonStyle == null)
            {
                _footerButtonStyle = "RL FooterButton";
                _footerBackgroundStyle = "RL Footer";
            }
            
            
            Rect footerRect = EditorGUILayout.BeginHorizontal();
            float xMax = footerRect.xMax;
#if UNITY_2019_3_OR_NEWER
            xMax -= 8;
            footerRect.height = 16;
#else
            footerRect.height = 11;
#endif
            float x = xMax - 8f;
            const float buttonWidth = 25;
            x -= buttonWidth * icons.Length;
            footerRect = new Rect(x, footerRect.y, xMax - x, footerRect.height);
                    
            if (Event.current.type == EventType.Repaint)
            {
                _footerBackgroundStyle.Draw(footerRect, false, false, false, false);
            }

#if !UNITY_2019_3_OR_NEWER
            footerRect.y -= 3f;
#endif
            
            for (int i = 0; i < icons.Length; ++i)
            {
                Rect buttonRect = new Rect(x + 4f + buttonWidth * i, footerRect.y, buttonWidth, 13f);

                        
                EditorGUI.BeginDisabledGroup(shouldDisable[i]);
                
                GUIStyle style = _footerButtonStyle;
                if (icons[i].image == null)
                {
                    style = new GUIStyle { alignment = TextAnchor.LowerCenter, fontSize = 8};
                    style.normal.textColor = GUI.skin.label.normal.textColor;
                }
                if (GUI.Button(buttonRect, icons[i], style))
                {
                    buttons[i]?.Invoke();
                }
                EditorGUI.EndDisabledGroup();
            }
                    
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(footerRect.height + 4);
        }

        public static Rect DrawErrorIcon(Rect rect, string reason)
        {
            GUIContent errorIcon = EditorGUIUtility.TrIconContent("CollabError", reason);
            Rect errorRect = new Rect(rect);
            float iconWidth = 15;
            float spaceBetween = 1;
            errorRect.width = iconWidth;
            errorRect.y += 3;
                
            EditorGUI.LabelField(errorRect, errorIcon);
                
            rect.x += iconWidth + spaceBetween;
            rect.width -= iconWidth + spaceBetween;

            return rect;
        }

        public static float GetHeightForActionInstanceInputEditors(
            CyanTriggerActionInstanceRenderData actionInstanceRenderData,
            Func<Type, List<CyanTriggerEditorVariableOption>> getVariableOptionsForType)
        {
            var actionProperty = actionInstanceRenderData.Property;
            var inputListProperty = actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.inputs));

            var variableDefinitions = actionInstanceRenderData.ActionInfo.GetVariables();
            if (inputListProperty.arraySize != variableDefinitions.Length)
            {
                Debug.LogWarning("Improper variable input size! " + inputListProperty.arraySize +" != " + variableDefinitions.Length);
                inputListProperty.arraySize = variableDefinitions.Length;
            }
            
            float height = 0;
            for (int curInput = 0; curInput < variableDefinitions.Length; ++curInput)
            {
                CyanTriggerActionVariableDefinition variableDefinition = variableDefinitions[curInput];
                
                Type propertyType = variableDefinition.type.type;
                
                // First option is a multi input editor
                if (curInput == 0 &&
                    (variableDefinition.variableType & CyanTriggerActionVariableTypeDefinition.AllowsMultiple) != 0)
                {
                    var multiInputListProperty = 
                        actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.multiInput));
                    
                    CreateActionVariableInstanceMultiInputEditor(
                        actionInstanceRenderData, 
                        curInput, 
                        multiInputListProperty,
                        variableDefinition, 
                        getVariableOptionsForType);
                    
                    height += GetHeightForActionVariableInstanceMultiInputEditor(
                        propertyType,
                        actionInstanceRenderData.ExpandedInputs[curInput],
                        actionInstanceRenderData.InputLists[curInput]);
                }
                else
                {
                    SerializedProperty inputProperty = inputListProperty.GetArrayElementAtIndex(curInput);
                    height += GetHeightForActionVariableInstanceInputEditor(
                        variableDefinition,
                        inputProperty,
                        actionInstanceRenderData.ExpandedInputs[curInput],
                        ref actionInstanceRenderData.InputLists[curInput]);
                }
            }

            return height + Mathf.Max(0, 5 * (variableDefinitions.Length + 1));
        }

        private static float GetHeightForActionVariableInstanceMultiInputEditor(
            Type propertyType,
            bool expandList,
            ReorderableList list)
        {
            if (!expandList)
            {
                return FoldoutListHeaderAreaHeight;
            }
            
            bool displayEditorInLine = TypeHasInLineEditor(propertyType);
            return FoldoutListHeaderAreaHeight
                   + list.GetHeight()
                   + (displayEditorInLine ? 0 : EditorGUIUtility.singleLineHeight + 5);
        }

        private static float GetHeightForActionVariableInstanceInputEditor(
            CyanTriggerActionVariableDefinition variableDefinition,
            SerializedProperty inputProperty,
            bool expandList,
            ref ReorderableList list)
        {
            float height = GetHeightForActionInputInLineEditor(variableDefinition);
            SerializedProperty isVariableProperty =
                inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
            
            Type propertyType = variableDefinition.type.type;
            // TODO Or is a type that is multiline editor
            if (propertyType.IsArray && !isVariableProperty.boolValue)
            {
                SerializedProperty dataProperty =
                    inputProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
                
                height += 5;
                height += HeightForEditor(
                    propertyType,
                    CyanTriggerSerializableObject.ObjectFromSerializedProperty(dataProperty),
                    expandList,
                    ref list);
            }

            return height;
        }

        public static void DrawActionInstanceInputEditors(
            CyanTriggerActionInstanceRenderData actionInstanceRenderData,
            Func<Type, List<CyanTriggerEditorVariableOption>> getVariableOptionsForType,
            Rect rect = default,
            bool layout = false)
        {
            actionInstanceRenderData.ContainsNull = false;
            
            var actionProperty = actionInstanceRenderData.Property;
            var variableDefinitions = actionInstanceRenderData.ActionInfo.GetVariables();
            var inputListProperty = actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.inputs));
            
            if (inputListProperty.arraySize != variableDefinitions.Length)
            {
                inputListProperty.arraySize = variableDefinitions.Length;
            }

            if (actionInstanceRenderData.ExpandedInputs.Length != variableDefinitions.Length)
            {
                int prevSize = actionInstanceRenderData.ExpandedInputs.Length;
                Array.Resize(ref actionInstanceRenderData.ExpandedInputs, variableDefinitions.Length);
                Array.Resize(ref actionInstanceRenderData.InputLists, variableDefinitions.Length);

                for (int cur = prevSize; cur < variableDefinitions.Length; ++cur)
                {
                    actionInstanceRenderData.ExpandedInputs[cur] = true;
                }
            }
            
            for (int curInput = 0; curInput < inputListProperty.arraySize; ++curInput)
            {
                CyanTriggerActionVariableDefinition variableDefinition = variableDefinitions[curInput];
                
                Rect inputRect = new Rect(rect);
                
                // First option is a multi input editor
                if (curInput == 0 &&
                    (variableDefinition.variableType & CyanTriggerActionVariableTypeDefinition.AllowsMultiple) != 0)
                {
                    var multiInputListProperty = 
                        actionProperty.FindPropertyRelative(nameof(CyanTriggerActionInstance.multiInput));
                    
                    DrawActionVariableInstanceMultiInputEditor(
                        actionInstanceRenderData,
                        curInput,
                        multiInputListProperty, 
                        variableDefinition,
                        getVariableOptionsForType, 
                        ref inputRect,
                        layout);
                }
                else
                {
                    SerializedProperty inputProperty = inputListProperty.GetArrayElementAtIndex(curInput);
                    DrawActionVariableInstanceInputEditor(
                        actionInstanceRenderData,
                        curInput,
                        inputProperty, 
                        variableDefinition,
                        getVariableOptionsForType, 
                        ref inputRect,
                        layout);
                }

                rect.y += inputRect.height + 5;
                rect.height -= inputRect.height + 5;
            }
        }

        private static void DrawActionVariableInstanceInputEditor(
            CyanTriggerActionInstanceRenderData actionInstanceRenderData,
            int inputIndex,
            SerializedProperty variableProperty,
            CyanTriggerActionVariableDefinition variableDefinition,
            Func<Type, List<CyanTriggerEditorVariableOption>> getVariableOptionsForType,
            ref Rect rect,
            bool layout = false)
        {
            Type propertyType = variableDefinition.type.type;

            GUIContent variableDisplayName =
                new GUIContent(variableDefinition.displayName, variableDefinition.description);
            
            rect.height = GetHeightForActionVariableInstanceInputEditor(
                variableDefinition,
                variableProperty,
                actionInstanceRenderData.ExpandedInputs[inputIndex],
                ref actionInstanceRenderData.InputLists[inputIndex]);
            
            Rect inputRect = new Rect(rect);
            inputRect.height = EditorGUIUtility.singleLineHeight;
            
            // Skip hidden input, but set default value
            if ((variableDefinition.variableType & CyanTriggerActionVariableTypeDefinition.Hidden) != 0)
            {
                // TODO verify this works?
                // This breaks custom defined variables...
                //CyanTriggerSerializableObject.UpdateSerializedProperty(dataProperty, variableDefinition.defaultValue);
                return;
            }

            if (layout)
            {
                EditorGUILayout.Space();
                inputRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(EditorGUIUtility.singleLineHeight));
                EditorGUILayout.Space();
            }

            SerializedProperty isVariableProperty =
                variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));

            bool isVariable = isVariableProperty.boolValue;
            RenderActionInputInLine(
                variableDefinition,
                variableProperty,
                getVariableOptionsForType,
                inputRect,
                true,
                variableDisplayName,
                true,
                GUIContent.none);

            if (isVariable != isVariableProperty.boolValue)
            {
                actionInstanceRenderData.NeedsRedraws = true;
            }

            actionInstanceRenderData.ContainsNull |= InputContainsNullVariableOrValue(variableProperty);
            
            if (layout)
            {
                EditorGUILayout.EndHorizontal();
            }

            // TODO handle other multiline editor types
            if (!actionInstanceRenderData.NeedsRedraws &&
                propertyType.IsArray && 
                !isVariableProperty.boolValue) // Or is a type that is multiline editor
            {
                SerializedProperty dataProperty =
                    variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
                
                inputRect.y += EditorGUIUtility.singleLineHeight + 5;
                inputRect.height = rect.height - EditorGUIUtility.singleLineHeight;

                bool prevShow = actionInstanceRenderData.ExpandedInputs[inputIndex];
                int size = actionInstanceRenderData.InputLists[inputIndex].count;

                GUIContent content =
                    new GUIContent(variableDefinition.displayName, variableDefinition.description);
                DrawArrayEditor(
                    dataProperty,
                    content,
                    propertyType,
                    ref actionInstanceRenderData.ExpandedInputs[inputIndex],
                    ref actionInstanceRenderData.InputLists[inputIndex], 
                    layout, 
                    inputRect);

                if (prevShow != actionInstanceRenderData.ExpandedInputs[inputIndex] ||
                    size != actionInstanceRenderData.InputLists[inputIndex].count)
                {
                    actionInstanceRenderData.NeedsRedraws = true;
                }
            }
        }

        private static void CreateActionVariableInstanceMultiInputEditor(
            CyanTriggerActionInstanceRenderData actionInstanceRenderData,
            int inputIndex,
            SerializedProperty variableProperty,
            CyanTriggerActionVariableDefinition variableDefinition,
            Func<Type, List<CyanTriggerEditorVariableOption>> getVariableOptionsForType)
        {
            if (actionInstanceRenderData.InputLists[inputIndex] == null)
            {
                Type propertyType = variableDefinition.type.type;
                bool displayEditorInLine = TypeHasInLineEditor(propertyType);
                
                ReorderableList list = new ReorderableList(
                    variableProperty.serializedObject, 
                    variableProperty, 
                    true, 
                    false, 
                    true, 
                    true);
                list.headerHeight = 0;
                list.drawElementCallback = (Rect elementRect, int index, bool isActive, bool isFocused) =>
                {
                    SerializedProperty property = variableProperty.GetArrayElementAtIndex(index);
                    RenderActionInputInLine(
                        variableDefinition,
                        property,
                        getVariableOptionsForType,
                        elementRect,
                        false,
                        GUIContent.none,
                        displayEditorInLine,
                        new GUIContent("Select to Edit"));
                };
                list.elementHeight = HeightForInLineEditor(propertyType);

                actionInstanceRenderData.InputLists[inputIndex] = list;
            }
        }
        
        public static void DrawActionVariableInstanceMultiInputEditor(
            CyanTriggerActionInstanceRenderData actionInstanceRenderData,
            int inputIndex,
            SerializedProperty variableProperty,
            CyanTriggerActionVariableDefinition variableDefinition,
            Func<Type, List<CyanTriggerEditorVariableOption>> getVariableOptionsForType,
            ref Rect rect,
            bool layout = false)
        {
            CreateActionVariableInstanceMultiInputEditor(
                actionInstanceRenderData, 
                inputIndex, 
                variableProperty,
                variableDefinition, 
                getVariableOptionsForType);

            if (layout)
            {
                EditorGUILayout.Space();
            }

            Type propertyType = variableDefinition.type.type;
            bool displayEditorInLine = TypeHasInLineEditor(propertyType);
            
            rect.height =
                GetHeightForActionVariableInstanceMultiInputEditor(
                    propertyType,
                    actionInstanceRenderData.ExpandedInputs[inputIndex],
                    actionInstanceRenderData.InputLists[inputIndex]);
            Rect inputRect = new Rect(rect);
            inputRect.height = FoldoutListHeaderAreaHeight;

            GUIContent variableDisplayName =
                new GUIContent(variableDefinition.displayName, variableDefinition.description);

            bool prevExpand = actionInstanceRenderData.ExpandedInputs[inputIndex];
            int arraySize = variableProperty.arraySize;
            
            DrawFoldoutListHeader(
                variableDisplayName,
                ref actionInstanceRenderData.ExpandedInputs[inputIndex],
                true,
                variableProperty.arraySize,
                size =>
                {
                    int prevSize = variableProperty.arraySize;
                    variableProperty.arraySize = size;

                    for (int i = prevSize; i < size; ++i)
                    {
                        SerializedProperty property = variableProperty.GetArrayElementAtIndex(i);
                        SerializedProperty dataProperty =
                            property.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
                        CyanTriggerSerializableObject.UpdateSerializedProperty(dataProperty, null);
                    }
                },
                // Only allow drag for GameObject or Component fields
                typeof(GameObject).IsAssignableFrom(propertyType) ||
                typeof(Component).IsAssignableFrom(propertyType) ||
                typeof(IUdonEventReceiver).IsAssignableFrom(propertyType),
                dragObjects =>
                {
                    List<Object> objects = GetGameObjectsOrComponentsFromDraggedObjects(dragObjects, propertyType);

                    int startIndex = variableProperty.arraySize;
                    variableProperty.arraySize += objects.Count;
                    for (int i = 0; i < objects.Count; ++i)
                    {
                        SerializedProperty property = variableProperty.GetArrayElementAtIndex(startIndex + i);
                        SerializedProperty isVarProperty =
                            property.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
                        isVarProperty.boolValue = false;
                        SerializedProperty dataProperty =
                            property.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
                        
                        CyanTriggerSerializableObject.UpdateSerializedProperty(dataProperty, objects[i]);
                    }
                    variableProperty.serializedObject.ApplyModifiedProperties();
                },
                false,
                true,
                layout,
                inputRect);

            if (actionInstanceRenderData.ExpandedInputs[inputIndex])
            {
                if (layout)
                {
                    GUILayout.Space(2);
                    actionInstanceRenderData.InputLists[inputIndex].DoLayoutList();
                }
                else
                {
                    inputRect.y += FoldoutListHeaderAreaHeight;
                    actionInstanceRenderData.InputLists[inputIndex].DoList(inputRect);
                }
            }
            
            if (prevExpand != actionInstanceRenderData.ExpandedInputs[inputIndex] ||
                arraySize != variableProperty.arraySize)
            {
                arraySize = variableProperty.arraySize;
                actionInstanceRenderData.NeedsRedraws = true;
            }

            actionInstanceRenderData.ContainsNull |= arraySize == 0;
            for (int curInput = 0; curInput < arraySize && !actionInstanceRenderData.ContainsNull; ++curInput)
            {
                var inputProp = variableProperty.GetArrayElementAtIndex(curInput);
                actionInstanceRenderData.ContainsNull |= InputContainsNullVariableOrValue(inputProp);
            }

            // TODO figure out how to get the list here.
            if (!displayEditorInLine && actionInstanceRenderData.InputLists[inputIndex].index != -1)
            {
                EditorGUILayout.LabelField("Selected item " + actionInstanceRenderData.InputLists[inputIndex].index);
            }
        }

        private static float GetHeightForActionInputInLineEditor(CyanTriggerActionVariableDefinition variableDefinition)
        {
            if ((variableDefinition.variableType & CyanTriggerActionVariableTypeDefinition.Hidden) != 0)
            {
                return 0;
            }
            bool allowsCustomValues =
                (variableDefinition.variableType & CyanTriggerActionVariableTypeDefinition.Constant) != 0;
            bool allowsVariables =
                (variableDefinition.variableType & CyanTriggerActionVariableTypeDefinition.VariableInput) != 0;

            if (!allowsCustomValues && !allowsVariables)
            {
                return 0;
            }
            
            return EditorGUIUtility.singleLineHeight;
        }
        
        
        private static void RenderActionInputInLine(
            CyanTriggerActionVariableDefinition variableDefinition,
            SerializedProperty variableProperty,
            Func<Type, List<CyanTriggerEditorVariableOption>> getVariableOptionsForType,
            Rect rect,
            bool displayLabel,
            GUIContent labelContent,
            bool displayEditor,
            GUIContent editorLabelContent)
        {
            SerializedProperty dataProperty =
                variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));

            // Skip hidden input, but set default value
            if ((variableDefinition.variableType & CyanTriggerActionVariableTypeDefinition.Hidden) != 0)
            {
                // TODO verify this works?
                CyanTriggerSerializableObject.UpdateSerializedProperty(dataProperty, variableDefinition.defaultValue);
                return;
            }

            bool allowsCustomValues =
                (variableDefinition.variableType & CyanTriggerActionVariableTypeDefinition.Constant) != 0;
            bool allowsVariables =
                (variableDefinition.variableType & CyanTriggerActionVariableTypeDefinition.VariableInput) != 0;
            bool outputVar = 
                (variableDefinition.variableType & CyanTriggerActionVariableTypeDefinition.VariableOutput) != 0;
            
            // TODO verify this isn't possible. What
            if (!allowsCustomValues && !allowsVariables)
            {
                return;
            }

            SerializedProperty isVariableProperty =
                variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));
            SerializedProperty idProperty =
                variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID));
            SerializedProperty nameProperty =
                variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.name));

            bool isVariable = isVariableProperty.boolValue;
            Type propertyType = variableDefinition.type.type;


            float spaceBetween = 5;
            float width = (rect.width - spaceBetween * 2) / 3f;
            Rect labelRect = new Rect(rect.x, rect.y, width, rect.height);
            Rect inputRectFull = new Rect(labelRect.xMax + spaceBetween, rect.y, width * 2 + spaceBetween, rect.height);
            Rect typeRect = new Rect(labelRect.xMax + spaceBetween, rect.y, Mathf.Min(width * 0.5f, 65f), rect.height);
            Rect inputRect = new Rect(typeRect.xMax + spaceBetween, rect.y, width * 2 - typeRect.width, rect.height);

            // TODO verify variable value and show error if not valid
            //if (!CyanTriggerUtil.IsValidActionVariableInstance(variableProperty))
            
            // TODO do this properly
            // var valid = variableInstance.IsValid();
            // if (valid != CyanTriggerUtil.InvalidReason.Valid)
            // {
            //     labelRect = CyanTriggerPropertyEditor.DrawErrorIcon(labelRect, valid.ToString());
            // }
            
            if (displayLabel)
            {
                string propertyTypeFriendlyName = CyanTriggerNameHelpers.GetTypeFriendlyName(propertyType);
                if (string.IsNullOrEmpty(labelContent.text))
                {
                    labelContent.text = (outputVar? "out " : "") + propertyTypeFriendlyName;
                }
                if (string.IsNullOrEmpty(labelContent.tooltip))
                {
                    labelContent.tooltip = $"{labelContent.text} ({propertyTypeFriendlyName})";
                }
                if (outputVar)
                {
                    labelContent.tooltip += " - The contents of this variable will be modified.";
                }
                
                // TODO show indicator if variable will be edited
                EditorGUI.LabelField(labelRect, labelContent);
            }
            else
            {
                inputRectFull.x -= labelRect.width;
                typeRect.x -= labelRect.width;
                inputRect.x -= labelRect.width;

                inputRectFull.width += labelRect.width;
                inputRect.width += labelRect.width;
            }

            Rect customRect = inputRectFull;
            if (allowsCustomValues && allowsVariables)
            {
                Rect popupRect = typeRect;
                if (!isVariable && propertyType.IsArray && displayEditor)
                {
                    popupRect = inputRectFull;
                }

                string[] options = {"Input", "Variable"};
                isVariable = isVariableProperty.boolValue = 1 == EditorGUI.Popup(popupRect, isVariable ? 1 : 0, options);
                customRect = inputRect;
            }
            else if (allowsCustomValues)
            {
                isVariable = isVariableProperty.boolValue = false;
            }
            else
            {
                isVariable = isVariableProperty.boolValue = true;
            }
            
            if (isVariable)
            {
                int selected = 0;
                List<string> options = new List<string>();
                List<CyanTriggerEditorVariableOption> varOptions = getVariableOptionsForType(propertyType);
                List<CyanTriggerEditorVariableOption> visibleOptions = new List<CyanTriggerEditorVariableOption>();
                options.Add("None");
                
                
                foreach (var varOption in varOptions)
                {
                    // Skip readonly variables for output var options
                    if (outputVar && varOption.IsReadOnly)
                    {
                        continue;
                    }
                    
                    if (idProperty.stringValue == varOption.ID || 
                        (string.IsNullOrEmpty(idProperty.stringValue) && nameProperty.stringValue == varOption.Name))
                    {
                        selected = options.Count;
                    }
                    visibleOptions.Add(varOption);

                    string optionName = propertyType != varOption.Type
                        ? $"{varOption.Name} ({CyanTriggerNameHelpers.GetTypeFriendlyName(varOption.Type)})"
                        : varOption.Name;
                    options.Add(optionName);
                }
                
                // TODO add option for new global variable or new local variable which creates the variable before this action

                selected = EditorGUI.Popup(customRect, selected, options.ToArray());

                if (selected == 0)
                {
                    idProperty.stringValue = "";
                    nameProperty.stringValue = "";
                }
                else
                {
                    var varOption = visibleOptions[selected - 1];
                    idProperty.stringValue = varOption.ID;
                    nameProperty.stringValue = varOption.Name;
                }
            }
            else if (!displayEditor)
            {
                EditorGUI.LabelField(customRect, editorLabelContent);
            }
            else if (!propertyType.IsArray)
            {
                DrawEditor(dataProperty, customRect, GUIContent.none, propertyType);
            }
            else
            {
                // Cannot edit arrays here, please call RenderActionInputArray directly
            }
        }

        public static bool InputContainsNullVariableOrValue(SerializedProperty variableProperty)
        {
            SerializedProperty isVariableProperty =
                variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.isVariable));

            if (isVariableProperty.boolValue)
            {
                SerializedProperty idProperty =
                    variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.variableID));
                SerializedProperty nameProperty =
                    variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.name));

                return string.IsNullOrEmpty(idProperty.stringValue) && string.IsNullOrEmpty(nameProperty.stringValue);
            }
            
            SerializedProperty dataProperty =
                variableProperty.FindPropertyRelative(nameof(CyanTriggerActionVariableInstance.data));
            return CyanTriggerSerializableObject.ObjectFromSerializedProperty(dataProperty) == null;
        }
    }
}