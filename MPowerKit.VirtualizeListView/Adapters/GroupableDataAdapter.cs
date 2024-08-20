using System.Collections;
using System.Collections.Specialized;

namespace MPowerKit.VirtualizeListView;

public class GroupableDataAdapter(VirtualizeListView listView) : DataAdapter(listView)
{
    public class GroupItem(IEnumerable group, object data) : AdapterItem(data)
    {
        public object Group { get; set; } = group;
    }
    public class GroupHeaderItem(IEnumerable group) : GroupItem(group, group) { }
    public class GroupFooterItem(IEnumerable group) : GroupItem(group, group) { }

    protected IEnumerable<IEnumerable> GroupedItems { get; set; } = [];

    public virtual bool HasGroupHeader => ListView.IsGrouped && ListView.GroupHeaderTemplate is not null;
    public virtual bool HasGroupFooter => ListView.IsGrouped && ListView.GroupFooterTemplate is not null;

    public virtual bool IsGroupHeader(DataTemplate template, int position)
    {
        return IsOneOf(ListView.GroupHeaderTemplate, template, position);
    }

    public virtual bool IsGroupFooter(DataTemplate template, int position)
    {
        return IsOneOf(ListView.GroupFooterTemplate, template, position);
    }

    public override bool IsSuplementary(int position)
    {
        return base.IsSuplementary(position)
            || InternalItems.ElementAtOrDefault(position) is GroupHeaderItem or GroupHeaderItem;
    }

    protected override DataTemplate GetItemTemplate(AdapterItem item)
    {
        if (item is GroupHeaderItem groupHeader)
        {
            return GetGroupHeaderTemplate(groupHeader.Group);
        }

        if (item is GroupFooterItem groupFooter)
        {
            return GetGroupFooterTemplate(groupFooter.Group);
        }

        return base.GetItemTemplate(item);
    }

    protected virtual DataTemplate GetGroupHeaderTemplate(object item)
    {
        if (ListView.GroupHeaderTemplate is DataTemplateSelector headerSelector)
        {
            return headerSelector.SelectTemplate(item, ListView);
        }

        return ListView.GroupHeaderTemplate;
    }

    protected virtual DataTemplate GetGroupFooterTemplate(object item)
    {
        if (ListView.GroupFooterTemplate is DataTemplateSelector footerSelector)
        {
            return footerSelector.SelectTemplate(item, ListView);
        }

        return ListView.GroupFooterTemplate;
    }

    public override List<(CellHolder cell, DataTemplate template)> CreateCellsPool(int poolSize)
    {
        var result = base.CreateCellsPool(poolSize);

        List<DataTemplate> templates = [];

        if (ListView.GroupHeaderTemplate is not null)
        {
            templates.AddRange(GetAllTemplatesByTemplate(ListView.GroupHeaderTemplate));
        }
        if (ListView.GroupFooterTemplate is not null)
        {
            templates.AddRange(GetAllTemplatesByTemplate(ListView.GroupFooterTemplate));
        }

        for (int i = 0; i < templates.Count; i++)
        {
            var template = templates[i];

            for (int j = 0; j < poolSize; j++)
            {
                result.Add((CreateEmptyCellForTemplate(template), template));
            }
        }

        return result;
    }

    public override CellHolder OnCreateCell(DataTemplate template, int position)
    {
        var holder = CreateEmptyCellForTemplate(template);
        var content = holder[0];

        if (HasHeader && position == 0 && content is VirtualizeListViewCell)
        {
            throw new ArgumentException("HeaderTemplate can't be typeof(VirtualizeListViewCell)");
        }

        if (HasFooter && position == ItemsCount - 1 && content is VirtualizeListViewCell)
        {
            throw new ArgumentException("FooterTemplate can't be typeof(VirtualizeListViewCell)");
        }

        var item = InternalItems[position];

        if (item is GroupHeaderItem && content is VirtualizeListViewCell)
        {
            throw new ArgumentException("GroupHeaderTemplate can't be typeof(VirtualizeListViewCell)");
        }

        if (item is GroupFooterItem && content is VirtualizeListViewCell)
        {
            throw new ArgumentException("GroupFooterTemplate can't be typeof(VirtualizeListViewCell)");
        }

        if (IsOneOf(ListView.ItemTemplate, template, position) && content is not VirtualizeListViewCell)
        {
            throw new ArgumentException("ItemTemplate has to be typeof(VirtualizeListViewCell)");
        }

        return holder;
    }

    public override (int RealPosition, int RealItemsCount) GetRealPositionAndCount(AdapterItem item, int position)
    {
        if (!ListView.IsGrouped)
        {
            return base.GetRealPositionAndCount(item, position);
        }

        var totalCount = InternalItems.Count;
        var realPosition = -1;
        var realItemsCount = 0;
        for (int i = 0; i < totalCount; i++)
        {
            if (InternalItems[i] is GroupHeaderItem or GroupFooterItem or HeaderItem or FooterItem) continue;

            realItemsCount++;

            if (i <= position) realPosition++;
        }

        return (realPosition, realItemsCount);
    }

    protected virtual int GetFlattenedGroupIndexOfGroup(IEnumerable group)
    {
        return InternalItems.FindIndex(i => i is GroupItem groupItem && groupItem.Group == group);
    }

    protected virtual int GetFlattenedGroupIndexForGroupIndex(int groupIndex)
    {
        var groups = GroupedItems!;

        var groupsToSkip = groups.Take(groupIndex);
        var skipCount = (HasGroupHeader.ToInt() + HasGroupFooter.ToInt()) * groupIndex;
        return skipCount + groupsToSkip.Sum(static g => g.Count()) + HasHeader.ToInt();
    }

    protected override void RemoveListenerCollection(IEnumerable? itemsSource)
    {
        if (!ListView.IsGrouped || itemsSource is null)
        {
            base.RemoveListenerCollection(itemsSource);
            return;
        }

        if (itemsSource is not IEnumerable<IEnumerable> groups)
        {
            throw new ArgumentException("The collection type for IsGrouped should be only typeof(IEnumerable<IEnumerable>) or derived interfaces or classes from it");
        }

        ResetGroups(groups);

        base.RemoveListenerCollection(itemsSource);
    }

    public override void InitCollection(IEnumerable? itemsSource)
    {
        if (!ListView.IsGrouped || itemsSource is null)
        {
            base.InitCollection(itemsSource);
            return;
        }

        if (itemsSource is not IEnumerable<IEnumerable>)
        {
            throw new ArgumentException("The collection type for IsGrouped should be only typeof(IEnumerable<IEnumerable>) or derived interfaces or classes from it");
        }

        base.InitCollection(itemsSource);
    }

    #region CollectionChanged events
    protected virtual List<AdapterItem> FlattenGroups(IEnumerable<IEnumerable> groups)
    {
        List<AdapterItem> flatItems = [];

        var hasGroupHeader = HasGroupHeader;
        var hasGroupFooter = HasGroupFooter;

        foreach (var group in groups)
        {
            if (group is INotifyCollectionChanged notifyGroup)
            {
                notifyGroup.CollectionChanged += GroupChanged;
            }

            if (hasGroupHeader) flatItems.Add(new GroupHeaderItem(group));
            foreach (var item in group) flatItems.Add(new GroupItem(group, item));
            if (hasGroupFooter) flatItems.Add(new GroupFooterItem(group));
        }

        return flatItems;
    }

    protected virtual int ResetGroups(IEnumerable<IEnumerable> groups)
    {
        var hasGroupHeader = HasGroupHeader;
        var hasGroupFooter = HasGroupFooter;

        var totalItemsCount = 0;
        foreach (IEnumerable group in groups)
        {
            if (group is INotifyCollectionChanged notifyGroup)
            {
                notifyGroup.CollectionChanged -= GroupChanged;
            }

            if (hasGroupHeader) totalItemsCount++;
            foreach (var item in group) totalItemsCount++;
            if (hasGroupFooter) totalItemsCount++;
        }

        return totalItemsCount;
    }

    protected override void OnCollectionChangedAdd(NotifyCollectionChangedEventArgs e)
    {
        if (!ListView.IsGrouped)
        {
            base.OnCollectionChangedAdd(e);
            return;
        }

        if (e.NewItems?.Count is null or 0) return;

        var realGroupIndex = GetFlattenedGroupIndexForGroupIndex(e.NewStartingIndex);

        var flattenedItems = FlattenGroups(e.NewItems.Cast<IEnumerable>());

        InternalItems.InsertRange(realGroupIndex, flattenedItems);
        NotifyItemRangeInserted(realGroupIndex, flattenedItems.Count);
    }

    protected override void OnCollectionChangedRemove(NotifyCollectionChangedEventArgs e)
    {
        if (!ListView.IsGrouped)
        {
            base.OnCollectionChangedRemove(e);
            return;
        }

        if (e.OldItems?.Count is null or 0) return;

        var realGroupIndex = GetFlattenedGroupIndexForGroupIndex(e.OldStartingIndex);

        var countToRemove = ResetGroups(e.OldItems.Cast<IEnumerable>());

        InternalItems.RemoveRange(realGroupIndex, countToRemove);
        NotifyItemRangeRemoved(realGroupIndex, countToRemove);
    }

    protected override void OnCollectionChangedReplace(NotifyCollectionChangedEventArgs e)
    {
        if (!ListView.IsGrouped)
        {
            base.OnCollectionChangedReplace(e);
            return;
        }

        if (e.NewItems?.Count is null or 0 || e.OldItems?.Count is null or 0) return;

        var realGroupIndex = GetFlattenedGroupIndexForGroupIndex(e.OldStartingIndex);

        var countToRemove = ResetGroups(e.OldItems.Cast<IEnumerable>());
        InternalItems.RemoveRange(realGroupIndex, countToRemove);

        var flattenedItems = FlattenGroups(e.OldItems.Cast<IEnumerable>());
        InternalItems.InsertRange(realGroupIndex, flattenedItems);

        NotifyItemRangeChanged(e.OldStartingIndex, countToRemove, flattenedItems.Count);
    }

    protected override void OnCollectionChangedMove(NotifyCollectionChangedEventArgs e)
    {
        if (!ListView.IsGrouped)
        {
            base.OnCollectionChangedMove(e);
            return;
        }

        if (e.NewItems?.Count is null or 0 or > 1 || e.OldItems?.Count is null or 0 or > 1
            || e.NewStartingIndex == e.OldStartingIndex) return;

        var oldIndex = e.OldStartingIndex;
        var newIndex = e.NewStartingIndex;
        var realOldIndex = GetFlattenedGroupIndexForGroupIndex(oldIndex);
        var realNewIndex = GetFlattenedGroupIndexForGroupIndex(newIndex);
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
        if (!ListView.IsGrouped || itemsSource is null)
        {
            base.OnCollectionChangedReset(itemsSource);
            return;
        }

        if (itemsSource is not IEnumerable<IEnumerable> groups)
        {
            throw new ArgumentException("The collection type for IsGrouped should be only typeof(IEnumerable<IEnumerable>) or derived interfaces or classes from it");
        }

        GroupedItems = groups;

        var flattenedItems = FlattenGroups(groups);

        if (HasHeader)
        {
            flattenedItems.Insert(0, new HeaderItem(ListView.Header));
        }

        if (HasFooter)
        {
            flattenedItems.Add(new FooterItem(ListView.Footer));
        }

        InternalItems = flattenedItems;

        NotifyDataSetChanged();
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

        var realGroupItemIndex = e.NewStartingIndex + GetFlattenedGroupIndexOfGroup(group) + HasGroupHeader.ToInt();

        InternalItems.InsertRange(realGroupItemIndex, e.NewItems.Cast<object>().Select(d => new GroupItem(group, d)));
        NotifyItemRangeInserted(realGroupItemIndex, e.NewItems.Count);
    }

    protected virtual void GroupItemsRemoved(IEnumerable group, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems?.Count is null or 0) return;

        var realGroupItemIndex = e.OldStartingIndex + GetFlattenedGroupIndexOfGroup(group) + HasGroupHeader.ToInt();

        var count = e.OldItems.Count;

        InternalItems.RemoveRange(realGroupItemIndex, count);
        NotifyItemRangeRemoved(realGroupItemIndex, count);
    }

    protected virtual void GroupItemsReplaced(IEnumerable group, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems?.Count is null or 0 || e.OldItems?.Count is null or 0) return;

        var realGroupItemIndex = e.NewStartingIndex + GetFlattenedGroupIndexOfGroup(group) + HasGroupHeader.ToInt();

        InternalItems.RemoveRange(realGroupItemIndex, e.OldItems.Count);
        InternalItems.InsertRange(realGroupItemIndex, e.NewItems.Cast<object>().Select(d => new GroupItem(group, d)));

        NotifyItemRangeChanged(realGroupItemIndex, e.OldItems.Count, e.NewItems.Count);
    }

    private void GroupItemsMoved(IEnumerable group, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems?.Count is null or 0 or > 1 || e.OldItems?.Count is null or 0 or > 1
            || e.NewStartingIndex == e.OldStartingIndex)
        {
            return;
        }

        var realGroupStartIndex = GetFlattenedGroupIndexOfGroup(group) + HasGroupHeader.ToInt();

        var newIndex = e.NewStartingIndex + realGroupStartIndex;
        var oldIndex = e.OldStartingIndex + realGroupStartIndex;

        InternalItems.Move(oldIndex, newIndex);
        NotifyItemMoved(oldIndex, newIndex);
    }

    private void GroupItemsReset(IEnumerable group, NotifyCollectionChangedEventArgs e)
    {
        var groupIndex = GetFlattenedGroupIndexOfGroup(group);
        if (groupIndex == -1) return;

        var totalCount = InternalItems.Count;

        var prevGroupItems = InternalItems.FindAll(i => i is GroupItem groupItem and not GroupHeaderItem and not GroupFooterItem
                                                        && groupItem.Group == group)
                                          .Select(i => i.Data)
                                          .ToList();

        var prevGroupCount = prevGroupItems.Count;

        var currentCount = group.Count();

        if (prevGroupCount == 0 && currentCount == 0) return;

        if (prevGroupCount == 0 && currentCount > 0)
        {
            GroupItemsAdd(group, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, group.Cast<object>().ToList(), 0));
            return;
        }

        if (prevGroupCount > 0 && currentCount == 0)
        {
            GroupItemsRemoved(group, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, prevGroupItems, 0));
            return;
        }

        if (prevGroupCount > 0 && currentCount > 0)
        {
            GroupItemsReplaced(group, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, group.Cast<object>().ToList(), prevGroupItems, 0));
            return;
        }
    }
    #endregion
}