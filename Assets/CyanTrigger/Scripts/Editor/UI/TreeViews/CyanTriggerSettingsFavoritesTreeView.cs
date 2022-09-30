using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace CyanTrigger
{
    public class CyanTriggerSettingsFavoritesTreeView : CyanTriggerScopedTreeView
    {
        private static MultiColumnHeader CreateColumnHeader()
        {
            MultiColumnHeaderState.Column[] columns =
            {
                new MultiColumnHeaderState.Column
                {
                    minWidth = 50f, width = 100f, headerTextAlignment = TextAlignment.Center, canSort = false
                }
            };
            MultiColumnHeader multiColumnHeader = new MultiColumnHeader(new MultiColumnHeaderState(columns))
            {
                height = 0f
            };
            multiColumnHeader.ResizeToFit();
            
            return multiColumnHeader;
        }
        
        private static string GetElementDisplayName(SerializedProperty property)
        {
            return property.FindPropertyRelative(nameof(CyanTriggerSettingsFavoriteItem.item)).stringValue;
        }
        
        private static int GetElementScopeDelta(SerializedProperty property)
        {
            return property.FindPropertyRelative(nameof(CyanTriggerSettingsFavoriteItem.scopeDelta)).intValue;
        }
        
        public CyanTriggerSettingsFavoritesTreeView(SerializedProperty elements) 
            : base (elements, CreateColumnHeader(), GetElementScopeDelta, GetElementDisplayName)
        {
            showBorder = true;
            showAlternatingRowBackgrounds = true;
            useScrollView = true;
            
            Reload();
        }
        
        protected override bool CanRename(TreeViewItem item)
        {
            return ((CyanTriggerScopedTreeItem)item).HasScope;
        }
        
        protected override void RenameEnded(RenameEndedArgs args)
        {
            int index = GetItemIndex(args.itemID);
            Elements.GetArrayElementAtIndex(index)
                .FindPropertyRelative(nameof(CyanTriggerSettingsFavoriteItem.item)).stringValue = args.newName;
            Items[index].displayName = args.newName;
        }
        
        // TODO make more generic?
        protected override void OnRowGUI(RowGUIArgs args)
        {
            var item = (CyanTriggerScopedTreeItem)args.item;
            Rect cellRect = args.GetCellRect(0);
            if (item.HasScope)
            {
                Rect folderRect = cellRect;
                folderRect.x += GetContentIndent(item);
                folderRect.width = 20;
                if (folderRect.xMax < cellRect.xMax)
                {
                    EditorGUI.LabelField(folderRect, EditorGUIUtility.TrIconContent("Folder Icon", item.displayName +" " +item.Index));
                    cellRect.width -= folderRect.width;
                    cellRect.x += folderRect.width;
                }
        
                // Default icon and label
                args.rowRect = cellRect;
            }
            base.OnRowGUI(args);
        }
    }
}

