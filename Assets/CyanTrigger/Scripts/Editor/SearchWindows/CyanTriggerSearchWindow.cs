using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
#if UNITY_2019_3_OR_NEWER
using UnityEditor.Experimental.GraphView;
#else
using UnityEditor.Experimental.UIElements.GraphView;
#endif

namespace CyanTrigger
{
    // Wrapper class for SearchWindow just so I can check if clicking search entries used the right mouse button...
    public class CyanTriggerSearchWindow : SearchWindow
    {
        public static bool WasEventRightClick;

        private static readonly MethodInfo OnGUIMethod;
        private static readonly MethodInfo InitMethod;
        private static readonly FieldInfo FilterWindowField;
        private static readonly FieldInfo LastClosedTimeField;
        
        static CyanTriggerSearchWindow()
        {
            OnGUIMethod = typeof(SearchWindow).GetMethod("OnGUI", BindingFlags.Instance | BindingFlags.NonPublic);
            InitMethod = typeof(SearchWindow).GetMethod("Init", BindingFlags.Instance | BindingFlags.NonPublic);
            FilterWindowField = typeof(SearchWindow).GetField("s_FilterWindow", BindingFlags.Static | BindingFlags.NonPublic);
            LastClosedTimeField = typeof(SearchWindow).GetField("s_LastClosedTime", BindingFlags.Static | BindingFlags.NonPublic);
        }
        
        // Taken and modified from Unity's SearchWindow.
        public new static bool Open<T>(SearchWindowContext context, T provider) where T : ScriptableObject, ISearchWindowProvider
        {
            UnityEngine.Object[] objectsOfTypeAll = Resources.FindObjectsOfTypeAll(typeof (SearchWindow));
            if (objectsOfTypeAll.Length > 0)
            {
                try
                {
                    ((EditorWindow) objectsOfTypeAll[0]).Close();
                    return false;
                }
                catch (Exception)
                {
                    FilterWindowField.SetValue(null, null);
                }
            }
            if (DateTime.Now.Ticks / 10000L < (long)LastClosedTimeField.GetValue(null) + 50L)  
                return false;

            SearchWindow window = (SearchWindow)FilterWindowField.GetValue(null);
            if (window == null)
            {
                window = ScriptableObject.CreateInstance<CyanTriggerSearchWindow>();
                window.hideFlags = HideFlags.HideAndDontSave;
                FilterWindowField.SetValue(null, window);
            }
            InitMethod.Invoke(window, new object[]{context, (ScriptableObject) provider});
            return true;
        }
        
        void OnGUI()
        {
            Event cur = Event.current;
            WasEventRightClick = cur.type == EventType.MouseDown && cur.button == 1;
            
            OnGUIMethod.Invoke(this, null);
            
            WasEventRightClick = false;
        }
    }
}
