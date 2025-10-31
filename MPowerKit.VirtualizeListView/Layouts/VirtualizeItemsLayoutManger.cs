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

    protected Dictionary<int, Queue<CellHolder>> CachedCells { get; } = [];

    public virtual HashSet<VirtualizeListViewItem> VisibleItems { get; protected set; } = new HashSet<VirtualizeListViewItem>(VisibleItemsCapacity);

    public virtual List<(AdapterItem Data, int Position)> VisibleDataItems => [..VisibleItems
        .Where(i => i.Cell?.Children[0] is VirtualizeListViewCell)
        .Select(i => (i.AdapterItem, i.Position))];

    public Size AvailableSpace { get; set; }

    protected virtual LayoutOptions ListViewHorizontalOptions => ListView?.HorizontalOptions ?? LayoutOptions.Fill;
    protected virtual LayoutOptions ListViewVerticalOptions => ListView?.VerticalOptions ?? LayoutOptions.Fill;

    public Rect Viewport { get; protected set; }
    protected bool NeedsAdjustScroll { get; set; }

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
    }

    public virtual void SendListViewAdapterReset()
    {
        var adapter = ListView?.Adapter;

        if (adapter is null) return;

        adapter.DataSetChanged -= AdapterDataSetChanged;
        adapter.ItemMoved -= AdapterItemMoved;
        adapter.ItemRangeChanged -= AdapterItemRangeChanged;
        adapter.ItemRangeInserted -= AdapterItemRangeInserted;
        adapter.ItemRangeRemoved -= AdapterItemRangeRemoved;

        Adapter = null;
    }

    public virtual void SendListViewScrolled(ScrolledEventArgs e)
    {
        if (ListView is null) return;

        var listView = ListView;
        var padding = listView.Padding;
        var availableSpace = listView.Bounds.Size;

        Viewport = new(e.ScrollX - padding.Left, e.ScrollY - padding.Top, availableSpace.Width, availableSpace.Height);

        var newScroll = e - PrevScroll;
        UpdateItemsLayout(0, true);
        PrevScroll = newScroll;
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

    /// <summary>
    /// Use only if there was no any items before, or itemtemplate has changed.
    /// Clears all items and lays out new ones.
    /// </summary>
    public virtual void InvalidateLayout()
    {
        ClearAll();

        if (!DoesListViewHaveSize() || Adapter?.ItemsCount is null or 0) return;

        var count = Adapter.ItemsCount;

        for (int i = 0; i < count; i++)
        {
            var item = CreateItemForPosition(i);

            LaidOutItems.Add(item);

            ShiftItemsChunk(LaidOutItems, i, LaidOutItems.Count);

            if (!item.IsOnScreen) continue;

            ReuseCell(item);
        }

        (this as IView)!.InvalidateMeasure();

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

        UpdateItemsLayout(attachedItems.Count == 0 ? 0 : attachedItems[0].Position);
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
            var listView = ListView!;

            AdapterItemRangeChanged(this, (0, LaidOutItems.Count, adapter.ItemsCount));
            if (listView.ScrollX == 0d && listView.ScrollY == 0d) return;
            listView.ScrollToAsync(0, 0, false);
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

        UpdateItemsLayout(startingIndex);

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

        var listView = ListView!;
        var scrollX = listView.ScrollX;
        var scrollY = listView.ScrollY;

        // if we removed items from the beginning
        // and if we are at the top we dont need to adjust the scroll position
        if (startingIndex == 0 && scrollY == 0d && scrollY == 0d)
        {
            UpdateItemsLayout(startingIndex);
            return;
        }

        // if we removed all visible items
        // then we need to adjust the scroll position
        if (firstVisibleItem is null)
        {
            var prevItem = LaidOutItems[startingIndex - 1];

            UpdateItemsLayout(startingIndex);
            var padding = listView.Padding;
            AdjustScrollIfNeeded(LaidOutItems, prevItem, new(scrollX - padding.Left, scrollY - padding.Top, 0, 0));
            return;
        }

        // if we removed items after the first visible item
        // so we dont need to adjust the scroll position
        if (firstVisibleItem.Position < startingIndex)
        {
            UpdateItemsLayout(startingIndex);
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

        var listView = ListView!;

        // if we replaced items from the beginning
        // and if we are at the top we dont need to adjust the scroll position
        if (startingIndex == 0 && listView.ScrollX == 0d && listView.ScrollY == 0d)
        {
            UpdateItemsLayout(startingIndex);
            return;
        }

        // if there is no any visible item 
        // or we replaced items after the first visible item
        // so we dont need to adjust the scroll position
        if (firstVisibleItem is null || firstVisibleItem.Position < startingIndex)
        {
            UpdateItemsLayout(startingIndex);
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

        //if (ReferenceEquals(firstVisibleItem, itemToMove))
        //{
        //    firstVisibleItem = itemsToRearrange.FirstOrDefault(i => !ReferenceEquals(i, itemToMove));
        //    prevVisibleCellBounds = firstVisibleItem?.Bounds ?? new();
        //    itemsToRearrange.Remove(itemToMove);
        //}
        //else if (ReferenceEquals(lastVisibleItem, itemToMove))
        //{
        //    lastVisibleItem = itemsToRearrange.LastOrDefault(i => !ReferenceEquals(i, itemToMove));
        //    itemsToRearrange.Remove(itemToMove);
        //}

        ShiftItemsConsecutively(LaidOutItems, start, end + 1);

        UpdateItemsLayout(start);
    }

    public virtual void OnItemSizeChanged(VirtualizeListViewItem item)
    {
        //ShiftItemsChunk(LaidOutItems, item.Position + 1, LaidOutItems.Count);
        //UpdateItemsLayout(item.Position + 1, false);

#if MACIOS
        //(this.Parent as IView)?.InvalidateMeasure();
#elif WINDOWS
        (this.Parent?.Parent as IView)?.InvalidateMeasure();
#endif
    }

    protected virtual void UpdateItemsLayout(int fromPosition, bool shouldAdjustScroll = false)
    {
        var laidOutItmes = LaidOutItems;
        var count = laidOutItmes.Count;
        if (count == 0) return;

        NeedsAdjustScroll = shouldAdjustScroll;

        for (int i = fromPosition; i < count; i++)
        {
            var item = laidOutItmes[i];

            if (!item.IsOnScreen && item.IsAttached)
            {
                (this as IView)!.InvalidateMeasure();
                return;
            }
        }

        for (int i = fromPosition; i < count; i++)
        {
            var item = laidOutItmes[i];

            if (item.IsOnScreen && !item.IsAttached)
            {
                (this as IView)!.InvalidateMeasure();
                return;
            }
        }

        NeedsAdjustScroll = false;
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

        var templateId = (item.Template as IDataTemplateController)!.Id;
        if (!CachedCells.TryGetValue(templateId, out var queue))
        {
            queue = new Queue<CellHolder>();
            CachedCells[templateId] = queue;
        }

        queue.Enqueue(cell);
        item.Cell = null;
    }

    protected virtual void ReuseCell(VirtualizeListViewItem item)
    {
        VisibleItems.Add(item);
        if (item.Cell is not null) return;

        var templateId = (item.Template as IDataTemplateController)!.Id;
        if (CachedCells.TryGetValue(templateId, out var queue) && queue.Count > 0)
        {
            var cell = queue.Dequeue();

            item.Cell = cell;

            cell.TranslationX = 0d;
            cell.TranslationY = 0d;
        }
        else
        {
            var freeItem = LaidOutItems.FirstOrDefault(i => (i.Template as IDataTemplateController)!.Id == templateId
                                                        && !i.IsAttached && !i.IsOnScreen && i.Cell is not null);
            if (freeItem is not null)
            {
                var cell = freeItem.Cell;
                freeItem.Cell = null;
                item.Cell = cell;
            }
            else
            {
                item.Cell = Adapter!.OnCreateCell(item.Template!, item.Position);

                this.Add(item.Cell);
            }
        }

        Adapter!.OnBindCell(item.Cell!, item.AdapterItem!, item.Position);
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

    protected virtual ScrollOrientation GetOrientation()
    {
        return ListView!.GetOrientation();
    }

    public virtual async Task ScrollToItem(object item, ScrollToPosition scrollToPosition, bool animated)
    {
        var listViewItem = LaidOutItems.FirstOrDefault(i => ReferenceEquals(i.AdapterItem?.Data, item));
        if (listViewItem is null) return;

        var listView = ListView!;
        var width = listView.Width;
        var height = listView.Height;
        var scrollX = listView.ScrollX;
        var scrollY = listView.ScrollY;
        var padding = listView.Padding;
        var paddingLeft = padding.Left;
        var leftTopX = listViewItem.LeftTop.X;
        var paddingLeftTopX = paddingLeft + leftTopX;
        var paddingTop = padding.Top;
        var leftTopY = listViewItem.LeftTop.Y;
        var paddingLeftTopY = paddingTop + leftTopY;
        var rightBottomX = listViewItem.RightBottom.X;
        var rightBottomY = listViewItem.RightBottom.Y;

        if (listViewItem.IsOnScreen)
        {
            await listView.ScrollToAsync(listViewItem.Cell, scrollToPosition, animated);
            return;
        }

        if (listViewItem.Position == 0)
        {
            await listView.ScrollToAsync(paddingLeftTopX, paddingLeftTopY, animated);
            return;
        }

        var (desiredX, newScrollToPositionX) = GetDesiredX(scrollToPosition);
        var (desiredY, newScrollToPositionY) = GetDesiredY(scrollToPosition);

        bool shouldAnimate = true;
        while (scrollX != desiredX || scrollY != desiredY)
        {
            await listView.ScrollToAsync(desiredX, desiredY, animated & shouldAnimate);

            (desiredX, newScrollToPositionX) = GetDesiredX(newScrollToPositionX);
            (desiredY, newScrollToPositionY) = GetDesiredY(newScrollToPositionY);

            shouldAnimate = false;
        }

        bool WhetherContentSizeBiggerThanSize(bool vertical)
        {
            return vertical
                ? listView.ContentSize.Height > height - padding.VerticalThickness
                : listView.ContentSize.Width > width - padding.HorizontalThickness;
        }

        (double, ScrollToPosition) GetDesiredX(ScrollToPosition scrollToPosition)
        {
            ScrollToPosition newScrollToPosition = scrollToPosition;
            if (scrollToPosition is ScrollToPosition.MakeVisible)
            {
                if (paddingLeftTopX <= scrollX)
                {
                    newScrollToPosition = ScrollToPosition.Start;
                }
                else if (paddingLeft + rightBottomX >= scrollX + width)
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
                ScrollToPosition.Start => WhetherContentSizeBiggerThanSize(false) ? paddingLeftTopX : 0d,
                ScrollToPosition.Center => WhetherContentSizeBiggerThanSize(false) ? paddingLeftTopX + (rightBottomX - leftTopX) / 2d - width / 2d : 0d,
                ScrollToPosition.End => WhetherContentSizeBiggerThanSize(false) ? paddingLeft + rightBottomX - width : 0d,
                _ => 0d,
            }, newScrollToPosition);
        }

        (double, ScrollToPosition) GetDesiredY(ScrollToPosition scrollToPosition)
        {
            ScrollToPosition newScrollToPosition = scrollToPosition;
            if (scrollToPosition is ScrollToPosition.MakeVisible)
            {
                if (paddingLeftTopY <= scrollY)
                {
                    newScrollToPosition = ScrollToPosition.Start;
                }
                else if (paddingTop + rightBottomY >= scrollY + height)
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
                ScrollToPosition.Start => WhetherContentSizeBiggerThanSize(true) ? paddingLeftTopY : 0d,
                ScrollToPosition.Center => WhetherContentSizeBiggerThanSize(true) ? paddingLeftTopY + (rightBottomY - leftTopY) / 2d - height / 2d : 0d,
                ScrollToPosition.End => WhetherContentSizeBiggerThanSize(true) ? paddingTop + rightBottomY - height : 0d,
                _ => 0d,
            }, newScrollToPosition);
        }
    }

    protected abstract void RepositionItemsFromIndex(IReadOnlyList<VirtualizeListViewItem> items, int index);
    public abstract VirtualizeListViewItem CreateItemForPosition(int position);
    protected abstract Thickness GetItemMargin(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item);
    protected abstract Size GetEstimatedItemSize(VirtualizeListViewItem item, Size availableSize);
    protected abstract void ShiftItemsChunk(IReadOnlyList<VirtualizeListViewItem> items, int start, int exclusiveEnd);
    protected abstract void ShiftItemsConsecutively(IReadOnlyList<VirtualizeListViewItem> items, int start, int exclusiveEnd);
    protected abstract Point GetOffsetToAdjustScroll(VirtualizeListViewItem item, Rect prevBoundsOfItem);
    protected abstract bool AdjustScrollIfNeeded(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item, Rect prevBoundsOfItem);

    #region ILayoutManager
    Size ILayoutManager.Measure(double widthConstraint, double heightConstraint)
    {
        return LayoutManagerMeasure(widthConstraint, heightConstraint);
    }

    protected abstract Size LayoutManagerMeasure(double widthConstraint, double heightConstraint);
    public abstract Size ArrangeChildren(Rect bounds);

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