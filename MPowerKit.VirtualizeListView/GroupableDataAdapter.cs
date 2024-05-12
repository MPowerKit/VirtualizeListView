using System.Collections;
using System.Collections.Specialized;

namespace MPowerKit.VirtualizeListView;

public class GroupableDataAdapter : DataAdapter
{
    public class GroupItem
    {
        public GroupItem(object item)
        {
            Item = item;
        }
        public object Item { get; set; }
    }
    public class GroupHeader(object item) : GroupItem(item) { }
    public class GroupFooter(object item) : GroupItem(item) { }

    protected IEnumerable<IEnumerable> GroupedItems { get; set; }

    public virtual bool HasGroupHeader => Control.IsGrouped && Control.GroupHeaderTemplate is not null;
    public virtual bool HasGroupFooter => Control.IsGrouped && Control.GroupFooterTemplate is not null;

    public GroupableDataAdapter(VirtualizeListView listView) : base(listView)
    {
    }

    public virtual bool IsGroupHeader(DataTemplate template, int position)
    {
        return IsOneOf(Control.GroupHeaderTemplate, template, position);
    }

    public virtual bool IsGroupFooter(DataTemplate template, int position)
    {
        return IsOneOf(Control.GroupFooterTemplate, template, position);
    }

    protected override DataTemplate GetItemTemplate(object item)
    {
        if (item is GroupHeader groupHeader)
        {
            return GetGroupHeaderTemplate(groupHeader.Item);
        }

        if (item is GroupFooter groupFooter)
        {
            return GetGroupFooterTemplate(groupFooter.Item);
        }

        return base.GetItemTemplate(item);
    }

    protected virtual DataTemplate GetGroupHeaderTemplate(object item)
    {
        if (Control.GroupHeaderTemplate is DataTemplateSelector headerSelector)
        {
            return headerSelector.SelectTemplate(item, Control);
        }

        return Control.GroupHeaderTemplate;
    }

    protected virtual DataTemplate GetGroupFooterTemplate(object item)
    {
        if (Control.GroupFooterTemplate is DataTemplateSelector footerSelector)
        {
            return footerSelector.SelectTemplate(item, Control);
        }

        return Control.GroupFooterTemplate;
    }

    public override CellHolder OnCreateCell(DataTemplate template, int position)
    {
        var content = template.CreateContent() as View;

        if (HasHeader && position == 0 && content is VirtualizeListViewCell)
        {
            throw new ArgumentException("HeaderTemplate can't be typeof(VirtualizeListViewCell)");
        }

        if (HasFooter && position == ItemsCount - 1 && content is VirtualizeListViewCell)
        {
            throw new ArgumentException("FooterTemplate can't be typeof(VirtualizeListViewCell)");
        }

        var item = InternalItems[position];

        if (item is GroupHeader && content is VirtualizeListViewCell)
        {
            throw new ArgumentException("GroupHeaderTemplate can't be typeof(VirtualizeListViewCell)");
        }

        if (item is GroupFooter && content is VirtualizeListViewCell)
        {
            throw new ArgumentException("GroupFooterTemplate can't be typeof(VirtualizeListViewCell)");
        }

        if (IsOneOf(Control.ItemTemplate, template, position) && content is not VirtualizeListViewCell)
        {
            throw new ArgumentException("ItemTemplate has to be typeof(VirtualizeListViewCell)");
        }

        var holder = new CellHolder()
        {
            /*Content =*/ content
        };
        return holder;
    }

    public override void OnBindCell(CellHolder holder, int position)
    {
        var data = Items[position];

        holder.BindingContext = data is GroupItem groupItem ? groupItem.Item : data;

        if (holder.Children[0] is not VirtualizeListViewCell cell) return;

        cell.SendAppearing();
        OnItemAppearing(data, position);
    }

    protected override void OnItemAppearing(object item, int position)
    {
        var additional = HasHeader.ToInt();

        var totalCount = InternalItems.Count;

        var groupItemsCount = InternalItems.Count(i => i is GroupItem);

        var realPosition = 0;
        for (int i = additional; i < position; i++)
        {
            if (InternalItems[i] is GroupItem) continue;
            realPosition++;
        }

        var count = totalCount - groupItemsCount - additional - HasFooter.ToInt();

        if (count == 0) return;

        if (count <= Control.RemainingItemsThreshold) return;

        if (realPosition >= count - Control.RemainingItemsThreshold)
        {
            Control.OnItemAppearing(item, realPosition);
        }
    }

    public override void OnCellRecycled(CellHolder holder, int position)
    {
        var content = holder.Children[0];

        try
        {
            if (content is not VirtualizeListViewCell cell) return;

            cell.SendDisappearing();
            Control.SendItemDisappearing(holder.BindingContext, position - HasHeader.ToInt());
        }
        finally
        {
            // commented out for better perfomance
            // theoretically bindingcontext should be nullified
            // but practically performance getting worse if uncommented
            //holder.BindingContext = null;
        }
    }

    protected virtual int GetFlattenedIndexOfGroup(IEnumerable group)
    {
        var groupIndex = GroupedItems.IndexOf(group);
        return GetFlattenedIndexForGroupIndex(groupIndex);
    }

    protected virtual int GetFlattenedIndexForGroupIndex(int groupIndex)
    {
        var groupsToSkip = GroupedItems.Take(groupIndex);
        var skipCount = (HasGroupHeader.ToInt() + HasGroupFooter.ToInt()) * groupIndex;
        return skipCount + groupsToSkip.Sum(static g => g.Count());
    }

    protected override void RemoveListenerCollection(IEnumerable? itemsSource)
    {
        if (!Control.IsGrouped || itemsSource is null)
        {
            base.RemoveListenerCollection(itemsSource);
            return;
        }

        if (itemsSource is not IEnumerable<IEnumerable> groups)
        {
            throw new ArgumentException("The collection type for IsGrouped should be only typeof(IEnumerable<IEnumerable>) or derived interfaces or classes from it");
        }

        foreach (var group in groups)
        {
            if (group is INotifyCollectionChanged notifyGroup)
            {
                notifyGroup.CollectionChanged -= GroupChanged;
            }
        }

        base.RemoveListenerCollection(itemsSource);
    }

    public override void InitCollection(IEnumerable? itemsSource)
    {
        if (!Control.IsGrouped || itemsSource is null)
        {
            base.InitCollection(itemsSource);
            return;
        }

        if (itemsSource is not IEnumerable<IEnumerable> groups)
        {
            throw new ArgumentException("The collection type for IsGrouped should be only typeof(IEnumerable<IEnumerable>) or derived interfaces or classes from it");
        }

        foreach (var group in groups)
        {
            if (group is INotifyCollectionChanged notifyGroup)
            {
                notifyGroup.CollectionChanged += GroupChanged;
            }
        }

        base.InitCollection(itemsSource);
    }

    #region CollectionChanged events
    protected virtual IEnumerable FlattenItems(IEnumerable<IEnumerable> groups)
    {
        List<object> flatItems = [];

        var hasHeader = HasGroupHeader;
        var hasFooter = HasGroupFooter;

        foreach (var group in GroupedItems)
        {
            if (hasHeader) flatItems.Add(new GroupHeader(group));
            foreach (var item in group) flatItems.Add(item);
            if (hasFooter) flatItems.Add(new GroupFooter(group));
        }

        return flatItems;
    }

    protected override void OnCollectionChangedAdd(IEnumerable? itemsSource, NotifyCollectionChangedEventArgs e)
    {
        if (!Control.IsGrouped)
        {
            base.OnCollectionChangedAdd(itemsSource, e);
            return;
        }

        if (e.NewItems?.Count is null or 0) return;

        var hasHeader = HasGroupHeader;
        var hasFooter = HasGroupFooter;
        var realIndex = GetFlattenedIndexForGroupIndex(e.NewStartingIndex) + HasHeader.ToInt();

        List<object> list = [];
        foreach (IEnumerable group in e.NewItems)
        {
            if (group is INotifyCollectionChanged notifyGroup)
            {
                notifyGroup.CollectionChanged += GroupChanged;
            }

            if (hasHeader) list.Add(new GroupHeader(group));
            foreach (var item in group) list.Add(item);
            if (hasFooter) list.Add(new GroupFooter(group));
        }

        InternalItems.InsertRange(realIndex, list);
        NotifyItemRangeInserted(realIndex, list.Count);
    }

    protected override void OnCollectionChangedRemove(IEnumerable? itemsSource, NotifyCollectionChangedEventArgs e)
    {
        if (!Control.IsGrouped)
        {
            base.OnCollectionChangedRemove(itemsSource, e);
            return;
        }

        if (e.OldItems?.Count is null or 0) return;

        var hasHeader = HasGroupHeader;
        var hasFooter = HasGroupFooter;
        var realIndex = GetFlattenedIndexForGroupIndex(e.OldStartingIndex) + HasHeader.ToInt();

        var countToRemove = 0;
        foreach (IEnumerable group in e.OldItems)
        {
            if (group is INotifyCollectionChanged notifyGroup)
            {
                notifyGroup.CollectionChanged -= GroupChanged;
            }

            if (hasHeader) countToRemove++;
            foreach (var item in group) countToRemove++;
            if (hasFooter) countToRemove++;
        }

        InternalItems.RemoveRange(realIndex, countToRemove);
        NotifyItemRangeRemoved(realIndex, countToRemove);
    }

    protected override void OnCollectionChangedReplace(IEnumerable? itemsSource, NotifyCollectionChangedEventArgs e)
    {
        if (!Control.IsGrouped)
        {
            base.OnCollectionChangedReplace(itemsSource, e);
            return;
        }

        if (e.NewItems?.Count is null or 0 || e.OldItems?.Count is null or 0) return;

        var hasHeader = HasGroupHeader;
        var hasFooter = HasGroupFooter;
        var realIndex = GetFlattenedIndexForGroupIndex(e.NewStartingIndex) + HasHeader.ToInt();

        var countToRemove = 0;
        foreach (IEnumerable group in e.OldItems)
        {
            if (group is INotifyCollectionChanged notifyGroup)
            {
                notifyGroup.CollectionChanged -= GroupChanged;
            }

            if (hasHeader) countToRemove++;
            foreach (var item in group) countToRemove++;
            if (hasFooter) countToRemove++;
        }
        InternalItems.RemoveRange(realIndex, countToRemove);

        List<object> list = [];
        foreach (IEnumerable group in e.NewItems)
        {
            if (group is INotifyCollectionChanged notifyGroup)
            {
                notifyGroup.CollectionChanged += GroupChanged;
            }

            if (hasHeader) list.Add(new GroupHeader(group));
            foreach (var item in group) list.Add(item);
            if (hasFooter) list.Add(new GroupFooter(group));
        }
        InternalItems.InsertRange(realIndex, list);

        NotifyItemRangeChanged(e.OldStartingIndex, countToRemove, list.Count);
    }

    protected override void OnCollectionChangedMove(IEnumerable? itemsSource, NotifyCollectionChangedEventArgs e)
    {
        if (!Control.IsGrouped)
        {
            base.OnCollectionChangedMove(itemsSource, e);
            return;
        }

        if (e.NewItems?.Count is null or 0 or > 1 || e.OldItems?.Count is null or 0 or > 1
            || e.NewStartingIndex == e.OldStartingIndex) return;

        var additional = HasHeader.ToInt();
        var oldIndex = e.OldStartingIndex;
        var newIndex = e.NewStartingIndex;
        var realOldIndex = GetFlattenedIndexForGroupIndex(oldIndex) + additional;
        var realNewIndex = GetFlattenedIndexForGroupIndex(newIndex) + additional;
        var group = (e.NewItems[0] as IEnumerable)!;

        if (newIndex < oldIndex)
        {
            foreach (var item in group)
            {
                InternalItems.Move(realOldIndex, realNewIndex);
                NotifyItemMoved(realOldIndex++, realNewIndex++);
            }
        }
        else
        {
            foreach (var item in group)
            {
                InternalItems.Move(realOldIndex, realNewIndex);
                NotifyItemMoved(realOldIndex, realNewIndex);
            }
        }
    }

    protected override void OnCollectionChangedReset(IEnumerable? itemsSource)
    {
        if (!Control.IsGrouped || itemsSource is null)
        {
            base.OnCollectionChangedReset(itemsSource);
            return;
        }

        if (itemsSource is not IEnumerable<IEnumerable> groups)
        {
            throw new ArgumentException("The collection type for IsGrouped should be only typeof(IEnumerable<IEnumerable>) or derived interfaces or classes from it");
        }

        GroupedItems = groups;

        var flattenedItems = FlattenItems(groups);

        base.OnCollectionChangedReset(flattenedItems);
    }

    protected virtual void GroupChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (IsDisposed) return;

        if (sender is not IEnumerable group) return;

        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                GroupItemsAdd(group, e);
                break;
            case NotifyCollectionChangedAction.Remove:
                GroupItemsRemoved(group, e);
                break;
            case NotifyCollectionChangedAction.Replace:
                GroupItemsReplaced(group, e);
                break;
            case NotifyCollectionChangedAction.Move:
                GroupItemsMoved(group, e);
                break;
            case NotifyCollectionChangedAction.Reset:
                GroupItemsReset(group, e);
                break;
        }
    }

    protected virtual void GroupItemsAdd(IEnumerable group, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems?.Count is null or 0) return;

        var realIndex = e.NewStartingIndex + GetFlattenedIndexOfGroup(group) + HasGroupHeader.ToInt() + HasHeader.ToInt();

        InternalItems.InsertRange(realIndex, e.NewItems.Cast<object>());
        NotifyItemRangeInserted(realIndex, e.NewItems.Count);
    }

    protected virtual void GroupItemsRemoved(IEnumerable group, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems?.Count is null or 0) return;

        var realIndex = e.OldStartingIndex + GetFlattenedIndexOfGroup(group) + HasGroupHeader.ToInt() + HasHeader.ToInt();

        var count = e.OldItems.Count;

        InternalItems.RemoveRange(realIndex, count);
        NotifyItemRangeRemoved(realIndex, count);
    }

    protected virtual void GroupItemsReplaced(IEnumerable group, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems?.Count is null or 0 || e.OldItems?.Count is null or 0)
        {
            return;
        }

        var realIndex = e.NewStartingIndex + GetFlattenedIndexOfGroup(group) + HasGroupHeader.ToInt() + HasHeader.ToInt();
        var countToRemove = e.OldItems.Count;

        InternalItems.RemoveRange(realIndex, countToRemove);
        InternalItems.InsertRange(realIndex, e.NewItems.Cast<object>());
        NotifyItemRangeChanged(realIndex, countToRemove, e.NewItems.Count);
    }

    private void GroupItemsMoved(IEnumerable group, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems?.Count is null or 0 or > 1 || e.OldItems?.Count is null or 0 or > 1
            || e.NewStartingIndex == e.OldStartingIndex)
        {
            return;
        }

        var realIndex = GetFlattenedIndexOfGroup(group) + HasGroupHeader.ToInt() + HasHeader.ToInt();
        var oldIndex = e.OldStartingIndex + realIndex;
        var newIndex = e.NewStartingIndex + realIndex;

        InternalItems.Move(oldIndex, newIndex);
        NotifyItemMoved(oldIndex, newIndex);
    }

    private void GroupItemsReset(IEnumerable group, NotifyCollectionChangedEventArgs e)
    {
        var count = group.Count();

        if ((e.NewItems is null && e.OldItems is null)
            || (count > 0 && e.OldItems?.Count is null or 0))
        {
            GroupItemsAdd(group, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, group.Cast<object>().ToList(), 0));
            return;
        }

        if (count == 0 && e.OldItems is not null && e.OldItems.Count > 0)
        {
            GroupItemsRemoved(group, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, e.OldItems, 0));
            return;
        }

        if (count > 0 && e.OldItems is not null && e.OldItems.Count > 0)
        {
            GroupItemsReplaced(group, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, group.Cast<object>().ToList(), e.OldItems, 0));
            return;
        }
    }
    #endregion
}