using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CyanTrigger
{
    public class CyanTriggerScopedTreeItem : TreeViewItem
    {
        public int Index;
        public bool HasScope;
        public int ScopeEndIndex;

        public CyanTriggerScopedTreeItem() {}
        public CyanTriggerScopedTreeItem(int id, int depth, string name) : base(id, depth, name) { }
    }
    
    public abstract class CyanTriggerScopedTreeView : TreeView
    {
        private const string kDragAndDropDataKey = "ScopedTreeViewDragging";
        private const string kDragAndDropObjectDataKey = "ScopedTreeViewDraggingObject";

        private SerializedProperty _elements;
        public SerializedProperty Elements
        {
            get => _elements;
            set => _elements = value;
        }

        protected CyanTriggerScopedTreeItem[] Items;
        protected SerializedProperty[] ItemElements;
        private readonly Func<SerializedProperty, int> _getElementScopeDelta;
        private readonly Func<SerializedProperty, string> _getElementDisplayName;

        private Action<Rect, TreeViewItem> _setGUIDragInsertRect;
        
        public int Size { get; private set; }
        public int VisualSize { get; private set; }

        private int _idStartIndex;
        public int IdStartIndex
        {
            get => _idStartIndex;
            set
            {
                if (value == _idStartIndex)
                {
                    return;
                }

                int prev = _idStartIndex;
                _idStartIndex = value;
                OnIdStartIndexChanged(prev, value);
                Reload();
            }
        }

        protected void OnIdStartIndexChanged(int prev, int cur)
        {
            UpdateExpandedAndSelection(prev);
        }

        protected CyanTriggerScopedTreeView(
            SerializedProperty elements, 
            MultiColumnHeader header, 
            Func<SerializedProperty, int> getElementScopeDelta,
            Func<SerializedProperty, string> getElementDisplayName) : base (new TreeViewState())
        {
            _elements = elements;
            multiColumnHeader = header;
            _getElementScopeDelta = getElementScopeDelta;
            _getElementDisplayName = getElementDisplayName;

            InitializeReflectionItems();
            
            Reload();
        }

        private void InitializeReflectionItems()
        {
            Func<TreeViewItem, int> getItemControlID = (item) => 0;
            Func<int> getDropTargetControlID = () => 0;
            
            // Get TreeViewController and allow deselection on clicking nothing
            FieldInfo info = typeof(TreeView).GetField("m_TreeView", BindingFlags.NonPublic | BindingFlags.Instance);
            if (info != null)
            {
                var item = info.GetValue(this);
                PropertyInfo deselectInfo = item.GetType().GetProperty("deselectOnUnhandledMouseDown");
                if (deselectInfo != null)
                {
                    deselectInfo.SetValue(item, true);
                }

                MethodInfo GetItemControlIDInfo = item.GetType().GetMethod("GetItemControlID", BindingFlags.Static | BindingFlags.NonPublic);
                getItemControlID = viewItem => (int)GetItemControlIDInfo.Invoke(null, new object[] {viewItem});
            }
            FieldInfo draggingInfo = typeof(TreeView).GetField("m_Dragging", BindingFlags.NonPublic | BindingFlags.Instance);
            if (draggingInfo != null)
            {
                var dragging = draggingInfo.GetValue(this);
                MethodInfo guiInsertRectProperty = dragging.GetType().GetMethod("GetDropTargetControlID");
                getDropTargetControlID = () => (int) guiInsertRectProperty.Invoke(dragging, new object[0]);
            }
            
            FieldInfo guiInfo = typeof(TreeView).GetField("m_GUI", BindingFlags.NonPublic | BindingFlags.Instance);
            if (guiInfo != null)
            {
                var treeGUI = guiInfo.GetValue(this);
                FieldInfo guiInsertRectProperty = treeGUI.GetType().GetField("m_DraggingInsertionMarkerRect",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                
                _setGUIDragInsertRect = (rect, item) =>
                {
                    if (getItemControlID(item) != getDropTargetControlID())
                    {
                        return;
                    }
                    
                    float foldoutIndent = GetFoldoutIndent(item) + 3;
                    rect = new Rect(rect.x + foldoutIndent + CyanTriggerEditorGUIUtil.FoldoutStyle.fixedWidth, rect.yMax - 4,
                        rect.width - foldoutIndent, rect.height);
                    guiInsertRectProperty.SetValue(treeGUI, rect);
                };
            }
        }

        public int GetItemIndex(TreeViewItem item)
        {
            return ((CyanTriggerScopedTreeItem) item).Index;
        }
        
        public int GetItemIndex(int id)
        {
            return id - IdStartIndex;
        }

        public CyanTriggerScopedTreeItem GetItem(int index)
        {
            return Items[index];
        }

        protected override bool CanBeParent(TreeViewItem item)
        {
            // Required to fix bug that breaks drag and drop from anywhere blocking top half of any property inspector.
            if (Event.current.type == EventType.DragUpdated || 
                Event.current.type == EventType.DragExited || 
                Event.current.type == EventType.DragPerform)
            {
                return true;
            }
            
            CyanTriggerScopedTreeItem customItem = (CyanTriggerScopedTreeItem) item;
            return customItem.HasScope;
        }

        // Override to reject for other reasons. 
        protected virtual bool ShouldRejectDragAndDrop(DragAndDropArgs args, CyanTriggerScopedTreeItem parent)
        {
            return false;
        }
        
        protected virtual bool CanItemBeRemoved(CyanTriggerScopedTreeItem item)
        {
            return true;
        }
        
        protected virtual bool CanItemBeMoved(CyanTriggerScopedTreeItem item)
        {
            return true;
        }

        protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args)
        {
            // TODO provide interface for dragging to other tree items.
            var dragData = DragAndDrop.GetGenericData(kDragAndDropObjectDataKey);
            if (dragData == null)
            {
                // TODO? This could be anything. 
                return DragAndDropVisualMode.None;
            }

            Type myTreeViewType = GetType();
            // Not a treeview or not the same type of treeview
            if (!(dragData is CyanTriggerScopedTreeView treeView) || treeView.GetType() != myTreeViewType)
            {
                return DragAndDropVisualMode.None;
            }
            
            if (!(DragAndDrop.GetGenericData(kDragAndDropDataKey) is List<int> draggedIds))
            {
                return DragAndDropVisualMode.None;
            }
            
            CyanTriggerScopedTreeItem parent = (CyanTriggerScopedTreeItem)args.parentItem;
            if (parent == null)
            {
                parent = (CyanTriggerScopedTreeItem)rootItem;
            }

            if (!parent.HasScope && args.dragAndDropPosition == DragAndDropPosition.UponItem)
            {
                CyanTriggerScopedTreeItem par = (CyanTriggerScopedTreeItem)parent.parent;
                args.dragAndDropPosition = DragAndDropPosition.BetweenItems;
                args.insertAtIndex = par.children.IndexOf(parent) + 1;
                parent = par;
            }
            
            if (ShouldRejectDragAndDrop(args, parent))
            {
                return DragAndDropVisualMode.Rejected;
            }
            
            if (treeView != this)
            {
                if (!IsOverridden(myTreeViewType, nameof(GetProperties)) ||
                    !IsOverridden(myTreeViewType, nameof(DuplicateProperties)))
                {
                    return DragAndDropVisualMode.Rejected;
                }
                
                if (args.performDrop)
                {
                    MoveItemsFromOtherTree(treeView, draggedIds, parent, args.dragAndDropPosition, args.insertAtIndex);
                }

                return DragAndDropVisualMode.Move;
            }
            

            HashSet<int> ids = new HashSet<int>(draggedIds);
            // Reject movement where a parent would be put underneath itself
            {
                CyanTriggerScopedTreeItem temp = parent;
                while (temp != null)
                {
                    if (ids.Contains(temp.id))
                    {
                        return DragAndDropVisualMode.Rejected;
                    }

                    temp = (CyanTriggerScopedTreeItem)temp.parent;
                }
            }
            
            if (args.performDrop)
            {
                SetSelection(draggedIds);
                MoveElements(draggedIds, parent, args.dragAndDropPosition, args.insertAtIndex);
            }
            return DragAndDropVisualMode.Move;
        }

        protected void MoveElements(
            List<int> movedIds, 
            CyanTriggerScopedTreeItem parent, 
            DragAndDropPosition dragPosition, 
            int insertPosition)
        {
            SetExpanded(parent.id, true);
            
            List<int> movedItems = new List<int>();
            foreach (int id in movedIds)
            {
                int index = GetItemIndex(id);
                if (CanItemBeMoved(Items[index]))
                {
                    movedItems.Add(id);
                }
            }
            movedItems.Sort();
                
            MoveElementProperties(new List<int>(movedItems), parent, dragPosition, insertPosition);
            MoveTreeNodes(movedItems, parent, dragPosition, insertPosition);
            UpdateExpandedAndSelection();
            _elements.serializedObject.ApplyModifiedProperties();
            Reload();
        }
        
        private void MoveElementProperties(
            List<int> movedIds, 
            CyanTriggerScopedTreeItem parent,
            DragAndDropPosition dragPosition, 
            int insertPosition)
        {
            movedIds.Reverse();
            
            int insertIndex = 0;
            if (dragPosition == DragAndDropPosition.UponItem)
            {
                insertIndex = parent.ScopeEndIndex;
            }
            else
            {
                int child = insertPosition - 1;
                if (child < 0)
                {
                    insertIndex = parent.Index + 1;
                }
                else
                {
                    insertIndex = parent.children == null
                        ? parent.ScopeEndIndex
                        : ((CyanTriggerScopedTreeItem) parent.children[child]).ScopeEndIndex + 1;
                }
            }
            
            int movedItems = 0;
            int origInsert = insertIndex;

            foreach (int id in movedIds)
            {
                int index = GetItemIndex(id);
                CyanTriggerScopedTreeItem item = Items[index];
                    
                int idIndex = index;
                if (idIndex >= origInsert)
                {
                    idIndex += movedItems;
                }

                int totalMoved = item.ScopeEndIndex - index + 1;
                
                for (int child = 0; child < totalMoved; ++child)
                {
                    int from = idIndex;
                    int to = insertIndex;

                    if (idIndex >= insertIndex)
                    {
                        from += child;
                        to += child;
                    }
                    else
                    {
                        --to;
                    }

                    if (from != to)
                    {
                        Elements.MoveArrayElement(from, to);
                    }
                }

                // loop through through all parents to update scope ends
                CyanTriggerScopedTreeItem ancestor = GetClosestAncestor(item, parent);
                CyanTriggerScopedTreeItem temp = (CyanTriggerScopedTreeItem) item.parent;
                while (temp != ancestor)
                {
                    temp.ScopeEndIndex -= totalMoved;
                    temp = (CyanTriggerScopedTreeItem) temp.parent;
                }

                if (idIndex < insertIndex)
                {
                    insertIndex -= totalMoved;
                }
                movedItems += totalMoved;
            }
        }

        private void MoveTreeNodes(
            List<int> movedIds,
            CyanTriggerScopedTreeItem parent,
            DragAndDropPosition dragPosition, 
            int insertPosition)
        {
            if (!parent.hasChildren)
            {
                parent.children = new List<TreeViewItem>();
            }
            
            int insertIndex = insertPosition;
            if (dragPosition == DragAndDropPosition.UponItem)
            {
                insertIndex = parent.children.Count;
            }
            
            foreach (int id in movedIds)
            {
                int index = GetItemIndex(id);
                var node = Items[index];
                var prevParent = node.parent;
                if (prevParent == parent)
                {
                    int nodeIndex = prevParent.children.IndexOf(node);
                    if (nodeIndex < insertIndex)
                    {
                        --insertIndex;
                    }
                }
                node.parent.children.Remove(node);
                parent.children.Insert(insertIndex, node);
                ++insertIndex;
            }
        }

        private void RemapNodeIds(CyanTriggerScopedTreeItem node, int[] mapping, ref int id)
        {
            if (node != rootItem)
            {
                mapping[node.Index] = IdStartIndex + id;
                node.id = IdStartIndex + id;
                node.Index = id;
                ++id;
            }

            if (node.children != null)
            {
                foreach (var child in node.children)
                {
                    RemapNodeIds((CyanTriggerScopedTreeItem)child, mapping, ref id);
                }
            }

            if (node.HasScope)
            {
                ++id;
            }
        }

        private void UpdateExpandedAndSelection(int prevIdStart = -1)
        {
            if (prevIdStart == -1)
            {
                prevIdStart = IdStartIndex;
            }
            
            int[] mapping = new int[Elements.arraySize];
            for (int i = 0; i < mapping.Length; ++i)
            {
                mapping[i] = -1;
            }
            int idRef = 0;
            RemapNodeIds((CyanTriggerScopedTreeItem)rootItem, mapping, ref idRef);

            List<int> selection = new List<int>();
            List<int> expanded = new List<int>();

            foreach (var id in GetSelection())
            {
                int index = id - prevIdStart;
                if (id != -1 && mapping[index] != -1)
                {
                    selection.Add(mapping[index]);
                }
            }
            foreach (var id in GetExpanded())
            {
                int index = id - prevIdStart;
                if (id != -1 && mapping[index] != -1)
                {
                    expanded.Add(mapping[index]);    
                }
            }
            OnElementsRemapped(mapping, prevIdStart);
            SetSelection(selection, TreeViewSelectionOptions.FireSelectionChanged);
            SetExpanded(expanded);
        }

        protected virtual void OnElementsRemapped(int[] mapping, int prevIdStart) { }

        public IList<int> GetExpandedWithoutStartId()
        {
            IList<int> expand = GetExpanded();
            for (int index = 0; index < expand.Count; ++index)
            {
                expand[index] -= _idStartIndex;
            }
            return expand;
        }

        public void SetExpandedApplyingStartId(IList<int> expand)
        {
            for (int index = 0; index < expand.Count; ++index)
            {
                expand[index] += _idStartIndex;
            }
            SetExpanded(expand);
        }

        // Remove selected and child elements
        public void RemoveSelected()
        {
            List<int> selected = new List<int>();
            foreach (int id in GetSelection())
            {
                int index = GetItemIndex(id);
                if (CanItemBeRemoved(Items[index]))
                {
                    selected.Add(id);
                }
            }
            selected.Sort();
            selected.Reverse();

            // Update node list first so that element size contains everything
            foreach (var id in selected)
            {
                int index = GetItemIndex(id);
                var item = Items[index];
                var parent = item.parent;
                parent.children.Remove(item);
            }

            List<CyanTriggerScopedTreeItem> removedItems = new List<CyanTriggerScopedTreeItem>();
            List<int> idsToRemove = new List<int>();
            
            // update serialized properties to remove the node and everything between it and the end scope node.
            foreach (int id in selected)
            {
                int index = GetItemIndex(id);
                var item = Items[index];
                if (item == null)
                {
                    continue;
                }

                removedItems.Add(item);
                idsToRemove.Add(index);
                
                if (item.ScopeEndIndex != index)
                {
                    int scope = 1;

                    int tmpIndex = index;
                    ++index;
                    while (index < Elements.arraySize)
                    {
                        int scopeDelta = _getElementScopeDelta(Elements.GetArrayElementAtIndex(index));
                        idsToRemove.Add(tmpIndex);
                        scope += scopeDelta;
                        if (scope == 0)
                        {
                            break;
                        }

                        if (Items[index] != null)
                        {
                            removedItems.Add(Items[index]);
                        }
                        ++index;
                    }
                }
            }

            OnItemsRemoved(removedItems);

            // Selection is cleared as selection was removed from the tree
            SetSelection(new List<int>(), TreeViewSelectionOptions.FireSelectionChanged);
            UpdateExpandedAndSelection();
            
            foreach (var id in idsToRemove)
            {
                Elements.DeleteArrayElementAtIndex(id);
            }
            
            _elements.serializedObject.ApplyModifiedProperties();
            Reload();
        }
        
        protected virtual void OnItemsRemoved(List<CyanTriggerScopedTreeItem> removedItems) { }
        
        protected override bool CanStartDrag(CanStartDragArgs args) => true;
        
        protected override void SetupDragAndDrop(SetupDragAndDropArgs args)
        {
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.SetGenericData(kDragAndDropDataKey, new List<int>(args.draggedItemIDs));
            DragAndDrop.SetGenericData(kDragAndDropObjectDataKey, this);
            DragAndDrop.objectReferences = new Object[0];
            DragAndDrop.StartDrag("Drag Elements");
        }

        protected CyanTriggerScopedTreeItem GetClosestAncestor(
            CyanTriggerScopedTreeItem n1,
            CyanTriggerScopedTreeItem n2)
        {
            // Get N1 to the same depth as n2
            while (n1.depth > n2.depth)
            {
                n1 = (CyanTriggerScopedTreeItem) n1.parent;
            }
            // Get N2 to the same depth as n1
            while (n2.depth > n1.depth)
            {
                n2 = (CyanTriggerScopedTreeItem) n2.parent;
            }

            // Keep crawling up until you find the same node.
            while (n1 != n2)
            {
                n1 = (CyanTriggerScopedTreeItem) n1.parent;
                n2 = (CyanTriggerScopedTreeItem) n2.parent;
            }

            return n1;
        }

        protected override TreeViewItem BuildRoot()
        {
            int arraySize = Elements.arraySize;
            Size = arraySize;
            VisualSize = 0;
            Stack<CyanTriggerScopedTreeItem> parents = new Stack<CyanTriggerScopedTreeItem>();
            
            CyanTriggerScopedTreeItem root = new CyanTriggerScopedTreeItem()
            {
                id = -1,
                depth = -1,
                displayName = "Root",
                Index = -1,
            };
            parents.Push(root);
            
            Items = new CyanTriggerScopedTreeItem[Size];
            ItemElements = new SerializedProperty[Size];
            
            List<TreeViewItem> treeViewItemList = new List<TreeViewItem>(arraySize);
            for (int id = 0; id < arraySize; ++id)
            {
                var property = Elements.GetArrayElementAtIndex(id);
                ItemElements[id] = property;
                string name = _getElementDisplayName(property);
                int scopeDelta = _getElementScopeDelta(property);
                
                if (scopeDelta < 0)
                {
                    var lastParent = parents.Pop();
                    lastParent.ScopeEndIndex = id;
                    continue;
                }

                ++VisualSize;

                CyanTriggerScopedTreeItem treeViewItem =
                    new CyanTriggerScopedTreeItem(id + IdStartIndex, parents.Count - 1, name);
                treeViewItemList.Add(treeViewItem);
                treeViewItem.Index = id;
                treeViewItem.ScopeEndIndex = id;
                treeViewItem.HasScope = scopeDelta != 0;
                Items[id] = treeViewItem;
                
                if (scopeDelta > 0)
                {
                    parents.Push(treeViewItem);
                }
            }
            SetupParentsAndChildrenFromDepths(root, treeViewItemList);
            OnBuildRoot(root);
            return root;
        }
        
        public override void OnGUI(Rect rect)
        {
            if (Size != Elements.arraySize)
            {
                Reload();
            }
            base.OnGUI(rect);

            HandleRightClick(rect);
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            if (Event.current.type == EventType.Repaint)
            {
                _setGUIDragInsertRect(args.rowRect, args.item);
            }
            
            OnRowGUI(args);
        }

        protected virtual void OnRowGUI(RowGUIArgs args)
        {
            base.RowGUI(args);
        }
        
        // Manually override drawing alternating rows since it does not properly handle rows with different heights.
        protected override void BeforeRowsGUI()
        {
            DrawAlternatingRowBackgrounds();
        }
        
        public void DrawAlternatingRowBackgrounds()
        {
            if (Event.current.rawType != EventType.Repaint)
                return;
            float height = treeViewRect.height + state.scrollPos.y;
            DefaultStyles.backgroundOdd.Draw(new Rect(0.0f, 0.0f, 100000f, height), false, false, false, false);
            int firstRowVisible = 0;
            int count = GetRows().Count;
            Rect position = new Rect(0.0f, 0.0f, 0.0f, rowHeight);
            int row = firstRowVisible;
            while ((double) position.yMax < (double) height)
            {
                if (row < count)
                    position = GetRowRect(row);
                else if (row > 0)
                    position.y += position.height;
                position.width = 100000f;
                
                if (row % 2 != 1)
                {
                    DefaultStyles.backgroundEven.Draw(position, false, false, false, false);
                }
                ++row;
            }
        }

        protected override void KeyEvent()
        {
            if (Event.current.type != EventType.KeyDown)
            {
                return;
            }
            
            KeyCode keyCode = Event.current.keyCode;
            switch (keyCode)
            {
                case KeyCode.Delete:
                {
                    Event.current.Use();
                    RemoveSelected();
                    break;
                }
                case KeyCode.D:
                {
                    if (Event.current.modifiers == EventModifiers.Control)
                    {
                        Event.current.Use();
                        if (CanDuplicate(GetSelection()))
                        {
                            DuplicateSelectedItems();
                        }
                    }
                    break;
                }
            }
        }

        protected virtual void OnBuildRoot(CyanTriggerScopedTreeItem root) { }

        protected virtual bool CanDuplicate(IEnumerable<int> items)
        {
            return false;
        }

        protected virtual List<int> DuplicateItems(IEnumerable<int> items)
        {
            throw new NotImplementedException();
        }

        protected virtual List<SerializedProperty> GetProperties(IEnumerable<int> items)
        {
            return new List<SerializedProperty>();
        }
        
        protected virtual List<SerializedProperty> DuplicateProperties(IEnumerable<SerializedProperty> items)
        {
            throw new NotImplementedException();
        }

        protected virtual void DuplicateSelectedItems()
        {
            List<int> sortedItems = new List<int>(GetSelection());
            sortedItems.Sort();

            int parentId = -1;
            int minDepth = Int32.MaxValue;
            foreach (int id in sortedItems)
            {
                int index = GetItemIndex(id);
                var item = Items[index];
                if (item.depth < minDepth)
                {
                    minDepth = item.depth;
                    parentId = item.parent.id;
                }
            }

            var dupedIds = DuplicateItems(sortedItems);
            SetSelection(new List<int>());
            Reload();

            var dupedRootIds = GetRootItemsFromList(dupedIds);
            SetSelection(dupedRootIds, TreeViewSelectionOptions.FireSelectionChanged);
            
            // Move items into nested position and not at the end of the root
            if (parentId != -1)
            {
                MoveElements(dupedRootIds, Items[GetItemIndex(parentId)], DragAndDropPosition.UponItem, 0);
            }
            _elements.serializedObject.ApplyModifiedProperties();
        }

        protected List<int> GetRootItemsFromList(List<int> itemsToMove)
        {
            List<int> rootItems = new List<int>();
            foreach (int id in itemsToMove)
            {
                int index = GetItemIndex(id);
                var item = Items[index];
                
                // Only find items at the root
                if (item != null && item.parent.id == -1)
                {
                    rootItems.Add(id);
                }
            }

            return rootItems;
        }

        private void MoveItemsFromOtherTree(
            CyanTriggerScopedTreeView srcTree, 
            List<int> draggedIds, 
            CyanTriggerScopedTreeItem parent,
            DragAndDropPosition dragPosition, 
            int insertPosition)
        {
            // Duplicate elements into new tree.
            // Move duplicate elements from bottom to expected location.
            // Remove elements from source tree.
            
            var properties = srcTree.GetProperties(draggedIds);
            if (properties == null || properties.Count == 0)
            {
                return;
            }

            int startIndex = Elements.arraySize;
            // Count is zero, prevent insertPosition from causing -1 exception
            if (startIndex == 0)
            {
                insertPosition = 0;
            }
            
            int count = DuplicateProperties(properties).Count;

            //Elements.serializedObject.ApplyModifiedProperties();

            SetSelection(new List<int>());
            Reload();
            
            List<int> movedIds = new List<int>();
            for (int i = startIndex; i < startIndex + count; ++i)
            {
                movedIds.Add(i + IdStartIndex);
            }
            
            var rootIds = GetRootItemsFromList(movedIds);
            SetSelection(movedIds);
            MoveElements(rootIds, parent, dragPosition, insertPosition);

            srcTree.SetSelection(draggedIds);
            srcTree.RemoveSelected();
        }

        protected virtual bool ShowRightClickMenu()
        {
            return true;
        }

        protected virtual bool AllowRenameOption()
        {
            return true;
        }

        protected virtual void GetRightClickMenuOptions(GenericMenu menu, Event currentEvent)
        {
            GUIContent deselectContent = new GUIContent("Deselect", "Deselect the current selection");
            GUIContent duplicateContent = new GUIContent("Duplicate", "Duplicate the current selected items");
            GUIContent deleteContent = new GUIContent("Delete", "Duplicate the current selected items");
            GUIContent renameContent = new GUIContent("Rename", "Rename current item");

            IList<int> selection = GetSelection();
            
            // Rename
            if (AllowRenameOption())
            {
                bool canRenameItem = false;
                TreeViewItem renameItem = null;
                if (selection.Count == 1)
                {
                    int id = selection[0];
                    renameItem = Items[GetItemIndex(id)];
                    canRenameItem = CanRename(renameItem);
                }

                if (canRenameItem)
                {
                    menu.AddItem(renameContent, false, () => BeginRename(renameItem));
                }
                else
                {
                    menu.AddDisabledItem(renameContent);
                }
            }

            if (selection.Count > 0)
            {
                menu.AddItem(deselectContent, false, () =>
                {
                    SetSelection(new int[0], TreeViewSelectionOptions.FireSelectionChanged);
                });
                
                if (CanDuplicate(selection))
                {
                    menu.AddItem(duplicateContent, false, DuplicateSelectedItems);
                }

                menu.AddItem(deleteContent, false, () =>
                {
                    RemoveSelected();
                    Elements.serializedObject.ApplyModifiedProperties();
                });
            }
            else
            {
                menu.AddDisabledItem(deselectContent, false);
                if (CanDuplicate(selection))
                {
                    menu.AddDisabledItem(duplicateContent, false);
                }
                menu.AddDisabledItem(deleteContent, false);
            }
        }
        
        private void HandleRightClick(Rect rect)
        {
            if (!ShowRightClickMenu())
            {
                return;
            }
            
            Event current = Event.current;
            if(current.type == EventType.ContextClick && rect.Contains(current.mousePosition))
            {
                GenericMenu menu = new GenericMenu();
                GetRightClickMenuOptions(menu, current);
                
                menu.ShowAsContext();
 
                current.Use(); 
            }
        }
        
        private static bool IsOverridden(Type type, string methodName)
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method != null)
                return method.GetBaseDefinition().DeclaringType != method.DeclaringType;
            Debug.LogError((object) ("IsOverridden: method name not found: " + methodName + " (check spelling against method declaration)"));
            return false;
        }
    }
}