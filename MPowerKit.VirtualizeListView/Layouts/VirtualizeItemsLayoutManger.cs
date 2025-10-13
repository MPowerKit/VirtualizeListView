using System.Runtime.InteropServices;

using Microsoft.Maui.Controls.Internals;
using Microsoft.Maui.Layouts;

using static MPowerKit.VirtualizeListView.DataAdapter;

namespace MPowerKit.VirtualizeListView;

public abstract class VirtualizeItemsLayoutManger : Layout, ILayoutManager, IDisposable
{
    protected const double AutoSize = -1d;
    protected const double CachedItemsCoords = -1000000d;
    protected static double EstimatedItemSize { get; set; } = 200d;
    protected const int VisibleItemsCapacity = 50;

    protected ScrollEventArgs PrevScroll { get; set; } = new(0d, 0d, 0d, 0d);
    protected Point PrevScrollBeforeSizeChange { get; set; }
    protected Size PrevContentSize { get; set; }
    protected Size PrevAvailableSpace { get; set; }

    public VirtualizeListView? ListView { get; protected set; }
    public DataAdapter? Adapter { get; protected set; }
    public int CachePoolSize { get; set; }
    public bool IsDisposed { get; protected set; }

    protected List<VirtualizeListViewItem> LaidOutItems { get; } = [];

    public IReadOnlyList<VirtualizeListViewItem> ReadOnlyLaidOutItems => LaidOutItems.AsReadOnly();

    protected List<(DataTemplate Template, CellHolder Cell)> CachedCells { get; } = [];

    public virtual HashSet<VirtualizeListViewItem> VisibleItems { get; protected set; } = new HashSet<VirtualizeListViewItem>(VisibleItemsCapacity);

    public virtual List<(AdapterItem Data, int Position)> VisibleDataItems => [..VisibleItems
        .Where(i => i.Cell?.Children[0] is VirtualizeListViewCell)
        .Select(i => (i.AdapterItem, i.Position))];

    public Size AvailableSpace { get; set; }

    protected virtual LayoutOptions ListViewHorizontalOptions => ListView?.HorizontalOptions ?? LayoutOptions.Fill;
    protected virtual LayoutOptions ListViewVerticalOptions => ListView?.VerticalOptions ?? LayoutOptions.Fill;

    protected override ILayoutManager CreateLayoutManager() => this;

    protected override void OnParentChanging(ParentChangingEventArgs args)
    {
        base.OnParentChanging(args);

        if (args.OldParent is null) return;

        SendListViewAdapterReset();

        ListView = null;
        BindingContext = null;
    }

    protected override void OnParentChanged()
    {
        base.OnParentChanged();

        if (this.Parent is null) return;

        if (this.Parent is not VirtualizeListView listView)
        {
            throw new InvalidOperationException("ItemsLayoutManager can be used only within VirtualizeListView");
        }

        ListView = listView;

        SendListViewAdapterSet();
    }

    protected virtual bool DoesListViewHaveSize()
    {
        var listView = ListView;
        return listView is not null
            && !double.IsNaN(listView.Width)
            && !double.IsNaN(listView.Height)
            && listView.Width >= 0d
            && listView.Height >= 0d
            && this.Handler is not null;
    }

    public virtual void SendListViewAdapterSet()
    {
        var adapter = ListView?.Adapter;

        if (adapter is null) return;

        Adapter = adapter;

        adapter.DataSetChanged += AdapterDataSetChanged;
        adapter.ItemMoved += AdapterItemMoved;
        adapter.ItemRangeChanged += AdapterItemRangeChanged;
        adapter.ItemRangeInserted += AdapterItemRangeInserted;
        adapter.ItemRangeRemoved += AdapterItemRangeRemoved;

        InvalidateLayout();

        AttachDecorators();
    }

    public virtual void SendListViewAdapterReset()
    {
        var adapter = ListView?.Adapter;

        if (adapter is null) return;

        DetachDecorators();

        ListView!.ItemDecorators.CollectionChanged -= ItemDecorators_CollectionChanged;

        adapter.DataSetChanged -= AdapterDataSetChanged;
        adapter.ItemMoved -= AdapterItemMoved;
        adapter.ItemRangeChanged -= AdapterItemRangeChanged;
        adapter.ItemRangeInserted -= AdapterItemRangeInserted;
        adapter.ItemRangeRemoved -= AdapterItemRangeRemoved;

        Adapter = null;
    }

    protected virtual void AttachDecorators()
    {
        foreach (var item in ListView!.ItemDecorators)
        {
            item.OnAttached(ListView!, this, Adapter!);
        }

        ListView!.ItemDecorators.CollectionChanged += ItemDecorators_CollectionChanged;
    }

    protected virtual void DetachDecorators()
    {
        ListView!.ItemDecorators.CollectionChanged -= ItemDecorators_CollectionChanged;

        foreach (var item in ListView!.ItemDecorators)
        {
            item.OnDetached(ListView!, this, Adapter!);
        }
    }

    private void ItemDecorators_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (ItemDecorator item in e.OldItems)
            {
                item.OnDetached(ListView!, this, Adapter!);
            }
        }

        if (e.NewItems is not null)
        {
            foreach (ItemDecorator item in e.NewItems)
            {
                item.OnAttached(ListView!, this, Adapter!);
            }
        }
    }

    protected virtual void OnDrawOver()
    {
        foreach (var item in ListView!.ItemDecorators)
        {
            item.OnDrawOver();
        }
    }

    public virtual void SendListViewScrolled(ScrolledEventArgs e)
    {
        if (ListView is null) return;

        var newScroll = e - PrevScroll;
        UpdateItemsLayout(0, true);
        PrevScroll = newScroll;

        OnDrawOver();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler is null) return;

        InvalidateLayout();
    }

    public virtual void SendListViewContentSizeChanged()
    {
        if (ListView is null) return;

        var newSpace = AvailableSpace;

        if (newSpace == PrevAvailableSpace) return;

        PrevAvailableSpace = newSpace;

        try
        {
            if (LaidOutItems.Count == 0)
            {
                InvalidateLayout();
                return;
            }
            else
            {
                RelayoutItems();
            }
        }
        finally
        {
            OnDrawOver();
        }
    }

    /// <summary>
    /// Use only if there was no any items before, or itemtemplate has changed.
    /// Clears all items and lays out new ones.
    /// </summary>
    public virtual void InvalidateLayout()
    {
        ClearAll();

        if (!DoesListViewHaveSize() || Adapter?.ItemsCount is null or 0) return;

        var count = Adapter.ItemsCount;

        var availableSpace = AvailableSpace;

        for (int i = 0; i < count; i++)
        {
            var item = CreateItemForPosition(i);

            LaidOutItems.Add(item);

            ShiftItemsChunk(LaidOutItems, i, LaidOutItems.Count);

            if (!item.IsOnScreen) continue;

            ReuseCell(item, true, availableSpace);
        }

        DrawAndTriggerResize();

        this.Dispatcher.Dispatch(() =>
        {
            CreateCachePool(CachePoolSize);
        });
    }

    protected virtual void ClearAll()
    {
        LaidOutItems.Clear();
        CachedCells.Clear();
        this.ClearItems();
        (this as IView).InvalidateMeasure();
    }

    protected virtual void CreateCachePool(int poolSize)
    {
        if (Adapter is null || poolSize <= 0) return;

        var pool = Adapter.CreateCellsPool(poolSize);

        for (int i = 0; i < pool.Count; i++)
        {
            var (cell, template) = pool[i];
            this.Add(cell);

            var item = CreateDummyItem(template, cell);

            CacheCell(item);
        }
    }

    protected virtual void RelayoutItems()
    {
        var count = LaidOutItems.Count;
        if (!DoesListViewHaveSize() || count == 0) return;

        var attachedItems = LaidOutItems.FindAll(i => i.IsAttached);

        if (attachedItems.Count == 0)
        {
            UpdateItemsLayout(0, false);
            return;
        }

        foreach (var item in attachedItems)
        {
            ArrangeItem(LaidOutItems, item, AvailableSpace);
        }

        UpdateItemsLayout(attachedItems[^1].Position + 1, false);
    }

    protected virtual void AdapterDataSetChanged(object? sender, EventArgs e)
    {
        var adapter = Adapter!;

        if (adapter.ItemsCount == 0 && LaidOutItems.Count == 0)
        {
            return;
        }
        else if (LaidOutItems.Count == 0 && CachedCells.Count == 0)
        {
            InvalidateLayout();
        }
        else if (adapter.ItemsCount == 0)
        {
            AdapterItemRangeRemoved(this, (0, LaidOutItems.Count));
        }
        else if (LaidOutItems.Count > 0)
        {
            AdapterItemRangeChanged(this, (0, LaidOutItems.Count, adapter.ItemsCount));
            if (ListView!.ScrollX == 0d && ListView.ScrollY == 0d) return;
            ListView.ScrollToAsync(0, 0, false);
        }
        else if (LaidOutItems.Count == 0)
        {
            AdapterItemRangeInserted(this, (0, adapter.ItemsCount));
        }
    }

    protected virtual void AdapterItemRangeInserted(object? sender, (int StartingIndex, int TotalCount) e)
    {
        if (!DoesListViewHaveSize()) return;

        var count = LaidOutItems.Count;
        var startingIndex = e.StartingIndex;

        if (Adapter!.ItemsCount == 0 || e.StartingIndex > count)
        {
            throw new ArgumentException("Invalid range");
        }

        if (count == 0 && CachedCells.Count == 0)
        {
            InvalidateLayout();
            return;
        }

        var finishIndex = startingIndex + e.TotalCount;

        var itemsToRearrange = LaidOutItems.Where(i => i.IsOnScreen && i.IsAttached);
        var firstVisibleItem = itemsToRearrange.FirstOrDefault();
        var prevVisibleCellBounds = firstVisibleItem?.Bounds ?? new();

        for (int index = startingIndex; index < finishIndex; index++)
        {
            var item = CreateItemForPosition(index);

            LaidOutItems.Insert(index, item);
        }

        RepositionItemsFromIndex(LaidOutItems, finishIndex);

        ShiftItemsConsecutively(LaidOutItems, startingIndex, finishIndex);

        if (startingIndex == 0)
        {
            ShiftItemsConsecutively(LaidOutItems, finishIndex, LaidOutItems.Count);
        }
        else ShiftItemsChunk(LaidOutItems, finishIndex, LaidOutItems.Count);

        UpdateItemsLayout(startingIndex, false);

        // if we inserted items defore the first visible item
        // then we need to adjust the scroll position
        if (firstVisibleItem is not null && firstVisibleItem.Position >= finishIndex)
        {
            AdjustScrollIfNeeded(LaidOutItems, firstVisibleItem, prevVisibleCellBounds);
        }
    }

    protected virtual void AdapterItemRangeRemoved(object? sender, (int StartingIndex, int TotalCount) e)
    {
        if (!DoesListViewHaveSize()) return;

        var count = LaidOutItems.Count;
        var startingIndex = e.StartingIndex;
        var finishIndex = startingIndex + e.TotalCount;

        if (count == 0 || finishIndex > count || startingIndex < 0)
        {
            throw new ArgumentException("Invalid range");
        }

        var itemsToRemove = LaidOutItems[startingIndex..finishIndex];
        LaidOutItems.RemoveRange(startingIndex, e.TotalCount);

        for (int i = 0; i < e.TotalCount; i++)
        {
            DetachCell(itemsToRemove[i]);
        }

        if (LaidOutItems.Count == 0)
        {
            (this as IView).InvalidateMeasure();
            return;
        }

        var itemsToRearrange = LaidOutItems.Where(i => i.IsOnScreen && i.IsAttached);
        var firstVisibleItem = itemsToRearrange.FirstOrDefault();
        var prevVisibleCellBounds = firstVisibleItem?.Bounds ?? new();

        RepositionItemsFromIndex(LaidOutItems, startingIndex);

        if (startingIndex == 0)
        {
            ShiftItemsConsecutively(LaidOutItems, startingIndex, LaidOutItems.Count);
        }
        else ShiftItemsChunk(LaidOutItems, startingIndex, LaidOutItems.Count);

        // if we removed items from the beginning
        // and if we are at the top we dont need to adjust the scroll position
        if (startingIndex == 0 && ListView!.ScrollX == 0d && ListView.ScrollY == 0d)
        {
            UpdateItemsLayout(startingIndex, false);
            return;
        }

        // if we removed all visible items
        // then we need to adjust the scroll position
        if (firstVisibleItem is null)
        {
            var prevItem = LaidOutItems[startingIndex - 1];

            UpdateItemsLayout(startingIndex, false);
            AdjustScrollIfNeeded(LaidOutItems, prevItem, new(ListView!.ScrollX - ListView.Padding.Left, ListView.ScrollY - ListView.Padding.Top, 0, 0));
            return;
        }

        // if we removed items after the first visible item
        // so we dont need to adjust the scroll position
        if (firstVisibleItem.Position < startingIndex)
        {
            UpdateItemsLayout(startingIndex, false);
            return;
        }

        // if we removed items before the first visible item
        // then we also need to adjust the scroll position
        if (firstVisibleItem.Position >= startingIndex)
        {
            AdjustScrollIfNeeded(LaidOutItems, firstVisibleItem, prevVisibleCellBounds);
        }
    }

    protected virtual void AdapterItemRangeChanged(object? sender, (int StartingIndex, int OldCount, int NewCount) e)
    {
        if (!DoesListViewHaveSize()) return;

        var count = LaidOutItems.Count;
        var startingIndex = e.StartingIndex;
        var oldCount = e.OldCount;
        var newCount = e.NewCount;
        var oldEnd = startingIndex + e.OldCount;
        var newEnd = startingIndex + e.NewCount;
        var adapterItemsCount = Adapter!.ItemsCount;

        if (count == 0 || adapterItemsCount == 0 || startingIndex < 0 || oldEnd > count || newEnd > adapterItemsCount)
        {
            throw new ArgumentException("Invalid range");
        }

        var itemsToRemove = CollectionsMarshal.AsSpan(LaidOutItems[startingIndex..oldEnd]);
        LaidOutItems.RemoveRange(startingIndex, oldCount);

        for (int i = 0; i < oldCount; i++)
        {
            //remove
            var itemToRemove = itemsToRemove[i];

            DetachCell(itemToRemove);
        }

        var itemsToRearrange = LaidOutItems.Where(i => i.IsOnScreen && i.IsAttached);
        var firstVisibleItem = itemsToRearrange.FirstOrDefault();
        var prevVisibleCellBounds = firstVisibleItem?.Bounds ?? new();

        for (int index = startingIndex; index < newEnd; index++)
        {
            var item = CreateItemForPosition(index);

            LaidOutItems.Insert(index, item);
        }

        RepositionItemsFromIndex(LaidOutItems, newEnd);

        ShiftItemsConsecutively(LaidOutItems, startingIndex, newEnd);
        ShiftItemsChunk(LaidOutItems, newEnd, LaidOutItems.Count);

        // if we replaced items from the beginning
        // and if we are at the top we dont need to adjust the scroll position
        if (startingIndex == 0 && ListView!.ScrollX == 0d && ListView.ScrollY == 0d)
        {
            UpdateItemsLayout(startingIndex, false);
            return;
        }

        // if there is no any visible item 
        // or we replaced items after the first visible item
        // so we dont need to adjust the scroll position
        if (firstVisibleItem is null || firstVisibleItem.Position < startingIndex)
        {
            UpdateItemsLayout(startingIndex, false);
            return;
        }

        // if we replaced items before the first visible item
        // then we also need to adjust the scroll position
        if (firstVisibleItem.Position >= startingIndex)
        {
            AdjustScrollIfNeeded(LaidOutItems, firstVisibleItem, prevVisibleCellBounds);
        }
    }

    protected virtual void AdapterItemMoved(object? sender, (int OldIndex, int NewIndex) e)
    {
        if (!DoesListViewHaveSize()) return;

        var count = LaidOutItems.Count;
        var newIndex = e.NewIndex;
        var oldIndex = e.OldIndex;

        if (Adapter?.ItemsCount is null or 0
            || count == 0 || newIndex < 0 || newIndex >= count || oldIndex < 0
            || oldIndex >= count || oldIndex == newIndex)
        {
            throw new ArgumentException("Invalid range");
        }

        var start = Math.Min(oldIndex, newIndex);
        var end = Math.Max(oldIndex, newIndex);

        var itemsToRearrange = LaidOutItems.FindAll(i => i.IsOnScreen && i.IsAttached);
        var firstVisibleItem = itemsToRearrange.First();
        var lastVisibleItem = itemsToRearrange.Last();
        var prevVisibleCellBounds = firstVisibleItem.Bounds;

        var itemToMove = LaidOutItems[oldIndex];

        LaidOutItems.RemoveAt(oldIndex);
        LaidOutItems.Insert(newIndex, itemToMove);

        RepositionItemsFromIndex(LaidOutItems, start);

        if ((start < firstVisibleItem.Position && end < firstVisibleItem.Position)
            || (start > lastVisibleItem.Position && end > lastVisibleItem.Position))
        {
            ShiftItemsConsecutively(LaidOutItems, start, end + 1);
            return;
        }

        if (ReferenceEquals(firstVisibleItem, itemToMove))
        {
            firstVisibleItem = itemsToRearrange.FirstOrDefault(i => !ReferenceEquals(i, itemToMove));
            prevVisibleCellBounds = firstVisibleItem?.Bounds ?? new();
            itemsToRearrange.Remove(itemToMove);
        }
        else if (ReferenceEquals(lastVisibleItem, itemToMove))
        {
            lastVisibleItem = itemsToRearrange.LastOrDefault(i => !ReferenceEquals(i, itemToMove));
            itemsToRearrange.Remove(itemToMove);
        }

        ShiftItemsChunk(LaidOutItems, start, itemToMove.Position);
        if (itemToMove.Cell is not null)
        {
            ArrangeItem(LaidOutItems, itemToMove, AvailableSpace);
        }
        ShiftItemsConsecutively(LaidOutItems, itemToMove.Position, end + 1);

        if (itemsToRearrange.Count != 0)
        {
            foreach (var item in itemsToRearrange)
            {
                ArrangeItem(LaidOutItems, item, AvailableSpace);
            }

            ShiftItemsChunk(LaidOutItems, itemsToRearrange[^1].Position + 1, count);
        }

        UpdateItemsLayout(start, false);
    }

    public virtual void OnItemSizeChanged(VirtualizeListViewItem item)
    {
        //ArrangeItem(LaidOutItems, item, AvailableSpace);
        //ShiftItemsChunk(LaidOutItems, item.Position + 1, LaidOutItems.Count);
        //UpdateItemsLayout(item.Position + 1, false);

#if MACIOS
        //(this.Parent as IView)?.InvalidateMeasure();
#elif WINDOWS
        (this.Parent?.Parent as IView)?.InvalidateMeasure();
#endif
    }

    protected virtual void UpdateItemsLayout(int fromPosition, bool shouldAdjustScroll)
    {
        var laidOutItmes = LaidOutItems;
        var count = laidOutItmes.Count;
        if (count == 0) return;

        var availableSpace = AvailableSpace;

        var spanList = CollectionsMarshal.AsSpan(laidOutItmes);

        //bool shouldInvalidate = false;

        for (int i = fromPosition; i < count; i++)
        {
            var item = spanList[i];

            if (!item.IsOnScreen && item.IsAttached)
            {
                (this as IView)!.InvalidateMeasure();
                return;
            }
        }

        //CacheItems(fromPosition);

        for (int i = fromPosition; i < count; i++)
        {
            var item = spanList[i];

            if (item.IsOnScreen && !item.IsAttached)
            {
                (this as IView)!.InvalidateMeasure();
                return;
            }

            //if (!item.IsOnScreen || item.IsAttached) continue;

            //shouldInvalidate = true;

            //break;

            //var prevBounds = item.Bounds;

            //ReuseCell(item, true, availableSpace);

            //reused = true;

            //if (item.Bounds == prevBounds) continue;

            //ShiftItemsChunk(laidOutItmes, item.Position + 1, count);

            //if (!shouldAdjustScroll) continue;

            //AdjustScrollForItemBoundsChange(laidOutItmes, item, prevBounds);
        }

        //if (shouldInvalidate) (this as IView)!.InvalidateMeasure();

        //var sizeChanged = DrawAndTriggerResize();

        //if (reused && !sizeChanged)
        //{
        //#if !MACIOS
        //(this as IView).InvalidateMeasure();
        //#endif
        //}
    }

    protected virtual void CacheItems(int fromPosition)
    {
        var items = LaidOutItems;
        var count = items.Count;

        for (int i = fromPosition; i < count; i++)
        {
            var item = items[i];

            if (!item.IsOnScreen) DetachCell(item);
        }
    }

    protected virtual void DetachCell(VirtualizeListViewItem item)
    {
        if (!item.IsAttached || item.Cell is null) return;

        VisibleItems.Remove(item);

        Adapter!.OnCellRecycled(item.Cell!, item.AdapterItem!, item.Position);

        CacheCell(item);
    }

    protected virtual void CacheCell(VirtualizeListViewItem item)
    {
        if (item.Cell is null) return;

        var cell = item.Cell;

        cell.TranslationX = CachedItemsCoords;
        cell.TranslationY = CachedItemsCoords;

        CachedCells.Add((item.Template!, cell));
        item.Cell = null;
    }

    protected virtual void ReuseCell(VirtualizeListViewItem item, bool createNewIfNoCached, Size availableSpace)
    {
        VisibleItems.Add(item);
        if (item.Cell is not null)
        {
            //ArrangeItem(LaidOutItems, item, availableSpace);
            return;
        }

        var freeCell = CachedCells.LastOrDefault(i => (i.Template as IDataTemplateController).Id == (item.Template as IDataTemplateController)!.Id);
        if (freeCell != default)
        {
            CachedCells.Remove(freeCell);

            var cell = freeCell.Cell;

            item.Cell = cell;
            //#if ANDROID
            cell.TranslationX = 0d;
            cell.TranslationY = 0d;
            //#endif
        }
        else
        {
            var freeItem = LaidOutItems.FirstOrDefault(i =>
                (i.Template as IDataTemplateController)!.Id == (item.Template as IDataTemplateController)!.Id
                && !i.IsAttached && !i.IsOnScreen && i.Cell is not null);
            if (freeItem is not null)
            {
                var cell = freeItem.Cell;
                freeItem.Cell = null;
                item.Cell = cell;
            }
            else if (createNewIfNoCached)
            {
                item.Cell = Adapter!.OnCreateCell(item.Template!, item.Position);
                //Probably its not needed
                //MeasureItem(LaidOutItems, item, availableSpace);
                this.Add(item.Cell);
            }
        }

        Adapter!.OnBindCell(item.Cell!, item.AdapterItem!, item.Position);

        //ArrangeItem(LaidOutItems, item, availableSpace);
    }

    protected virtual bool DrawAndTriggerResize()
    {
#if !ANDROID
        // on Android we must arrange item in real bounds, not using translation
        // issue #5
        //foreach (var item in LaidOutItems.Where(i => i.Cell is not null))
        //{
        //DrawItem(LaidOutItems, item);
        //}
#endif
        return TriggerResizeLayout();
    }

    protected virtual void DrawItem(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item)
    {
        if (items.Count == 0 || item.Position < 0 || item.Cell is null) return;

        var view = item.Cell;

#if !ANDROID
        // on Android we must arrange item in real bounds, not using translation
        // issue #5
        if (view.TranslationX != item.Bounds.X ||
            view.TranslationY != item.Bounds.Y)
        {
            view.TranslationX = item.Bounds.X;
            view.TranslationY = item.Bounds.Y;
        }
#endif
    }

    protected virtual bool TriggerResizeLayout()
    {
        if (IsOrientation(ScrollOrientation.Both)) return false;

        var availableSpace = AvailableSpace;

        var desiredSize = IsOrientation(ScrollOrientation.Vertical)
            ? GetDesiredLayoutSize(AvailableSpace.Width, double.PositiveInfinity, availableSpace)
            : GetDesiredLayoutSize(double.PositiveInfinity, AvailableSpace.Height, availableSpace);

        if (PrevContentSize == desiredSize) return false;

        PrevContentSize = desiredSize;

        View? view =
#if MACIOS
            ListView;
#else
            this;
#endif

        (view as IView)?.InvalidateMeasure();

        return true;
    }

    [Obsolete("Needs to be reconsidered")]
    public virtual Size MeasureItem(VirtualizeListViewItem item, Size availableSpace)
    {
        if (IsOrientation(ScrollOrientation.Both)) return new();

        var iview = (item.Cell as IView)!;

        Size measure;

        if (IsOrientation(ScrollOrientation.Vertical))
        {
            measure = iview.Measure(GetEstimatedItemSize(item, availableSpace).Width, double.PositiveInfinity);

            item.Size = new(availableSpace.Width, measure.Height);
        }
        else
        {
            measure = iview.Measure(double.PositiveInfinity, GetEstimatedItemSize(item, availableSpace).Height);

            item.Size = new(measure.Width, availableSpace.Height);
        }

        return measure;
    }

    protected virtual VirtualizeListViewItem CreateDummyItem(DataTemplate template, CellHolder cell)
    {
        var item = new VirtualizeListViewItem(this)
        {
            Template = template,
            Cell = cell,
            Position = 0
        };

        item.Size = GetEstimatedItemSize(item, AvailableSpace);

        return item;
    }

    protected virtual bool IsOrientation(ScrollOrientation orientation)
    {
        return ListView!.IsOrientation(orientation);
    }

    protected virtual Size GetDesiredLayoutSize(double widthConstraint, double heightConstraint, Size availableSpace)
    {
        if (IsOrientation(ScrollOrientation.Both) || LaidOutItems.Count == 0) return new();

        var visibleItems = VisibleItems;

        return IsOrientation(ScrollOrientation.Vertical)
            ? new(Math.Min(widthConstraint, ListViewHorizontalOptions == LayoutOptions.Fill
                    ? availableSpace.Width
                    : visibleItems.Count == 0 ? 0d : visibleItems.Max(i => i.MeasuredSize.Width)),
                LaidOutItems[^1].RightBottomWithMargin.Y)
            : new(LaidOutItems[^1].RightBottomWithMargin.X,
                Math.Min(heightConstraint, ListViewVerticalOptions == LayoutOptions.Fill
                    ? availableSpace.Height
                    : visibleItems.Count == 0 ? 0d : visibleItems.Max(i => i.MeasuredSize.Height)));
    }

    public virtual async Task ScrollToItem(object item, ScrollToPosition scrollToPosition, bool animated)
    {
        var listViewItem = LaidOutItems.FirstOrDefault(i => ReferenceEquals(i.AdapterItem?.Data, item));
        if (listViewItem is null) return;

        if (listViewItem.IsOnScreen)
        {
            await ListView!.ScrollToAsync(listViewItem.Cell, scrollToPosition, animated);
            return;
        }

        if (listViewItem.Position == 0)
        {
            await ListView!.ScrollToAsync(ListView.Padding.Left + listViewItem.LeftTop.X, ListView.Padding.Top + listViewItem.LeftTop.Y, animated);
            return;
        }

        var (desiredX, newScrollToPositionX) = GetDesiredX(scrollToPosition);
        var (desiredY, newScrollToPositionY) = GetDesiredY(scrollToPosition);

        bool shouldAnimate = true;
        while (ListView!.ScrollX != desiredX || ListView.ScrollY != desiredY)
        {
            await ListView.ScrollToAsync(desiredX, desiredY, animated & shouldAnimate);

            (desiredX, newScrollToPositionX) = GetDesiredX(newScrollToPositionX);
            (desiredY, newScrollToPositionY) = GetDesiredY(newScrollToPositionY);

            shouldAnimate = false;
        }

        bool WhetherContentSizeBiggerThanSize(bool vertical)
        {
            return vertical
                ? ListView!.ContentSize.Height > ListView.Height - ListView.Padding.VerticalThickness
                : ListView!.ContentSize.Width > ListView.Width - ListView.Padding.HorizontalThickness;
        }

        (double, ScrollToPosition) GetDesiredX(ScrollToPosition scrollToPosition)
        {
            ScrollToPosition newScrollToPosition = scrollToPosition;
            if (scrollToPosition is ScrollToPosition.MakeVisible)
            {
                if (ListView!.Padding.Left + listViewItem.LeftTop.X <= ListView.ScrollX)
                {
                    newScrollToPosition = ScrollToPosition.Start;
                }
                else if (ListView.Padding.Left + listViewItem.RightBottom.X >= ListView.ScrollX + ListView.Width)
                {
                    newScrollToPosition = ScrollToPosition.End;
                }
                else
                {
                    newScrollToPosition = ScrollToPosition.Center;
                }
            }

            return (newScrollToPosition switch
            {
                ScrollToPosition.Start => WhetherContentSizeBiggerThanSize(false) ? ListView!.Padding.Left + listViewItem.LeftTop.X : 0d,
                ScrollToPosition.Center => WhetherContentSizeBiggerThanSize(false) ? ListView!.Padding.Left + listViewItem.LeftTop.X + (listViewItem.RightBottom.X - listViewItem.LeftTop.X) / 2d - ListView.Width / 2d : 0d,
                ScrollToPosition.End => WhetherContentSizeBiggerThanSize(false) ? ListView!.Padding.Left + listViewItem.RightBottom.X - ListView.Width : 0d,
                _ => 0d,
            }, newScrollToPosition);
        }

        (double, ScrollToPosition) GetDesiredY(ScrollToPosition scrollToPosition)
        {
            ScrollToPosition newScrollToPosition = scrollToPosition;
            if (scrollToPosition is ScrollToPosition.MakeVisible)
            {
                if (ListView!.Padding.Top + listViewItem.LeftTop.Y <= ListView.ScrollY)
                {
                    newScrollToPosition = ScrollToPosition.Start;
                }
                else if (ListView.Padding.Top + listViewItem.RightBottom.Y >= ListView.ScrollY + ListView.Height)
                {
                    newScrollToPosition = ScrollToPosition.End;
                }
                else
                {
                    newScrollToPosition = ScrollToPosition.Center;
                }
            }

            return (newScrollToPosition switch
            {
                ScrollToPosition.Start => WhetherContentSizeBiggerThanSize(true) ? ListView!.Padding.Top + listViewItem.LeftTop.Y : 0d,
                ScrollToPosition.Center => WhetherContentSizeBiggerThanSize(true) ? ListView!.Padding.Top + listViewItem.LeftTop.Y + (listViewItem.RightBottom.Y - listViewItem.LeftTop.Y) / 2d - ListView.Height / 2d : 0d,
                ScrollToPosition.End => WhetherContentSizeBiggerThanSize(true) ? ListView!.Padding.Top + listViewItem.RightBottom.Y - ListView.Height : 0d,
                _ => 0d,
            }, newScrollToPosition);
        }
    }

    protected abstract void RepositionItemsFromIndex(IReadOnlyList<VirtualizeListViewItem> items, int index);
    public abstract VirtualizeListViewItem CreateItemForPosition(int position);
    protected abstract Thickness GetItemMargin(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item);
    protected abstract Size GetEstimatedItemSize(VirtualizeListViewItem item, Size availableSize);
    protected abstract Size MeasureItem(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item, Size availableSpace);
    protected abstract void ArrangeItem(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item, Size availableSpace);
    protected abstract void ShiftItemsChunk(IReadOnlyList<VirtualizeListViewItem> items, int start, int exclusiveEnd);
    protected abstract void ShiftItemsConsecutively(IReadOnlyList<VirtualizeListViewItem> items, int start, int exclusiveEnd);
    protected abstract void AdjustScrollForItemBoundsChange(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item, Rect prevBoundsOfItem);
    protected abstract bool AdjustScrollIfNeeded(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item, Rect prevBoundsOfItem);

    #region ILayoutManager
    Size ILayoutManager.Measure(double widthConstraint, double heightConstraint)
    {
        return LayoutManagerMeasure(widthConstraint, heightConstraint);
    }

    public virtual Size LayoutManagerMeasure(double widthConstraint, double heightConstraint)
    {
        var items = LaidOutItems;
        var length = this.LaidOutItems.Count;
        var availableSpace = AvailableSpace;

        var width = availableSpace.Width;

        var maxWidth = 0d;

        for (int i = 0; i < length; i++)
        {
            var item = items[i];
            if (!item.IsOnScreen)
            {
                DetachCell(item);
                continue;
            }

            var prevBounds = item.Bounds;

            if (item.Cell is null)
                ReuseCell(item, true, availableSpace);

            var iview = item.Cell as IView;
            var measure = iview!.Measure(GetEstimatedItemSize(item, availableSpace).Width, double.PositiveInfinity);

            item.MeasuredSize = measure;
            item.Size = new(width, measure.Height);
            maxWidth = Math.Max(maxWidth, measure.Width);

            if (item.Bounds == prevBounds) continue;

            ShiftItemsChunk(items, item.Position + 1, length);

            AdjustScrollForItemBoundsChange(items, item, prevBounds);
        }

        Size desiredSize = new(Math.Min(widthConstraint, ListViewHorizontalOptions == LayoutOptions.Fill
                    ? availableSpace.Width
                    : maxWidth),
                length == 0 ? 0 : items[^1].RightBottomWithMargin.Y);

        //        var items = CollectionsMarshal.AsSpan((this as IBindableLayout).Children as List<IView>);
        //        var length = items.Length;

        //        var availableSpace = AvailableSpace;

        //        for (int n = 0; n < length; n++)
        //        {
        //            var child = items[n];
        //            if (child is not CellHolder view) continue;

        //            if ((view.IsCached || !view.Item!.IsAttached)
        //#if MACIOS
        //                // on Mac and iOS we must to do initial measure of not measured items
        //                && view.WasMeasured
        //#endif
        //                ) continue;

        //#if MACIOS
        //            if (!view.WasMeasured && view.Item is null)
        //            {
        //                child.Measure(double.PositiveInfinity, double.PositiveInfinity);
        //            }
        //            else
        //#endif
        //            // this triggers item size change when needed
        //            MeasureItem(LaidOutItems, view.Item!, availableSpace);
        //        }

        //var desiredSize = GetDesiredLayoutSize(widthConstraint, heightConstraint, availableSpace);
        return desiredSize;
    }

    public virtual Size ArrangeChildren(Rect bounds)
    {
        var items = VisibleItems.OrderBy(i => i.Position).ToList();
        var length = items.Count;

        var maxWidth = items.Max(i => i.MeasuredSize.Width);

        foreach (var item in items)
        {
            var iview = item.Cell as IView;

            Rect newBounds = new(item.LeftTop, new(maxWidth, item.MeasuredSize.Height));

            //if (newBounds == item.Cell!.Bounds) continue;

            iview!.Arrange(new(item.LeftTop, new(maxWidth, item.MeasuredSize.Height)));
        }

        return new(bounds.Width, bounds.Height);
    }

    //    public virtual Size ArrangeChildren(Rect bounds)
    //    {
    //        var items = CollectionsMarshal.AsSpan((this as IBindableLayout).Children as List<IView>);
    //        var length = items.Length;

    //        for (int n = 0; n < length; n++)
    //        {
    //            var child = items[n];

    //            if (child is not CellHolder view) continue;

    //            if ((view.IsCached || !view.Item!.IsAttached)
    //#if MACIOS
    //                        // on Mac and iOS we must to do initial arrange of not arranged items
    //                        && view.WasArranged
    //#endif
    //                ) continue;

    //            Point loc =
    //#if ANDROID
    //                        // on Android we must arrange item in real bounds, not using translation
    //                        // issue #5
    //                        view.Item.LeftTop;
    //#else
    //                new(0d, 0d);
    //#endif

    //            Size size = view.Item?.Size ?? new(view.DesiredSize.Width, view.DesiredSize.Height);

    //            Rect newBounds = new(loc, size);

    //#if MACIOS
    //                    // on other platforms we need to arrange items anyway
    //                    if (view.Bounds == newBounds) continue;
    //#endif
    //            child.Arrange(newBounds);
    //        }

    //        return new(bounds.Width, bounds.Height);
    //    }

    protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
    {
        var layout = this as Microsoft.Maui.ILayout;

        var margin = this.Margin;
        var marginHorizontal = margin.HorizontalThickness;
        var marginVertical = margin.VerticalThickness;

        widthConstraint -= marginHorizontal;
        heightConstraint -= marginVertical;

        var desiredSize = this.Handler?.GetDesiredSize(widthConstraint, heightConstraint) ?? Size.Zero;

        return new(desiredSize.Width + marginHorizontal, desiredSize.Height + marginVertical);
    }

#if !MACIOS
    protected override Size ArrangeOverride(Rect bounds)
    {
        var newBounds = new Rect(bounds.X, bounds.Y, this.DesiredSize.Width, this.DesiredSize.Height);

        return base.ArrangeOverride(newBounds);
    }
#endif

    #endregion

    #region IDisposable
    protected virtual void Dispose(bool disposing)
    {
        if (this.IsDisposed) return;

        SendListViewAdapterReset();

        IsDisposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~VirtualizeItemsLayoutManger()
    {
        Dispose(false);
    }
    #endregion
}