using System.ComponentModel;
using System.Runtime.InteropServices;

namespace MPowerKit.VirtualizeListView;

public abstract class ItemsLayoutManager : AbsoluteLayout, IDisposable
{
    protected const double FullSize = -1d;

    protected ScrollEventArgs PrevScroll { get; set; } = new(0d, 0d, 0d, 0d);
    protected Point PrevScrollBeforeSizeChange { get; set; }
    protected Size PrevContentSize { get; set; }
    protected Size PrevAvailableSpace { get; set; }

    public VirtualizeListView? Control { get; protected set; }
    public bool IsDisposed { get; protected set; }

    protected List<VirtualizeListViewItem> LaidOutItems { get; } = [];
    protected List<VirtualizeListViewItem> CachedItems { get; } = [];

    public List<(object Data, int Position)> VisibleItems => LaidOutItems.FindAll(i => i.IsOnScreen && i.IsAttached && i.Cell?.Children[0] is VirtualizeListViewCell).Select(i => (i.BindingContext, i.Position)).ToList();

    protected virtual Size AvailableSpace => GetAvailableSpace();

    protected bool PendingAdjustScroll { get; set; }

    protected override void OnParentChanging(ParentChangingEventArgs args)
    {
        base.OnParentChanging(args);

        UnsubscribeFromEvents();

        Control = null;
    }

    protected override void OnParentChanged()
    {
        base.OnParentChanged();

        if (this.Parent is null) return;

        if (this.Parent is not VirtualizeListView listView)
        {
            throw new InvalidOperationException("ItemsLayoutManager can be used only within VirtualizeListView");
        }

        Control = listView;

        SubscribeToEvents();
    }

    protected virtual void UnsubscribeFromEvents()
    {
        if (Control is null) return;

        OnAdapterReset();

        Control.PropertyChanging -= Control_PropertyChanging;
        Control.PropertyChanged -= Control_PropertyChanged;
        Control.SizeChanged -= Control_SizeChanged;
        Control.Scrolled -= Control_Scrolled;
    }

    protected virtual void SubscribeToEvents()
    {
        if (Control is null) return;

        Control.PropertyChanging += Control_PropertyChanging;
        Control.PropertyChanged += Control_PropertyChanged;
        Control.SizeChanged += Control_SizeChanged;
        Control.Scrolled += Control_Scrolled;

        OnAdapterSet();
    }

    protected virtual bool DoesScrollHaveSize()
    {
        return Control is not null && !double.IsNaN(Control.Width) && !double.IsNaN(Control.Height) && Control.Width >= 0d && Control.Height >= 0d;
    }

    protected virtual void OnAdapterSet()
    {
        if (Control?.Adapter is null) return;

        Control.Adapter.DataSetChanged += AdapterDataSetChanged;
        Control.Adapter.ItemMoved += AdapterItemMoved;
        Control.Adapter.ItemRangeChanged += AdapterItemRangeChanged;
        Control.Adapter.ItemRangeInserted += AdapterItemRangeInserted;
        Control.Adapter.ItemRangeRemoved += AdapterItemRangeRemoved;

        InvalidateLayout();
    }

    protected virtual void OnAdapterReset()
    {
        if (Control?.Adapter is null) return;

        Control.Adapter.DataSetChanged -= AdapterDataSetChanged;
        Control.Adapter.ItemMoved -= AdapterItemMoved;
        Control.Adapter.ItemRangeChanged -= AdapterItemRangeChanged;
        Control.Adapter.ItemRangeInserted -= AdapterItemRangeInserted;
        Control.Adapter.ItemRangeRemoved -= AdapterItemRangeRemoved;
    }

    protected virtual void Control_Scrolled(object? sender, ScrolledEventArgs e)
    {
        var newScroll = e - PrevScroll;
        UpdateItemsLayout(0, true);
        PrevScroll = newScroll;
    }

    protected virtual void Control_PropertyChanging(object? sender, Microsoft.Maui.Controls.PropertyChangingEventArgs e)
    {
        if (e.PropertyName == VirtualizeListView.AdapterProperty.PropertyName)
        {
            OnAdapterReset();
        }
    }

    protected virtual void Control_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == VirtualizeListView.AdapterProperty.PropertyName)
        {
            OnAdapterSet();
        }
        //else if (e.PropertyName == VirtualizeListView.ContentSizeProperty.PropertyName)
        //{
        //    AdjustScrollPosition();
        //}
        else if (e.PropertyName == VirtualizeListView.PaddingProperty.PropertyName)
        {
            Control_SizeChanged(Control, EventArgs.Empty);
        }
    }

    protected virtual void Control_SizeChanged(object? sender, EventArgs e)
    {
        var newSpace = GetAvailableSpace();

        if (newSpace == PrevAvailableSpace) return;

        PrevAvailableSpace = GetAvailableSpace();

        if (LaidOutItems.Count == 0)
        {
            InvalidateLayout();
            return;
        }
        else
        {
            RelayoutItems();
            //PendingAdjustScroll = true;
        }

        //ContentSizeChanging();

        //await content width change
        //await Task.Yield();
        //await content height change
        //await Task.Yield();

        //AdjustScrollPosition();
    }

    //protected virtual void ContentSizeChanging()
    //{
    //    PrevScrollBeforeSizeChange = new(Control.ScrollX, Control.ScrollY);

    //    PrevContentSize = LayedOutItems.Count == 0 ? new(0d, 0d) : new(Control.ContentSize.Width, Control.ContentSize.Height);
    //}

    //protected virtual void AdjustScrollPosition()
    //{
    //    //if (Items.Count == 0 || !PendingAdjustScroll
    //    //    || Math.Abs(Math.Round(Control.ContentSize.Height) - Math.Round(PrevContentSize.Height)) <= 1) return;

    //    //PendingAdjustScroll = false;

    //    var newScrollX = PrevScrollBeforeSizeChange.X <= Control.Padding.Left
    //        ? PrevScrollBeforeSizeChange.X
    //        : (((PrevScrollBeforeSizeChange.X - Control.Padding.Left) / PrevContentSize.Width * Control.ContentSize.Width) + Control.Padding.Left);
    //    var newScrollY = PrevScrollBeforeSizeChange.Y <= Control.Padding.Top
    //        ? PrevScrollBeforeSizeChange.Y
    //        : (((PrevScrollBeforeSizeChange.Y - Control.Padding.Top) / PrevContentSize.Height * Control.ContentSize.Height) + Control.Padding.Top);

    //    Control.ScrollToAsync(newScrollX, newScrollY, false);
    //}

    /// <summary>
    /// Use only if there was no any items before, or itemtemplate has changed.
    /// Clears all items and lays out new ones.
    /// </summary>
    public virtual void InvalidateLayout()
    {
        LaidOutItems.Clear();
        CachedItems.Clear();
        this.Clear();
        ResizeLayout();

        if (!DoesScrollHaveSize()) return;

        if (Control?.Adapter is null || Control.Adapter.Items.Count == 0) return;

        var dataItems = Control.Adapter.Items;
        var count = dataItems.Count;

        for (int i = 0; i < count; i++)
        {
            var item = CreateItemForPosition(dataItems, i);

            LaidOutItems.Add(item);

            var estimatedSize = GetEstimatedItemSize(item);
            item.CellBounds = item.Bounds = new(0d, 0d, estimatedSize.Width, estimatedSize.Height);

            ShiftAllItems(LaidOutItems, i, LaidOutItems.Count);

            if (!item.IsOnScreen) continue;

            item.Cell = Control.Adapter.OnCreateCell(item.Template, item.Position);
            this.Add(item.Cell);

            Control.Adapter.OnBindCell(item.Cell, item.Position);

            ArrangeItem(LaidOutItems, item, AvailableSpace);

            item.IsAttached = true;
        }

        DrawAndResize();
    }

    protected virtual void RelayoutItems()
    {
        var count = LaidOutItems.Count;
        if (!DoesScrollHaveSize() || count == 0) return;

        var attachedItems = LaidOutItems.FindAll(i => i.IsAttached);

        if (attachedItems.Count == 0)
        {
            UpdateItemsLayout(0, false);
            return;
        }

        attachedItems.Sort((a, b) => a.Position.CompareTo(b.Position));

        foreach (var item in attachedItems)
        {
            ArrangeItem(LaidOutItems, item, AvailableSpace);
        }

        UpdateItemsLayout(attachedItems[^1].Position + 1, false);
    }

    protected virtual void AdapterDataSetChanged(object? sender, EventArgs e)
    {
        if (Control!.Adapter.ItemsCount == 0 && LaidOutItems.Count == 0)
        {
            return;
        }
        else if (LaidOutItems.Count == 0 && CachedItems.Count == 0)
        {
            InvalidateLayout();
        }
        else if (Control!.Adapter.ItemsCount == 0)
        {
            AdapterItemRangeRemoved(this, (0, LaidOutItems.Count));
        }
        else if (LaidOutItems.Count > 0)
        {
            AdapterItemRangeChanged(this, (0, LaidOutItems.Count, Control.Adapter.ItemsCount));
            Control.ScrollToAsync(0, 0, false);
        }
        else if (LaidOutItems.Count == 0)
        {
            AdapterItemRangeInserted(this, (0, Control.Adapter.ItemsCount));
        }
    }

    protected virtual void AdapterItemRangeInserted(object? sender, (int StartingIndex, int TotalCount) e)
    {
        if (!DoesScrollHaveSize()) return;

        var count = LaidOutItems.Count;
        var startingIndex = e.StartingIndex;

        if (Control!.Adapter.ItemsCount == 0 || e.StartingIndex > count)
        {
            throw new ArgumentException("Invalid range");
        }

        if (count == 0 && CachedItems.Count == 0)
        {
            InvalidateLayout();
            return;
        }

        var finishIndex = startingIndex + e.TotalCount;

        var itemsToRearrange = LaidOutItems.FindAll(i => i.IsOnScreen && i.IsAttached);
        var firstVisibleItem = itemsToRearrange.FirstOrDefault();
        var prevVisibleCellBounds = firstVisibleItem?.CellBounds ?? new();

        var dataItems = Control.Adapter.Items;

        for (int index = startingIndex; index < finishIndex; index++)
        {
            var item = CreateItemForPosition(dataItems, index);

            LaidOutItems.Insert(index, item);

            var estimatedSize = GetEstimatedItemSize(item);
            item.CellBounds = item.Bounds = new(0d, 0d, estimatedSize.Width, estimatedSize.Height);
        }

        count = LaidOutItems.Count;
        for (int i = finishIndex; i < count; i++)
        {
            LaidOutItems[i].Position = i;
        }

        ShiftItemsConsecutively(LaidOutItems, startingIndex, finishIndex);
        ShiftAllItems(LaidOutItems, finishIndex, count);

        itemsToRearrange = itemsToRearrange.FindAll(i => i.Position >= finishIndex);

        if (itemsToRearrange.Count == 0 || firstVisibleItem!.Position <= startingIndex)
        {
            UpdateItemsLayout(startingIndex, false);
            return;
        }

        foreach (var item in itemsToRearrange)
        {
            ArrangeItem(LaidOutItems, item, AvailableSpace);
        }

        ShiftAllItems(LaidOutItems, itemsToRearrange[^1].Position + 1, count);

        if (AdjustScrollIfNeeded(LaidOutItems, firstVisibleItem, prevVisibleCellBounds)) return;

        UpdateItemsLayout(startingIndex, false);
    }

    protected virtual void AdapterItemRangeRemoved(object? sender, (int StartingIndex, int TotalCount) e)
    {
        if (!DoesScrollHaveSize()) return;

        var count = LaidOutItems.Count;
        var startingIndex = e.StartingIndex;
        var finishIndex = startingIndex + e.TotalCount;

        if (count == 0 || finishIndex > count || startingIndex < 0)
        {
            throw new ArgumentException("Invalid range");
        }

        for (int i = startingIndex; i < finishIndex; i++)
        {
            var itemToRemove = LaidOutItems[startingIndex];
            LaidOutItems.RemoveAt(startingIndex);

            if (itemToRemove.IsAttached)
            {
                Control!.Adapter.OnCellRecycled(itemToRemove.Cell!, itemToRemove.Position);
                itemToRemove.IsAttached = false;
            }

            if (itemToRemove.Cell is null) continue;

            var itemWithoutCell = LaidOutItems.Find(i => i.Template == itemToRemove.Template && i.Cell is null);
            if (itemWithoutCell is null)
            {
                CachedItems.Add(itemToRemove);
                DrawCachedItem(itemToRemove);
            }
            else
            {
                var cell = itemToRemove.Cell;
                itemToRemove.Cell = null;
                itemWithoutCell.Cell = cell;
            }
        }

        count = LaidOutItems.Count;
        for (int i = startingIndex; i < count; i++)
        {
            LaidOutItems[i].Position = i;
        }

        var itemsToRearrange = LaidOutItems.FindAll(i => i.IsOnScreen && i.IsAttached);
        var firstVisibleItem = itemsToRearrange.FirstOrDefault();
        var prevVisibleCellBounds = firstVisibleItem?.CellBounds ?? new();
        var lastVisibleItemBeforRemoved = itemsToRearrange.FindLast(i => i.Position < startingIndex);
        var prevIndex = startingIndex - 1;
        var itemBeforeRemoved = prevIndex < 0 ? null : LaidOutItems[prevIndex];
        itemsToRearrange = itemsToRearrange.FindAll(i => i.Position >= startingIndex);

        ShiftAllItems(LaidOutItems, startingIndex, itemsToRearrange.Count == 0 ? count : itemsToRearrange[0].Position);

        if (itemsToRearrange.Count == 0)
        {
            if (lastVisibleItemBeforRemoved is not null || (Control!.ScrollX == 0d && Control.ScrollY == 0d))
            {
                UpdateItemsLayout(startingIndex, false);
                return;
            }

            if (itemBeforeRemoved is null)
            {
                Control.ScrollToAsync(0d, 0d, false);
                return;
            }

            prevVisibleCellBounds = new(
                Control.ScrollX - Control.Padding.Left - itemBeforeRemoved.CellBounds.Width,
                Control.ScrollY - Control.Padding.Top - itemBeforeRemoved.CellBounds.Height,
                itemBeforeRemoved.CellBounds.Width,
                itemBeforeRemoved.CellBounds.Height);

            firstVisibleItem = itemBeforeRemoved;
        }

        if (itemsToRearrange.Count != 0)
        {
            foreach (var item in itemsToRearrange)
            {
                ArrangeItem(LaidOutItems, item, AvailableSpace);
            }

            ShiftAllItems(LaidOutItems, itemsToRearrange[^1].Position + 1, count);
        }

        if (AdjustScrollIfNeeded(LaidOutItems, firstVisibleItem!, prevVisibleCellBounds)) return;

        UpdateItemsLayout(startingIndex, false);
    }

    protected virtual void AdapterItemRangeChanged(object? sender, (int StartingIndex, int OldCount, int NewCount) e)
    {
        if (!DoesScrollHaveSize()) return;

        var count = LaidOutItems.Count;
        var start = e.StartingIndex;
        var oldCount = e.OldCount;
        var oldEnd = start + e.OldCount;
        var newEnd = start + e.NewCount;
        var adapterItemsCount = Control!.Adapter.ItemsCount;

        if (count == 0 || adapterItemsCount == 0 || start < 0 || oldEnd > count || newEnd > adapterItemsCount)
        {
            throw new ArgumentException("Invalid range");
        }

        var dataItems = Control.Adapter.Items;
        var smallestEnd = Math.Min(oldEnd, newEnd);

        var itemsToRearrange = LaidOutItems.FindAll(i => i.IsOnScreen && i.IsAttached && (i.Position < start || i.Position >= oldEnd));
        var firstVisibleItem = itemsToRearrange.FirstOrDefault();
        var prevVisibleCellBounds = firstVisibleItem?.CellBounds ?? new();

        for (int i = start; i < smallestEnd; i++)
        {
            //remove
            var itemToRemove = LaidOutItems[i];
            LaidOutItems.RemoveAt(i);
            if (itemToRemove.IsAttached)
            {
                Control.Adapter.OnCellRecycled(itemToRemove.Cell!, itemToRemove.Position);
                itemToRemove.IsAttached = false;
            }

            //create
            var item = CreateItemForPosition(dataItems, i);
            LaidOutItems.Insert(i, item);
            if (itemToRemove.Cell is not null && item.Template == itemToRemove.Template)
            {
                var cell = itemToRemove.Cell;
                itemToRemove.Cell = null;
                item.Cell = cell;
                item.Bounds = itemToRemove.Bounds;
                item.CellBounds = itemToRemove.CellBounds;
            }
            else
            {
                var estimatedSize = GetEstimatedItemSize(item);
                item.CellBounds = item.Bounds = new(0d, 0d, estimatedSize.Width, estimatedSize.Height);
            }

            //reuse
            if (itemToRemove.Cell is not null)
            {
                var itemWithoutCell = LaidOutItems.Find(i => i.Template == itemToRemove.Template && i.Cell is null);
                if (itemWithoutCell is null)
                {
                    CachedItems.Add(itemToRemove);
                    DrawCachedItem(itemToRemove);
                }
                else
                {
                    var cell = itemToRemove.Cell;
                    itemToRemove.Cell = null;
                    itemWithoutCell.Cell = cell;
                }
            }
        }

        if (oldEnd != newEnd)
        {
            if (oldEnd < newEnd)
            {
                for (int i = smallestEnd; i < newEnd; i++)
                {
                    var item = CreateItemForPosition(dataItems, i);

                    LaidOutItems.Insert(i, item);

                    var estimatedSize = GetEstimatedItemSize(item);
                    item.CellBounds = item.Bounds = new(0d, 0d, estimatedSize.Width, estimatedSize.Height);
                }
            }
            else
            {
                for (int i = smallestEnd; i < oldEnd; i++)
                {
                    var itemToRemove = LaidOutItems[smallestEnd];
                    LaidOutItems.RemoveAt(smallestEnd);

                    if (itemToRemove.IsAttached)
                    {
                        Control.Adapter.OnCellRecycled(itemToRemove.Cell!, itemToRemove.Position);
                        itemToRemove.IsAttached = false;
                    }

                    if (itemToRemove.Cell is null) continue;

                    var itemWithoutCell = LaidOutItems.Find(i => i.Template == itemToRemove.Template && i.Cell is null);
                    if (itemWithoutCell is null)
                    {
                        CachedItems.Add(itemToRemove);
                        DrawCachedItem(itemToRemove);
                    }
                    else
                    {
                        var cell = itemToRemove.Cell;
                        itemToRemove.Cell = null;
                        itemWithoutCell.Cell = cell;
                    }
                }
            }
        }

        count = LaidOutItems.Count;
        for (int i = newEnd; i < count; i++)
        {
            LaidOutItems[i].Position = i;
        }

        itemsToRearrange = itemsToRearrange.FindAll(i => i.Position >= newEnd);

        ShiftItemsConsecutively(LaidOutItems, start, newEnd);
        ShiftAllItems(LaidOutItems, newEnd, count);

        if (itemsToRearrange.Count == 0)
        {
            UpdateItemsLayout(start, false);
            return;
        }

        foreach (var item in itemsToRearrange)
        {
            ArrangeItem(LaidOutItems, item, AvailableSpace);
        }

        ShiftAllItems(LaidOutItems, itemsToRearrange[^1].Position + 1, count);

        if (AdjustScrollIfNeeded(LaidOutItems, firstVisibleItem!, prevVisibleCellBounds)) return;

        UpdateItemsLayout(start, false);
    }

    protected virtual void AdapterItemMoved(object? sender, (int OldIndex, int NewIndex) e)
    {
        var count = LaidOutItems.Count;
        var newIndex = e.NewIndex;
        var oldIndex = e.OldIndex;

        if (!DoesScrollHaveSize()) return;
        if (Control?.Adapter is null || Control.Adapter.Items.Count == 0
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
        var prevVisibleCellBounds = firstVisibleItem.CellBounds;

        var itemToMove = LaidOutItems[oldIndex];

        LaidOutItems.RemoveAt(oldIndex);
        LaidOutItems.Insert(newIndex, itemToMove);

        for (int i = start; i <= end; i++)
        {
            LaidOutItems[i].Position = i;
        }

        if ((start < firstVisibleItem.Position && end < firstVisibleItem.Position)
            || (start > lastVisibleItem.Position && end > lastVisibleItem.Position))
        {
            ShiftItemsConsecutively(LaidOutItems, start, end + 1);
            return;
        }

        if (firstVisibleItem == itemToMove)
        {
            firstVisibleItem = itemsToRearrange.FirstOrDefault(i => i != itemToMove);
            prevVisibleCellBounds = firstVisibleItem?.CellBounds ?? new();
            itemsToRearrange.Remove(itemToMove);
        }
        else if (lastVisibleItem == itemToMove)
        {
            lastVisibleItem = itemsToRearrange.LastOrDefault(i => i != itemToMove);
            itemsToRearrange.Remove(itemToMove);
        }

        ShiftAllItems(LaidOutItems, start, itemToMove.Position);
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

            ShiftAllItems(LaidOutItems, itemsToRearrange[^1].Position + 1, count);
        }

        if (firstVisibleItem is not null && AdjustScrollIfNeeded(LaidOutItems, firstVisibleItem, prevVisibleCellBounds)
            && (Control.ScrollX != 0d || Control.ScrollY != 0d)) return;

        UpdateItemsLayout(start, false);
    }

    public virtual void OnItemSizeChanged(VirtualizeListViewItem item)
    {
        ArrangeItem(LaidOutItems, item, AvailableSpace);
        item.PendingSizeChange = false;
        ShiftAllItems(LaidOutItems, item.Position + 1, LaidOutItems.Count);
        DrawAndResize();
    }

    protected virtual void UpdateItemsLayout(int fromPosition, bool shouldAdjustScroll)
    {
        var count = LaidOutItems.Count;

        if (count == 0) return;

        var control = Control!;

        var availableSpace = AvailableSpace;

        var spanList = CollectionsMarshal.AsSpan(LaidOutItems);

        for (int i = fromPosition; i < count; i++)
        {
            var item = spanList[i];

            var onScreen = item.IsOnScreen;

            if (!onScreen && item.IsAttached)
            {
                control.Adapter.OnCellRecycled(item.Cell!, item.Position);
                item.IsAttached = false;
            }

            if (!onScreen || item.IsAttached) continue;

            ReuseOrCreateCell(item);

            var prevBounds = item.Bounds;

            control.Adapter.OnBindCell(item.Cell!, item.Position);

            ArrangeItem(LaidOutItems, item, availableSpace);

            item.IsAttached = true;

            if (item.Bounds == prevBounds) continue;

            ShiftAllItems(LaidOutItems, item.Position + 1, count);

            if (!shouldAdjustScroll) continue;

            AdjustScrollForItemBoundsChange(item, prevBounds);
        }

        DrawAndResize();
    }

    protected virtual void DrawAndResize()
    {
        foreach (var item in LaidOutItems.FindAll(i => i.Cell is not null))
        {
            DrawItem(LaidOutItems, item);
        }

        ResizeLayout();
    }

    protected virtual void DrawCachedItems()
    {
        foreach (var item in CachedItems)
        {
            DrawCachedItem(item);
        }
    }

    protected virtual void ReuseOrCreateCell(VirtualizeListViewItem item)
    {
        if (item.Cell is not null) return;

        var freeItem = LaidOutItems.Find(i => i.Template == item.Template && !i.IsAttached && !i.IsOnScreen && i.Cell is not null);
        if (freeItem is not null)
        {
            var cell = freeItem.Cell;
            freeItem.Cell = null;
            item.Cell = cell;
        }
        else
        {
            freeItem = CachedItems.Find(i => i.Template == item.Template);
            if (freeItem is not null)
            {
                CachedItems.Remove(freeItem);

                var cell = freeItem.Cell;
                freeItem.Cell = null;
                item.Cell = cell;
            }
            else
            {
                item.Cell = Control!.Adapter.OnCreateCell(item.Template, item.Position);
                this.Add(item.Cell);
            }
        }
    }

    protected virtual void ResizeLayout()
    {
        if (LaidOutItems.Count == 0)
        {
            this.WidthRequest = FullSize;
            this.HeightRequest = FullSize;
            return;
        }

        var control = Control!;

        var lastItem = LaidOutItems[^1];

        if (IsOrientation(ScrollOrientation.Vertical))
        {
            this.HeightRequest = lastItem.Bounds.Bottom < (control.Height - control.Padding.VerticalThickness) ? FullSize : lastItem.Bounds.Bottom;
            this.WidthRequest = FullSize;
        }
        else if (IsOrientation(ScrollOrientation.Horizontal))
        {
            this.HeightRequest = FullSize;
            this.WidthRequest = lastItem.Bounds.Right < (control.Width - control.Padding.HorizontalThickness) ? FullSize : lastItem.Bounds.Right;
        }
        else
        {
            this.HeightRequest = lastItem.Bounds.Bottom < (control.Height - control.Padding.VerticalThickness) ? FullSize : lastItem.Bounds.Bottom;
            this.WidthRequest = lastItem.Bounds.Right < (control.Width - control.Padding.HorizontalThickness) ? FullSize : lastItem.Bounds.Right;
        }
    }

    protected virtual Size GetAvailableSpace()
    {
        return new Size(Control!.Width - Control.Padding.HorizontalThickness, Control.Height - Control.Padding.VerticalThickness);
    }

    protected virtual VirtualizeListViewItem CreateItemForPosition(IReadOnlyList<object> dataItems, int position)
    {
        return new VirtualizeListViewItem(this)
        {
            BindingContext = dataItems[position],
            Template = Control!.Adapter.GetTemplate(position),
            Position = position
        };
    }

    protected virtual void DrawItem(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item)
    {
        if (items.Count == 0 || item.Position < 0 || item.Cell is null) return;

        item.Cell.TranslationY = item.CellBounds.Y;
        item.Cell.TranslationX = item.CellBounds.X;
    }

    protected virtual void DrawCachedItem(VirtualizeListViewItem item)
    {
        if (item.Cell is null) return;

        // assume there won't be any item bigger than 1000000
        item.Cell.TranslationY = -(item.Cell.Height + 1000000d);
        item.Cell.TranslationX = -(item.Cell.Width + 1000000d);
    }

    protected virtual Thickness GetItemMargin(VirtualizeListViewItem item)
    {
        return new Thickness();
    }

    protected abstract Size GetEstimatedItemSize(VirtualizeListViewItem item);
    protected abstract SizeRequest MeasureItem(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item, Size availableSpace);
    protected abstract void ArrangeItem(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem item, Size availableSpace);
    protected abstract void ShiftAllItems(IReadOnlyList<VirtualizeListViewItem> items, int start, int exclusiveEnd);
    protected abstract void ShiftItemsConsecutively(IReadOnlyList<VirtualizeListViewItem> items, int start, int exclusiveEnd);
    protected abstract void AdjustScrollForItemBoundsChange(VirtualizeListViewItem item, Rect prevBounds);
    protected abstract bool AdjustScrollIfNeeded(IReadOnlyList<VirtualizeListViewItem> items, VirtualizeListViewItem prevFirstVisiblItem, Rect prevBounds);

    protected virtual bool IsOrientation(ScrollOrientation orientation)
    {
        return Control!.Orientation == orientation
            || (Control.Orientation == ScrollOrientation.Neither && Control.PrevScrollOrientation == orientation);
    }

    #region IDisposable
    protected virtual void Dispose(bool disposing)
    {
        if (this.IsDisposed) return;

        UnsubscribeFromEvents();

        IsDisposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~ItemsLayoutManager()
    {
        Dispose(false);
    }
    #endregion
}