using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace CyanTrigger
{
    public abstract class CyanTriggerScopedDataTreeView<T> : CyanTriggerScopedTreeView
    {
        private Dictionary<int, T> _itemIdsToData = new Dictionary<int, T>();
        
        protected CyanTriggerScopedDataTreeView(
            SerializedProperty elements, 
            MultiColumnHeader header, 
            Func<SerializedProperty, int> getElementScopeDelta, 
            Func<SerializedProperty, string> getElementDisplayName) 
            : base(elements, header, getElementScopeDelta, getElementDisplayName) { }

        
        
        protected override void OnElementsRemapped(int[] mapping, int prevIdStart)
        {
            var newItems = new Dictionary<int, T>();
            foreach (var item in _itemIdsToData)
            {
                int id = item.Key;
                int index = id - prevIdStart;
                if (id != -1 && index < mapping.Length && mapping[index] != -1)
                {
                    newItems.Add(mapping[index], item.Value);
                    OnElementRemapped(item.Value, id, mapping[index]);
                }
            }
            _itemIdsToData = newItems;
        }
        
        protected virtual void OnElementRemapped(T element, int prevIndex, int newIndex) { }

        protected void SetData(int id, T data)
        {
            _itemIdsToData[id] = data;
        }

        protected T GetData(int id)
        {
            if (_itemIdsToData.TryGetValue(id, out T data))
            {
                return data;
            }
            return default;
        }
        
        protected void ClearData()
        {
            _itemIdsToData.Clear();
        }

        protected IEnumerable<(T, int)> GetData()
        {
            foreach (var item in _itemIdsToData)
            {
                yield return (item.Value, item.Key);
            }
        }
    }
}
