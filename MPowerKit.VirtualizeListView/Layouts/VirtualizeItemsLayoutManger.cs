using System.Runtime.InteropServices;

using Microsoft.Maui.Controls.Internals;
using Microsoft.Maui.Layouts;

using static MPowerKit.VirtualizeListView.DataAdapter;

namespace MPowerKit.VirtualizeListView;

public abstract class VirtualizeItemsLayoutManager : Layout, ILayoutManager, IDisposable
{
    protected const double AutoSize = -1d;
    protected static double EstimatedItemSize { get; set; } = 200d;
    protected const int VisibleItemsCapacity = 50;

    public TimeSpan OpacityAnimationDuration { get; set; } = TimeSpan.FromMilliseconds(1500);
    public TimeSpan ShiftAnimationDuration { get; set; } = TimeSpan.FromMilliseconds(1500);
    public TimeSpan ZeroAnimationDelay { get; set; } = TimeSpan.FromMilliseconds(0);
    public TimeSpan DefaultAnimationDelay { get; set; } = TimeSpan.FromMilliseconds(750);

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

    protected Dictionary<int, Stack<CellHolder>> CachedCells { get; } = [];

    public virtual HashSet<VirtualizeListViewItem> VisibleItems { get; protected set; } = new HashSet<VirtualizeListViewItem>(VisibleItemsCapacity);

    public virtual List<(AdapterItem Data, int Position)> VisibleDataItems => [..VisibleItems
        .Where(i => i.Cell?.Children[0] is VirtualizeListViewCell)
        .Select(i => (i.AdapterItem, i.Position))];

    public Size AvailableSpace { get; set; }

    protected virtual LayoutOptions ListViewHorizontalOptions => ListView?.HorizontalOptions ?? LayoutOptions.Fill;
    protected virtual LayoutOptions ListViewVerticalOptions => ListView?.VerticalOptions ?? LayoutOptions.Fill;

    public virtual Rect Viewport
    {
        get
        {
            if (ListView is not { } listView) return new();

            var padding = listView.Padding;
            var availableSpace = AvailableSpace;
            Size listViewBounds = new(availableSpace.Width + padding.HorizontalThickness, availableSpace.Height + padding.VerticalThickness);
            return new(listView.ScrollX - padding.Left, listView.ScrollY - padding.Top, listViewBounds.Width, listViewBounds.Height);
        }
    }//{ get; protected set; }

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
    /// Use only if there was no any items before, or item template has changed.
    /// Clears all items and lays out new ones.
    /// </summary>
    public virtual void InvalidateLayout()
    {
        ClearAll();

        if (!DoesListViewHaveSize() || Adapter?.ItemsCount is null or 0) return;

        var count = Adapter.ItemsCount;
        var viewport = Viewport;

        for (int i = 0; i < count; i++)
        {
            var item = CreateItemForPosition(i);

            LaidOutItems.Add(item);

            ShiftItemsChunk(LaidOutItems, i, LaidOutItems.Count);

            if (!IsOnScreen(item, viewport)) continue;

            AttachCell(item, viewport);
        }

        (this.Parent as IView)!.InvalidateMeasure();

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

        var itemsToRearrange = VisibleItems.OrderBy(i => i.Position).ToList();
        var firstVisibleItem = itemsToRearrange.FirstOrDefault();
        var lastVisibleItem = itemsToRearrange.LastOrDefault();
        var prevVisibleCellBounds = firstVisibleItem?.Bounds ?? new();

        List<VirtualizeListViewItem> newItems = [];
        for (int index = startingIndex; index < finishIndex; index++)
        {
            var item = CreateItemForPosition(index);

            LaidOutItems.Insert(index, item);
            newItems.Add(item);
        }

        var laidOutItems = LaidOutItems;
        count = laidOutItems.Count;

        // we inserted items after the visible items
        // so just reposition and return
        if (startingIndex > lastVisibleItem?.Position)
        {
            RepositionItemsFromIndex(laidOutItems, startingIndex);
            ShiftItemsConsecutively(laidOutItems, startingIndex, count);
            return;
        }

        // if we inserted items before the first visible item
        // then we need to adjust the scroll position
        if (firstVisibleItem?.Position >= startingIndex)
        {
            RepositionItemsFromIndex(laidOutItems, finishIndex);
            ShiftItemsConsecutively(laidOutItems, startingIndex, count);

            AdjustScrollIfNeeded(laidOutItems, firstVisibleItem, prevVisibleCellBounds);

            (this as IView)!.InvalidateMeasure();
            return;
        }

        //fade in new items if they are visible
        if (startingIndex >= (firstVisibleItem?.Position ?? 0)
            && startingIndex <= (lastVisibleItem?.Position ?? 0))
        {
            foreach (var item in newItems)
            {
                item.AddFadeInAnimation(OpacityAnimationDuration, DefaultAnimationDelay);
                item.AnimateAll();
            }
        }

        var itemsBeforeInsertedRange = itemsToRearrange.Where(i => i.Position < startingIndex)
            .Select(i => (i, i.Bounds)).ToList();
        var itemsAfterInsertedRange = itemsToRearrange.Where(i => i.Position >= startingIndex)
            .Select(i => (i, i.Bounds)).ToList();

        RepositionItemsFromIndex(laidOutItems, finishIndex);
        ShiftItemsConsecutively(laidOutItems, startingIndex, count);

        OnInsertedIntoVisibleRect(itemsAfterInsertedRange, newItems);
    }

    protected virtual void AdapterItemRangeRemoved(object? sender, (int StartingIndex, int TotalCount) e)
    {
        if (!DoesListViewHaveSize()) return;

        var laidOutItems = LaidOutItems;
        var count = laidOutItems.Count;
        var startingIndex = e.StartingIndex;
        var finishIndex = startingIndex + e.TotalCount;
        var prevViewport = Viewport;

        if (count == 0 || finishIndex > count || startingIndex < 0)
        {
            throw new ArgumentException("Invalid range");
        }

        var itemsToRemove = LaidOutItems[startingIndex..finishIndex];
        LaidOutItems.RemoveRange(startingIndex, e.TotalCount);
        laidOutItems = [.. LaidOutItems];

        var visibleItems = VisibleItems.Except(itemsToRemove).OrderBy(i => i.Position).ToList();
        var firstVisibleItem = visibleItems.FirstOrDefault();
        var firstItemPrevVisibleCellBounds = firstVisibleItem?.Bounds ?? new();

        // we removed items after the visible items
        // so just reposition and return
        if (laidOutItems.Count > 0
            && !IsOnScreen(itemsToRemove[0], prevViewport) && startingIndex > firstVisibleItem?.Position)
        {
            RepositionItemsFromIndex(laidOutItems, startingIndex);
            ShiftItemsConsecutively(laidOutItems, startingIndex, laidOutItems.Count);
            return;
        }

        for (int i = 0; i < e.TotalCount; i++)
        {
            var item = itemsToRemove[i];
            if (!IsOnScreen(item, prevViewport)) continue;

            VisibleItems.Remove(item);
            item.AddFadeOutAnimation(OpacityAnimationDuration, ZeroAnimationDelay);
            item.AnimateAll(() => DetachCell(item));
        }

        // no items left, nothing to animate
        if (LaidOutItems.Count == 0)
        {
            (this as IView)!.InvalidateMeasure();
            return;
        }

        var itemsToAnimateBeforeRemovedRange =
            laidOutItems.Take(startingIndex).Select(i => (i, i.Bounds)).ToList();
        var itemsToAnimateAfterRemovedRange =
            laidOutItems.Skip(startingIndex).Select(i => (i, i.Bounds)).ToList();

        RepositionItemsFromIndex(laidOutItems, startingIndex);
        ShiftItemsConsecutively(laidOutItems, startingIndex, laidOutItems.Count);

        // we removed items before the first visible item
        // so we need just to adjust scroll position and redraw
        if (!IsOnScreen(itemsToRemove[^1], prevViewport) && firstVisibleItem?.Position >= startingIndex)
        {
            AdjustScrollIfNeeded(laidOutItems, firstVisibleItem!, firstItemPrevVisibleCellBounds);
            (this as IView)!.InvalidateMeasure();
            return;
        }

        var listView = ListView!;
        var prevScrollX = listView.ScrollX;
        var prevScrollY = listView.ScrollY;
        var padding = listView.Padding;

        bool scrollWasAdjusted = false;

        try
        {
            // if we removed items from the beginning
            // we need animate items below
            if (startingIndex == 0 && prevScrollY == 0d && prevScrollX == 0d)
            {
                return;
            }

            // if we removed all visible items
            // then we need to adjust the scroll position
            // and animate items from the bottom
            if (firstVisibleItem is null)
            {
                var itemBeforeRemoved = laidOutItems[startingIndex - 1];

                scrollWasAdjusted = AdjustScrollIfNeeded(laidOutItems, itemBeforeRemoved, new(prevScrollX - padding.Left, prevScrollY - padding.Top, 0, 0));
                return;
            }

            // if we removed items before the first visible item
            // then we also need to adjust the scroll position
            if (firstVisibleItem.Position >= startingIndex)
            {
                scrollWasAdjusted = AdjustScrollIfNeeded(laidOutItems, firstVisibleItem, firstItemPrevVisibleCellBounds);
            }
        }
        finally
        {
            var scrollX = listView.ScrollX;
            var scrollY = listView.ScrollY;
            var scrollDeltaX = scrollX - prevScrollX;
            var scrollDeltaY = scrollY - prevScrollY;

            bool hasVisibleItemsBefore = firstVisibleItem?.Position < startingIndex;
            bool hasVisibleItemsAfter = visibleItems.Any(i => i.Position >= startingIndex);

            // we need to animate items before removed range
            // from the top of the viewport
            if (!hasVisibleItemsBefore && hasVisibleItemsAfter && scrollWasAdjusted)
            {
                OnRemovedLeadingVisibleItems(
                    [.. itemsToAnimateBeforeRemovedRange, .. itemsToAnimateAfterRemovedRange], itemsToRemove, prevViewport, scrollDeltaX, scrollDeltaY);
            }
            // we need to animate already visible items
            // to avoid the blink effect after the rendering cycle
            else if (hasVisibleItemsBefore && hasVisibleItemsAfter && !scrollWasAdjusted)
            {
                OnRemovedMiddleVisibleItems(itemsToAnimateAfterRemovedRange, itemsToRemove, prevViewport, scrollDeltaX, scrollDeltaY);
            }
            // we have removed all visible range
            // thus we need to animate items from the bottom
            else if (!hasVisibleItemsBefore && !hasVisibleItemsAfter && scrollWasAdjusted)
            {
                OnRemovedAllVisibleItems(itemsToAnimateAfterRemovedRange, itemsToRemove, prevViewport, scrollDeltaX, scrollDeltaY);
            }
            // we need to animate items from the bottom
            else if (hasVisibleItemsBefore && !hasVisibleItemsAfter)
            {
                OnRemovedTrailingVisibleItems(itemsToAnimateAfterRemovedRange, itemsToRemove, prevViewport, scrollDeltaX, scrollDeltaY);
            }
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

        var prevViewPort = Viewport;

        var itemsToRearrange = LaidOutItems.Where(i => IsOnScreen(i, prevViewPort) && i.IsAttached);
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
        // and if we are at the top we don't need to adjust the scroll position
        if (startingIndex == 0 && listView.ScrollX == 0d && listView.ScrollY == 0d)
        {
            UpdateItemsLayout(startingIndex);
            return;
        }

        // if there is no any visible item 
        // or we replaced items after the first visible item
        // so we don't need to adjust the scroll position
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

        var prevViewPort = Viewport;

        var itemsToRearrange = LaidOutItems.FindAll(i => IsOnScreen(i, prevViewPort) && i.IsAttached);
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
        var laidOutItems = LaidOutItems;
        var count = laidOutItems.Count;
        if (count == 0) return;

        NeedsAdjustScroll = shouldAdjustScroll;
        var viewport = Viewport;

        for (int i = fromPosition; i < count; i++)
        {
            var item = laidOutItems[i];

            if (!IsOnScreen(item, viewport) && item.IsAttached)
            {
                (this as IView)!.InvalidateMeasure();
                return;
            }
        }

        for (int i = fromPosition; i < count; i++)
        {
            var item = laidOutItems[i];

            if (IsOnScreen(item, viewport) && !item.IsAttached)
            {
                (this as IView)!.InvalidateMeasure();
                return;
            }
        }

        NeedsAdjustScroll = false;
    }

    protected virtual void DetachCell(VirtualizeListViewItem item)
    {
        if (!item.IsAttached || item.Cell is null) return;

        VisibleItems.Remove(item);

        Adapter!.DetachCell(item.Cell!, item.AdapterItem!, item.Position);

        CacheCell(item);
    }

    protected virtual void CacheCell(VirtualizeListViewItem item)
    {
        if (item.Cell is null) return;

        var cell = item.Cell;

        var templateId = (item.Template as IDataTemplateController)!.Id;
        if (!CachedCells.TryGetValue(templateId, out var stack))
        {
            stack = new Stack<CellHolder>();
            CachedCells[templateId] = stack;
        }

        stack.Push(cell);
        item.Cell = null;
    }

    protected virtual void AttachCell(VirtualizeListViewItem item, Rect viewport)
    {
        VisibleItems.Add(item);
        if (item.Cell is not null) return;

        Adapter!.AttachCell(ReuseCell(item, viewport), item.AdapterItem!, item.Position);
    }

    protected virtual CellHolder ReuseCell(VirtualizeListViewItem item, Rect viewport)
    {
        CellHolder cell;

        var templateId = (item.Template as IDataTemplateController)!.Id;
        if (CachedCells.TryGetValue(templateId, out var stack) && stack.Count > 0)
        {
            cell = stack.Pop();

            item.Cell = cell;
        }
        else
        {
            var freeItem = LaidOutItems.FirstOrDefault(i => i.Cell is not null
                && (i.Template as IDataTemplateController)!.Id == templateId
                && !i.IsAttached && !i.IntersectsWithRect(viewport));
            if (freeItem is not null)
            {
                cell = freeItem.Cell!;
                freeItem.Cell = null;
                item.Cell = cell;
            }
            else
            {
                cell = Adapter!.OnCreateCell(item.Template!, item.Position);
                item.Cell = cell;

                this.Add(cell);
            }
        }

        return cell;
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

    protected virtual bool BeforeItemMeasure(VirtualizeListViewItem item, Rect viewport)
    {
        if (!IsOnScreen(item, viewport))
        {
            if (!item.AnyPendingAnimation
                && !item.AnyAnimatingAnimation)
            {
                DetachCell(item);
            }

            return false;
        }

        if (item.Cell is null) AttachCell(item, viewport);
        return true;
    }

    protected abstract void RepositionItemsFromIndex(IReadOnlyList<VirtualizeListViewItem> items, int index);
    public abstract VirtualizeListViewItem CreateItemForPosition(int position);
    protected abstract Thickness GetItemMargin(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item);
    protected abstract Size GetEstimatedItemSize(VirtualizeListViewItem item, Size availableSize);
    protected abstract void ShiftItemsChunk(IReadOnlyList<VirtualizeListViewItem> items, int start, int exclusiveEnd);
    protected abstract void ShiftItemsConsecutively(IReadOnlyList<VirtualizeListViewItem> items, int start, int exclusiveEnd);
    protected abstract bool AdjustScrollIfNeeded(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item, Rect prevBoundsOfItem);
    protected abstract Size MeasureItem(VirtualizeListViewItem item, Rect viewport, Size availableSpace);

    protected virtual bool IsOnScreen(VirtualizeListViewItem item, Rect viewport)
    {
        return item.IntersectsWithRect(viewport);
    }

    #region Animation

    protected virtual void OnInsertedIntoVisibleRect(List<(VirtualizeListViewItem item, Rect prevBounds)> itemsToShift,
        List<VirtualizeListViewItem> insertedItems)
    {
        var orientation = GetOrientation();
        if (orientation is ScrollOrientation.Both) return;

        var listView = ListView!;
        var padding = listView.Padding;

        var availableSpace = AvailableSpace;
        var viewport = Viewport;

        double totalOffsetX = 0d, totalOffsetY = 0d;

        if (orientation is ScrollOrientation.Vertical)
        {
            var totalHeightToFill = viewport.Bottom - insertedItems[0].LeftTopWithMargin.Y;

            foreach (var item in insertedItems)
            {
                if (totalHeightToFill <= totalOffsetY)
                {
                    totalOffsetY += item.Bounds.Height + item.Margin.VerticalThickness;
                    continue;
                }

                var measure = MeasureItem(item, viewport, availableSpace);
                totalOffsetY += measure.Height + item.Margin.VerticalThickness;
            }
        }
        else
        {
            var totalWidthToFill = viewport.Right - insertedItems[0].LeftTopWithMargin.X;

            foreach (var item in insertedItems)
            {
                if (totalWidthToFill <= totalOffsetX)
                {
                    totalOffsetX += item.Bounds.Width + item.Margin.HorizontalThickness;
                    continue;
                }

                var measure = MeasureItem(item, viewport, availableSpace);
                totalOffsetX += measure.Width + item.Margin.HorizontalThickness;
            }
        }

        foreach (var (item, prevBounds) in itemsToShift)
        {
            if (item.Cell is CellHolder cell)
            {
                if (orientation is ScrollOrientation.Vertical)
                {

                }
                cell.TranslationX -= totalOffsetX;
                cell.TranslationY -= totalOffsetY;
            }

            //item.AddTranslationYAnimation(prevBounds.Y, ShiftAnimationDuration, ZeroAnimationDelay);
            //item.AddTranslationXAnimation(prevBounds.X, ShiftAnimationDuration, ZeroAnimationDelay);
        }

        PrepareItemsForPreTranslationAnimation(insertedItems);

        (this as IView)!.InvalidateMeasure();

        this.Dispatcher.Dispatch(() =>
        {
            viewport = Viewport;

            CleanupItemsPreTranslationAnimation(insertedItems);
            foreach (var item in insertedItems.Where(i => IsOnScreen(i, viewport)))
            {
                if (item.Cell is CellHolder cell)
                {
                    cell.GetFromCache();
                }
            }

            //foreach (var item in insertedItems.Where(i => IsOnScreen(i, viewport)))
            //{
            //    item.AddFadeInAnimation(OpacityAnimationDuration, DefaultAnimationDelay);
            //    item.AnimateAll();
            //}

            //if (itemsToShift.Any(i => IsOnScreen(i.item, viewport)))
            //{
            //    foreach (var (item, prevBounds) in itemsToShift)
            //    {
            //        Action<VirtualizeListViewItem> callback = !IsOnScreen(item, viewport)
            //            ? itemToCache => DetachCell(itemToCache)
            //            : itemToCache => { };

            //        item.AnimateAll(() => callback(item));
            //    }
            //    return;
            //}

            //if (orientation is ScrollOrientation.Vertical)
            //{
            //    var yOffset = viewport.Bottom - itemsToShift[0].prevBounds.Y;

            //    foreach (var (item, prevBounds) in itemsToShift)
            //    {
            //        Task.WhenAll(
            //            item.Animate<TranslationYAnimation>(prevBounds.Y + yOffset),
            //            item.Animate<TranslationXAnimation>(prevBounds.X)
            //        ).ContinueWith(_ => DetachCell(item));
            //    }
            //}
            //else
            //{
            //    var xOffset = viewport.Right - itemsToShift[0].prevBounds.X;
            //    foreach (var (item, prevBounds) in itemsToShift)
            //    {
            //        Task.WhenAll(
            //            item.Animate<TranslationXAnimation>(prevBounds.X + xOffset),
            //            item.Animate<TranslationYAnimation>(prevBounds.Y)
            //        ).ContinueWith(_ => DetachCell(item));
            //    }
            //}
        });
    }

    protected virtual void OnRemovedLeadingVisibleItems(List<(VirtualizeListViewItem item, Rect prevBounds)> itemsToShift,
        List<VirtualizeListViewItem> removedItems,
        Rect prevViewport, double scrollDeltaX, double scrollDeltaY)
    {
        var orientation = GetOrientation();
        if (orientation is ScrollOrientation.Both) return;

        var listView = ListView!;
        var padding = listView.Padding;
        var scrollX = listView.ScrollX;
        var scrollY = listView.ScrollY;

        var availableSpace = AvailableSpace;
        var viewport = Viewport;

        var firstRemovedItemPosition = removedItems[0].Position;

        List<(VirtualizeListViewItem item, Rect prevBounds)> itemsBeforeRemoved = [];
        List<(VirtualizeListViewItem item, Rect prevBounds)> itemsAfterRemoved = [];
        List<(VirtualizeListViewItem item, Rect prevBounds)> visibleItemsAfterRemoved = [];
        List<(VirtualizeListViewItem item, Rect prevBounds)> invisibleItemsAfterRemoved = [];
        foreach (var tpl in itemsToShift)
        {
            if (tpl.item.Position < firstRemovedItemPosition)
            {
                itemsBeforeRemoved.Add(tpl);
                continue;
            }

            itemsAfterRemoved.Add(tpl);

            if (IsOnScreen(tpl.item, viewport) && tpl.item.Cell is not null)
            {
                visibleItemsAfterRemoved.Add(tpl);
                continue;
            }

            invisibleItemsAfterRemoved.Add(tpl);
        }

        var adapter = Adapter!;

        var firstItemAfterRemoved = itemsAfterRemoved[0];

        double totalAdjustY = 0d, totalAdjustX = 0d;
        double totalHeightToFill = 0d, totalWidthToFill = 0d;

        if (orientation is ScrollOrientation.Vertical)
        {
            totalHeightToFill = firstItemAfterRemoved.prevBounds.Y - firstItemAfterRemoved.item.Margin.Top - prevViewport.Y;

            foreach (var tpl in itemsBeforeRemoved)
            {
                if (totalHeightToFill <= 0) break;

                var prevItemBounds = tpl.prevBounds;

                var item = tpl.item;
                var measure = MeasureItem(item, viewport, availableSpace);

                totalAdjustY += measure.Height - prevItemBounds.Height;
                totalHeightToFill -= measure.Height + item.Margin.VerticalThickness;
            }

            totalHeightToFill -= padding.Top;
            if (totalHeightToFill > 0)
            {
                scrollDeltaY = 0;

                foreach (var item in visibleItemsAfterRemoved)
                {
                    if (item.item.Cell is CellHolder cell)
                    {
                        cell.TranslationY += totalHeightToFill;
                    }
                }

                listView.AdjustScroll(totalAdjustX, totalAdjustY);
            }
            else
            {
                totalHeightToFill = 0;
                scrollDeltaY = totalAdjustY;
            }
        }
        else
        {
            totalWidthToFill = firstItemAfterRemoved.prevBounds.X - firstItemAfterRemoved.item.Margin.Left - prevViewport.X;

            foreach (var tpl in itemsBeforeRemoved)
            {
                if (totalWidthToFill <= 0) break;

                var prevItemBounds = tpl.prevBounds;

                var item = tpl.item;
                var measure = MeasureItem(item, viewport, availableSpace);

                totalAdjustX += measure.Width - prevItemBounds.Width;
                totalWidthToFill -= measure.Width + item.Margin.HorizontalThickness;
            }

            totalWidthToFill -= padding.Left;
            if (totalWidthToFill > 0)
            {
                scrollDeltaX = 0;

                foreach (var item in visibleItemsAfterRemoved)
                {
                    if (item.item.Cell is CellHolder cell)
                    {
                        cell.TranslationX += totalWidthToFill;
                    }
                }

                listView.AdjustScroll(totalAdjustX, totalAdjustY);
            }
            else
            {
                totalWidthToFill = 0;
                scrollDeltaX = totalAdjustX;
            }
        }

        viewport = Viewport;
        var scrollDeltaXFinal = viewport.X - prevViewport.X + totalAdjustX;
        var scrollDeltaYFinal = viewport.Y - prevViewport.Y + scrollDeltaY;

        ApplyScrollDeltaToRemovedItems(removedItems, prevViewport, scrollDeltaXFinal, scrollDeltaYFinal);

        var itemsToPrepare = itemsBeforeRemoved.Concat(invisibleItemsAfterRemoved)
            .Select(tpl => tpl.item).ToList();

        PrepareItemsForPreTranslationAnimation(itemsToPrepare);

        (this as IView).InvalidateMeasure();

        this.Dispatcher.Dispatch(() =>
        {
            CleanupItemsPreTranslationAnimation(itemsToPrepare);

            viewport = Viewport;

            var lastItemRightBottom = itemsBeforeRemoved[^1].item.RightBottom;

            Func<VirtualizeListViewItem, Rect, (double newX, double newY)> beforeRemovedTranslationFunc;
            Func<VirtualizeListViewItem, Rect, (double newX, double newY)> afterRemovedTranslationFunc;

            if (orientation is ScrollOrientation.Vertical)
            {
                var extraTopOffsetY = scrollY - padding.Top - lastItemRightBottom.Y + totalAdjustY;

                beforeRemovedTranslationFunc = (item, prevBounds) =>
                {
                    var newY = item.LeftTop.Y + extraTopOffsetY;
                    return (prevBounds.X, newY);
                };

                afterRemovedTranslationFunc = (item, prevBounds) =>
                {
                    var newY = item.LeftTop.Y + totalHeightToFill;
                    return (prevBounds.X, newY);
                };
            }
            else
            {
                var extraLeftOffsetX = scrollX - padding.Left - lastItemRightBottom.X;

                beforeRemovedTranslationFunc = (item, prevBounds) =>
                {
                    var newX = item.LeftTop.X + extraLeftOffsetX;
                    return (newX, prevBounds.Y);
                };

                afterRemovedTranslationFunc = (item, prevBounds) =>
                {
                    var newX = item.LeftTop.X + totalWidthToFill;
                    return (newX, prevBounds.Y);
                };
            }

            foreach (var (item, prevBounds) in itemsBeforeRemoved)
            {
                if (item.AnyAnimatingAnimation) continue;

                var (newX, newY) = beforeRemovedTranslationFunc(item, prevBounds);

                item.AddTranslationXAnimation(newX, ShiftAnimationDuration, DefaultAnimationDelay);
                item.AddTranslationYAnimation(newY, ShiftAnimationDuration, DefaultAnimationDelay);
                item.AnimateAll();
            }

            foreach (var (item, prevBounds) in itemsAfterRemoved.Where(i => IsOnScreen(i.item, viewport)))
            {
                if (item.AnyAnimatingAnimation) continue;

                var (newX, newY) = afterRemovedTranslationFunc(item, prevBounds);

                item.AddTranslationXAnimation(newX, ShiftAnimationDuration, DefaultAnimationDelay);
                item.AddTranslationYAnimation(newY, ShiftAnimationDuration, DefaultAnimationDelay);
                item.AnimateAll();
            }
        });
    }

    protected virtual void OnRemovedMiddleVisibleItems(List<(VirtualizeListViewItem item, Rect prevBounds)> itemsToShift,
        List<VirtualizeListViewItem> removedItems,
        Rect prevViewport, double scrollDeltaX, double scrollDeltaY)
    {
        var orientation = GetOrientation();
        if (orientation is ScrollOrientation.Both) return;

        var padding = ListView!.Padding;
        var scrollX = ListView.ScrollX;
        var scrollY = ListView.ScrollY;

        var viewport = Viewport;

        List<(VirtualizeListViewItem item, Rect prevBounds)> invisibleItemsToShift = [];
        List<(VirtualizeListViewItem item, Rect prevBounds)> visibleItemsToShift = [];

        foreach (var tpl in itemsToShift)
        {
            if (IsOnScreen(tpl.item, viewport) && tpl.item.Cell is not null)
            {
                visibleItemsToShift.Add(tpl);
                continue;
            }

            invisibleItemsToShift.Add(tpl);
        }

        var itemsToPrepare = invisibleItemsToShift.Select(tpl => tpl.item).ToList();

        PrepareItemsForPreTranslationAnimation(itemsToPrepare);

        double totalRemovedHeight = 0d, totalRemovedWidth = 0d;
        if (orientation is ScrollOrientation.Vertical)
        {
            totalRemovedHeight = removedItems.Sum(i => i.Size.Height + i.Margin.VerticalThickness);
        }
        else
        {
            totalRemovedWidth = removedItems.Sum(i => i.Size.Width + i.Margin.HorizontalThickness);
        }

        foreach (var (item, prevBounds) in visibleItemsToShift)
        {
            if (item.Cell is CellHolder cell)
            {
                cell.TranslationX += totalRemovedWidth;
                cell.TranslationY += totalRemovedHeight;
            }
        }

        (this as IView).InvalidateMeasure();

        this.Dispatcher.Dispatch(() =>
        {
            CleanupItemsPreTranslationAnimation(itemsToPrepare);

            var viewport = Viewport;

            foreach (var (item, prevBounds) in itemsToShift.Where(tpl => IsOnScreen(tpl.item, viewport) && !tpl.item.AnyAnimatingAnimation))
            {
                item.AddTranslationXAnimation(prevBounds.X, ShiftAnimationDuration, DefaultAnimationDelay);
                item.AddTranslationYAnimation(prevBounds.Y, ShiftAnimationDuration, DefaultAnimationDelay);
                item.AnimateAll();
            }
        });
    }

    protected virtual void OnRemovedAllVisibleItems(List<(VirtualizeListViewItem item, Rect prevBounds)> itemsToShift,
        List<VirtualizeListViewItem> removedItems,
        Rect prevViewport, double scrollDeltaX, double scrollDeltaY)
    {
        var orientation = GetOrientation();
        if (orientation is ScrollOrientation.Both) return;

        var padding = ListView!.Padding;
        var scrollX = ListView.ScrollX;
        var scrollY = ListView.ScrollY;

        ApplyScrollDeltaToRemovedItems(removedItems, prevViewport, scrollDeltaX, scrollDeltaY);

        var itemsToPrepare = itemsToShift.Select(tpl => tpl.item).ToList();

        PrepareItemsForPreTranslationAnimation(itemsToPrepare);

        (this as IView).InvalidateMeasure();

        this.Dispatcher.Dispatch(() =>
        {
            CleanupItemsPreTranslationAnimation(itemsToPrepare);

            var viewport = Viewport;

            itemsToShift = [.. itemsToShift.Where(tpl => IsOnScreen(tpl.item, viewport))];

            var firstItemLeftTop = itemsToShift[0].item.LeftTop;

            Func<VirtualizeListViewItem, Rect, (double newX, double newY)> translationFunc;

            if (orientation is ScrollOrientation.Vertical)
            {
                var extraOffsetY = Viewport.Bottom - firstItemLeftTop.Y;

                translationFunc = (item, prevBounds) =>
                {
                    var newY = item.LeftTop.Y + extraOffsetY;
                    return (prevBounds.X, newY);
                };
            }
            else
            {
                var extraOffsetX = Viewport.Right - firstItemLeftTop.X;

                translationFunc = (item, prevBounds) =>
                {
                    var newX = item.LeftTop.X + extraOffsetX;
                    return (newX, prevBounds.Y);
                };
            }

            foreach (var (item, prevBounds) in itemsToShift)
            {
                if (item.AnyAnimatingAnimation) continue;

                var (newX, newY) = translationFunc(item, prevBounds);

                item.AddTranslationYAnimation(newY, ShiftAnimationDuration, DefaultAnimationDelay);
                item.AddTranslationXAnimation(newX, ShiftAnimationDuration, DefaultAnimationDelay);
                item.AnimateAll();
            }
        });
    }

    protected virtual void OnRemovedTrailingVisibleItems(List<(VirtualizeListViewItem item, Rect prevBounds)> itemsToShift,
        List<VirtualizeListViewItem> removedItems,
        Rect prevViewport, double scrollDeltaX, double scrollDeltaY)
    {
        OnRemovedAllVisibleItems(itemsToShift, removedItems, prevViewport, scrollDeltaX, scrollDeltaY);
    }

    protected virtual void ApplyScrollDeltaToRemovedItems(List<VirtualizeListViewItem> itemsToApplyDelta,
        Rect viewport, double scrollDeltaX, double scrollDeltaY)
    {
        var visibleRemovedItems = itemsToApplyDelta.Where(i => i.Bounds.IntersectsWith(viewport)).ToList();
        foreach (var item in visibleRemovedItems)
        {
            if (item.Cell is CellHolder holder)
            {
                holder.TranslationX = scrollDeltaX;
                holder.TranslationY = scrollDeltaY;
            }
        }
    }

    protected virtual void CleanupItemsPreTranslationAnimation(List<VirtualizeListViewItem> itemsToCleanup)
    {
        foreach (var item in itemsToCleanup)
        {
            item.OnCellAttached -= Item_OnCellAttached_CacheCell;
            item.PreTranslationAnimation = false;
        }
    }

    protected virtual void Item_OnCellAttached_CacheCell(object? sender, CellHolder? e)
    {
        var item = (sender as VirtualizeListViewItem)!;
        item.OnCellAttached -= Item_OnCellAttached_CacheCell;
        e?.Cache();
    }

    protected virtual void PrepareItemsForPreTranslationAnimation(List<VirtualizeListViewItem> itemsToAnimate)
    {
        foreach (var item in itemsToAnimate)
        {
            // we need to move offscreen invisible items
            // which are about to appear
            // to avoid the blink effect after the rendering cycle
            item.OnCellAttached += Item_OnCellAttached_CacheCell;

            item.PreTranslationAnimation = true;

            if (item.Cell is not CellHolder holder) continue;

            // here we need to move item off-screen
            // to avoid the blink effect after the rendering cycle
            holder.Cache();
        }
    }

    #endregion

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

    ~VirtualizeItemsLayoutManager()
    {
        Dispose(false);
    }
    #endregion
}