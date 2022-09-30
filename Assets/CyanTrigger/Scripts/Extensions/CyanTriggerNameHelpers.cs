using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace CyanTrigger
{
    public static class CyanTriggerNameHelpers
    {
        public static string GetTypeFriendlyName(Type type)
        {
            if (type == null)
            {
                return "null";
            }
            
            if (type.IsArray)
            {
                return GetTypeFriendlyName(type.GetElementType()) + "Array";
            }
            
            if (type == typeof(int))
            {
                return "int";
            }
            if (type == typeof(uint))
            {
                return "uint";
            }
            if (type == typeof(short))
            {
                return "short";
            }
            if (type == typeof(ushort))
            {
                return "ushort";
            }
            if (type == typeof(long))
            {
                return "long";
            }
            if (type == typeof(ulong))
            {
                return "ulong";
            }
            if (type == typeof(float))
            {
                return "float";
            }
            if (type == typeof(bool))
            {
                return "bool";
            }
            if (type == typeof(IUdonEventReceiver))
            {
                return nameof(UdonBehaviour);
            }

            string name = type.Name;

            if (type.IsEnum)
            {
                name += "Enum";
            }
            
            return name;
        }

        public static string GetSanitizedTypeName(Type type)
        {
            // TODO?
            return type.ToString().Replace(".", "").Replace("+", "_");
        }

        public static string GetMethodFriendlyName(string methodName)
        {
            methodName = methodName.Replace("op_", "");
            if (methodName.StartsWith("set_"))
            {
                methodName = "Set " + methodName.Substring(4);
            }
            if (methodName.StartsWith("get_"))
            {
                methodName = "Get " + methodName.Substring(4);
            }
            if (methodName.Replace("ector", "").Contains("ctor"))
            {
                methodName = methodName.Replace("ctor", "Constructor");
            }

            return methodName;
        }

        public static string SanitizeName(string originalName)
        {
            if (originalName.Length > 0 && char.IsDigit(originalName[0]))
            {
                originalName = "_" + originalName;
            }
            
            string name = Regex.Replace(originalName, @"[^a-zA-Z0-9_]", "").Trim();
            if (string.IsNullOrEmpty(name))
            {
                name = "name";
            }
            return name;
        }
        
        public static void TruncateContent(GUIContent content, Rect rect)
        {
            string originalText = content.text;
            Vector2 dim = GUI.skin.label.CalcSize(content);

            int min = 4;
            int max = originalText.Length;

            int itr = 0;
            if (dim.x > rect.width)
            {
                while (min < max && itr < 20)
                {
                    ++itr;
                    int mid = (min + max + 1) / 2;
                    
                    content.text = originalText.Substring(0,mid) + "...";
                    dim = GUI.skin.label.CalcSize(content);

                    if (dim.x > rect.width)
                    {
                        max = mid - 1;
                    }
                    else
                    {
                        if (mid == min)
                        {
                            break;
                        }
                        min = mid;
                    }
                }

                if (itr > 10)
                {
                    Debug.LogWarning("Infinite binary search!");
                }
                content.text = originalText.Substring(0,min) + "...";
            }
        }
    }
}
